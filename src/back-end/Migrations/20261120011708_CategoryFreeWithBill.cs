using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <inheritdoc />
    public partial class CategoryFreeWithBill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Free_With_Bill",
                table: "Categories",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            // The free ice cream used to be found by the name "Dessert": keep that category free with
            // the bill, unless its items are on a menu (then it's a real, paid Dessert category)
            migrationBuilder.Sql(@"
                UPDATE Categories c
                SET c.Free_With_Bill = 1
                WHERE c.Category_name = 'Dessert'
                  AND NOT EXISTS (
                    SELECT 1 FROM menu_item mi
                    JOIN menu_item_assignment a ON a.item_id = mi.item_id
                    WHERE mi.Category_id = c.Category_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Free_With_Bill",
                table: "Categories");
        }
    }
}
