using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAssignmentOrderCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastOrdered",
                table: "menu_item_assignment");

            migrationBuilder.DropColumn(
                name: "Total_Units_Ordered",
                table: "menu_item_assignment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastOrdered",
                table: "menu_item_assignment",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "Total_Units_Ordered",
                table: "menu_item_assignment",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
