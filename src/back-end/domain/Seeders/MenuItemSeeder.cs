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
    /// Seeds menu items and their menu assignments from the Sushi Toshi dine-in menu.
    ///
    /// Pricing:
    ///   - AYCE items: free; Price is only what an extra costs over a Charge limit (0 otherwise).
    ///   - Add-ons and drinks: Is_Add_On = true, charged at Price every time.
    ///   - Hot Stone: Price = 5.00, charged only for each item over the category's 1-per-person limit.
    ///   - Leftovers are a flat fee per table on the bill, not per item.
    ///
    /// Limits (0 = unlimited; toddler 0 = same as child):
    ///   - Per-order limits apply to each item type, set from the category config below.
    ///   - Chop Chop nigiri adds a per-person limit of 2 per dining session.
    ///   - Sashimi (30 per order) and Hot Stone (1 per person) are category limits, set in CategorySeeder.
    ///
    /// Item names must be unique, so items that appear in more than one section
    /// carry their section in the name "Salmon Sashimi", "Salmon Nigiri".
    /// </summary>
    public class MenuItemSeeder : ISeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MenuItemSeeder> _logger;

        public MenuItemSeeder(ApplicationDbContext context, ILogger<MenuItemSeeder> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private static readonly Dictionary<string, CategoryConfig> CategoryConfigs = new()
        {
            // AYCE
            ["Sashimi"] = new CategoryConfig(IsAddOn: false, PerOrderLimit: 0, OverLimitPrice: 0m), // 30 per order is a category limit
            ["Nigiri"] = new CategoryConfig(IsAddOn: false, PerOrderLimit: 1, OverLimitPrice: 0m),
            ["Maki"] = new CategoryConfig(IsAddOn: false, PerOrderLimit: 2, OverLimitPrice: 0m),
            ["Rolls"] = new CategoryConfig(IsAddOn: false, PerOrderLimit: 2, OverLimitPrice: 0m),
            ["Temaki"] = new CategoryConfig(IsAddOn: false, PerOrderLimit: 1, OverLimitPrice: 0m),
            ["Hot Stone"] = new CategoryConfig(IsAddOn: false, PerOrderLimit: 0, OverLimitPrice: 5.00m),
            ["Cooked/Hot"] = new CategoryConfig(IsAddOn: false, PerOrderLimit: 0, OverLimitPrice: 0m),
            ["Kitchen"] = new CategoryConfig(IsAddOn: false, PerOrderLimit: 1, OverLimitPrice: 0m),
            ["Deep Fried"] = new CategoryConfig(IsAddOn: false, PerOrderLimit: 2, OverLimitPrice: 0m),

            // Charged
            ["Add On"] = new CategoryConfig(IsAddOn: true, PerOrderLimit: 0, OverLimitPrice: 0m),
            ["Wine"] = new CategoryConfig(IsAddOn: true, PerOrderLimit: 0, OverLimitPrice: 0m),
            ["Beer"] = new CategoryConfig(IsAddOn: true, PerOrderLimit: 0, OverLimitPrice: 0m),
            ["Sake & Soju"] = new CategoryConfig(IsAddOn: true, PerOrderLimit: 0, OverLimitPrice: 0m),
            ["Specialty Drinks"] = new CategoryConfig(IsAddOn: true, PerOrderLimit: 0, OverLimitPrice: 0m),
            ["Liquor"] = new CategoryConfig(IsAddOn: true, PerOrderLimit: 0, OverLimitPrice: 0m),
            ["Other Beverages"] = new CategoryConfig(IsAddOn: true, PerOrderLimit: 0, OverLimitPrice: 0m),
        };

        public void Seed()
        {
            SeedMenuItems();
            _context.SaveChanges();
            _logger.LogInformation("Menu items seeded successfully");

            SeedMenuItemAssignments();
            _context.SaveChanges();
            _logger.LogInformation("Menu item assignments seeded successfully");
        }

        private void SeedMenuItems()
        {
            var categories = _context.Categories.ToDictionary(c => c.Category_name, c => c.Category_id);
            var tags = _context.Tags.ToDictionary(t => t.tag_name, t => t);
            var menuData = GetMenuItemsData();
            var added = 0;

            foreach (var (categoryName, items) in menuData)
            {
                if (!categories.TryGetValue(categoryName, out var categoryId))
                {
                    _logger.LogWarning("Category '{Category}' not found in database. Skipping.", categoryName);
                    continue;
                }

                foreach (var itemData in items)
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
                            menuItem.MenuItemTags.Add(new MenuItemTag { Tag_id = tag.tag_id, Tag = tag });
                        }
                        else
                        {
                            _logger.LogWarning("Tag '{Tag}' not found. Skipping tag for '{Item}'.", tagName, itemData.Name);
                        }
                    }

                    _context.MenuItems.Add(menuItem);
                    added++;
                }
            }

            _logger.LogInformation("Added {Count} menu items across {Categories} categories", added, menuData.Count);
        }

        private void SeedMenuItemAssignments()
        {
            var menus = _context.Menus.ToList();
            var menuItems = _context.MenuItems.Include(m => m.Category).ToList();

            var seedsByName = GetMenuItemsData()
                .SelectMany(kvp => kvp.Value.Select(seed => (Category: kvp.Key, Seed: seed)))
                .ToDictionary(x => x.Seed.Name, x => x);

            var count = 0;

            foreach (var menu in menus)
            {
                foreach (var item in menuItems)
                {
                    if (!seedsByName.TryGetValue(item.Name, out var entry) ||
                        !CategoryConfigs.TryGetValue(entry.Category, out var config))
                    {
                        _logger.LogWarning("No seed config for '{Item}'. Skipping assignment.", item.Name);
                        continue;
                    }

                    var seed = entry.Seed;
                    // Add-ons: their menu price. Included items: what an extra costs over a Charge limit (or 0)
                    var price = config.IsAddOn ? seed.Price ?? 0m : config.OverLimitPrice;

                    _context.MenuItemAssignments.Add(new MenuItemAssignment
                    {
                        Menu_Id = menu.Menu_id,
                        Item_Id = item.item_id,
                        Price = price,
                        Is_Add_On = config.IsAddOn,
                        Status = MenuItemStatus.Available,
                        Adult_Limit = seed.AdultLimit ?? 0,
                        Child_limit = seed.ChildLimit ?? 0,
                        Senior_limit = seed.SeniorLimit ?? 0,
                        Tot_Limit = seed.TotLimit ?? 0,
                        Per_Order_Limit = seed.PerOrderLimit ?? config.PerOrderLimit,
                        Over_Limit_Behavior = OverLimitBehavior.Block
                    });
                    count++;
                }
            }

            _logger.LogInformation("Created {Count} menu item assignments across {Menus} menus", count, menus.Count);
        }

        private static Dictionary<string, List<ItemSeed>> GetMenuItemsData() => new()
        {
            ["Sashimi"] = new()
            {
                new("Salmon Sashimi", "Fresh sliced salmon", T("Raw", "Popular", "Gluten-Free")),
                new("Tuna Sashimi", "Fresh sliced tuna", T("Raw", "Popular", "Gluten-Free")),
                new("Red Snapper Sashimi", "Fresh sliced red snapper", T("Raw", "Gluten-Free")),
                new("Sweet Shrimp Sashimi", "Fresh sweet shrimp", T("Raw", "Contains Shellfish", "Gluten-Free")),
                new("Surf Clam Sashimi", "Sliced surf clam", T("Raw", "Contains Shellfish", "Gluten-Free")),
                new("Crab Stick Sashimi", "Sliced crab stick", T("Cooked", "Contains Shellfish")),
                new("Mackerel Sashimi", "Cured mackerel", T("Raw", "Gluten-Free")),
                new("Tamago Sashimi", "Sweet Japanese omelette", T("Cooked", "Vegetarian")),
                new("Butter Fish Sashimi", "Butter fish (white tuna)", T("Raw", "Gluten-Free")),
            },
            ["Nigiri"] = new()
            {
                new("Salmon Nigiri", "Salmon over seasoned rice", T("Raw", "Popular")),
                new("Tuna Nigiri", "Tuna over seasoned rice", T("Raw", "Popular")),
                new("Red Snapper Nigiri", "Red snapper over seasoned rice", T("Raw")),
                new("Ebi Nigiri", "Cooked shrimp over seasoned rice", T("Cooked", "Contains Shellfish")),
                new("Crab Stick Nigiri", "Crab stick over seasoned rice", T("Cooked", "Contains Shellfish")),
                new("Surf Clam Nigiri", "Surf clam over seasoned rice", T("Raw", "Contains Shellfish")),
                new("Tamago Nigiri", "Sweet Japanese omelette over seasoned rice", T("Cooked", "Vegetarian")),
                new("Saba Nigiri", "Mackerel over seasoned rice", T("Raw")),
                new("Chop Chop Nigiri", "Chopped scallop gunkan. Limit 2 per person.", T("Raw", "Contains Shellfish", "Chef's Special"))
                {
                    AdultLimit = 2, ChildLimit = 2, SeniorLimit = 2, TotLimit = 0   // per person per session; still 1 per order
                },
                new("Masago Nigiri", "Capelin roe gunkan", T("Raw")),
                new("Shredded Crab Nigiri", "Shredded crab gunkan", T("Cooked", "Contains Shellfish")),
                new("Avocado Nigiri", "Avocado over seasoned rice", T("Vegetarian", "Vegan")),
                new("Mango Nigiri", "Mango over seasoned rice", T("Vegetarian", "Vegan")),
                new("Inari Nigiri", "Seasoned bean curd", T("Vegetarian", "Vegan")),
                new("Shiitake Nigiri", "Shiitake mushroom over seasoned rice", T("Vegetarian", "Vegan")),
                new("Butter Fish Nigiri", "Butter fish (white tuna) over seasoned rice", T("Raw")),
                new("Sweet Shrimp Nigiri", "Sweet shrimp over seasoned rice", T("Raw", "Contains Shellfish")),
            },
            ["Add On"] = new()
            {
                new("Toro Sashimi", "Premium fatty tuna, $1 per piece", T("Raw", "Chef's Special")) { Price = 1.00m },
                new("Toro Nigiri", "Premium fatty tuna over seasoned rice, $1 per piece", T("Raw", "Chef's Special")) { Price = 1.00m },
                new("Unagi Sashimi", "Grilled freshwater eel, $1 per piece", T("Cooked")) { Price = 1.00m },
                new("Unagi Nigiri", "Grilled freshwater eel over seasoned rice, $1 per piece", T("Cooked")) { Price = 1.00m },
                new("Seared Salmon Belly Sashimi", "Seared salmon belly, $1 per piece", T("Chef's Special")) { Price = 1.00m },
                new("Seared Salmon Belly Nigiri", "Seared salmon belly over seasoned rice, $1 per piece", T("Chef's Special")) { Price = 1.00m },
                new("Nigitoro Roll", "Whole roll", T("Raw", "Chef's Special")) { Price = 9.95m },
                new("Green Tea Ice Cream", "Premium ice cream, $1 per scoop", T("Vegetarian")) { Price = 1.00m },
                new("Sesame Ice Cream", "Premium ice cream, $1 per scoop", T("Vegetarian")) { Price = 1.00m },
            },
            ["Maki"] = new()
            {
                new("Salmon Maki", "Small salmon roll", T("Raw")),
                new("Tekka Maki", "Small tuna roll", T("Raw")),
                new("Kappa Maki", "Small cucumber roll", T("Vegetarian", "Vegan")),
                new("California Maki", "Small California roll", T("Contains Shellfish")),
                new("Spicy Salmon Maki", "Small spicy salmon roll", T("Raw", "Spicy")),
                new("Spicy Tuna Maki", "Small spicy tuna roll", T("Raw", "Spicy")),
                new("Unagi Maki", "Small eel roll", T("Cooked")),
                new("Avocado Maki", "Small avocado roll", T("Vegetarian", "Vegan")),
            },
            ["Rolls"] = new()
            {
                new("B.T.S Roll", "Bacon, tomato, and cheese sausage inside, with cheddar cheese on top", T("Cooked")),
                new("Avocado Roll", "Avocado inside, with sesame seed on top", T("Vegetarian", "Vegan")),
                new("California Roll", "Crab meat, cucumber, and avocado inside, with sesame seed on top", T("Cooked", "Popular", "Contains Shellfish")),
                new("Tempura Shrimp Roll", "Tempura shrimp, plain mayonnaise, and avocado inside, with sesame seed on top", T("Cooked", "Contains Shellfish")),
                new("Smoke Salmon Roll", "Smoked salmon and cream cheese inside, with sesame seed on top", T("Popular")),
                new("Tempura Yam Roll", "Tempura yam inside, with sesame seed on top", T("Cooked", "Vegetarian")),
                new("Spicy Salmon Roll", "Chopped salmon mixed with gochujang, with sesame seed on top", T("Raw", "Spicy", "Popular")),
                new("Spicy Tuna Roll", "Chopped tuna mixed with gochujang, with sesame seed on top", T("Raw", "Spicy")),
                new("Teriyaki Chicken Roll", "Grilled chicken teriyaki and cucumber inside, with sesame seed on top", T("Cooked")),
                new("Vegetarian Roll", "Avocado, cucumber, and seasoned bean curd inside, with sesame seed on top", T("Vegetarian", "Vegan")),
                new("Unagi Roll", "Grilled sea eel and avocado inside, with sesame seed on top", T("Cooked")),
                new("Chop Chop Roll", "Chopped scallop mixed with plain mayonnaise and fish roe, avocado, with sesame seed on top", T("Raw", "Contains Shellfish")),
                new("Bacon Roll", "Bacon, cucumber, and cream cheese inside, with sesame seed on top", T("Cooked")),
                new("B.C Roll", "Grilled crispy salmon skin and cucumber inside, with sesame seed on top", T("Cooked")),
                new("Mango Roll", "Salmon and cream cheese inside, mango slices on top, dressed with mango mayo", T("Raw", "Popular")),
                new("Hurricane Roll", "Tempura shrimp and cucumber inside, spicy tuna on top, dressed with tempura crunch and green onions", T("Spicy", "Contains Shellfish", "Chef's Special")),
                new("Canadian Roll", "Crab stick, sushi shrimp, cucumber, and plain mayonnaise inside, with masago on top", T("Cooked", "Contains Shellfish")),
                new("Alberta Roll", "Grilled teriyaki beef and cucumber inside, with sesame seed on top", T("Cooked")),
                new("Alaska Roll", "Crab meat and tamago inside, sushi shrimp on top, dressed with house special mayo", T("Cooked", "Contains Shellfish")),
                new("Sunset Roll", "Salmon, avocado, masago, and plain mayonnaise inside, crab stick on top, dressed with cheese sauce and unagi sauce", T("Contains Shellfish", "Chef's Special")),
            },
            ["Temaki"] = new()
            {
                new("Salmon Temaki", "Salmon hand roll cone", T("Raw")),
                new("Tuna Temaki", "Tuna hand roll cone", T("Raw")),
                new("Spicy Salmon Temaki", "Spicy salmon hand roll cone", T("Raw", "Spicy")),
                new("Spicy Tuna Temaki", "Spicy tuna hand roll cone", T("Raw", "Spicy")),
                new("Avocado Temaki", "Avocado hand roll cone", T("Vegetarian", "Vegan")),
                new("Veggie Temaki", "Vegetable hand roll cone", T("Vegetarian", "Vegan")),
                new("Unagi Temaki", "Eel hand roll cone", T("Cooked")),
                new("California Temaki", "California hand roll cone", T("Cooked", "Contains Shellfish")),
                new("Canadian Temaki", "Canadian hand roll cone", T("Cooked", "Contains Shellfish")),
                new("Tempura Shrimp Temaki", "Tempura shrimp hand roll cone", T("Cooked", "Contains Shellfish")),
            },
            ["Hot Stone"] = new()
            {
                new("Hot Stone Steak", "6 oz steak. Choose a dipping sauce: ginger, sweet soy, soya butter, or sesame.", T("Cooked", "Popular")),
                new("Hot Stone Pork Belly", "8 oz pork belly. Choose a dipping sauce: ginger, sweet soy, soya butter, or sesame.", T("Cooked")),
                new("Hot Stone Shrimp", "5 shrimp. Choose a dipping sauce: ginger, sweet soy, soya butter, or sesame.", T("Cooked", "Contains Shellfish")),
                new("Hot Stone Basa Fillet", "8 oz basa fillet. Choose a dipping sauce: ginger, sweet soy, soya butter, or sesame.", T("Cooked")),
                new("Hot Stone Vegetarian", "Assorted vegetables. Choose a dipping sauce: ginger, sweet soy, soya butter, or sesame.", T("Cooked", "Vegetarian")),
            },
            ["Cooked/Hot"] = new()
            {
                new("Miso Soup", "Traditional soybean soup", T("Vegetarian")),
                new("Seaweed Soup", "Seaweed soup", T("Vegetarian")),
                new("Wonton Soup", "Wonton dumplings in broth", T("Cooked")),
                new("Kimchi", "Spicy fermented cabbage", T("Spicy", "Vegetarian")),
                new("Edamame Beans", "Steamed soybeans", T("Vegetarian", "Vegan", "Gluten-Free")),
                new("Beef Tataki", "Lightly seared, marinated fillet beef, served with citrus soy dipping sauce", T("Chef's Special")),
                new("Salmon Poke", "Salmon poke bowl", T("Raw")),
                new("Tuna Poke", "Tuna poke bowl", T("Raw")),
                new("Sunset Poke", "Salmon, tuna, chopped cucumber, and avocado mixed with house special poke sauce, with tempura crunch and unagi sauce on top", T("Raw", "Chef's Special")),
                new("Rainbow Salad", "Mixed greens with fresh vegetables", T("Vegetarian", "Vegan")),
                new("Seaweed Salad", "Seasoned seaweed salad", T("Vegetarian", "Vegan")),
            },
            ["Kitchen"] = new()
            {
                new("Beef Short Rib", "Grilled beef short rib", T("Cooked", "Popular")),
                new("Baked Hot House Mushroom", "Baked mushrooms", T("Cooked", "Vegetarian")),
                new("Crab Meat Fried Rice", "Fried rice with crab meat", T("Cooked", "Contains Shellfish")),
                new("Grilled Mushroom", "Grilled mushrooms", T("Cooked", "Vegetarian")),
                new("Grilled Eggplant", "Grilled eggplant", T("Cooked", "Vegetarian")),
                new("Grilled Zucchini", "Grilled zucchini", T("Cooked", "Vegetarian")),
                new("Grilled Pumpkin", "Grilled pumpkin", T("Cooked", "Vegetarian")),
                new("Grilled Sweet Potato", "Grilled sweet potato", T("Cooked", "Vegetarian")),
                new("Grilled Pork Belly", "Grilled pork belly", T("Cooked")),
                new("Stir Fried Udon", "Choose from chicken, veggie, beef, or seafood", T("Cooked")),
                new("Udon Soup", "Choose from chicken, veggie, beef, seafood, or pork belly", T("Cooked")),
                new("Gyoza", "Pan fried dumplings", T("Cooked", "Popular")),
                new("Beef Enoki Wrap", "Beef wrapped around enoki mushrooms", T("Cooked")),
                new("Pan Fried Mixed Veggies", "Pan fried mixed vegetables", T("Cooked", "Vegetarian")),
                new("Pan Fried Bean Sprout with Enoki", "Pan fried bean sprouts with enoki mushrooms", T("Cooked", "Vegetarian")),
                new("Teriyaki Basa", "Basa fillet with teriyaki sauce", T("Cooked")),
                new("Teriyaki Beef", "Beef with teriyaki sauce", T("Cooked")),
                new("Teriyaki Chicken", "Chicken with teriyaki sauce", T("Cooked", "Popular")),
                new("Shigureni Beef", "Japanese style beef stew", T("Cooked")),
                new("Pork Belly Roll", "Rolled pork belly", T("Cooked")),
            },
            ["Deep Fried"] = new()
            {
                new("Chicken Karaage", "Japanese fried chicken", T("Cooked", "Popular")),
                new("Vegetarian Spring Roll", "Vegetable spring roll", T("Cooked", "Vegetarian")),
                new("Chicken Wing", "Fried chicken wing", T("Cooked")),
                new("Agedashi Tofu", "Fried tofu in dashi broth", T("Cooked", "Vegetarian")),
                new("Green Onion Cake", "Fried green onion cake", T("Cooked", "Vegetarian")),
                new("Shrimp & Cheese Spring Roll", "Shrimp and cheese spring roll", T("Cooked", "Contains Shellfish")),
                new("Golden Dragon Eye", "Deep fried roll with salmon and avocado inside, served with spicy mayo", T("Cooked", "Spicy")),
                new("Golden California", "Deep fried California roll", T("Cooked", "Contains Shellfish")),
                new("Golden Budda", "Deep fried roll with avocado, cucumber, and seasoned bean curd inside", T("Cooked", "Vegetarian")),
                new("Golden Big Eye", "Deep fried futomaki with sriracha salmon and cucumber inside, served with spicy mayo", T("Cooked", "Spicy")),
                new("Golden Philadelphia", "Deep fried roll with salmon and cream cheese inside, served with cheese mayo", T("Cooked")),
                new("Mango Shrimp", "Fried shrimp with mango and mayo", T("Cooked", "Contains Shellfish")),
                new("Deep Fried Gyoza", "Deep fried dumplings", T("Cooked")),
                new("Tempura Basa", "Basa fillet tempura", T("Cooked")),
                new("Tempura Broccoli", "Broccoli tempura", T("Cooked", "Vegetarian")),
                new("Tempura Crab Stick", "Crab stick tempura", T("Cooked", "Contains Shellfish")),
                new("Tempura Eggplant", "Eggplant tempura", T("Cooked", "Vegetarian")),
                new("Tempura Pumpkin", "Pumpkin tempura", T("Cooked", "Vegetarian")),
                new("Tempura Shrimp", "Shrimp tempura", T("Cooked", "Contains Shellfish", "Popular")),
                new("Tempura Yam", "Yam tempura", T("Cooked", "Vegetarian")),
                new("Tempura Zucchini", "Zucchini tempura", T("Cooked", "Vegetarian")),
                new("Tempura Squid", "Squid tempura", T("Cooked", "Contains Shellfish")),
            },

            // Drinks (all charged at Price)
            ["Wine"] = new()
            {
                new("Jackson Triggs Cabernet Sauvignon", "Red, by the bottle", T()) { Price = 29.00m },
                new("Lonely Shaman Semi-Sweet Merlot Cabernet", "Red, by the bottle", T()) { Price = 39.00m },
                new("Barefoot Moscato", "White, by the bottle", T()) { Price = 27.00m },
                new("Beringer Chardonnay", "White, by the bottle", T()) { Price = 29.00m },
                new("House Wine 6 oz", "Peller Estates Shiraz or Chardonnay", T()) { Price = 7.50m },
                new("House Wine 9 oz", "Peller Estates Shiraz or Chardonnay", T()) { Price = 9.95m },
                new("House Wine 1/2 Litre", "Peller Estates Shiraz or Chardonnay", T()) { Price = 18.00m },
                new("House Wine Full Litre", "Peller Estates Shiraz or Chardonnay", T()) { Price = 32.95m },
                new("La Marca Prosecco", "Sparkling, by the bottle", T()) { Price = 39.00m },
            },
            ["Beer"] = new()
            {
                new("Canadian", "Domestic bottled beer", T()) { Price = 6.50m },
                new("Budweiser", "Domestic bottled beer", T()) { Price = 6.50m },
                new("Coors Light", "Domestic bottled beer", T()) { Price = 6.50m },
                new("Kokanee", "Domestic bottled beer", T()) { Price = 6.50m },
                new("MGD", "Domestic bottled beer", T()) { Price = 6.50m },
                new("Sleeman's Honey Brown", "Domestic bottled beer", T()) { Price = 6.50m },
                new("Corona", "Imported bottled beer", T()) { Price = 7.50m },
                new("Heineken", "Imported bottled beer", T()) { Price = 7.50m },
                new("Stella Artois", "Imported bottled beer", T()) { Price = 7.50m },
                new("Guinness Draught", "Imported bottled beer", T()) { Price = 7.50m },
                new("1664 Kronenbourg Blanc", "Imported bottled beer", T()) { Price = 7.50m },
                new("Sapporo 500 ml", "Japanese beer", T()) { Price = 7.95m },
                new("Sapporo 650 ml", "Japanese beer", T()) { Price = 9.95m },
                new("Asahi 500 ml", "Japanese beer", T()) { Price = 7.95m },
                new("Asahi 2000 ml", "Japanese beer", T()) { Price = 29.95m },
            },
            ["Sake & Soju"] = new()
            {
                new("Gekkeikan Sake 5 oz", "Warm or cold", T()) { Price = 6.95m },
                new("Gekkeikan Sake 10 oz", "Warm or cold", T()) { Price = 11.95m },
                new("Gekkeikan Sake 750 ml", "Warm or cold", T()) { Price = 28.95m },
                new("Peach Momo Sake 300 ml", "Peach flavoured sake, by the bottle", T()) { Price = 33.00m },
                new("Soju 360 ml", "Green grape or peach, by the bottle", T()) { Price = 17.00m },
            },
            ["Specialty Drinks"] = new()
            {
                new("Hennessy with Sweet Green Tea", "Specialty drink", T()) { Price = 10.00m },
                new("Bacardi Breezer", "Pineapple or orange", T()) { Price = 6.00m },
                new("Smirnoff Ice", "Specialty drink", T()) { Price = 6.00m },
                new("Mike's Hard Lemonade", "Specialty drink", T()) { Price = 6.00m },
                new("Rock Creek Cider", "Specialty drink", T()) { Price = 6.25m },
                new("Palm Bay Strawberry - Pineapple", "Specialty drink", T()) { Price = 6.00m },
            },
            ["Liquor"] = new()
            {
                new("Bombay Sapphire Gin", "1 oz", T()) { Price = 6.95m },
                new("Captain Morgan's Rum", "1 oz", T()) { Price = 6.95m },
                new("Crown Royal Whiskey", "1 oz", T()) { Price = 6.95m },
                new("Absolut Vodka", "1 oz, Citron or Cherry", T()) { Price = 6.95m },
                new("Jose Cuervo Gold Tequila", "1 oz", T()) { Price = 6.95m },
                new("Soho Lychee Liqueur", "1 oz", T()) { Price = 6.95m },
            },
            ["Other Beverages"] = new()
            {
                new("Tea", "Hot tea", T()) { Price = 2.95m },
                new("Perrier Water", "Sparkling water", T()) { Price = 3.75m },
                new("Coffee", "Hot coffee", T()) { Price = 2.95m },
                new("Juice Small", "Apple, orange, or cranberry", T()) { Price = 3.75m },
                new("Juice Large", "Apple, orange, or cranberry", T()) { Price = 5.25m },
                new("Specialty Juice Small", "Mango, Pineapple, or Clamato", T()) { Price = 4.55m },
                new("Specialty Juice Large", "Mango, Pineapple, or Clamato", T()) { Price = 5.25m },
            },
        };

        private static string[] T(params string[] tags) => tags;

        /// <summary>One menu item. Optional properties override the category config for this item only.</summary>
        private sealed record ItemSeed(string Name, string Description, string[] Tags)
        {
            public decimal? Price { get; init; }        // add-ons and drinks only
            public int? AdultLimit { get; init; }       // per person, per dining session
            public int? ChildLimit { get; init; }
            public int? SeniorLimit { get; init; }
            public int? TotLimit { get; init; }         // 0 = same as child
            public int? PerOrderLimit { get; init; }    // overrides the category default
        }

        /// <summary>Defaults for every item in a category.</summary>
        private sealed record CategoryConfig(bool IsAddOn, int PerOrderLimit, decimal OverLimitPrice);
    }
}