using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_locations_users_User_id",
                table: "locations");

            migrationBuilder.DropIndex(
                name: "IX_locations_User_id",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "User_id",
                table: "locations");

            migrationBuilder.CreateTable(
                name: "staff_location",
                columns: table => new
                {
                    User_Id = table.Column<int>(type: "int", nullable: false),
                    Location_Id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_location", x => new { x.User_Id, x.Location_Id });
                    table.ForeignKey(
                        name: "FK_staff_location_locations_Location_Id",
                        column: x => x.Location_Id,
                        principalTable: "locations",
                        principalColumn: "Location_Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staff_location_users_User_Id",
                        column: x => x.User_Id,
                        principalTable: "users",
                        principalColumn: "User_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_staff_location_Location_Id",
                table: "staff_location",
                column: "Location_Id");

            // Assign each staff member (Role 3 = UserRoles.Staff) to the location they're
            // working at now, so nobody loses access
            migrationBuilder.Sql(@"INSERT INTO staff_location (User_Id, Location_Id)
                SELECT User_id, Location_id FROM users
                WHERE Role = 3 AND Location_id IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_location");

            migrationBuilder.AddColumn<int>(
                name: "User_id",
                table: "locations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_locations_User_id",
                table: "locations",
                column: "User_id");

            migrationBuilder.AddForeignKey(
                name: "FK_locations_users_User_id",
                table: "locations",
                column: "User_id",
                principalTable: "users",
                principalColumn: "User_id");
        }
    }
}
