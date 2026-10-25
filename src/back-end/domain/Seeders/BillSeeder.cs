using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain;
using back_end.domain.DbContexts;
using back_end.domain.enums;
using Microsoft.EntityFrameworkCore;

namespace back_end.domain.Seeders
{
  /// <summary>
  /// Seeds bills with guest counts/status, tied to assigned tables and participants.
  /// </summary>
  public class BillSeeder : ISeeder
  {
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BillSeeder> _logger;
    private readonly Random _rng = new();

    private Dictionary<int, (string DisplayName, string GivenName, string Surname)> _userSeedData = new();

    public BillSeeder(ApplicationDbContext context, ILogger<BillSeeder> logger)
    {
      _context = context;
      _logger = logger;
    }

    public void SetUserSeedData(Dictionary<int, (string DisplayName, string GivenName, string Surname)> userSeedData)
    {
      _userSeedData = userSeedData;
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

      int created = 0;
      var userUsageCount = new Dictionary<int, int>();
      var userIds = _userSeedData.Keys.ToList();

      var sessionsToProcess = sessions;

      foreach (var s in sessionsToProcess)
      {
        int totalSeats;
        List<int> tableNumbers;

        if (s.Table_Id.HasValue && s.Table != null)
        {
          totalSeats = s.Table.seat_count;
          tableNumbers = new List<int> { s.Table.table_number };
        }
        else if (s.TableGroup_Id.HasValue && s.TableGroup != null)
        {
          totalSeats = s.TableGroup.Tables.Sum(t => t.seat_count);
          tableNumbers = s.TableGroup.Tables.Select(t => t.table_number).ToList();
        }
        else
        {
          continue;
        }

        var remaining = totalSeats;

        var numBillsForSession = _rng.Next(1, Math.Min(4, totalSeats + 1));

        for (int billIndex = 0; billIndex < numBillsForSession; billIndex++)
        {
          if (remaining <= 0) break;

          // Pick a random user as the bill owner
          var userId = userIds[_rng.Next(userIds.Count)];
          var info = _userSeedData[userId];

          var maxGuests = Math.Min(remaining, 6);
          var adult = _rng.Next(1, maxGuests + 1);
          var senior = _rng.Next(0, Math.Max(0, maxGuests - adult) + 1);
          var child = _rng.Next(0, Math.Max(0, maxGuests - adult - senior) + 1);
          var tot = _rng.Next(0, Math.Max(0, maxGuests - adult - senior - child) + 1);
          var total = adult + senior + child + tot;

          if (total == 0)
          {
            // Ensure at least one guest
            adult = 1;
            total = 1;
          }

          remaining -= total;

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

          var tableNumbersStr = tableNumbers.Select(n => n.ToString());
          var billName = $"Table {string.Join(" & ", tableNumbersStr)} - Party of {total}";

          var participant = _context.SessionParticipants
              .Where(p => p.Session_Id == s.Session_Id)
              .OrderBy(p => p.Joined_At)
              .FirstOrDefault();

          var tableEnd = s.Ended_At ?? DateTime.UtcNow;
          var createdAt = (participant?.Joined_At ?? s.Started_At).AddMinutes(_rng.Next(5, 31));
          if (createdAt > tableEnd) createdAt = tableEnd;

          // Closed and cancelled bills both get Closed_At. A bill closed or cancelled at a 
          // table that's still open closed sometime between its creation and now.
          DateTime? closedAt = status switch
          {
            BillStatus.Open => null,
            _ when s.Ended_At != null => s.Ended_At,
            _ => createdAt.AddMinutes(_rng.NextDouble() * (tableEnd - createdAt).TotalMinutes)
          };

          // Track user usage
          if (!userUsageCount.ContainsKey(userId))
            userUsageCount[userId] = 0;
          userUsageCount[userId]++;

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
          created++;
        }
      }

      var totalOpen = _context.Bills.Count(b => b.Status == BillStatus.Open);
      var totalClosed = _context.Bills.Count(b => b.Status == BillStatus.Closed);
      var totalCancelled = _context.Bills.Count(b => b.Status == BillStatus.Cancelled);
      _logger.LogInformation($"Created {created} bills ({totalOpen} open, {totalClosed} closed, {totalCancelled} cancelled)");
    }
  }
}