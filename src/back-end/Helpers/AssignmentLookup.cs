using back_end.domain.Entities;

namespace back_end.Helpers
{
    public static class AssignmentLookup
    {
        // Default rows + this location's overrides for a menu (works in EF queries)
        public static IQueryable<MenuItemAssignment> ForLocation(
            this IQueryable<MenuItemAssignment> q, int menuId, int? locationId) =>
            q.Where(a => a.Menu_Id == menuId &&
                         (a.Location_Id == null || a.Location_Id == locationId));

        // One row per item: the override if there is one, otherwise the default
        public static List<MenuItemAssignment> Effective(this IEnumerable<MenuItemAssignment> rows) =>
            rows.GroupBy(a => a.Item_Id)
                .Select(g => g.FirstOrDefault(a => a.Location_Id != null) ?? g.First())
                .ToList();

        // One row for a single item (in-memory)
        public static MenuItemAssignment? EffectiveFor(
            this IEnumerable<MenuItemAssignment> rows, int menuId, int itemId, int? locationId) =>
            rows.Where(a => a.Menu_Id == menuId && a.Item_Id == itemId &&
                            (a.Location_Id == null || a.Location_Id == locationId))
                .OrderBy(a => a.Location_Id == null)   // override first
                .FirstOrDefault();
    }
}
