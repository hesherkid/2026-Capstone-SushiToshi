using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;

namespace back_end.Helpers
{
    // Who may read a dining session's bills and service requests
    public static class SessionAccess
    {
        // Admins see every table, staff every table at the location they're working at.
        // Customers (guests included) only the table they're currently seated at, so changing
        // an ID in the URL can't show another table's bill.
        public static async Task<bool> CanViewSessionAsync(ApplicationDbContext context, ClaimsPrincipal? user, int sessionId)
        {
            if (user == null) return false;
            if (user.IsInRole("Admin")) return true;

            if (!int.TryParse(ClaimsHelpers.GetUserId(user), out var userId)) return false;

            if (user.IsInRole("Staff"))
            {
                // From the database, not the token (see LocationAccessService)
                var current = await context.Users
                    .Where(u => u.User_id == userId)
                    .Select(u => u.Location_id)
                    .FirstOrDefaultAsync();
                if (current is null) return false;

                // A session that doesn't exist is left to the endpoint's 404
                return !await context.DiningSessions.AnyAsync(s => s.Session_Id == sessionId && s.Location_Id != current);
            }

            return
                await context.SessionParticipants.AnyAsync(sp =>
                    sp.Session_Id == sessionId && sp.User_Id == userId && sp.Left_At == null);
        }
    }
}
