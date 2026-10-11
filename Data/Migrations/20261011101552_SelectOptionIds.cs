using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuireClone.Data.Migrations
{
    /// <summary>
    /// Select options get a stable id. Stored values and saved filters refer to that id instead of the option text,
    /// so renaming an option no longer has to rewrite them. The schema itself is unchanged.
    /// </summary>
    public partial class SelectOptionIds : Migration
    {
        // A random version 4 GUID in the lowercase format .NET writes for Guid.ToString().
        private const string NewGuid =
            "lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))), 2) || '-' || " +
            "substr('89ab', 1 + abs(random() % 4), 1) || substr(lower(hex(randomblob(2))), 2) || '-' || lower(hex(randomblob(6)))";

        // Every option of every select field with its id and name, and the key views use for the field.
        private const string SelectOptions = """
            SELECT 'field:' || lower(c."Id") AS FieldKey, c."Id" AS FieldId,
                   json_extract(o.value, '$.Id') AS OptionId, json_extract(o.value, '$.Name') AS OptionName
            FROM "CustomFields" c, json_each(c."OptionsJson") o
            WHERE c."Type" = 'Select'
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ["Hoch", …] becomes [{"Id": "…", "Name": "Hoch"}, …].
            migrationBuilder.Sql($"""
                UPDATE "CustomFields" SET "OptionsJson" = (
                    SELECT json_group_array(json_object('Id', {NewGuid}, 'Name', o.value))
                    FROM json_each("CustomFields"."OptionsJson") o)
                WHERE json_type("OptionsJson", '$[0]') = 'text';
                """);

            // Values are kept as they are if they don't match any option.
            migrationBuilder.Sql($"""
                UPDATE "TaskFieldValues" SET "Value" = COALESCE((
                    SELECT s.OptionId FROM ({SelectOptions}) s
                    WHERE s.FieldId = "TaskFieldValues"."FieldId" AND s.OptionName = "TaskFieldValues"."Value"), "Value")
                WHERE "FieldId" IN (SELECT "Id" FROM "CustomFields" WHERE "Type" = 'Select');
                """);

            migrationBuilder.Sql(RewriteFilterValues($"""
                SELECT FieldKey, OptionName AS OldValue, OptionId AS NewValue FROM ({SelectOptions})
                """));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RewriteFilterValues($"""
                SELECT FieldKey, OptionId AS OldValue, OptionName AS NewValue FROM ({SelectOptions})
                """));

            migrationBuilder.Sql($"""
                UPDATE "TaskFieldValues" SET "Value" = COALESCE((
                    SELECT s.OptionName FROM ({SelectOptions}) s
                    WHERE s.FieldId = "TaskFieldValues"."FieldId" AND s.OptionId = "TaskFieldValues"."Value"), "Value")
                WHERE "FieldId" IN (SELECT "Id" FROM "CustomFields" WHERE "Type" = 'Select');
                """);

            migrationBuilder.Sql("""
                UPDATE "CustomFields" SET "OptionsJson" = (
                    SELECT json_group_array(json_extract(o.value, '$.Name'))
                    FROM json_each("CustomFields"."OptionsJson") o)
                WHERE json_type("OptionsJson", '$[0]') = 'object';
                """);
        }

        // Replaces the value of every saved filter on a select field using the given mapping (FieldKey, OldValue, NewValue).
        private static string RewriteFilterValues(string mapping) => $"""
            UPDATE "ViewConfigurations" SET "FiltersJson" = (
                SELECT json_group_array(json(CASE WHEN m.NewValue IS NULL THEN f.value
                    ELSE json_set(f.value, '$.Value', m.NewValue) END))
                FROM json_each("ViewConfigurations"."FiltersJson") f
                LEFT JOIN ({mapping}) m
                    ON m.FieldKey = json_extract(f.value, '$.Field') AND m.OldValue = trim(json_extract(f.value, '$.Value')))
            WHERE "FiltersJson" LIKE '%field:%';
            """;
    }
}
