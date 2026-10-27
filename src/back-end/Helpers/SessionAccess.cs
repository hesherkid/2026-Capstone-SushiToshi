using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;

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

        // Seated = the user hasn't left the table (Left_At == null) and the table's dining session 
        // is still open. Every active-session lookup uses this so the menu, orders, bill and 
        // service-request pages agree on where someone is sitting.
        public static IQueryable<SessionParticipant> CurrentSeats(this IQueryable<SessionParticipant> participants, int userId) =>
            participants.Where(sp => sp.User_Id == userId &&
                                     sp.Left_At == null &&
                                     sp.DiningSession.Ended_At == null);
    }
}