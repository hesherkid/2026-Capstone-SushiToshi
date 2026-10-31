using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <inheritdoc />
    public partial class RestrictMenuDeleteWithHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dining_sessions_menu_Menu_Id",
                table: "dining_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_order_item_menu_Menu_Id",
                table: "order_item");

            migrationBuilder.AddForeignKey(
                name: "FK_dining_sessions_menu_Menu_Id",
                table: "dining_sessions",
                column: "Menu_Id",
                principalTable: "menu",
                principalColumn: "Menu_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_item_menu_Menu_Id",
                table: "order_item",
                column: "Menu_Id",
                principalTable: "menu",
                principalColumn: "Menu_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_dining_sessions_menu_Menu_Id",
                table: "dining_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_order_item_menu_Menu_Id",
                table: "order_item");

            migrationBuilder.AddForeignKey(
                name: "FK_dining_sessions_menu_Menu_Id",
                table: "dining_sessions",
                column: "Menu_Id",
                principalTable: "menu",
                principalColumn: "Menu_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_order_item_menu_Menu_Id",
                table: "order_item",
                column: "Menu_Id",
                principalTable: "menu",
                principalColumn: "Menu_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
