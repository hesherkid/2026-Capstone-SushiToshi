using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <inheritdoc />
    public partial class AddOverLimitAndLeftoverFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Charge_Reason",
                table: "order_item",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Leftover_Price_At_Time",
                table: "order_item",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Leftover_Quantity",
                table: "order_item",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Full_Price",
                table: "menu_item_assignment",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Over_Limit_Behavior",
                table: "menu_item_assignment",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "over_limit_behavior",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Charge_Reason",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "Leftover_Price_At_Time",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "Leftover_Quantity",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "Full_Price",
                table: "menu_item_assignment");

            migrationBuilder.DropColumn(
                name: "Over_Limit_Behavior",
                table: "menu_item_assignment");

            migrationBuilder.DropColumn(
                name: "over_limit_behavior",
                table: "Categories");
        }
    }
}
