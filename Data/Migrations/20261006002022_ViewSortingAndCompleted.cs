using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuireClone.Data.Migrations
{
    /// <inheritdoc />
    public partial class ViewSortingAndCompleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HideCompleted",
                table: "ViewConfigurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SortDescending",
                table: "ViewConfigurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SortField",
                table: "ViewConfigurations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Existing workspaces get the "Anstehend" view; new ones receive it from DatabaseInitializer.
            migrationBuilder.Sql("""
                INSERT INTO "ViewConfigurations" ("Id", "Name", "Kind", "FiltersJson", "VisibleFieldsJson", "SortOrder", "SortField", "SortDescending", "HideCompleted")
                SELECT '6f1d3c2a-8b4e-4f7a-9c1d-2e5b7a9c0d14'::uuid, 'Anstehend', 'Tree',
                    '[{"Field":"dueDate","Operator":"before","Value":"@today+7"}]'::jsonb,
                    '["title","status","dueDate"]'::jsonb,
                    MAX("SortOrder") + 1, 'dueDate', false, true
                FROM "ViewConfigurations"
                HAVING COUNT(*) > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HideCompleted",
                table: "ViewConfigurations");

            migrationBuilder.DropColumn(
                name: "SortDescending",
                table: "ViewConfigurations");

            migrationBuilder.DropColumn(
                name: "SortField",
                table: "ViewConfigurations");
        }
    }
}
