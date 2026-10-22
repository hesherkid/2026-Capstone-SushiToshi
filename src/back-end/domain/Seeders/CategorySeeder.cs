using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using back_end.domain.enums;

namespace back_end.domain.Seeders
{
    /// <summary>
    /// Seeds the categories table from the Sushi Toshi dine-in menu
    /// Limit rules: 0 = unlimited; toddler 0 = same as child.
    /// Category-level limits only where the menu applies them to the whole section:
    ///   - Sashimi: max 30 per order (whole table, all sashimi combined), Block
    ///   - Hot Stone: max 1 per person per dining session, extras charged (each items price), Charge
    /// Per-item per-order limits (Nigiri, Maki, Rolls, Temaki, Kitchen, Deep Fried) are set on the
    /// menu assignments in MenuItemSeeder, because they apply to each item type separately.
    /// </summary>
    public class CategorySeeder : ISeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CategorySeeder> _logger;

        public CategorySeeder(ApplicationDbContext context, ILogger<CategorySeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public void Seed()
        {
            var categories = new List<Category>
            {
                //  All you can eat 
                Unlimited("Sashimi", "Fresh sliced raw fish, ordered by the piece. Maximum 30 pieces per order.", perOrderLimit: 30),
                Unlimited("Nigiri", "Hand-pressed sushi. 1 piece of each type per order."),
                Unlimited("Maki", "Small rolls. 2 of each type per order."),
                Unlimited("Rolls", "Inside-out and specialty rolls. 2 of each type per order."),
                Unlimited("Temaki", "Hand rolled cones. 1 of each type per order."),
                new Category
                {
                    Category_name = "Hot Stone",
                    Description = "Cooked at your table on a hot stone, with your choice of dipping sauce. Maximum 1 per person; $5 for each extra item.",
                    adult_limit = 1,
                    child_limit = 1,
                    senior_limit = 1,
                    toddler_limit = 0,
                    per_order_limit = 0,
                    over_limit_behavior = OverLimitBehavior.Charge
                },
                Unlimited("Cooked/Hot", "Appetizers, soups, and salads."),
                Unlimited("Kitchen", "Grilled, pan fried, and udon dishes. 1 of each type per order."),
                Unlimited("Deep Fried", "Tempura, and fried appetizers. 2 of each type per order."),

                //  Charged (add-ons and drinks) 
                Unlimited("Add On", "Premium sashimi, nigiri, rolls, and ice cream charged per piece."),
                Unlimited("Wine", "Red, white, house, and sparkling wine."),
                Unlimited("Beer", "Domestic, imported, and Japanese bottled beer."),
                Unlimited("Sake & Soju", "Warm or cold sake, flavoured sake, and soju."),
                Unlimited("Specialty Drinks", "Coolers, ciders, and specialty mixed drinks."),
                Unlimited("Liquor", "1 oz pours of spirits and liqueurs."),
                Unlimited("Other Beverages", "Tea, coffee, sparkling water, and juice.")
            };

            _context.Categories.AddRange(categories);
            _logger.LogInformation("Added {Count} categories", categories.Count);
        }

        // Category with no per-person limits (0 = unlimited) and an optional per-order cap for the whole table
        private static Category Unlimited(string name, string description, int perOrderLimit = 0) => new Category
        {
            Category_name = name,
            Description = description,
            adult_limit = 0,
            child_limit = 0,
            senior_limit = 0,
            toddler_limit = 0,
            per_order_limit = perOrderLimit,
            over_limit_behavior = OverLimitBehavior.Block
        };
    }
}