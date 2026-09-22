using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public sealed class WorkspaceService(AppDbContext db)
{
    public async Task<WorkspaceDto> GetAsync(Guid? viewId, CancellationToken cancellationToken)
    {
        var tasks = await db.Tasks.AsNoTracking().Include(x => x.FieldValues)
            .OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        var fields = await db.CustomFields.AsNoTracking().OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var views = await db.ViewConfigurations.AsNoTracking().OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var selectedView = viewId is null ? views.FirstOrDefault() : views.FirstOrDefault(x => x.Id == viewId);
        List<ViewFilter> activeFilters = selectedView is null ? [] : Deserialize<List<ViewFilter>>(selectedView.FiltersJson);

        var taskDtos = tasks.Select(x => new TaskDto(x.Id, x.Title, x.Description,
            x.ParentTaskId, x.Status, x.StartDate, x.DueDate, x.SortOrder,
            x.UpdatedAt, x.FieldValues.ToDictionary(v => v.FieldId, v => v.Value))).ToList();
        var matching = taskDtos.Where(x => activeFilters.All(f => Matches(x, f)))
            .Select(x => x.Id).ToList();

        return new WorkspaceDto(taskDtos, matching,
            fields.Select(x => new CustomFieldDto(x.Id, x.Name, x.Type,
                Deserialize<List<string>>(x.OptionsJson), x.SortOrder)).ToList(),
            views.Select(x => new ViewConfigurationDto(x.Id, x.Name, x.Kind,
                Deserialize<List<ViewFilter>>(x.FiltersJson),
                Deserialize<List<string>>(x.VisibleFieldsJson), x.SortOrder)).ToList());
    }

    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json,
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private static bool Matches(TaskDto task, ViewFilter filter)
    {
        var value = filter.Field switch
        {
            "title" => task.Title,
            "description" => task.Description,
            "status" => task.Status.ToString(),
            "startDate" => task.StartDate?.ToString("yyyy-MM-dd"),
            "dueDate" => task.DueDate?.ToString("yyyy-MM-dd"),
            _ when filter.Field.StartsWith("field:", StringComparison.Ordinal) &&
                Guid.TryParse(filter.Field[6..], out var fieldId) =>
                task.Fields.GetValueOrDefault(fieldId),
            _ => null
        };
        var comparison = StringComparison.OrdinalIgnoreCase;
        var expected = filter.Value.Trim();
        return filter.Operator switch
        {
            "isEmpty" => string.IsNullOrWhiteSpace(value),
            "isNotEmpty" => !string.IsNullOrWhiteSpace(value),
            "equals" => string.Equals(value?.Trim(), expected, comparison),
            "notEquals" => !string.Equals(value?.Trim(), expected, comparison),
            "contains" => value?.Contains(expected, comparison) == true,
            "before" => string.Compare(value, expected, comparison) < 0 && value is not null,
            "after" => string.Compare(value, expected, comparison) > 0 && value is not null,
            "greaterThan" => CompareNumbers(value, expected, true),
            "lessThan" => CompareNumbers(value, expected, false),
            _ => false
        };
    }

    private static bool CompareNumbers(string? value, string expected, bool greater)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var left) ||
            !decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var right))
            return false;
        return greater ? left > right : left < right;
    }
}
