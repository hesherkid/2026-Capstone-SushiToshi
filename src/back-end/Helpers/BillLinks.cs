using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Helpers
{
    // Which bill a seated customer is on. Customers pick or start their own; staff can move them
    public static class BillLinks
    {
        // Why this person can't move off their current bill, or null if they can.
        // Once they've ordered on an open bill, only staff can move them, so nobody walks away
        // from orders they placed
        public static async Task<string?> CantLeaveReasonAsync(ApplicationDbContext context, SessionParticipant seat)
        {
            if (seat.Bill_Id == null) return null;

            var current = await context.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Bill_Id == seat.Bill_Id);
            if (current == null || current.Status != BillStatus.Open) return null;

            var hasOrders = await context.SessionOrders.AnyAsync(o =>
                o.Bill_Id == current.Bill_Id && o.User_Id == seat.User_Id && o.Status != OrderStatus.Cancelled);
            return hasOrders
                ? $"You've already ordered on {current.Bill_Name}. Ask your server to move you to another bill."
                : null;
        }

        // Why this bill can't be joined, or null if it can
        public static string? CantJoinReason(Billing? bill, int sessionId)
        {
            if (bill == null || bill.Session_Id != sessionId) return "That bill isn't at your table.";
            if (bill.Status != BillStatus.Open) return $"{bill.Bill_Name} is already {bill.Status.ToString().ToLower()}.";
            if (bill.Requested_At != null) return $"{bill.Bill_Name} has already asked for the bill.";
            return null;
        }

        // Seats at the session's table, or across its table group's tables (load them first)
        public static int TableCapacity(DiningSession session) =>
            session.Table?.seat_count ?? session.TableGroup?.Tables.Sum(t => t.seat_count) ?? 0;
    }
}
