using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;
using back_end.Dtos;
using back_end.Helpers;

namespace back_end.Services
{
    public class OrderPlanningService : IOrderPlanningService
    {
        private readonly ApplicationDbContext _context;

        public OrderPlanningService(ApplicationDbContext context)
        {
            _context = context;
        }

        // Checks the items sent before anything is looked up; null when they're fine
        public static string? ItemsPayloadError(List<OrderItemCreateDto>? items)
        {
            if (items == null || !items.Any())
            {
                return "No items provided.";
            }
            if (items.Any(i => i.Quantity <= 0 || i.Quantity > 99))
            {
                return "Each quantity must be between 1 and 99.";
            }
            return null;
        }

        private static OrderLimitRules.Limits CategoryLimits(Category c) => new(c.adult_limit, c.child_limit,
            c.senior_limit, c.toddler_limit, c.per_order_limit, c.over_limit_behavior, c.per_order_per_guest);

        private static OrderLimitRules.Limits ItemLimits(MenuItemAssignment a) => new(a.Adult_Limit, a.Child_limit,
            a.Senior_limit, a.Tot_Limit, a.Per_Order_Limit, a.Over_Limit_Behavior);

        // Headcount for per-person limits: one bill's guests, or the whole table when billId is null.
        // Limits are per bill, so each party gets its own allowance
        private async Task<OrderLimitRules.Headcount> SessionHeadcountAsync(int sessionId, int? billId = null)
        {
            var bills = await _context.Bills
                .Where(b => b.Session_Id == sessionId && b.Status != BillStatus.Cancelled
                         && (billId == null || b.Bill_Id == billId))
                .ToListAsync();

            var heads = new OrderLimitRules.Headcount(
                bills.Sum(b => b.Adult_Count),
                bills.Sum(b => b.Child_Count),
                bills.Sum(b => b.Senior_Count),
                bills.Sum(b => b.Tot_Count));
            return heads.IsEmpty ? OrderLimitRules.Headcount.Fallback : heads;
        }

        private sealed record UsedUnits(int OrderKey, int ItemId, int CategoryId, int Quantity, bool Charged);

        // Units already ordered on one bill, or the whole table when billId is null
        // (cancelled orders/items don't count). Charged = billed as over the limit.
        private Task<List<UsedUnits>> SessionUsageAsync(int sessionId, int? billId = null) =>
            _context.OrderItems
                .Where(oi => oi.SessionOrder.session_id == sessionId
                        && (billId == null || oi.SessionOrder.Bill_Id == billId)
                        && oi.SessionOrder.Status != OrderStatus.Cancelled
                        && oi.Order_Item_Status != OrderStatus.Cancelled)
                .Select(oi => new UsedUnits(oi.Order_Key, oi.Item_Id, oi.MenuItem.Category_id, oi.Quantity, oi.Charge_Reason == ChargeReason.OverLimit))
                .ToListAsync();

