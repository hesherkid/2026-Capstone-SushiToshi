using System.Security.Claims;
using back_end.domain.DbContexts;
using back_end.domain.enums;
using back_end.Helpers;
using Microsoft.EntityFrameworkCore;

namespace back_end.Services
{
    public sealed class LocationAccessService : ILocationAccessService
    {
        private readonly ApplicationDbContext _context;

        public LocationAccessService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int?> ResolveLocationAsync(
            ClaimsPrincipal user, int? requestedLocationId, CancellationToken ct = default)
        {
            if (IsAdmin(user)) return requestedLocationId;

            return await CurrentLocationAsync(user, ct);
        }

        public async Task<bool> CanAccessLocationAsync(
            ClaimsPrincipal user, int locationId, CancellationToken ct = default)
        {
            if (IsAdmin(user)) return true;

            return await CurrentLocationAsync(user, ct) == locationId;
        }

        private static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(nameof(UserRoles.Admin));

        // Read from the database, not the token, so switching location (or an admin
        // changing it) applies on the next request without logging in again
        private async Task<int?> CurrentLocationAsync(ClaimsPrincipal user, CancellationToken ct)
        {
            if (!int.TryParse(ClaimsHelpers.GetUserId(user), out var userId)) return null;

            return await _context.Users
                .Where(u => u.User_id == userId)
                .Select(u => u.Location_id)
                .FirstOrDefaultAsync(ct);
        }
    }
}
