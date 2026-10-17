using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain;
using back_end.domain.DbContexts;
using back_end.domain.enums;

namespace back_end.domain.Seeders
{
    /// <summary>
    /// Seeds SessionParticipant with staff and customers per session.
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
            var activeSessions = _context.DiningSessions
                .Where(s => s.Ended_At == null)
                .OrderByDescending(s => s.Started_At)
                .ToList();

            var historicalSessions = _context.DiningSessions
                .Where(s => s.Ended_At != null)
                .OrderByDescending(s => s.Started_At)
                .Take(40)
                .ToList();

            var sessions = activeSessions.Concat(historicalSessions).ToList();

            if (!sessions.Any())
                throw new InvalidOperationException("No dining sessions found. Seed sessions first.");

            var userUsageCount = new Dictionary<int, int>();
            int created = 0;

            foreach (var s in sessions)
            {
                var sessionStart = s.Started_At;
                var isActive = s.Ended_At == null;

                // Pick a random staff from CSV
                var staffUserIds = _userSeedData.Keys.ToList();
                var staffUserId = staffUserIds[_rng.Next(staffUserIds.Count)];
                var staffInfo = _userSeedData[staffUserId];
                if (!userUsageCount.ContainsKey(staffUserId)) userUsageCount[staffUserId] = 0;
                userUsageCount[staffUserId]++;
                _context.SessionParticipants.Add(new SessionParticipant
                {
                    Session_Id = s.Session_Id,
                    User_Id = staffUserId,
                    User_Name = $"{staffInfo.GivenName} {staffInfo.Surname}".Trim(),
                    Joined_At = sessionStart,
                    Left_At = s.Ended_At
                });
                created++;

                var numCustomers = isActive ? _rng.Next(2, 6) : _rng.Next(1, 4);
                var customerUserIds = staffUserIds.Where(id => id != staffUserId).OrderBy(_ => _rng.Next()).Take(numCustomers).ToList();

                foreach (var customerUserId in customerUserIds)
                {
                    var customerInfo = _userSeedData[customerUserId];
                    if (!userUsageCount.ContainsKey(customerUserId)) userUsageCount[customerUserId] = 0;
                    userUsageCount[customerUserId]++;
                    var joinTime = sessionStart.AddMinutes(_rng.Next(0, 31));
                    _context.SessionParticipants.Add(new SessionParticipant
                    {
                        Session_Id = s.Session_Id,
                        User_Id = customerUserId,
                        User_Name = $"{customerInfo.GivenName} {customerInfo.Surname}".Trim(),
                        Joined_At = joinTime,
                        Left_At = s.Ended_At
                    });
                    created++;
                }
            }

            var unusedUsers = _userSeedData.Keys.Where(userId => !userUsageCount.ContainsKey(userId) || userUsageCount[userId] < 2).ToList();

            if (unusedUsers.Any() && activeSessions.Any())
            {
                int sessionIndex = 0;

                foreach (var userId in unusedUsers)
                {
                    var usage = userUsageCount.GetValueOrDefault(userId, 0);
                    for (int i = usage; i < 2; i++)
                    {
                        var session = activeSessions[sessionIndex % activeSessions.Count];
                        sessionIndex++;

                        var info = _userSeedData[userId];

                        _context.SessionParticipants.Add(new SessionParticipant
                        {
                            Session_Id = session.Session_Id,
                            User_Id = userId,
                            User_Name = $"{info.GivenName} {info.Surname}".Trim(),
                            Joined_At = session.Started_At.AddMinutes(_rng.Next(1, 30)),
                            Left_At = null
                        });
                        created++;
                    }
                }
            }

            var totalActive = _context.SessionParticipants.Count(p => p.Left_At == null);
            var totalCompleted = _context.SessionParticipants.Count(p => p.Left_At != null);
            _logger.LogInformation($"Created {created} session participants ({totalActive} active, {totalCompleted} completed)");
        }
    }
}