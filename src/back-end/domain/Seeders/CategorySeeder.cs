using Microsoft.Extensions.Logging;
using back_end.domain.Entities;
using back_end.domain.DbContexts;
using back_end.domain.enums;

namespace back_end.domain.Seeders
{
    /// <summary>
    /// Seeder for the categories table with initial data.
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
                new Category
                {
                    Category_name = "Sushi Rolls",
                    Description = "Traditional and specialty sushi rolls",
                    adult_limit = 0,
                    child_limit = 0,
                    senior_limit = 0,
                    toddler_limit = 0,
                    total_limit = 0
                },
                new Category
                {
                    Category_name = "Nigiri",
                    Description = "Hand-pressed sushi with fresh fish",
                    adult_limit = 0,
                    child_limit = 0,
                    senior_limit = 0,
                    toddler_limit = 0,
                    total_limit = 0
                },
                new Category
                {
                    Category_name = "Sashimi",
                    Description = "Fresh sliced raw fish",
                    adult_limit = 20,
                    child_limit = 20,
                    senior_limit = 20,
                    toddler_limit = 20,
                    total_limit = 0,
                    over_limit_behavior = OverLimitBehavior.Block
                },
                new Category
                {
                    Category_name = "Appetizers",
                    Description = "Starters and small dishes",
                    adult_limit = 0,
                    child_limit = 0,
                    senior_limit = 0,
                    toddler_limit = 0,
                    total_limit = 0
                },
                new Category
                {
                    Category_name = "Hot Dishes",
                    Description = "Cooked meals and hot specialties",
                    adult_limit = 1,
                    child_limit = 1,
                    senior_limit = 1,
                    toddler_limit = 1,
                    total_limit = 0,
                    over_limit_behavior = OverLimitBehavior.Charge
                },
                new Category
                {
                    Category_name = "Desserts",
                    Description = "Sweet treats to end your meal",
                    adult_limit = 0,
                    child_limit = 0,
                    senior_limit = 0,
                    toddler_limit = 0,
                    total_limit = 0
                }
            };

            _context.Categories.AddRange(categories);
        }
    }
}
