using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <summary>
    /// The free ice cream staff send with the bill: adds a "Free Ice Cream" category (Free with the
    /// bill on) with four flavours, once, so the feature works out of the box. Skipped when a
    /// category is already free with the bill. After this, admins manage the flavours in the app.
    /// </summary>
    public partial class FreeIceCreamDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only when no category is free with the bill yet (an earlier "Dessert" one counts)
            migrationBuilder.Sql(@"
                INSERT INTO Categories
                    (Category_name, Description, adult_limit, child_limit, senior_limit, toddler_limit,
                     per_order_limit, over_limit_behavior, Free_With_Bill)
                SELECT 'Free Ice Cream',
                       'Free ice cream flavours, sent by staff when guests ask for the bill. Add an item here for a new flavour.',
                       0, 0, 0, 0, 0, 0, 1
                FROM DUAL
                WHERE NOT EXISTS (SELECT 1 FROM Categories WHERE Free_With_Bill = 1);");

            // The flavours, into that category while it's empty. Item names are unique, so a flavour
            // whose name is already taken is skipped. Status 0 = Available; on no menu
            migrationBuilder.Sql(@"
                INSERT INTO menu_item (Name, Description, Category_id, Status)
                SELECT f.Name,
                       'Free with the bill; staff send it to the kitchen. Set it Unavailable to stop offering it.',
                       c.Category_id, 0
                FROM Categories c
                JOIN (SELECT 'Strawberry Ice Cream' AS Name, 1 AS Ord
                      UNION ALL SELECT 'Chocolate Ice Cream', 2
                      UNION ALL SELECT 'Mango Ice Cream', 3
                      UNION ALL SELECT 'Vanilla Ice Cream', 4) f
                WHERE c.Free_With_Bill = 1
                  AND c.Category_name = 'Free Ice Cream'
                  AND NOT EXISTS (SELECT 1 FROM menu_item mi WHERE mi.Category_id = c.Category_id)
                  AND NOT EXISTS (SELECT 1 FROM menu_item mi WHERE mi.Name = f.Name)
                ORDER BY f.Ord;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Left in place: by now the flavours may have orders, and admins may have changed them
        }
    }
}
