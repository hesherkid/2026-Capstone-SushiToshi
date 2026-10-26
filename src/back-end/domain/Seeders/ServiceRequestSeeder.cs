using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using back_end.domain.enums;

namespace back_end.domain.Seeders
{
    /// <summary>
    /// Seeds service requests the way ServiceRequestController makes them:
    /// - Asked by someone seated at the table at that moment, after they joined and before they left.
    /// - Filed against the session's table, or the first table of its group.
    /// - Pending with nobody assigned, or Completed by staff (CompleteServiceRequest sets
    /// Claimed_By, Claimed_At and Completed_At together).
    /// - Ended tables only have completed requests; open tables can have pending ones.
    /// - Notes are written the way the customer form writes them: picked tiles, then any typed note.
    /// </summary>
    public class ServiceRequestSeeder : ISeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ServiceRequestSeeder> _logger;
        private readonly Random _rng = new();
        private Dictionary<int, (string DisplayName, string GivenName, string Surname)> _userSeedData = new();

        // The tiles on the customer "Call your server" page
        public static readonly string[] CommonRequests =
            { "Water refill", "Napkins", "Utensils", "Soy sauce", "Wasabi", "Ginger" };

        public static readonly string[] TypedNotes =
        {
            "Could we get a high chair?",
            "Could someone clear the empty plates?",
            "We have a question about the menu",
            "One of us has a shellfish allergy, can someone check our order?",
            "Can we get two more glasses?",
            "Our last order hasn't arrived yet"
        };

        public static string BuildNotes(IReadOnlyList<string> picked, string? note)
        {
            var parts = new List<string>();
            if (picked.Count > 0)
                parts.Add("Need " + string.Join(", ", picked.Select(p => p.ToLowerInvariant())));
            if (!string.IsNullOrWhiteSpace(note))
                parts.Add(note.Trim());
            return string.Join(". ", parts);
        }

        // Mostly tiles, now and then tiles plus a typed note, or only a typed note.
        private string RandomNotes()
        {
            var roll = _rng.NextDouble();
            var picked = roll < 0.85
                ? CommonRequests.OrderBy(_ => _rng.Next()).Take(_rng.Next(1, 4)).ToList()
                : new List<string>();
            var note = roll >= 0.70 ? TypedNotes[_rng.Next(TypedNotes.Length)] : null;
            return BuildNotes(picked, note);
        }

        public ServiceRequestSeeder(ApplicationDbContext context, ILogger<ServiceRequestSeeder> logger)
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
                .Include(s => s.TableGroup).ThenInclude(g => g!.Tables)
                .ToList();

            var seats = _context.SessionParticipants
                .Where(p => p.User_Id != null)
                .ToList()
                .GroupBy(p => p.Session_Id)
                .ToDictionary(g => g.Key, g => g.ToList());

            var staff = _context.Users
                .Where(u => u.Role == UserRoles.Staff || u.Role == UserRoles.Admin)
                .ToList();

            var now = DateTime.UtcNow;
            int pending = 0, completed = 0;

            foreach (var session in sessions)
            {
                if (!seats.TryGetValue(session.Session_Id, out var seated) || seated.Count == 0)
                    continue;

                var tableId = session.Table_Id ?? session.TableGroup?.Tables.FirstOrDefault()?.Table_Id;
                if (tableId == null) continue;

                var isOpen = session.Ended_At == null;
                var tableEnd = session.Ended_At ?? now;

                // Not every table asks for something; open tables a little more, so the staff view has work
                var count = _rng.NextDouble() < (isOpen ? 0.6 : 0.35) ? _rng.Next(1, 3) : 0;

                for (int i = 0; i < count; i++)
                {
                    var seat = seated[_rng.Next(seated.Count)];
                    var from = seat.Joined_At;
                    var to = seat.Left_At ?? tableEnd;
                    if (to <= from) continue;

                    var createdAt = from.AddMinutes(_rng.NextDouble() * (to - from).TotalMinutes);

                    var stillWaiting = isOpen && i == count - 1 && _rng.NextDouble() < 0.6;

                    var request = new ServiceRequest
                    {
                        Session_Id = session.Session_Id,
                        Table_Id = tableId.Value,
                        Request_By = seat.User_Id!.Value,
                        Notes = RandomNotes(),
                        Created_At = createdAt
                    };

                    if (stillWaiting)
                    {
                        request.Status = ServiceRequestStatus.Pending;
                        pending++;
                    }
                    else
                    {
                        var doneAt = createdAt.AddMinutes(_rng.Next(2, 11));
                        if (doneAt > tableEnd) doneAt = tableEnd;

                        var completer = staff.Where(u => u.Location_id == session.Location_Id).OrderBy(_ => _rng.Next()).FirstOrDefault()
                                        ?? staff.OrderBy(_ => _rng.Next()).FirstOrDefault();

                        request.Status = ServiceRequestStatus.Completed;
                        request.Claimed_By = completer?.User_id;
                        request.Claimed_At = doneAt;
                        request.Completed_At = doneAt;
                        completed++;
                    }

                    _context.ServiceRequests.Add(request);
                }
            }

            _logger.LogInformation("Added {Total} service requests ({Pending} pending, {Completed} completed)",
                pending + completed, pending, completed);
        }
    }
}