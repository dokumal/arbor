using System;
using Microsoft.EntityFrameworkCore.Migrations;
using QuireClone.Domain;

#nullable disable

namespace QuireClone.Data.Migrations
{
    /// <summary>
    /// The fixed statuses Todo, Doing and Done become rows of the new TaskStatuses table, each in one of the
    /// categories Open, Active and Done. Tasks refer to their status by id; documents and folders have none.
    /// Saved status filters are rewritten to the new ids.
    /// </summary>
    public partial class ConfigurableStatuses : Migration
    {
        // Old value, default status, name, category and colour.
        private static readonly (string OldValue, Guid Id, string Name, StatusCategory Category, string Color)[] Defaults =
        [
            ("Todo", DefaultStatuses.Open, "Offen", StatusCategory.Open, "slate"),
            ("Doing", DefaultStatuses.Active, "In Arbeit", StatusCategory.Active, "green"),
            ("Done", DefaultStatuses.Done, "Erledigt", StatusCategory.Done, "green")
        ];

        // Ids in a SQL literal as Microsoft.Data.Sqlite stores them (uppercase) and as they appear in JSON (lowercase).
        private static string Column(Guid id) => $"'{id.ToString().ToUpperInvariant()}'";
        private static string Json(Guid id) => $"'{id}'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskStatuses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatuses_Name",
                table: "TaskStatuses",
                column: "Name",
                unique: true);

            foreach (var status in Defaults)
                migrationBuilder.InsertData(
                    table: "TaskStatuses",
                    columns: ["Id", "Name", "Category", "Color", "SortOrder"],
                    values: new object[] { status.Id, status.Name, status.Category.ToString(), status.Color, 0 });

            migrationBuilder.AddColumn<Guid>(
                name: "StatusId",
                table: "Tasks",
                type: "TEXT",
                nullable: true);

            foreach (var status in Defaults)
                migrationBuilder.Sql($"""
                    UPDATE "Tasks" SET "StatusId" = {Column(status.Id)} WHERE "Kind" = 'Task' AND "Status" = '{status.OldValue}';
                    """);
            // Tasks with an unknown value start over as open.
            migrationBuilder.Sql($"""
                UPDATE "Tasks" SET "StatusId" = {Column(DefaultStatuses.Open)} WHERE "Kind" = 'Task' AND "StatusId" IS NULL;
                """);
            migrationBuilder.Sql(RewriteStatusFilters(Defaults.Select(x => (x.OldValue, Json(x.Id)))));

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Tasks");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_StatusId",
                table: "Tasks",
                column: "StatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_TaskStatuses_StatusId",
                table: "Tasks",
                column: "StatusId",
                principalTable: "TaskStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Tasks",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Todo");

            // Every status goes back to the fixed value of its category.
            migrationBuilder.Sql("""
                UPDATE "Tasks" SET "Status" = COALESCE((
                    SELECT CASE s."Category" WHEN 'Active' THEN 'Doing' WHEN 'Done' THEN 'Done' ELSE 'Todo' END
                    FROM "TaskStatuses" s WHERE s."Id" = "Tasks"."StatusId"), 'Todo');
                """);
            migrationBuilder.Sql(RewriteStatusFilters(Defaults.Select(x => ($"category:{x.Category}", $"'{x.OldValue}'"))));
            migrationBuilder.Sql("""
                UPDATE "ViewConfigurations" SET "FiltersJson" = (
                    SELECT json_group_array(json(CASE WHEN json_extract(f.value, '$.Field') = 'status' THEN json_set(f.value, '$.Value',
                        COALESCE((SELECT CASE s."Category" WHEN 'Active' THEN 'Doing' WHEN 'Done' THEN 'Done' ELSE 'Todo' END
                            FROM "TaskStatuses" s WHERE lower(s."Id") = lower(trim(json_extract(f.value, '$.Value')))),
                            json_extract(f.value, '$.Value'))) ELSE f.value END))
                    FROM json_each("ViewConfigurations"."FiltersJson") f)
                WHERE "FiltersJson" LIKE '%"status"%';
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_TaskStatuses_StatusId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_StatusId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "TaskStatuses");
        }

        // Replaces the value of every saved status filter using the given (old value, new value as SQL literal) pairs.
        private static string RewriteStatusFilters(IEnumerable<(string OldValue, string NewValue)> mapping)
        {
            var cases = string.Join(" ", mapping.Select(x => $"WHEN '{x.OldValue}' THEN {x.NewValue}"));
            return $"""
                UPDATE "ViewConfigurations" SET "FiltersJson" = (
                    SELECT json_group_array(json(CASE WHEN json_extract(f.value, '$.Field') = 'status' THEN json_set(f.value, '$.Value',
                        CASE trim(json_extract(f.value, '$.Value')) {cases} ELSE json_extract(f.value, '$.Value') END) ELSE f.value END))
                    FROM json_each("ViewConfigurations"."FiltersJson") f)
                WHERE "FiltersJson" LIKE '%"status"%';
                """;
        }
    }
}
