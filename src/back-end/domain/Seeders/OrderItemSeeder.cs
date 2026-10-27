using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using back_end.domain.enums;
using back_end.Helpers;

namespace back_end.domain.Seeders
{
    /// <summary>
    /// Seeds OrderItems using wave patterns (first / middle / last order of a bill) and the real menu categories.
    ///
    /// Every line goes through the same checks as POST /api/Order/{orderId}/items:
    /// - The item's effective assignment must be Available.
    /// - OrderLimitRules.ApplyLimits decides the lines: per-order limits always block, per-person
    /// (per session) Block limits stop, Charge limits split off an over-limit line at Price.
    /// - Headcount comes from the session's non-cancelled bills, usage from non-cancelled orders.
    /// When a request would be refused, the seeder asks for less or skips the item, as a customer would.
    /// Approved orders that end up with no items are removed, since staff can't approve an empty order.
    /// </summary>
    public class OrderItemSeeder : ISeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrderItemSeeder> _logger;
        private readonly Random _rng = new();

        public OrderItemSeeder(ApplicationDbContext context, ILogger<OrderItemSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        private static readonly Dictionary<string, (string Category, int Min, int Max)[]> WavePatterns = new()
        {
            ["first_wave"] = new[]
            {
                ("Cooked/Hot", 1, 3), ("Sashimi", 2, 3), ("Nigiri", 3, 5), ("Rolls", 1, 3),
                ("Hot Stone", 1, 2), ("Beer", 0, 2), ("Other Beverages", 0, 2)
            },
            ["middle_wave"] = new[]
            {
                ("Rolls", 2, 4), ("Maki", 1, 2), ("Nigiri", 3, 5), ("Sashimi", 1, 3), ("Temaki", 0, 2),
                ("Kitchen", 1, 3), ("Deep Fried", 1, 3), ("Hot Stone", 0, 2), ("Sake & Soju", 0, 1)
            },
            ["last_wave"] = new[]
            {
                ("Nigiri", 1, 3), ("Deep Fried", 0, 2), ("Add On", 0, 3), ("Other Beverages", 0, 1)
            }
        };

        public void Seed()
        {
            var orders = _context.SessionOrders
                .Include(o => o.DiningSession)
                .OrderBy(o => o.Created_At)
                .ToList();
            if (!orders.Any())
                throw new InvalidOperationException("No session orders found. Please seed session orders first.");

            var items = _context.MenuItems.Include(mi => mi.Category).ToList();
            if (!items.Any())
                throw new InvalidOperationException("No menu items found. Please seed menu items first.");

            var byCategory = items.GroupBy(i => i.Category.Category_name)
                                  .ToDictionary(g => g.Key, g => g.ToList());

            var assignments = _context.MenuItemAssignments
                .Include(a => a.MenuItem).ThenInclude(mi => mi.Category)
                .ToList();

            var headcounts = _context.Bills
                .Where(b => b.Status != BillStatus.Cancelled)
                .ToList()
                .GroupBy(b => b.Session_Id)
                .ToDictionary(g => g.Key, g => new OrderLimitRules.Headcount(
                    g.Sum(b => b.Adult_Count), g.Sum(b => b.Child_Count),
                    g.Sum(b => b.Senior_Count), g.Sum(b => b.Tot_Count)));

            var sessionCategoryUsed = new Dictionary<(int SessionId, int CategoryId), int>();
            var sessionItemUsed = new Dictionary<(int SessionId, int ItemId), int>();

            var orderBills = orders.GroupBy(o => o.Bill_Id)
                .ToDictionary(g => g.Key, g => g.OrderBy(o => o.Created_At).Select(o => o.Order_Id).ToList());

            int created = 0, overLimitLines = 0, refused = 0;
            var emptyOrders = new List<SessionOrder>();

            foreach (var order in orders)
            {
                var session = order.DiningSession;
                var sessionId = order.session_id;
                var heads = headcounts.GetValueOrDefault(sessionId, OrderLimitRules.Headcount.Fallback);
                if (heads.IsEmpty) heads = OrderLimitRules.Headcount.Fallback;
                var countsTowardLimits = order.Status != OrderStatus.Cancelled;

                // Per-order usage resets for every order
                var orderCategoryUsed = new Dictionary<int, int>();
                var orderItemUsed = new Dictionary<int, int>();
                var linesAdded = 0;

                foreach (var (categoryName, min, max) in WavePatterns[GetWave(order, orderBills)])
                {
                    if (!byCategory.TryGetValue(categoryName, out var categoryItems))
                        continue;

                    var chosenItems = categoryItems
                        .OrderBy(_ => _rng.Next())
                        .Take(Math.Min(_rng.Next(min, max + 1), categoryItems.Count))
                        .ToList();

                    foreach (var item in chosenItems)
                    {
                        var assignment = assignments.EffectiveFor(session.Menu_Id, item.item_id, session.Location_Id);
                        if (assignment == null || assignment.Status != MenuItemStatus.Available)
                            continue;

                        var category = assignment.MenuItem.Category;
                        var categoryId = category.Category_id;

                        // Ask for less until the rules accept it, or give up on the item
                        OrderLimitRules.LimitResult? plan = null;
                        int quantity;
                        for (quantity = RequestedQuantity(categoryName); quantity > 0; quantity--)
                        {
                            plan = OrderLimitRules.ApplyLimits(
                                itemName: assignment.MenuItem.Name,
                                categoryName: category.Category_name,
                                requested: quantity,
                                category: new OrderLimitRules.Limits(category.adult_limit, category.child_limit,
                                    category.senior_limit, category.toddler_limit, category.per_order_limit, category.over_limit_behavior),
                                item: new OrderLimitRules.Limits(assignment.Adult_Limit, assignment.Child_limit,
                                    assignment.Senior_limit, assignment.Tot_Limit, assignment.Per_Order_Limit, assignment.Over_Limit_Behavior),
                                heads: heads,
                                used: new OrderLimitRules.Usage(
                                    orderCategoryUsed.GetValueOrDefault(categoryId),
                                    orderItemUsed.GetValueOrDefault(item.item_id),
                                    sessionCategoryUsed.GetValueOrDefault((sessionId, categoryId)),
                                    sessionItemUsed.GetValueOrDefault((sessionId, item.item_id))),
                                isAddOn: assignment.Is_Add_On,
                                price: assignment.Price);

                            if (plan.IsValid) break;
                        }

                        if (plan == null || !plan.IsValid || quantity == 0)
                        {
                            refused++;
                            continue;
                        }

                        foreach (var line in plan.Lines)
                        {
                            _context.OrderItems.Add(new OrderItems
                            {
                                Order_Key = order.Order_Id,
                                Menu_Id = session.Menu_Id,
                                Item_Id = item.item_id,
                                Quantity = line.Quantity,
                                Price_At_Time = line.Price,
                                Charge_Reason = line.Reason,
                                Order_Item_Status = order.Status,
                                Completed_At = order.Completed_At
                            });
                            created++;
                            linesAdded++;
                            if (line.Reason == ChargeReason.OverLimit) overLimitLines++;
                        }

                        orderItemUsed[item.item_id] = orderItemUsed.GetValueOrDefault(item.item_id) + quantity;
                        orderCategoryUsed[categoryId] = orderCategoryUsed.GetValueOrDefault(categoryId) + quantity;
                        if (countsTowardLimits)
                        {
                            sessionItemUsed[(sessionId, item.item_id)] = sessionItemUsed.GetValueOrDefault((sessionId, item.item_id)) + quantity;
                            sessionCategoryUsed[(sessionId, categoryId)] = sessionCategoryUsed.GetValueOrDefault((sessionId, categoryId)) + quantity;
                        }
                    }
                }

                if (linesAdded == 0 && order.Status is OrderStatus.Processing or OrderStatus.Delivered)
                    emptyOrders.Add(order);
            }

            _context.SessionOrders.RemoveRange(emptyOrders);

            var kept = orders.Except(emptyOrders).ToList();
            foreach (var session in orders.Select(o => o.DiningSession).Distinct())
            {
                session.First_Order_At = kept.Where(o => o.session_id == session.Session_Id)
                                             .Select(o => (DateTime?)o.Created_At)
                                             .Min();
            }

            _context.SaveChanges();
            _logger.LogInformation(
                "Created {Created} order items ({OverLimit} over-limit charged lines, {Refused} items the limits refused, {Empty} empty approved orders removed)",
                created, overLimitLines, refused, emptyOrders.Count);
        }

        // First, last, or middle order on the same bill
        private static string GetWave(SessionOrder order, Dictionary<int, List<int>> orderBills)
        {
            var sameBill = orderBills[order.Bill_Id];
            var idx = sameBill.IndexOf(order.Order_Id);
            return idx == 0 ? "first_wave"
                 : idx == sameBill.Count - 1 ? "last_wave"
                 : "middle_wave";
        }

        // How many units a table may ask for, before limits are applied
        private int RequestedQuantity(string category) => category switch
        {
            "Sashimi" => _rng.Next(2, 7),       // pieces
            "Nigiri" or "Temaki" or "Kitchen" => 1,
            "Maki" or "Rolls" or "Deep Fried" or "Cooked/Hot" => _rng.Next(1, 3),
            "Hot Stone" => _rng.Next(1, 3),
            "Add On" => _rng.Next(1, 4),
            _ => _rng.Next(1, 3)                 // drinks
        };
    }
}