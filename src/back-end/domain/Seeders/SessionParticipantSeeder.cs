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
    /// - Every open table has at least one throwaway guest account, same as "Continue as guest".
    /// - Registered customers sit at past tables (history and analytics). The demo customer
    /// (UserSeeder.DemoCustomerEmail) and about half the others are also seated at an open table, at most
    /// two besides the demo customer per table, so staff see real names as well as guest codes
    /// </summary>
    public class SessionParticipantSeeder : ISeeder
    {
        // Where the demo customer's open table is, if that location has one. Location 1's staff
        // account (sarah.jones) can then see them on the dashboards
        private const int DemoLocationId = 1;

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

            // Registered customers at open tables are picked before the loop and marked busy, so none
            // of their past visits overlap the open table
            var openSessions = sessions.Where(s => s.Ended_At == null).ToList();
            var openSeatsFor = openSessions.ToDictionary(s => s.Session_Id, _ => new List<User>());

            var demo = customers.FirstOrDefault(c => c.Email == UserSeeder.DemoCustomerEmail);
            var demoSession = demo == null ? null : openSessions
                .OrderBy(s => s.Location_Id == DemoLocationId ? 0 : 1)
                .ThenBy(_ => _rng.Next())
                .FirstOrDefault();
            if (demo != null && demoSession != null)
                openSeatsFor[demoSession.Session_Id].Add(demo);

            foreach (var customer in customers.Where(c => c != demo).OrderBy(_ => _rng.Next()))
            {
                if (_rng.NextDouble() >= 0.5) continue;

                var table = openSessions
                    .Where(s => openSeatsFor[s.Session_Id].Count(u => u != demo) < 2)
                    .OrderBy(_ => _rng.Next())
                    .FirstOrDefault();
                if (table == null) break;
                openSeatsFor[table.Session_Id].Add(customer);
            }

            foreach (var s in openSessions)
                foreach (var customer in openSeatsFor[s.Session_Id])
                    busy[customer.User_id].Add((s.Started_At, now));

            foreach (var s in sessions)
            {
                var isOpen = s.Ended_At == null;
                var end = s.Ended_At ?? now;
                var seats = isOpen ? _rng.Next(2, 6) : _rng.Next(1, 4);

                // Diners join within the first 30 minutes, and never after the table ended (or "now")
                var latestJoinMinutes = Math.Max(0, Math.Min(30, (int)(end - s.Started_At).TotalMinutes));

                // Registered customers picked for this open table, then guests for the rest (at least one)
                var seatedHere = isOpen ? openSeatsFor[s.Session_Id] : new List<User>();
                if (isOpen) seats = Math.Max(seats, seatedHere.Count + 1);

                foreach (var customer in seatedHere)
                {
                    // The demo customer sat down first, so their bill is likely named after them
                    var joinedAt = customer == demo
                        ? s.Started_At
                        : s.Started_At.AddMinutes(_rng.Next(0, latestJoinMinutes + 1));

                    _context.SessionParticipants.Add(new SessionParticipant
                    {
                        Session_Id = s.Session_Id,
                        User_Id = customer.User_id,
                        User_Name = DisplayName(customer),
                        Joined_At = joinedAt
                    });
                    registered++;
                    seats--;
                }

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
                "Created {Total} session participants ({Registered} registered customers, {Guests} guest accounts)",
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