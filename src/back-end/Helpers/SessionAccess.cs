using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;

namespace back_end.Helpers
{
    // Who may read a dining session's bills and service requests
    public static class SessionAccess
    {
        // Staff and admins see every table. Customers (guests included) only the table they're
        // currently seated at, so changing an ID in the URL can't show another table's bill.
        public static async Task<bool> CanViewSessionAsync(ApplicationDbContext context, ClaimsPrincipal? user, int sessionId)
        {
            if (user == null) return false;
            if (user.IsInRole("Admin") || user.IsInRole("Staff")) return true;

            return int.TryParse(ClaimsHelpers.GetUserId(user), out var userId) &&
                await context.SessionParticipants.AnyAsync(sp =>
                    sp.Session_Id == sessionId && sp.User_Id == userId && sp.Left_At == null);
        }
    }
}
