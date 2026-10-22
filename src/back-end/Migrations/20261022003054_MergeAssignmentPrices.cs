using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <inheritdoc />
    public partial class MergeAssignmentPrices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE menu_item_assignment
                SET Price = CASE WHEN Full_Price > 0 THEN Full_Price ELSE 0 END
                WHERE Is_Add_On = 0;");

            migrationBuilder.DropColumn(
                name: "Full_Price",
                table: "menu_item_assignment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Full_Price",
                table: "menu_item_assignment",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(@"
                UPDATE menu_item_assignment SET Full_Price = Price;
                UPDATE menu_item_assignment SET Price = 0 WHERE Is_Add_On = 0;");
        }
    }
}