using Microsoft.EntityFrameworkCore;
using back_end.domain.Entities;

namespace back_end.Helpers
{
    public static class StaffOrderQuery
    {
        // Orders with everything the staff order views need (see OrderMappings.ToStaffOrder).
        // Shared by all-orders and order history
        public static IQueryable<SessionOrder> WithStaffOrderDetails(this IQueryable<SessionOrder> orders) =>
            orders
                .Include(o => o.DiningSession)
                    .ThenInclude(ds => ds.Menu)
                .Include(o => o.DiningSession)
                    .ThenInclude(ds => ds.Table)
                .Include(o => o.DiningSession)
                    .ThenInclude(ds => ds.TableGroup)
                        .ThenInclude(tg => tg!.Tables)
                .Include(o => o.Bill)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.MenuItem)
                        .ThenInclude(mi => mi.MenuAssignments);
    }
}
