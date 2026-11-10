using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain;
using back_end.domain.DbContexts;
using back_end.domain.enums;
using Microsoft.EntityFrameworkCore;
using back_end.Helpers;
using back_end.Services;

namespace back_end.domain.Seeders
{
  /// <summary>
  /// Seeds bills with guest counts/status, and puts everyone seated at the session on one of them
  /// (SessionParticipant.Bill_Id), the way picking or starting a bill does in the app.
  /// - Every bill has at least one person on it, and is named after the first of them
  /// - A bill's guest count is never less than the people on it (kids without a phone make up the rest)
  /// - People stay on their bill after it's closed or cancelled, same as the app
  /// </summary>
  public class BillSeeder : ISeeder
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BillSeeder> _logger;
    private readonly IPricingService _pricingService;
    private readonly Random _rng = new();

    public BillSeeder(ApplicationDbContext context, ILogger<BillSeeder> logger, IPricingService pricingService)
    {
      _context = context;
      _logger = logger;
      _pricingService = pricingService;
    }

    public void Seed()
    {
      var sessions = _context.DiningSessions
          .Include(s => s.Table)
          .Include(s => s.TableGroup)
              .ThenInclude(tg => tg!.Tables)
          .OrderBy(s => s.Session_Id)
          .ToList();

      // Bills for every session, open and past. Orders and order items are created per bill, so
      // past sessions need bills too, or analytics has no sales history before today.
      if (!sessions.Any())
      {
        _logger.LogWarning("No dining sessions found to create bills for.");
        return;
      }

      // Tracked, so setting Bill below saves each person's Bill_Id with the bills
      var seatsBySession = _context.SessionParticipants
          .ToList()
          .GroupBy(p => p.Session_Id)
          .ToDictionary(g => g.Key, g => g.ToList());

      int created = 0, seated = 0;

      foreach (var s in sessions)
      {
        int totalSeats;

        if (s.Table_Id.HasValue && s.Table != null)
        {
          totalSeats = s.Table.seat_count;
        }
        else if (s.TableGroup_Id.HasValue && s.TableGroup != null)
        {
          totalSeats = s.TableGroup.Tables.Sum(t => t.seat_count);
        }
        else
        {
          continue;
        }

        if (!seatsBySession.TryGetValue(s.Session_Id, out var people) || people.Count == 0)
        {
          continue;
        }

        // 1 to 3 bills, never more than the people seated, so nobody's bill is empty
        var numBillsForSession = _rng.Next(1, Math.Min(3, people.Count) + 1);

        // Deal the shuffled people out across the bills
        var shuffled = people.OrderBy(_ => _rng.Next()).ToList();
        var peopleOnBill = Enumerable.Range(0, numBillsForSession)
            .Select(i => shuffled.Where((_, n) => n % numBillsForSession == i).OrderBy(p => p.Joined_At).ToList())
            .ToList();

        // Seats left over after one per person, for kids and anyone who didn't scan in
        var spareSeats = Math.Max(0, totalSeats - people.Count);

        foreach (var onBill in peopleOnBill)
        {
          var owner = onBill[0];

          var extra = _rng.Next(0, Math.Min(spareSeats, 3) + 1);
          spareSeats -= extra;
          var total = onBill.Count + extra;

          // Everyone with a phone is an adult or senior; at least one adult per bill
          var grownUps = onBill.Count + _rng.Next(0, extra + 1);
          var senior = _rng.Next(0, grownUps);
          var adult = grownUps - senior;
          var child = _rng.Next(0, total - grownUps + 1);
          var tot = total - grownUps - child;

          BillStatus status;
          if (s.Ended_At != null)
          {
            status = BillStatus.Closed;
          }
          else
          {
            // 70% Open, 20% Closed, 10% Cancelled for active sessions
            var rand = _rng.NextDouble();
            if (rand < 0.7)
              status = BillStatus.Open;
            else if (rand < 0.9)
              status = BillStatus.Closed;
            else
              status = BillStatus.Cancelled;
          }

          // Named after whoever started it, like a bill started from the menu
          var billName = owner.User_Name ?? $"Party of {total}";

          var tableEnd = s.Ended_At ?? DateTime.UtcNow;
          var createdAt = owner.Joined_At.AddMinutes(_rng.Next(5, 31));
          if (createdAt > tableEnd) createdAt = tableEnd;

          // Closed and cancelled bills both get Closed_At. A bill closed or cancelled at a 
          // table that's still open closed sometime between its creation and now.
          DateTime? closedAt = status switch
          {
            BillStatus.Open => null,
            _ when s.Ended_At != null => s.Ended_At,
            _ => createdAt.AddMinutes(_rng.NextDouble() * (tableEnd - createdAt).TotalMinutes)
          };

          var bill = new Billing
          {
            Session_Id = s.Session_Id,
            Bill_Name = billName,
            Senior_Count = senior,
            Adult_Count = adult,
            Child_Count = child,
            Tot_Count = tot,
            Total_Count = total,
            Status = status,
            Created_At = createdAt,
            Closed_At = closedAt
          };

          _context.Bills.Add(bill);
          onBill.ForEach(p => p.Bill = bill);
          created++;
          seated += onBill.Count;
        }
      }

      var totalOpen = _context.Bills.Count(b => b.Status == BillStatus.Open);
      var totalClosed = _context.Bills.Count(b => b.Status == BillStatus.Closed);
      var totalCancelled = _context.Bills.Count(b => b.Status == BillStatus.Cancelled);
      _logger.LogInformation($"Created {created} bills ({totalOpen} open, {totalClosed} closed, {totalCancelled} cancelled) with {seated} people on them");
    }

    /// <summary>
    /// Freezes prices on closed bills that don't have them yet, the same way Close_bill does:
    /// the location's prices on the day the bill was made, and the gratuity the table's party size earns. 
    /// Run it after Seed() has been saved, so every bill at a table counts towards the party size correctly.
    /// </summary>
    public async Task<int> FreezeClosedBillsAsync(CancellationToken ct = default)
    {
      var bills = await _context.Bills
          .Include(b => b.DiningSession)
          .Where(b => b.Status == BillStatus.Closed && b.Pricing_Type == null)
          .ToListAsync(ct);

      if (bills.Count == 0)
      {
        return 0;
      }

      var sessionIds = bills.Select(b => b.Session_Id).Distinct().ToList();
      var partySizes = await _context.Bills
          .Where(b => sessionIds.Contains(b.Session_Id) && b.Status != BillStatus.Cancelled)
          .GroupBy(b => b.Session_Id)
          .Select(g => new { Session_Id = g.Key, Size = g.Sum(b => b.Adult_Count + b.Senior_Count + b.Child_Count + b.Tot_Count) })
          .ToDictionaryAsync(x => x.Session_Id, x => x.Size, ct);

      var pricingByDay = new Dictionary<(int LocationId, DateOnly Day), PricingInfo>();

      foreach (var bill in bills)
      {
        var locationId = bill.DiningSession.Location_Id;
        var day = DateOnly.FromDateTime(PricingService.ToRestaurantTime(bill.Created_At));

        if (!pricingByDay.TryGetValue((locationId, day), out var pricing))
        {
          pricing = await _pricingService.GetPricingAsync(locationId, bill.Created_At, ct);
          pricingByDay[(locationId, day)] = pricing;
        }

        var partySize = partySizes.GetValueOrDefault(bill.Session_Id);
        BillPriceSnapshot.Freeze(bill, pricing, BillCalculator.GratuityRateFor(pricing, partySize));
      }

      _logger.LogInformation("Froze prices on {Count} closed bills", bills.Count);
      return bills.Count;
    }
  }
}