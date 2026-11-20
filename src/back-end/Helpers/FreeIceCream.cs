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
    // customer menu. The FreeIceCreamDefaults migration adds a "Free Ice Cream" category with four
    // flavours once, so it works out of the box.
    public static class FreeIceCream
    {
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
    }
}
