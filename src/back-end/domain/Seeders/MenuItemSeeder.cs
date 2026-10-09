using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using back_end.domain;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using back_end.domain.enums;

namespace back_end.domain.Seeders
{
    public class MenuItemSeeder : ISeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MenuItemSeeder> _logger;

        public MenuItemSeeder(ApplicationDbContext context, ILogger<MenuItemSeeder> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Seed()
        {
            SeedMenuItems();
            _context.SaveChanges();
            _logger.LogInformation("Menu items seeded successfully");

            SeedMenuItemAssignments();
            //_context.SaveChanges();
            _logger.LogInformation("Menu item assignments seeded successfully");
        }

        private void SeedMenuItems()
        {
            var categories = _context.Categories.ToDictionary(c => c.Category_name, c => c.Category_id);
            var tags = _context.Tags.ToDictionary(t => t.tag_name, t => t);

            var menuItemsData = GetMenuItemsData();

            foreach (var categoryData in menuItemsData)
            {
                if (!categories.TryGetValue(categoryData.Key, out var categoryId))
                {
                    _logger.LogWarning($"Category '{categoryData.Key}' not found in database. Skipping.");
                    continue;
                }

                foreach (var itemData in categoryData.Value)
                {
                    var menuItem = new Menu_Item
                    {
                        Name = itemData.Name,
                        Description = itemData.Description,
                        Category_id = categoryId,
                        Status = MenuItemStatus.Available,
                        image_url = null,
                        MenuItemTags = new List<MenuItemTag>()
                    };

                    foreach (var tagName in itemData.Tags)
                    {
                        if (tags.TryGetValue(tagName, out var tag))
                        {
                            menuItem.MenuItemTags.Add(new MenuItemTag
                            {
                                Tag_id = tag.tag_id,
                                Tag = tag
                            });
                        }
                        else
                        {
                            _logger.LogWarning($"Tag '{tagName}' not found in database. Skipping tag for menu item '{itemData.Name}'.");
                        }
                    }

                    _context.MenuItems.Add(menuItem);
                }
            }

            var totalItems = menuItemsData.Sum(kvp => kvp.Value.Count);
            _logger.LogInformation($"Added {totalItems} menu items across {menuItemsData.Count} categories");
        }

        private void SeedMenuItemAssignments()
        {
            var menus = _context.Menus.ToList();
            _logger.LogInformation($"MENUS FOUND: {menus.Count}");
            var menuItems = _context.MenuItems.Include(m => m.Category).ToList();
            _logger.LogInformation($"MENU ITEMS FOUND: {menuItems.Count}");
            var categoryConfigs = new Dictionary<string, CategoryConfig>
            {
                ["Sushi Rolls"] = new CategoryConfig { RegularPrice = 12.99m, AdultLimit = 4, ChildLimit = 3, SeniorLimit = 3, TotLimit = 2 },
                ["Nigiri"] = new CategoryConfig { RegularPrice = 8.99m, AdultLimit = 6, ChildLimit = 4, SeniorLimit = 5, TotLimit = 2 },
                ["Sashimi"] = new CategoryConfig { RegularPrice = 14.99m, AdultLimit = 3, ChildLimit = 2, SeniorLimit = 2, TotLimit = 1 },
                ["Appetizers"] = new CategoryConfig { RegularPrice = 7.99m, AdultLimit = 3, ChildLimit = 2, SeniorLimit = 2, TotLimit = 1 },
                ["Hot Dishes"] = new CategoryConfig { RegularPrice = 15.99m, AdultLimit = 2, ChildLimit = 1, SeniorLimit = 1, TotLimit = 1 },
                ["Desserts"] = new CategoryConfig { RegularPrice = 6.99m, AdultLimit = 2, ChildLimit = 2, SeniorLimit = 2, TotLimit = 1 }
            };

            foreach (var config in categoryConfigs.Values)
            {
                config.LunchPrice = Math.Round(config.RegularPrice * 0.8m, 2);
            }

            int assignmentsCount = 0;

            foreach (var menu in menus)
            {
                var randomNumber = new Random();
                foreach (var item in menuItems)
                {
                    if (!categoryConfigs.TryGetValue(item.Category.Category_name, out var categoryConfig))
                    {
                        _logger.LogWarning($"Category config not found for category '{item.Category.Category_name}'. Skipping menu item assignment.");
                        continue;
                    }

                    var price = menu.Name == "Lunch Special" ? categoryConfig.LunchPrice : categoryConfig.RegularPrice;

                    var assignment = new MenuItemAssignment
                    {
                        Menu_Id = menu.Menu_id,
                        Item_Id = item.item_id,
                        Price = price,
                        Adult_Limit = categoryConfig.AdultLimit,
                        Child_limit = categoryConfig.ChildLimit,
                        Senior_limit = categoryConfig.SeniorLimit,
                        Tot_Limit = categoryConfig.TotLimit,
                        Total_Units_Ordered = randomNumber.Next(100),
                        Status = MenuItemStatus.Available,
                        Is_Add_On = false
                    };

                    _context.MenuItemAssignments.Add(assignment);
                    assignmentsCount++;

                    if (assignmentsCount % 50 == 0)
                    {
                        //_context.SaveChanges();
                    }
                }
            }

            //_context.SaveChanges();

            _logger.LogInformation($"Created {assignmentsCount} menu item assignments");
        }

        // Creates at least 15 items per category
        private Dictionary<string, List<MenuItemData>> GetMenuItemsData()
        {
            return new Dictionary<string, List<MenuItemData>>
            {
                ["Sushi Rolls"] = new List<MenuItemData>
                {
                    new MenuItemData { Name = "California Roll", Description = "Crab meat, avocado, cucumber", Tags = new[] { "Popular", "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Spicy Tuna Roll", Description = "Fresh tuna, spicy mayo, cucumber", Tags = new[] { "Spicy", "Raw", "Popular" } },
                    new MenuItemData { Name = "Dragon Roll", Description = "Eel, cucumber, topped with avocado", Tags = new[] { "Cooked", "Popular" } },
                    new MenuItemData { Name = "Rainbow Roll", Description = "California roll topped with assorted sashimi", Tags = new[] { "Raw", "Popular", "Contains Shellfish" } },
                    new MenuItemData { Name = "Philadelphia Roll", Description = "Smoked salmon, cream cheese, cucumber", Tags = new[] { "Popular", "Contains Shellfish" } },
                    new MenuItemData { Name = "Spider Roll", Description = "Soft shell crab, avocado, cucumber, spicy mayo", Tags = new[] { "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Vegetable Roll", Description = "Assorted fresh vegetables", Tags = new[] { "Vegetarian", "Vegan" } },
                    new MenuItemData { Name = "Tempura Roll", Description = "Shrimp tempura, avocado, cucumber", Tags = new[] { "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Salmon Avocado Roll", Description = "Fresh salmon, avocado", Tags = new[] { "Raw", "Popular" } },
                    new MenuItemData { Name = "Spicy Scallop Roll", Description = "Scallop, spicy mayo, cucumber", Tags = new[] { "Spicy", "Raw", "Contains Shellfish" } },
                    new MenuItemData { Name = "Dynamite Roll", Description = "Shrimp tempura, spicy mayo, avocado", Tags = new[] { "Spicy", "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Boston Roll", Description = "Shrimp, lettuce, cucumber, mayo", Tags = new[] { "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Alaska Roll", Description = "Salmon, avocado, cucumber", Tags = new[] { "Raw", "Popular" } },
                    new MenuItemData { Name = "Caterpillar Roll", Description = "Eel, cucumber, topped with avocado", Tags = new[] { "Cooked", "Popular" } },
                    new MenuItemData { Name = "Cucumber Roll", Description = "Fresh cucumber", Tags = new[] { "Vegetarian", "Vegan" } }
                },
                ["Nigiri"] = new List<MenuItemData>
                {
                    new MenuItemData { Name = "Salmon Nigiri", Description = "Fresh salmon over seasoned rice", Tags = new[] { "Raw", "Popular" } },
                    new MenuItemData { Name = "Tuna Nigiri", Description = "Fresh tuna over seasoned rice", Tags = new[] { "Raw", "Popular" } },
                    new MenuItemData { Name = "Yellowtail Nigiri", Description = "Fresh yellowtail over seasoned rice", Tags = new[] { "Raw", "Popular" } },
                    new MenuItemData { Name = "Eel Nigiri", Description = "Grilled eel over seasoned rice", Tags = new[] { "Cooked", "Popular" } },
                    new MenuItemData { Name = "Shrimp Nigiri", Description = "Cooked shrimp over seasoned rice", Tags = new[] { "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Octopus Nigiri", Description = "Cooked octopus over seasoned rice", Tags = new[] { "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Red Snapper Nigiri", Description = "Fresh red snapper over seasoned rice", Tags = new[] { "Raw" } },
                    new MenuItemData { Name = "Mackerel Nigiri", Description = "Cured mackerel over seasoned rice", Tags = new[] { "Raw" } },
                    new MenuItemData { Name = "Scallop Nigiri", Description = "Fresh scallop over seasoned rice", Tags = new[] { "Raw", "Contains Shellfish" } },
                    new MenuItemData { Name = "Albacore Nigiri", Description = "Seared albacore tuna over seasoned rice", Tags = new[] { "Raw" } },
                    new MenuItemData { Name = "Sea Urchin Nigiri", Description = "Fresh sea urchin over seasoned rice", Tags = new[] { "Raw", "Chef's Special" } },
                    new MenuItemData { Name = "Salmon Roe Nigiri", Description = "Salmon roe over seasoned rice", Tags = new[] { "Raw" } },
                    new MenuItemData { Name = "Sweet Shrimp Nigiri", Description = "Raw sweet shrimp over seasoned rice", Tags = new[] { "Raw", "Contains Shellfish" } },
                    new MenuItemData { Name = "Sea Bass Nigiri", Description = "Fresh sea bass over seasoned rice", Tags = new[] { "Raw" } },
                    new MenuItemData { Name = "Egg Nigiri", Description = "Sweet egg omelet over seasoned rice", Tags = new[] { "Cooked", "Vegetarian" } }
                },
                ["Sashimi"] = new List<MenuItemData>
                {
                    new MenuItemData { Name = "Salmon Sashimi", Description = "Fresh sliced salmon (5 pieces)", Tags = new[] { "Raw", "Popular", "Gluten-Free" } },
                    new MenuItemData { Name = "Tuna Sashimi", Description = "Fresh sliced tuna (5 pieces)", Tags = new[] { "Raw", "Popular", "Gluten-Free" } },
                    new MenuItemData { Name = "Yellowtail Sashimi", Description = "Fresh sliced yellowtail (5 pieces)", Tags = new[] { "Raw", "Popular", "Gluten-Free" } },
                    new MenuItemData { Name = "Albacore Sashimi", Description = "Fresh sliced albacore tuna (5 pieces)", Tags = new[] { "Raw", "Gluten-Free" } },
                    new MenuItemData { Name = "Red Snapper Sashimi", Description = "Fresh sliced red snapper (5 pieces)", Tags = new[] { "Raw", "Gluten-Free" } },
                    new MenuItemData { Name = "Octopus Sashimi", Description = "Thinly sliced octopus (5 pieces)", Tags = new[] { "Raw", "Contains Shellfish", "Gluten-Free" } },
                    new MenuItemData { Name = "Scallop Sashimi", Description = "Fresh sliced scallop (5 pieces)", Tags = new[] { "Raw", "Contains Shellfish", "Gluten-Free" } },
                    new MenuItemData { Name = "Sea Bass Sashimi", Description = "Fresh sliced sea bass (5 pieces)", Tags = new[] { "Raw", "Gluten-Free" } },
                    new MenuItemData { Name = "Mackerel Sashimi", Description = "Fresh sliced mackerel (5 pieces)", Tags = new[] { "Raw", "Gluten-Free" } },
                    new MenuItemData { Name = "Sweet Shrimp Sashimi", Description = "Fresh sweet shrimp (5 pieces)", Tags = new[] { "Raw", "Contains Shellfish", "Gluten-Free" } },
                    new MenuItemData { Name = "Salmon Belly Sashimi", Description = "Premium salmon belly (5 pieces)", Tags = new[] { "Raw", "Chef's Special", "Gluten-Free" } },
                    new MenuItemData { Name = "Tuna Belly Sashimi", Description = "Premium tuna belly (5 pieces)", Tags = new[] { "Raw", "Chef's Special", "Gluten-Free" } },
                    new MenuItemData { Name = "Yellowtail Belly Sashimi", Description = "Premium yellowtail belly (5 pieces)", Tags = new[] { "Raw", "Chef's Special", "Gluten-Free" } },
                    new MenuItemData { Name = "Sea Urchin Sashimi", Description = "Fresh sea urchin (5 pieces)", Tags = new[] { "Raw", "Chef's Special", "Gluten-Free" } },
                    new MenuItemData { Name = "Assorted Sashimi", Description = "Chef's selection of fresh fish (15 pieces)", Tags = new[] { "Raw", "Popular", "Gluten-Free" } }
                },
                ["Appetizers"] = new List<MenuItemData>
                {
                    new MenuItemData { Name = "Edamame", Description = "Steamed soybeans with sea salt", Tags = new[] { "Vegetarian", "Vegan", "Gluten-Free" } },
                    new MenuItemData { Name = "Gyoza", Description = "Pan-fried pork dumplings (6 pieces)", Tags = new[] { "Cooked", "Popular" } },
                    new MenuItemData { Name = "Vegetable Tempura", Description = "Assorted vegetables in tempura batter", Tags = new[] { "Vegetarian", "Cooked" } },
                    new MenuItemData { Name = "Shrimp Tempura", Description = "Crispy fried shrimp (4 pieces)", Tags = new[] { "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Miso Soup", Description = "Traditional Japanese soybean soup", Tags = new[] { "Vegetarian", "Gluten-Free" } },
                    new MenuItemData { Name = "Seaweed Salad", Description = "Seasoned seaweed with sesame dressing", Tags = new[] { "Vegetarian", "Vegan" } },
                    new MenuItemData { Name = "Agedashi Tofu", Description = "Fried tofu in dashi broth", Tags = new[] { "Vegetarian", "Cooked" } },
                    new MenuItemData { Name = "Takoyaki", Description = "Octopus balls with bonito flakes", Tags = new[] { "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Sunomono", Description = "Cucumber salad with vinegar dressing", Tags = new[] { "Vegetarian", "Vegan", "Gluten-Free" } },
                    new MenuItemData { Name = "Spring Rolls", Description = "Vegetable spring rolls (4 pieces)", Tags = new[] { "Vegetarian", "Vegan", "Cooked" } },
                    new MenuItemData { Name = "Karaage", Description = "Japanese fried chicken", Tags = new[] { "Cooked", "Popular" } },
                    new MenuItemData { Name = "Shumai", Description = "Steamed shrimp dumplings", Tags = new[] { "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Wakame Salad", Description = "Seasoned seaweed and cucumber salad", Tags = new[] { "Vegetarian", "Vegan" } },
                    new MenuItemData { Name = "Calamari", Description = "Fried squid rings with spicy mayo", Tags = new[] { "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Green Salad", Description = "Mixed greens with ginger dressing", Tags = new[] { "Vegetarian", "Vegan", "Gluten-Free" } }
                },
                ["Hot Dishes"] = new List<MenuItemData>
                {
                    new MenuItemData { Name = "Chicken Teriyaki", Description = "Grilled chicken with teriyaki sauce", Tags = new[] { "Cooked", "Popular" } },
                    new MenuItemData { Name = "Salmon Teriyaki", Description = "Grilled salmon with teriyaki sauce", Tags = new[] { "Cooked", "Popular" } },
                    new MenuItemData { Name = "Beef Teriyaki", Description = "Grilled beef with teriyaki sauce", Tags = new[] { "Cooked", "Popular" } },
                    new MenuItemData { Name = "Vegetable Udon", Description = "Thick noodles with vegetables in broth", Tags = new[] { "Vegetarian", "Cooked" } },
                    new MenuItemData { Name = "Tempura Udon", Description = "Udon noodles with shrimp tempura", Tags = new[] { "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Chicken Katsu", Description = "Breaded chicken cutlet with sauce", Tags = new[] { "Cooked", "Popular" } },
                    new MenuItemData { Name = "Tonkatsu", Description = "Breaded pork cutlet with sauce", Tags = new[] { "Cooked", "Popular" } },
                    new MenuItemData { Name = "Yakisoba", Description = "Stir-fried noodles with vegetables", Tags = new[] { "Cooked", "Vegetarian" } },
                    new MenuItemData { Name = "Beef Sukiyaki", Description = "Sliced beef and vegetables in hot pot", Tags = new[] { "Cooked", "Chef's Special" } },
                    new MenuItemData { Name = "Nabeyaki Udon", Description = "Hot pot with thick noodles and tempura", Tags = new[] { "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Vegetable Curry", Description = "Japanese curry with vegetables", Tags = new[] { "Vegetarian", "Cooked" } },
                    new MenuItemData { Name = "Chicken Curry", Description = "Japanese curry with chicken", Tags = new[] { "Cooked", "Popular" } },
                    new MenuItemData { Name = "Beef Curry", Description = "Japanese curry with beef", Tags = new[] { "Cooked", "Popular" } },
                    new MenuItemData { Name = "Seafood Udon", Description = "Udon noodles with mixed seafood", Tags = new[] { "Cooked", "Contains Shellfish" } },
                    new MenuItemData { Name = "Vegetable Tempura Bowl", Description = "Rice bowl with vegetable tempura", Tags = new[] { "Vegetarian", "Cooked" } }
                },
                ["Desserts"] = new List<MenuItemData>
                {
                    new MenuItemData { Name = "Mochi Ice Cream", Description = "Ice cream wrapped in rice cake (2 pieces)", Tags = new[] { "Vegetarian" } },
                    new MenuItemData { Name = "Green Tea Ice Cream", Description = "Traditional matcha flavored ice cream", Tags = new[] { "Vegetarian" } },
                    new MenuItemData { Name = "Red Bean Ice Cream", Description = "Sweet red bean flavored ice cream", Tags = new[] { "Vegetarian" } },
                    new MenuItemData { Name = "Tempura Ice Cream", Description = "Fried ice cream with chocolate sauce", Tags = new[] { "Vegetarian", "Cooked" } },
                    new MenuItemData { Name = "Matcha Cheesecake", Description = "Green tea flavored cheesecake", Tags = new[] { "Vegetarian" } },
                    new MenuItemData { Name = "Taiyaki", Description = "Fish-shaped cake with red bean filling", Tags = new[] { "Vegetarian", "Cooked" } },
                    new MenuItemData { Name = "Dorayaki", Description = "Red bean pancake sandwich", Tags = new[] { "Vegetarian", "Cooked" } },
                    new MenuItemData { Name = "Black Sesame Ice Cream", Description = "Nutty black sesame flavored ice cream", Tags = new[] { "Vegetarian", "Contains Nuts" } },
                    new MenuItemData { Name = "Mango Mochi", Description = "Sweet rice cake with mango filling", Tags = new[] { "Vegetarian" } },
                    new MenuItemData { Name = "Strawberry Daifuku", Description = "Mochi with strawberry and red bean", Tags = new[] { "Vegetarian" } },
                    new MenuItemData { Name = "Matcha Tiramisu", Description = "Green tea flavored tiramisu", Tags = new[] { "Vegetarian" } },
                    new MenuItemData { Name = "Banana Tempura", Description = "Fried banana with honey and ice cream", Tags = new[] { "Vegetarian", "Cooked" } },
                    new MenuItemData { Name = "Sesame Balls", Description = "Sweet rice balls with red bean filling", Tags = new[] { "Vegetarian", "Cooked", "Contains Nuts" } },
                    new MenuItemData { Name = "Fruit Plate", Description = "Seasonal fresh fruit assortment", Tags = new[] { "Vegetarian", "Vegan", "Gluten-Free" } },
                    new MenuItemData { Name = "Mixed Mochi Plate", Description = "Assorted mochi selection (6 pieces)", Tags = new[] { "Vegetarian" } }
                }
            };
        }

        private class MenuItemData
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string[] Tags { get; set; } = Array.Empty<string>();
        }

        private class CategoryConfig
        {
            public decimal RegularPrice { get; set; }
            public decimal LunchPrice { get; set; }
            public int AdultLimit { get; set; }
            public int ChildLimit { get; set; }
            public int SeniorLimit { get; set; }
            public int TotLimit { get; set; }
        }
    }
}