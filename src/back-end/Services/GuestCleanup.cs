using back_end.Configurations;
using back_end.domain.DbContexts;
using back_end.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace back_end.Services
{
    // "Continue as Guest" makes an account per phone, so a table of four can leave three
    // accounts that never ordered. This removes them; their seat history stays, unlinked.
    public class GuestCleanup
    {
        private const int BatchSize = 500;

        private readonly ApplicationDbContext _context;
        private readonly GuestCleanupSettings _settings;
        private readonly ILogger<GuestCleanup> _logger;

        public GuestCleanup(ApplicationDbContext context, IOptions<GuestCleanupSettings> settings, ILogger<GuestCleanup> logger)
        {
            _context = context;
            _settings = settings.Value;
            _logger = logger;
        }

        /// <summary>Removes unused guest accounts and returns how many were removed.</summary>
        public async Task<int> RunAsync(DateTime utcNow, CancellationToken ct = default)
        {
            var cutoff = utcNow.AddHours(-_settings.MinAgeHours);
            var guestSuffix = "@" + GuestAccounts.EmailDomain;
            var removed = 0;

            while (true)
            {
                var guests = await _context.Users
                    .Where(u => u.Email.EndsWith(guestSuffix)
                             && u.Created_at < cutoff
                             && !u.Orders.Any()
                             && !u.RequestedServices.Any()
                             // Strict version from the issue: && !u.SessionParticipants.Any()
                             && !u.SessionParticipants.Any(sp => sp.DiningSession.Ended_At == null))
                    .OrderBy(u => u.User_id)
                    .Take(BatchSize)
                    .ToListAsync(ct);

                if (guests.Count == 0) break;

                // session_participants.User_Id blocks the delete, so unlink the seats first.
                // User_Name ("Guest K7P2") stays, so the table history still reads the same.
                var ids = guests.Select(g => g.User_id).ToList();
                var seats = await _context.SessionParticipants
                    .Where(sp => sp.User_Id != null && ids.Contains(sp.User_Id.Value))
                    .ToListAsync(ct);
                foreach (var seat in seats) seat.User_Id = null;

                // Tokens and favourites are removed by the database (cascade)
                _context.Users.RemoveRange(guests);
                await _context.SaveChangesAsync(ct);
                _context.ChangeTracker.Clear();

                removed += guests.Count;
            }

            _logger.LogInformation("Guest cleanup removed {Count} unused guest accounts older than {Cutoff:u}", removed, cutoff);
            return removed;
        }
    }
}
