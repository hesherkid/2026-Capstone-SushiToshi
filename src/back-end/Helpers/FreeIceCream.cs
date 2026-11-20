using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using back_end.domain.DbContexts;
using back_end.domain.Entities;
using back_end.domain.enums;

namespace back_end.Helpers
{
    // The free ice cream guests get when they ask for the bill. Guests only say yes or no on their
    // bill; staff ask the table which flavours and how many, then send them for every bill that's
    // ready as one $0 order (BillController.SendIceCream). Each flavour is its own menu item, so
    // analytics count them.
    //
    // The flavours are the Available items in categories marked Free_With_Bill, so admins manage
    // them with the usual tools: switch "Free with the bill" on for a category, add an item there
    // for a new flavour, or set one Unavailable to stop offering it. The name doesn't matter, so a
    // normal (paid) Dessert category can still go on menus. Free-with-the-bill items can't go on a
    // menu (CanGoOnAMenuAsync), so guests can't order them, and their empty section stays off the
    // customer menu.
    public static class FreeIceCream
    {
        // The category created the first time, while no category is free with the bill
        public const string DefaultCategoryName = "Free Ice Cream";

        public static readonly IReadOnlyList<string> DefaultFlavours = new[] { "Strawberry", "Chocolate", "Mango", "Vanilla" };

        public static string ItemName(string flavour) => $"{flavour} Ice Cream";

        public const string NotOnAMenuMessage =
            "This item is free with the bill (staff send it when guests ask for the bill), so it can't go on a menu.";

        // The flavours staff can send now, in the order they were added (so the defaults keep theirs)
        public static IQueryable<Menu_Item> Flavours(ApplicationDbContext context) =>
            context.MenuItems
                .Where(i => i.Category.Free_With_Bill && i.Status == MenuItemStatus.Available)
                .OrderBy(i => i.item_id);

        // False for the free-with-the-bill items: on a menu, guests could order them anytime
        public static async Task<bool> CanGoOnAMenuAsync(ApplicationDbContext context, int itemId) =>
            !await context.MenuItems.AnyAsync(i => i.item_id == itemId && i.Category.Free_With_Bill);

        // Adds a free-with-the-bill category with the default flavours the first time. Once any
        // category is free with the bill it's the admins' to manage, so flavours they removed don't
        // come back. Run at startup
        public static async Task EnsureDefaultsAsync(ApplicationDbContext context)
        {
            if (await context.Categories.AnyAsync(c => c.Free_With_Bill))
            {
                return;
            }

            var category = new Category
            {
                Category_name = await context.Categories.AnyAsync(c => c.Category_name == DefaultCategoryName)
                    ? $"{DefaultCategoryName} (free with the bill)"
                    : DefaultCategoryName,
                Description = "Free ice cream flavours, sent by staff when guests ask for the bill. Add an item here for a new flavour.",
                Free_With_Bill = true,
            };
            foreach (var flavour in DefaultFlavours)
            {
                var name = ItemName(flavour);
                // Item names are unique: a paid item with the same name keeps its own
                if (await context.MenuItems.AnyAsync(i => i.Name == name)) continue;
                context.MenuItems.Add(new Menu_Item
                {
                    Name = name,
                    Description = "Free with the bill; staff send it to the kitchen. Set it Unavailable to stop offering it.",
                    Category = category,
                });
            }
            context.Categories.Add(category);
            await context.SaveChangesAsync();
        }
    }
}
