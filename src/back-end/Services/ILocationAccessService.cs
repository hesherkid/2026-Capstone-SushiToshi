using System.Security.Claims;

namespace back_end.Services
{
    // Which location a signed-in user's request is for. Staff are limited to the location
    // they're working at (User.Location_id); admins can work with any location
    public interface ILocationAccessService
    {
        // Shown to staff who have no location to work at
        const string NoLocationMessage = "Ask an admin to assign you a location.";

        // Staff: their current location from the database. Any location they ask for is ignored.
        // Admins: the location they ask for.
        // Null when there's no location to use: staff with no current location, or an admin who didn't ask for one
        Task<int?> ResolveLocationAsync(ClaimsPrincipal user, int? requestedLocationId, CancellationToken ct = default);

        // Whether the user may see or change something that belongs to this location.
        // For endpoints that act on one table, session, order, etc. by its ID
        Task<bool> CanAccessLocationAsync(ClaimsPrincipal user, int locationId, CancellationToken ct = default);
    }
}
