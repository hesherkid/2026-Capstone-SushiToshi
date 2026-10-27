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

        public bool IsLimitedToOneLocation(ClaimsPrincipal user) => user.IsInRole(nameof(UserRoles.Staff));

        public async Task<LocationScope> ResolveLocationAsync(
            ClaimsPrincipal user, int? requestedLocationId, CancellationToken ct = default)
        {
            if (!IsLimitedToOneLocation(user)) return LocationScope.For(requestedLocationId);

            var current = await CurrentLocationAsync(user, ct);
            return current is null ? LocationScope.Forbidden : LocationScope.For(current);
        }

        public async Task<bool> CanAccessLocationAsync(
            ClaimsPrincipal user, int locationId, CancellationToken ct = default)
        {
            if (!IsLimitedToOneLocation(user)) return true;

            return await CurrentLocationAsync(user, ct) == locationId;
        }

        public async Task<bool> CanSwitchToLocationAsync(
            ClaimsPrincipal user, int locationId, CancellationToken ct = default)
        {
            if (!IsLimitedToOneLocation(user)) return true;
            if (!int.TryParse(ClaimsHelpers.GetUserId(user), out var userId)) return false;

            return await _context.StaffLocations
                .AnyAsync(sl => sl.User_Id == userId && sl.Location_Id == locationId, ct);
        }

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
