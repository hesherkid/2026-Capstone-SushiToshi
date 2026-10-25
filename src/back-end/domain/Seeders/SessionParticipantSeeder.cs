using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using back_end.domain.enums;
using back_end.Helpers;

namespace back_end.domain.Seeders
{
    /// <summary>
    /// Seeds who sat at each dining session, following the same rules as POST /api/DiningSession/join
    /// - Only diners are participants. Staff seat a table but never join it
    /// - Only one seat at a time can used, a user is never seated at two open tables, registered customers past 
    /// visits don't overlap
    /// - Open tables are seated by throwaway guest accounts, same as "Continue as guest". 
    /// - Regisitered customers appear only in ended sessions (history and analytics)
    /// </summary>
    public class SessionParticipantSeeder : ISeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SessionParticipantSeeder> _logger;
        private readonly Random _rng = new();

        private Dictionary<int, (string DisplayName, string GivenName, string Surname)> _userSeedData = new();

        public SessionParticipantSeeder(ApplicationDbContext context, ILogger<SessionParticipantSeeder> logger)
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
            var sessions = _context.DiningSessions.OrderBy(s => s.Started_At).ToList();
            if (!sessions.Any())
                throw new InvalidOperationException("No dining sessions found. Seed sessions first.");

            // Registered customers from the seed pool; staff, admins and guest accounts are left out
            var seedIds = _userSeedData.Keys.ToList();
            var customers = _context.Users
                .Where(u => seedIds.Contains(u.User_id) && u.Role == UserRoles.Customer)
                .ToList()
                .Where(u => !GuestAccounts.IsGuest(u.Email))
                .ToList();

            // Times each customer is already seated somewhere, so past visits never overlap
            var busy = customers.ToDictionary(c => c.User_id, _ => new List<(DateTime From, DateTime To)>());

            var now = DateTime.UtcNow;
            int registered = 0, guests = 0;

            foreach (var s in sessions)
            {
                var isOpen = s.Ended_At == null;
                var end = s.Ended_At ?? now;
                var seats = isOpen ? _rng.Next(2, 6) : _rng.Next(1, 4);

                // Diners join within the first 30 minutes, and never after the table ended (or "now")
                var latestJoinMinutes = Math.Max(0, Math.Min(30, (int)(end - s.Started_At).TotalMinutes));

                var free = isOpen
                    ? new List<User>()
                    : customers.Where(c => busy[c.User_id].All(b => b.To <= s.Started_At || b.From >= end))
                               .OrderBy(_ => _rng.Next())
                               .ToList();

                for (int i = 0; i < seats; i++)
                {
                    var joinedAt = s.Started_At.AddMinutes(_rng.Next(0, latestJoinMinutes + 1));

                    // Past tables: registered customers when one is free, otherwise a guest
                    if (i < free.Count && _rng.NextDouble() < 0.7)
                    {
                        var customer = free[i];
                        busy[customer.User_id].Add((s.Started_At, end));
                        _context.SessionParticipants.Add(new SessionParticipant
                        {
                            Session_Id = s.Session_Id,
                            User_Id = customer.User_id,
                            User_Name = DisplayName(customer),
                            Joined_At = joinedAt,
                            Left_At = s.Ended_At
                        });
                        registered++;
                        continue;
                    }

                    var guest = NewGuest(joinedAt);
                    _context.Users.Add(guest);
                    _context.SessionParticipants.Add(new SessionParticipant
                    {
                        Session_Id = s.Session_Id,
                        User = guest,
                        User_Name = DisplayName(guest),
                        Joined_At = joinedAt,
                        Left_At = s.Ended_At
                    });
                    guests++;
                }
            }

            _logger.LogInformation(
                "Created {Total} session participants ({Registered} registered customers in past sessions, {Guests} guest accounts)",
                registered + guests, registered, guests);
        }

        private static User NewGuest(DateTime createdAt) => new()
        {
            Email = GuestAccounts.NewEmail(),
            Password_hash = null,
            First_name = "Guest",
            Last_name = GuestAccounts.NewCode(),
            Role = UserRoles.Customer,
            Status = UserStatus.Active,
            Is_email_confirmed = true,
            Created_at = createdAt,
            Last_Interaction_at = createdAt
        };

        private static string DisplayName(User u) =>
            $"{u.First_name} {u.Last_name}".Trim() is { Length: > 0 } name ? name : u.Email;
    }
}