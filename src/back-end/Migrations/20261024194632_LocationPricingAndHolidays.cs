using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <inheritdoc />
    public partial class LocationPricingAndHolidays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Province_Code",
                table: "locations",
                type: "varchar(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "AB")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "Adult_Price",
                table: "bills",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Child_Price",
                table: "bills",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Gratuity_Min_Party_Size",
                table: "bills",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Gratuity_Rate",
                table: "bills",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Holiday_Name",
                table: "bills",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Pricing_Type",
                table: "bills",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Senior_Price",
                table: "bills",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Tax_Rate",
                table: "bills",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Toddler_Price",
                table: "bills",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "holidays",
                columns: table => new
                {
                    Holiday_Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Province_Code = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Observed_Date = table.Column<DateOnly>(type: "date", nullable: true),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Imported_At = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Reviewed_By = table.Column<int>(type: "int", nullable: true),
                    Reviewed_At = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Created_At = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_holidays", x => x.Holiday_Id);
                    table.ForeignKey(
                        name: "FK_holidays_users_Reviewed_By",
                        column: x => x.Reviewed_By,
                        principalTable: "users",
                        principalColumn: "User_id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pricing_profiles",
                columns: table => new
                {
                    Pricing_Profile_Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    location_id = table.Column<int>(type: "int", nullable: true),
                    location_key = table.Column<int>(type: "int", nullable: false, computedColumnSql: "IFNULL(location_id, 0)", stored: true),
                    Tax_Rate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    Gratuity_Rate = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    Gratuity_Min_Party_Size = table.Column<int>(type: "int", nullable: false),
                    Leftover_Fee = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Weekend_Days = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Apply_Holiday_Pricing = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Created_At = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Updated_At = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_profiles", x => x.Pricing_Profile_Id);
                    table.ForeignKey(
                        name: "FK_pricing_profiles_locations_location_id",
                        column: x => x.location_id,
                        principalTable: "locations",
                        principalColumn: "Location_Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "pricing_rates",
                columns: table => new
                {
                    Pricing_Rate_Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Pricing_Profile_Id = table.Column<int>(type: "int", nullable: false),
                    Day_Type = table.Column<int>(type: "int", nullable: false),
                    Adult_Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Senior_Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Child_Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Toddler_Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_rates", x => x.Pricing_Rate_Id);
                    table.ForeignKey(
                        name: "FK_pricing_rates_pricing_profiles_Pricing_Profile_Id",
                        column: x => x.Pricing_Profile_Id,
                        principalTable: "pricing_profiles",
                        principalColumn: "Pricing_Profile_Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_holiday_province_status_date",
                table: "holidays",
                columns: new[] { "Province_Code", "Status", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_holidays_Reviewed_By",
                table: "holidays",
                column: "Reviewed_By");

            migrationBuilder.CreateIndex(
                name: "UX_holiday_province_date",
                table: "holidays",
                columns: new[] { "Province_Code", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pricing_profiles_location_id",
                table: "pricing_profiles",
                column: "location_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_pricing_profile_location",
                table: "pricing_profiles",
                column: "location_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_pricing_rate_profile_day_type",
                table: "pricing_rates",
                columns: new[] { "Pricing_Profile_Id", "Day_Type" },
                unique: true);
            
            // Locations stored "Alberta"; the holiday API and holiday lookups need the 2-letter code
            migrationBuilder.Sql(@"
                UPDATE locations SET Province_Code = CASE UPPER(TRIM(Province))
                    WHEN 'ALBERTA' THEN 'AB'                    WHEN 'AB' THEN 'AB'
                    WHEN 'BRITISH COLUMBIA' THEN 'BC'           WHEN 'BC' THEN 'BC'
                    WHEN 'MANITOBA' THEN 'MB'                   WHEN 'MB' THEN 'MB'
                    WHEN 'NEW BRUNSWICK' THEN 'NB'              WHEN 'NB' THEN 'NB'
                    WHEN 'NEWFOUNDLAND AND LABRADOR' THEN 'NL'  WHEN 'NL' THEN 'NL'
                    WHEN 'NOVA SCOTIA' THEN 'NS'                WHEN 'NS' THEN 'NS'
                    WHEN 'NORTHWEST TERRITORIES' THEN 'NT'      WHEN 'NT' THEN 'NT'
                    WHEN 'NUNAVUT' THEN 'NU'                    WHEN 'NU' THEN 'NU'
                    WHEN 'ONTARIO' THEN 'ON'                    WHEN 'ON' THEN 'ON'
                    WHEN 'PRINCE EDWARD ISLAND' THEN 'PE'       WHEN 'PE' THEN 'PE'
                    WHEN 'QUEBEC' THEN 'QC'                     WHEN 'QC' THEN 'QC'
                    WHEN 'SASKATCHEWAN' THEN 'SK'               WHEN 'SK' THEN 'SK'
                    WHEN 'YUKON' THEN 'YT'                      WHEN 'YT' THEN 'YT'
                    ELSE 'AB' END;");

            // Default pricing for every location without its own profile (location_id NULL).
            // Values are the ones previously in appsettings.Development.json; GST only (Alberta).
            var seededAt = new DateTime(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);

            migrationBuilder.InsertData(
                table: "pricing_profiles",
                columns: new[] { "Pricing_Profile_Id", "location_id", "Tax_Rate", "Gratuity_Rate",
                                 "Gratuity_Min_Party_Size", "Leftover_Fee", "Weekend_Days",
                                 "Apply_Holiday_Pricing", "Created_At", "Updated_At" },
                values: new object[] { 1, null, 0.05m, 0.18m, 6, 15.00m, "Friday,Saturday,Sunday",
                                       true, seededAt, seededAt });

            // Day_Type: 0 = Weekday, 1 = Weekend, 2 = Holiday
            migrationBuilder.InsertData(
                table: "pricing_rates",
                columns: new[] { "Pricing_Rate_Id", "Pricing_Profile_Id", "Day_Type",
                                 "Adult_Price", "Senior_Price", "Child_Price", "Toddler_Price" },
                values: new object[,]
                {
                    { 1, 1, 0, 40.95m, 35.95m, 25.95m, 7.95m },
                    { 2, 1, 1, 42.95m, 39.95m, 28.95m, 7.95m },
                    { 3, 1, 2, 42.95m, 39.95m, 28.95m, 7.95m },
                });

            // Alberta general holidays (canada-holidays.ca), already approved so holiday pricing
            // works before an admin presses Load. Boxing Day is optional in Alberta: left out
            // until the client decides. Source 1 = Imported, Status 1 = Approved.
            migrationBuilder.InsertData(
                table: "holidays",
                columns: new[] { "Province_Code", "Date", "Observed_Date", "Name",
                                 "Source", "Status", "Imported_At", "Created_At" },
                values: new object[,]
                {
                    { "AB", new DateOnly(2026, 1, 1),   new DateOnly(2026, 1, 1),   "New Year's Day",  1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2026, 2, 16),  new DateOnly(2026, 2, 16),  "Family Day",      1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2026, 4, 3),   new DateOnly(2026, 4, 3),   "Good Friday",     1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2026, 5, 18),  new DateOnly(2026, 5, 18),  "Victoria Day",    1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2026, 7, 1),   new DateOnly(2026, 7, 1),   "Canada Day",      1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2026, 9, 7),   new DateOnly(2026, 9, 7),   "Labour Day",      1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 12), "Thanksgiving",    1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2026, 11, 11), new DateOnly(2026, 11, 11), "Remembrance Day", 1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2026, 12, 25), new DateOnly(2026, 12, 25), "Christmas Day",   1, 1, seededAt, seededAt },

                    { "AB", new DateOnly(2027, 1, 1),   new DateOnly(2027, 1, 1),   "New Year's Day",  1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2027, 2, 15),  new DateOnly(2027, 2, 15),  "Family Day",      1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2027, 3, 26),  new DateOnly(2027, 3, 26),  "Good Friday",     1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2027, 5, 24),  new DateOnly(2027, 5, 24),  "Victoria Day",    1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2027, 7, 1),   new DateOnly(2027, 7, 1),   "Canada Day",      1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2027, 9, 6),   new DateOnly(2027, 9, 6),   "Labour Day",      1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2027, 10, 11), new DateOnly(2027, 10, 11), "Thanksgiving",    1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2027, 11, 11), new DateOnly(2027, 11, 11), "Remembrance Day", 1, 1, seededAt, seededAt },
                    { "AB", new DateOnly(2027, 12, 25), new DateOnly(2027, 12, 27), "Christmas Day",   1, 1, seededAt, seededAt },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "holidays");

            migrationBuilder.DropTable(
                name: "pricing_rates");

            migrationBuilder.DropTable(
                name: "pricing_profiles");

            migrationBuilder.DropColumn(
                name: "Province_Code",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "Adult_Price",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "Child_Price",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "Gratuity_Min_Party_Size",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "Gratuity_Rate",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "Holiday_Name",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "Pricing_Type",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "Senior_Price",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "Tax_Rate",
                table: "bills");

            migrationBuilder.DropColumn(
                name: "Toddler_Price",
                table: "bills");
        }
    }
}
