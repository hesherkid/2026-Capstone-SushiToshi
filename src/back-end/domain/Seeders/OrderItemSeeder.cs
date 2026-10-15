using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using back_end.domain.enums;

namespace back_end.domain.Seeders
{
    /// <summary>
    /// Seeds OrderItems entries based on wave patterns and menu/category.
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

        public void Seed()
        {
            var orders = _context.SessionOrders.ToList();
            if (!orders.Any())
                throw new InvalidOperationException("No session orders found. Please seed session orders first.");

            var items = _context.MenuItems.Include(mi => mi.Category).ToList();
            if (!items.Any())
                throw new InvalidOperationException("No menu items found. Please seed menu items first.");

            // Group menu items by Category_name (from Category entity)
            var byCategory = items.GroupBy(i => i.Category.Category_name)
                                  .ToDictionary(g => g.Key, g => g.ToList());

            // Lookup current prices from MenuItemAssignments (need MenuId and ItemId and Price)
            var assignments = _context.MenuItemAssignments.Where(a => a.Location_Id == null).ToList();
            var priceLookup = assignments.ToDictionary(a => (a.Menu_Id, a.Item_Id), a => a.Price);

            int created = 0;

            var wavePatterns = new Dictionary<string, (string category, int min, int max)[]>
            {
                ["first_wave"] = new[] {
                    ("Appetizers", 2, 4), ("Sushi Rolls", 2, 3), ("Hot Dishes", 1, 2), ("Nigiri", 2, 3), ("Sashimi", 1, 2)
                },
                ["middle_wave"] = new[] {
                    ("Sushi Rolls", 3, 5), ("Hot Dishes", 2, 3), ("Nigiri", 3, 4), ("Sashimi", 2, 3), ("Appetizers", 1, 2)
                },
                ["last_wave"] = new[] {
                    ("Desserts", 1, 2), ("Sushi Rolls", 1, 2), ("Nigiri", 1, 2)
                }
            };

            foreach (var order in orders)
            {
                // Find all orders sharing the same Bill_Id and order by creation time
                var sameBillOrders = orders.Where(o => o.Bill_Id == order.Bill_Id)
                                           .OrderBy(o => o.Created_At)
                                           .ToList();

                var idx = sameBillOrders.FindIndex(o => o.Order_Id == order.Order_Id);

                var pattern = idx switch
                {
                    0 => "first_wave",
                    var i when i == sameBillOrders.Count - 1 => "last_wave",
                    _ => "middle_wave"
                };

                foreach (var (category, min, max) in wavePatterns[pattern])
                {
                    if (!byCategory.ContainsKey(category))
                        continue;

                    int numItems = _rng.Next(min, max + 1);
                    var chosenItems = byCategory[category]
                        .OrderBy(_ => _rng.Next())
                        .Take(Math.Min(numItems, byCategory[category].Count))
                        .ToList();

                    foreach (var item in chosenItems)
                    {
                        // Get price for (MenuId, ItemId) or default to 0
                        decimal unitPrice = priceLookup.GetValueOrDefault((order.DiningSession.Menu_Id, item.item_id), 0m);


                        // Quantity logic by category
                        int quantity = category switch
                        {
                            "Sashimi" or "Nigiri" => _rng.Next(2, 5),
                            "Desserts" => 1,
                            _ => _rng.Next(1, 3)
                        };

                        var orderItem = new OrderItems
                        {
                            Order_Key = order.Order_Id,
                            Menu_Id = order.DiningSession.Menu_Id,
                            Item_Id = item.item_id,
                            Quantity = quantity,
                            Price_At_Time = unitPrice,
                            Order_Item_Status = order.Status,
                            Completed_At = order.Completed_At
                        };

                        _context.OrderItems.Add(orderItem);
                        created++;

                        if (created % 100 == 0)
                        {
                            //_context.SaveChanges();
                        }
                    }
                }
            }

            //_context.SaveChanges();

            var totalPending = _context.OrderItems.Count(i => i.Order_Item_Status == OrderStatus.Pending);
            var totalCompleted = _context.OrderItems.Count(i => i.Order_Item_Status == OrderStatus.Delivered);
            _logger.LogInformation($"Created {created} order items ({totalPending} pending, {totalCompleted} completed)");
        }
    }
}
