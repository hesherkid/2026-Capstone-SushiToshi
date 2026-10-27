using System.Security.Claims;

namespace back_end.Services
{
    // Which location a signed-in user's request is for. Staff are limited to the location
    // they're working at (User.Location_id); admins can work with any location.
    // Customers aren't limited here: customer endpoints check they're in the session instead
    public interface ILocationAccessService
    {
        // Shown to staff who have no location to work at
        const string NoLocationMessage = "Ask an admin to assign you a location.";

        // Shown to staff who try to switch to a location they aren't assigned to
        const string NotAssignedMessage = "You aren't assigned to this location. Ask an admin to assign you to it.";

        // Shown to staff who try to see or change something at another location
        const string OtherLocationMessage = "This belongs to another location.";

        // Whether this user only works at one location at a time (staff)
        bool IsLimitedToOneLocation(ClaimsPrincipal user);

        // Staff: their current location from the database. Any location they ask for is ignored,
        // and the scope is forbidden when they have no current location.
        // Everyone else: the location they ask for, or all locations when they don't ask
        Task<LocationScope> ResolveLocationAsync(ClaimsPrincipal user, int? requestedLocationId, CancellationToken ct = default);

        // Whether the user may see or change something that belongs to this location.
        // For endpoints that act on one table, session, order, etc. by its ID
        Task<bool> CanAccessLocationAsync(ClaimsPrincipal user, int locationId, CancellationToken ct = default);

        // Whether the user may switch to working at this location: admins anywhere,
        // staff only at the locations an admin assigned them (StaffLocation)
        Task<bool> CanSwitchToLocationAsync(ClaimsPrincipal user, int locationId, CancellationToken ct = default);
    }

    // The location a request is limited to. LocationId null means all locations
    public sealed record LocationScope(bool IsForbidden, int? LocationId)
    {
        public static LocationScope Forbidden { get; } = new(true, null);
        public static LocationScope For(int? locationId) => new(false, locationId);
    }
}
