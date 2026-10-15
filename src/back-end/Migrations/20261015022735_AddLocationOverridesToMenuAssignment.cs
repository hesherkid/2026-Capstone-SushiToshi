using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace back_end.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationOverridesToMenuAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Give the menu_id FK its own index BEFORE dropping the PK (otherwise errno 150)
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS IX_menu_item_assignment_menu_id
                ON menu_item_assignment (menu_id);");

            // 2. Nullable location column + FK (existing rows stay NULL, so they become the defaults)
            migrationBuilder.Sql(@"ALTER TABLE menu_item_assignment
                ADD COLUMN IF NOT EXISTS location_id INT NULL;");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS IX_menu_item_assignment_location_id
                ON menu_item_assignment (location_id);");
            migrationBuilder.Sql(@"ALTER TABLE menu_item_assignment
                ADD CONSTRAINT FK_menu_item_assignment_locations_location_id
                FOREIGN KEY IF NOT EXISTS (location_id) REFERENCES locations (Location_Id) ON DELETE CASCADE;");

            // 3. Computed key + unique index: one default row and one row per location for each menu + item
            migrationBuilder.Sql(@"ALTER TABLE menu_item_assignment
                ADD COLUMN IF NOT EXISTS location_key INT AS (IFNULL(location_id, 0)) PERSISTENT;");
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS UX_assignment_menu_item_location
                ON menu_item_assignment (menu_id, item_id, location_key);");

            // 4. Swap the PK in ONE statement (an AUTO_INCREMENT column must be a key when it's added)
            migrationBuilder.Sql(@"ALTER TABLE menu_item_assignment
                DROP PRIMARY KEY,
                ADD COLUMN assignment_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY FIRST;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Location overrides can't fit the old (menu_id, item_id) key, so remove them
            migrationBuilder.Sql("DELETE FROM menu_item_assignment WHERE location_id IS NOT NULL;");

            migrationBuilder.Sql(@"ALTER TABLE menu_item_assignment
                DROP PRIMARY KEY,
                DROP COLUMN assignment_id,
                ADD PRIMARY KEY (menu_id, item_id);");

            migrationBuilder.Sql("DROP INDEX IF EXISTS UX_assignment_menu_item_location ON menu_item_assignment;");
            migrationBuilder.Sql("ALTER TABLE menu_item_assignment DROP COLUMN IF EXISTS location_key;");
            migrationBuilder.Sql("ALTER TABLE menu_item_assignment DROP FOREIGN KEY IF EXISTS FK_menu_item_assignment_locations_location_id;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_menu_item_assignment_location_id ON menu_item_assignment;");
            migrationBuilder.Sql("ALTER TABLE menu_item_assignment DROP COLUMN IF EXISTS location_id;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_menu_item_assignment_menu_id ON menu_item_assignment;");
        }
    }
}