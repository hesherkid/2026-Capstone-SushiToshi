using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <inheritdoc />
    public partial class BillLeftoverCharge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Leftover_Price_At_Time",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "Leftover_Quantity",
                table: "order_item");

            migrationBuilder.AddColumn<decimal>(
                name: "Leftover_Charge",
                table: "bills",
                type: "decimal(65,30)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Leftover_Charge",
                table: "bills");

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
        }
    }
}
