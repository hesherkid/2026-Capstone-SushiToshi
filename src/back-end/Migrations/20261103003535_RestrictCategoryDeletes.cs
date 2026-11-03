using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <inheritdoc />
    public partial class RestrictCategoryDeletes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_menu_item_Categories_Category_id",
                table: "menu_item");

            migrationBuilder.DropForeignKey(
                name: "FK_order_item_menu_item_Item_Id",
                table: "order_item");

            migrationBuilder.AddForeignKey(
                name: "FK_menu_item_Categories_Category_id",
                table: "menu_item",
                column: "Category_id",
                principalTable: "Categories",
                principalColumn: "Category_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_item_menu_item_Item_Id",
                table: "order_item",
                column: "Item_Id",
                principalTable: "menu_item",
                principalColumn: "item_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_menu_item_Categories_Category_id",
                table: "menu_item");

            migrationBuilder.DropForeignKey(
                name: "FK_order_item_menu_item_Item_Id",
                table: "order_item");

            migrationBuilder.AddForeignKey(
                name: "FK_menu_item_Categories_Category_id",
                table: "menu_item",
                column: "Category_id",
                principalTable: "Categories",
                principalColumn: "Category_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_order_item_menu_item_Item_Id",
                table: "order_item",
                column: "Item_Id",
                principalTable: "menu_item",
                principalColumn: "item_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
