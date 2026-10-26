using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using back_end.domain.enums;

namespace back_end.domain.Seeders
{
    /// <summary>
    /// Seeds orders (waves) per bill, following the rules OrderController and BillController
    /// - Orders are placed by someone seated at that session
    /// - Orders are placed while the bill is open
    /// - Status follows the order workflow, Pending -> Processing (approved by staff) -> Delivered.
    /// Delivered/approved times never pass the bill's close time or "now".
    /// - Cancelling a bill cancels its pending orders only (Cancel_Bill), so a cancelled bill's last
    /// wave is Cancelled as earlier waves were already delivered.
    /// </summary>
    public class SessionOrderSeeder : ISeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SessionOrderSeeder> _logger;
        private readonly Random _rng = new();

        private Dictionary<int, (string DisplayName, string GivenName, string Surname)> _userSeedData = new();

        public SessionOrderSeeder(ApplicationDbContext context, ILogger<SessionOrderSeeder> logger)
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
            var bills = _context.Bills.OrderBy(b => b.Created_At).ToList();
            if (!bills.Any()) throw new InvalidOperationException("No bills found. Seed bills first.");

            var sessions = _context.DiningSessions.ToDictionary(s => s.Session_Id);
            var participants = _context.SessionParticipants
                .Where(p => p.User_Id != null)
                .ToList()
                .GroupBy(p => p.Session_Id)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Joined_At).ToList());

            var staff = _context.Users
                .Where(u => u.Role == UserRoles.Staff || u.Role == UserRoles.Admin)
                .ToList();

            var now = DateTime.UtcNow;
            int created = 0, skippedBills = 0;

            foreach (var bill in bills)
            {
                if (!sessions.TryGetValue(bill.Session_Id, out var session) ||
                    !participants.TryGetValue(bill.Session_Id, out var seated) || seated.Count == 0)
                {
                    skippedBills++;
                    continue;
                }

                var opensAt = Max(bill.Created_At, session.Started_At, seated[0].Joined_At);
                var closesAt = Min(bill.Closed_At ?? session.Ended_At ?? now, session.Ended_At ?? now, now);
                if (closesAt <= opensAt) { skippedBills++; continue; }

                var stillOpen = bill.Status == BillStatus.Open && session.Ended_At == null;

                (int min, int max) waves = bill.Status switch
                {
                    BillStatus.Open => (2, 4),
                    BillStatus.Closed => (3, 5),
                    BillStatus.Cancelled => (1, 2),
                    _ => (2, 4)
                };
                var numWaves = _rng.Next(waves.min, waves.max + 1);

                var approver = staff.Where(u => u.Location_id == session.Location_Id).OrderBy(_ => _rng.Next()).FirstOrDefault()
                               ?? staff.OrderBy(_ => _rng.Next()).FirstOrDefault();

                var time = opensAt.AddMinutes(_rng.Next(2, 11));

                for (int i = 0; i < numWaves && time < closesAt; i++)
                {
                    var canOrder = seated.Where(p => p.Joined_At <= time).ToList();
                    var orderer = canOrder.Count > 0 ? canOrder[_rng.Next(canOrder.Count)] : seated[0];

                    var isLast = i == numWaves - 1 || time.AddMinutes(15) >= closesAt;

                    OrderStatus status = bill.Status switch
                    {
                        BillStatus.Cancelled when isLast => OrderStatus.Cancelled,
                        _ when stillOpen && isLast =>
                            new[] { OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Delivered }[_rng.Next(3)],
                        _ => OrderStatus.Delivered
                    };

                    DateTime? approvedAt = null;
                    DateTime? completedAt = null;

                    if (status is OrderStatus.Processing or OrderStatus.Delivered)
                    {
                        approvedAt = time.AddMinutes(_rng.Next(1, 7));
                        if (approvedAt > closesAt) { approvedAt = null; status = OrderStatus.Pending; }
                    }
                    if (status == OrderStatus.Delivered && approvedAt != null)
                    {
                        completedAt = approvedAt.Value.AddMinutes(_rng.Next(10, 26));
                        if (completedAt > closesAt)
                        {
                            if (stillOpen) { completedAt = null; status = OrderStatus.Processing; }
                            else completedAt = closesAt;
                        }
                    }

                    if (!stillOpen && status is OrderStatus.Pending or OrderStatus.Processing)
                        status = bill.Status == BillStatus.Cancelled ? OrderStatus.Cancelled : OrderStatus.Delivered;
                    if (status == OrderStatus.Delivered && completedAt == null)
                    {
                        approvedAt ??= Min(time.AddMinutes(1), closesAt);
                        completedAt = closesAt;
                    }
                    if (status == OrderStatus.Cancelled) { approvedAt = null; completedAt = null; }

                    _context.SessionOrders.Add(new SessionOrder
                    {
                        session_id = bill.Session_Id,
                        Bill_Id = bill.Bill_Id,
                        User_Id = orderer.User_Id,
                        User_Name = orderer.User_Name,
                        Status = status,
                        Created_At = time,
                        Approved_At = approvedAt,
                        Approved_By = approvedAt != null ? approver?.User_id : null,
                        Completed_At = completedAt
                    });
                    created++;

                    if (isLast) break;
                    time = time.AddMinutes(_rng.Next(15, 46));
                }
            }

            _logger.LogInformation("Created {Created} session orders ({Skipped} bills had no seated diner or no open window)",
                created, skippedBills);
        }

        private static DateTime Max(params DateTime[] values) => values.Max();
        private static DateTime Min(params DateTime[] values) => values.Min();
    }
}