        // What the whole table has used, leaving out lines already charged as over the limit
        private async Task<(OrderLimitRules.Headcount Heads, Dictionary<int, int> ItemFree, Dictionary<int, int> CategoryFree)>
            TableUsageAsync(int sessionId)
        {
            var heads = await SessionHeadcountAsync(sessionId);
            var free = (await SessionUsageAsync(sessionId)).Where(u => !u.Charged).ToList();
            return (heads,
                free.GroupBy(u => u.ItemId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity)),
                free.GroupBy(u => u.CategoryId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity)));
        }

        public async Task<OrderLinePlan> PlanOrderLinesAsync(DiningSession session, int billId, int? orderId, List<OrderItemCreateDto> items)
        {
            // Combine duplicate item IDs so per-order limits can't be bypassed by splitting a request
            var requested = items
                .GroupBy(i => i.ItemId)
                .Select(g => (ItemId: g.Key, Quantity: g.Sum(i => i.Quantity)))
                .ToList();
            var itemIds = requested.Select(r => r.ItemId).ToList();

            // Effective assignments location override first, with category limits
            var menuRows = await _context.MenuItemAssignments
                .ForLocation(session.Menu_Id, session.Location_Id)
                .Where(a => itemIds.Contains(a.Item_Id))
                .Include(a => a.MenuItem)
                    .ThenInclude(mi => mi.Category)
                .ToListAsync();

            var heads = await SessionHeadcountAsync(session.Session_Id, billId);
            var used = await SessionUsageAsync(session.Session_Id, billId);
            var table = await TableUsageAsync(session.Session_Id);

            var sessionItemUsed = used.GroupBy(u => u.ItemId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));
            var sessionCategoryUsed = used.GroupBy(u => u.CategoryId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));
            var orderItemUsed = used.Where(u => u.OrderKey == orderId)
                .GroupBy(u => u.ItemId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));
            var orderCategoryUsed = used.Where(u => u.OrderKey == orderId)
                .GroupBy(u => u.CategoryId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));

            var errors = new List<string>();
            var newLines = new List<OrderItems>();

            foreach (var (itemId, quantity) in requested)
            {
                var assignment = menuRows.EffectiveFor(session.Menu_Id, itemId, session.Location_Id);
                if (assignment == null || assignment.Status != MenuItemStatus.Available)
                {
                    errors.Add($"Item {itemId} is not available at this location.");
                    continue;
                }

                var category = assignment.MenuItem.Category;
                var categoryId = category.Category_id;

                var categoryLimits = CategoryLimits(category);
                var itemLimits = ItemLimits(assignment);

                var plan = OrderLimitRules.ApplyLimits(
                    itemName: assignment.MenuItem.Name,
                    categoryName: category.Category_name,
                    requested: quantity,
                    category: categoryLimits,
                    item: itemLimits,
                    heads: heads,
                    used: new OrderLimitRules.Usage(
                        orderCategoryUsed.GetValueOrDefault(categoryId),
                        orderItemUsed.GetValueOrDefault(itemId),
                        OrderLimitRules.EffectSessionUsed(categoryLimits, heads,
                            sessionCategoryUsed.GetValueOrDefault(categoryId), table.Heads, table.CategoryFree.GetValueOrDefault(categoryId)),
                        OrderLimitRules.EffectSessionUsed(itemLimits, heads,
                            sessionItemUsed.GetValueOrDefault(itemId), table.Heads, table.ItemFree.GetValueOrDefault(itemId))),
                    isAddOn: assignment.Is_Add_On,
                    price: assignment.Price);

                if (!plan.IsValid)
                {
                    errors.Add(plan.Error!);
                    continue;
                }

                foreach (var line in plan.Lines)
                {
                    newLines.Add(new OrderItems
                    {
                        Menu_Id = session.Menu_Id,
                        Item_Id = itemId,
                        Quantity = line.Quantity,
                        Price_At_Time = line.Price,          // server decides, client price is ignored
                        Charge_Reason = line.Reason,
                        Order_Item_Status = OrderStatus.Pending
                    });
                }

                // Count this request's units so later items in the same request see them
                orderItemUsed[itemId] = orderItemUsed.GetValueOrDefault(itemId) + quantity;
                orderCategoryUsed[categoryId] = orderCategoryUsed.GetValueOrDefault(categoryId) + quantity;
                sessionItemUsed[itemId] = sessionItemUsed.GetValueOrDefault(itemId) + quantity;
                sessionCategoryUsed[categoryId] = sessionCategoryUsed.GetValueOrDefault(categoryId) + quantity;

                // Included units from this request count against the table too, charged ones don't
                var freeQty = plan.Lines.Where(l => l.Reason == ChargeReason.None).Sum(l => l.Quantity);
                table.ItemFree[itemId] = table.ItemFree.GetValueOrDefault(itemId) + freeQty;
                table.CategoryFree[categoryId] = table.CategoryFree.GetValueOrDefault(categoryId) + freeQty;
            }

            var namesById = menuRows.GroupBy(a => a.Item_Id).ToDictionary(g => g.Key, g => g.First().MenuItem.Name);
            return new OrderLinePlan(newLines, namesById, errors);
        }

        public async Task<SessionOrderLimitsDto> GetRemainingLimitsAsync(DiningSession session, int? billId)
        {
            var sessionId = session.Session_Id;

            var assignments = (await _context.MenuItemAssignments
                    .ForLocation(session.Menu_Id, session.Location_Id)
                    .Include(a => a.MenuItem)
                        .ThenInclude(mi => mi.Category)
                    .ToListAsync())
                .Effective();

            var heads = await SessionHeadcountAsync(sessionId, billId);
            var used = await SessionUsageAsync(sessionId, billId);
            var itemUsed = used.GroupBy(u => u.ItemId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));
            var categoryUsed = used.GroupBy(u => u.CategoryId).ToDictionary(g => g.Key, g => g.Sum(u => u.Quantity));
            var table = await TableUsageAsync(sessionId);

            return new SessionOrderLimitsDto
            {
                Items = assignments.Select(a =>
                {
                    var limits = ItemLimits(a);
                    var effectUsed = OrderLimitRules.EffectSessionUsed(limits, heads, itemUsed.GetValueOrDefault(a.Item_Id), table.Heads, table.ItemFree.GetValueOrDefault(a.Item_Id));
                    return new ItemOrderLimitDto
                    {
                        ItemId = a.Item_Id,
                        CategoryId = a.MenuItem.Category_id,
                        PerOrder = OrderLimitRules.PerOrderCap(limits, heads),
                        SessionLeft = OrderLimitRules.SessionLeftBeforeBlock(limits, heads, effectUsed),
                        FreeLeft = OrderLimitRules.SessionLeftBeforeCharge(limits, heads, effectUsed),
                        ExtraPrice = a.Price,
                    };
                }).ToList(),
                Categories = assignments
                    .Select(a => a.MenuItem.Category)
                    .DistinctBy(c => c.Category_id)
                    .Select(c =>
                    {
                        var limits = CategoryLimits(c);
                        var effectUsed = OrderLimitRules.EffectSessionUsed(limits, heads, categoryUsed.GetValueOrDefault(c.Category_id), table.Heads, table.CategoryFree.GetValueOrDefault(c.Category_id));
                        return new CategoryOrderLimitDto
                        {
                            CategoryId = c.Category_id,
                            PerOrder = OrderLimitRules.PerOrderCap(limits, heads),
                            SessionLeft = OrderLimitRules.SessionLeftBeforeBlock(limits, heads, effectUsed),
                            FreeLeft = OrderLimitRules.SessionLeftBeforeCharge(limits, heads, effectUsed),
                        };
                    }).ToList(),
            };
        }
    }
}
