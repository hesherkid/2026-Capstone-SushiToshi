using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <inheritdoc />
    public partial class PerOrderLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "total_limit",
                table: "Categories",
                newName: "per_order_limit");

            migrationBuilder.AddColumn<int>(
                name: "Per_Order_Limit",
                table: "menu_item_assignment",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Per_Order_Limit",
                table: "menu_item_assignment");

            migrationBuilder.RenameColumn(
                name: "per_order_limit",
                table: "Categories",
                newName: "total_limit");
        }
    }
}
