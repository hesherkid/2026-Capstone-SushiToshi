using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "menu_item_view",
                columns: table => new
                {
                    View_Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Item_Id = table.Column<int>(type: "int", nullable: false),
                    Session_Id = table.Column<int>(type: "int", nullable: false),
                    Menu_Id = table.Column<int>(type: "int", nullable: false),
                    Location_Id = table.Column<int>(type: "int", nullable: false),
                    User_Id = table.Column<int>(type: "int", nullable: true),
                    Viewed_At = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    View_Seconds = table.Column<int>(type: "int", nullable: false),
                    Was_Available = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_item_view", x => x.View_Id);
                    table.ForeignKey(
                        name: "FK_menu_item_view_dining_sessions_Session_Id",
                        column: x => x.Session_Id,
                        principalTable: "dining_sessions",
                        principalColumn: "Session_Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_menu_item_view_menu_item_Item_Id",
                        column: x => x.Item_Id,
                        principalTable: "menu_item",
                        principalColumn: "item_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_menu_item_view_Item_Id",
                table: "menu_item_view",
                column: "Item_Id");

            migrationBuilder.CreateIndex(
                name: "IX_menu_item_view_session_item",
                table: "menu_item_view",
                columns: new[] { "Session_Id", "Item_Id" });

            migrationBuilder.CreateIndex(
                name: "IX_menu_item_view_viewed_at_location",
                table: "menu_item_view",
                columns: new[] { "Viewed_At", "Location_Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "menu_item_view");
        }
    }
}
