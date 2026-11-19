using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;

namespace back_end.Helpers
{
    // The free ice cream guests get when they ask for the bill. Guests only say yes or no on their
    // bill; staff ask the table which flavours and how many, then send them for every bill that's
    // ready as one $0 order (BillController.SendIceCream). Each flavour is its own menu item, so analytics count them.
    // The items are on no menu, so guests can't order them, and their Dessert category stays off
    // the customer menu because the menu hides empty sections.
    public static class FreeIceCream
    {
        public const string CategoryName = "Dessert";

        public static readonly IReadOnlyList<string> Flavours = new[] { "Strawberry", "Chocolate", "Mango", "Vanilla" };

        public static string ItemName(string flavour) => $"{flavour} Ice Cream";

        // The flavour as written in Flavours ("mango" -> "Mango"), or null if it isn't one
        public static string? Match(string? flavour) =>
            Flavours.FirstOrDefault(f => string.Equals(f, flavour?.Trim(), StringComparison.OrdinalIgnoreCase));

        // Each flavour's menu item id, adding any that are missing (and the category) the first time.
        // Run at startup so admins can see them, and again by SendIceCream in case one was removed
        public static async Task<Dictionary<string, int>> EnsureItemsAsync(ApplicationDbContext context)
        {
            var names = Flavours.Select(ItemName).ToList();
            var existing = await context.MenuItems
                .Where(i => names.Contains(i.Name) && i.Category.Category_name == CategoryName)
                .ToListAsync();

            var missing = Flavours.Where(f => existing.All(i => i.Name != ItemName(f))).ToList();
            if (missing.Count > 0)
            {
                var category = await context.Categories.FirstOrDefaultAsync(c => c.Category_name == CategoryName)
                    ?? new Category
                    {
                        Category_name = CategoryName,
                        Description = "Free ice cream, given when guests ask for the bill.",
                    };
                foreach (var flavour in missing)
                {
                    var item = new Menu_Item
                    {
                        Name = ItemName(flavour),
                        Description = "Free with the bill; staff send it to the kitchen. Don't add it to a menu, or guests could order it anytime.",
                        Category = category,
                    };
                    context.MenuItems.Add(item);
                    existing.Add(item);
                }
                await context.SaveChangesAsync();
            }

            return Flavours.ToDictionary(f => f, f => existing.First(i => i.Name == ItemName(f)).item_id);
        }
    }
}
