using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <inheritdoc />
    public partial class LinkParticipantsToBills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bill_Id",
                table: "session_participant",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Started_By_Guest",
                table: "bills",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_session_participant_Bill_Id",
                table: "session_participant",
                column: "Bill_Id");

            migrationBuilder.AddForeignKey(
                name: "FK_session_participant_bills_Bill_Id",
                table: "session_participant",
                column: "Bill_Id",
                principalTable: "bills",
                principalColumn: "Bill_Id",
                onDelete: ReferentialAction.SetNull);

            // People already seated keep ordering on the bill they last ordered on
            migrationBuilder.Sql(@"
                UPDATE session_participant sp
                SET sp.Bill_Id = (
                    SELECT o.Bill_Id FROM session_order o
                    WHERE o.session_id = sp.Session_Id AND o.User_Id = sp.User_Id
                    ORDER BY o.Created_At DESC LIMIT 1)
                WHERE sp.Left_At IS NULL AND sp.User_Id IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_session_participant_bills_Bill_Id",
                table: "session_participant");

            migrationBuilder.DropIndex(
                name: "IX_session_participant_Bill_Id",
                table: "session_participant");

            migrationBuilder.DropColumn(
                name: "Bill_Id",
                table: "session_participant");

            migrationBuilder.DropColumn(
                name: "Started_By_Guest",
                table: "bills");
        }
    }
}
