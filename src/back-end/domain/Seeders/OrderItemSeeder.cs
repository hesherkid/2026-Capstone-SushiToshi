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
    /// Seeded orders follow the same limit rules the app enforces:
    ///   - Per-order limits (item and category) always block, e.g. Nigiri 1 of each type, Sashimi 30 combined.
    ///   - Per-person limits are per dining session: limit x headcount per age group (from the session's bills).
    ///     Block limits are clamped; Charge limits (Hot Stone) split into a free line and an over-limit line
    ///     charged at Full_Price with Charge_Reason = OverLimit.
    ///   - AYCE lines are priced at 0; add-ons and drinks at the assignment Price.
    /// Cancelled orders do not count toward limits.
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
                .OrderBy(o => o.session_id).ThenBy(o => o.Created_At)
                .ToList();
            if (!orders.Any())
                throw new InvalidOperationException("No session orders found. Please seed session orders first.");

            var items = _context.MenuItems.Include(mi => mi.Category).ToList();
            if (!items.Any())
                throw new InvalidOperationException("No menu items found. Please seed menu items first.");

            var byCategory = items.GroupBy(i => i.Category.Category_name)
                                  .ToDictionary(g => g.Key, g => g.ToList());

            // Default (all locations) assignments hold the price, full price, and item-level limits
            var assignments = _context.MenuItemAssignments
                .Where(a => a.Location_Id == null)
                .ToDictionary(a => (a.Menu_Id, a.Item_Id));

            // Headcount per dining session, summed across its bills
            var headcounts = _context.Bills
                .GroupBy(b => b.Session_Id)
                .Select(g => new
                {
                    SessionId = g.Key,
                    Adults = g.Sum(b => b.Adult_Count),
                    Children = g.Sum(b => b.Child_Count),
                    Seniors = g.Sum(b => b.Senior_Count),
                    Toddlers = g.Sum(b => b.Tot_Count)
                })
                .ToDictionary(h => h.SessionId, h => new Headcount(h.Adults, h.Children, h.Seniors, h.Toddlers));

            // Running totals for per-person (session) limits
            var sessionCategoryUsed = new Dictionary<(int SessionId, int CategoryId), int>();
            var sessionItemUsed = new Dictionary<(int SessionId, int ItemId), int>();

            int created = 0, pending = 0, delivered = 0, overLimitLines = 0;

            foreach (var order in orders)
            {
                var menuId = order.DiningSession.Menu_Id;
                var sessionId = order.session_id;
                var heads = headcounts.GetValueOrDefault(sessionId, Headcount.Fallback);
                if (heads.IsEmpty) heads = Headcount.Fallback;
                var countsTowardLimits = order.Status != OrderStatus.Cancelled;

                // Running totals for per-order limits (reset for every order)
                var orderCategoryUsed = new Dictionary<int, int>();
                var orderItemUsed = new Dictionary<int, int>();

                foreach (var (categoryName, min, max) in WavePatterns[GetWave(order, orders)])
                {
                    if (!byCategory.TryGetValue(categoryName, out var categoryItems))
                        continue;

                    var category = categoryItems[0].Category;
                    var chosenItems = categoryItems
                        .OrderBy(_ => _rng.Next())
                        .Take(Math.Min(_rng.Next(min, max + 1), categoryItems.Count))
                        .ToList();

                    foreach (var item in chosenItems)
                    {
                        if (!assignments.TryGetValue((menuId, item.item_id), out var assignment))
                            continue;

                        var requested = RequestedQuantity(categoryName);

                        // Per-order limits always block
                        var itemOrderRemaining = LimitRules.Cap(assignment.Per_Order_Limit) - orderItemUsed.GetValueOrDefault(item.item_id);
                        var categoryOrderRemaining = LimitRules.Cap(category.per_order_limit) - orderCategoryUsed.GetValueOrDefault(category.Category_id);
                        requested = Math.Min(requested, Math.Min(itemOrderRemaining, categoryOrderRemaining));
                        if (requested <= 0) continue;

                        // Per-person limits for the dining session (both category and item apply)
                        var categoryRemaining = SessionCap(category.adult_limit, category.child_limit, category.senior_limit, category.toddler_limit, heads)
                                                - sessionCategoryUsed.GetValueOrDefault((sessionId, category.Category_id));
                        var itemRemaining = SessionCap(assignment.Adult_Limit, assignment.Child_limit, assignment.Senior_limit, assignment.Tot_Limit, heads)
                                            - sessionItemUsed.GetValueOrDefault((sessionId, item.item_id));

                        var remaining = Math.Max(0, Math.Min(categoryRemaining, itemRemaining));
                        var behavior = categoryRemaining <= itemRemaining ? category.over_limit_behavior : assignment.Over_Limit_Behavior;

                        var freeQty = Math.Min(requested, remaining);
                        var overQty = requested - freeQty;
                        if (behavior == OverLimitBehavior.Block) overQty = 0;   // blocked: drop the excess
                        if (freeQty + overQty <= 0) continue;

                        // Lines: within-limit (AYCE free / add-on price) and over-limit (charged at Full_Price)
                        if (freeQty > 0)
                        {
                            AddLine(order, menuId, item.item_id, freeQty,
                                    assignment.Is_Add_On ? assignment.Price : 0m, ChargeReason.None);
                        }
                        if (overQty > 0)
                        {
                            AddLine(order, menuId, item.item_id, overQty, assignment.Full_Price, ChargeReason.OverLimit);
                            overLimitLines++;
                        }

                        var lines = (freeQty > 0 ? 1 : 0) + (overQty > 0 ? 1 : 0);
                        created += lines;
                        if (order.Status == OrderStatus.Pending) pending += lines;
                        if (order.Status == OrderStatus.Delivered) delivered += lines;

                        var total = freeQty + overQty;
                        orderItemUsed[item.item_id] = orderItemUsed.GetValueOrDefault(item.item_id) + total;
                        orderCategoryUsed[category.Category_id] = orderCategoryUsed.GetValueOrDefault(category.Category_id) + total;

                        if (countsTowardLimits)
                        {
                            sessionItemUsed[(sessionId, item.item_id)] = sessionItemUsed.GetValueOrDefault((sessionId, item.item_id)) + total;
                            sessionCategoryUsed[(sessionId, category.Category_id)] = sessionCategoryUsed.GetValueOrDefault((sessionId, category.Category_id)) + total;
                        }
                    }
                }
            }

            _context.SaveChanges();
            _logger.LogInformation(
                "Created {Created} order items ({Pending} pending, {Delivered} delivered, {OverLimit} over-limit charged lines)",
                created, pending, delivered, overLimitLines);
        }

        private void AddLine(SessionOrder order, int menuId, int itemId, int quantity, decimal price, ChargeReason reason)
        {
            _context.OrderItems.Add(new OrderItems
            {
                Order_Key = order.Order_Id,
                Menu_Id = menuId,
                Item_Id = itemId,
                Quantity = quantity,
                Price_At_Time = price,
                Charge_Reason = reason,
                Order_Item_Status = order.Status,
                Completed_At = order.Completed_At
            });
        }

        // First, last, or middle order on the same bill
        private static string GetWave(SessionOrder order, List<SessionOrder> orders)
        {
            var sameBill = orders.Where(o => o.Bill_Id == order.Bill_Id).OrderBy(o => o.Created_At).ToList();
            var idx = sameBill.FindIndex(o => o.Order_Id == order.Order_Id);
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

        // Per-person session cap: limit x headcount for each age group present.
        // 0 = unlimited for that group; toddler 0 = same as child.
        private static int SessionCap(int adult, int child, int senior, int toddler, Headcount heads)
        {
            var groups = new (int Limit, int Count)[]
            {
                (adult, heads.Adults),
                (child, heads.Children),
                (senior, heads.Seniors),
                (LimitRules.EffectToddler(toddler, child), heads.Toddlers)
            };

            long total = 0;
            foreach (var (limit, count) in groups)
            {
                if (count == 0) continue;
                if (limit == 0) return int.MaxValue;   // unlimited for someone at the table
                total += (long)limit * count;
            }
            return (int)Math.Min(total, int.MaxValue);
        }

        private sealed record Headcount(int Adults, int Children, int Seniors, int Toddlers)
        {
            public static readonly Headcount Fallback = new(1, 0, 0, 0);
            public bool IsEmpty => Adults + Children + Seniors + Toddlers == 0;
        }
    }
}