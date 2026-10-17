using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public sealed partial class WorkspaceService(AppDbContext db, StatusService statusService, ProjectService projects)
{
    // Everything shown on the workspace page belongs to one project.
    public async Task<WorkspaceDto> GetAsync(Guid projectId, Guid? viewId, CancellationToken cancellationToken)
    {
        await projects.EnsureExistsAsync(projectId, cancellationToken);
        var items = await db.Items.AsNoTracking().Include(x => x.FieldValues).Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var statuses = await statusService.ListAsync(projectId, cancellationToken);
        var categories = statuses.ToDictionary(x => x.Id, x => x.Category);
        var fields = await db.CustomFields.AsNoTracking().Where(x => x.ProjectId == projectId).OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var views = await db.ViewConfigurations.AsNoTracking().Where(x => x.ProjectId == projectId).OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var selectedView = viewId is null ? views.FirstOrDefault() : views.FirstOrDefault(x => x.Id == viewId);
        var activeFilters = selectedView?.Filters ?? [];

        var links = await db.ItemLinks.AsNoTracking().Where(x => x.Task.ProjectId == projectId).ToListAsync(cancellationToken);
        var itemDtos = items.Select(x => new WorkspaceItemDto(x.Id, x.Kind, x.Title, x.Description,
            x.ParentId, x.StatusId, x.StartDate, x.DueDate, x.SortOrder,
            x.UpdatedAt, x.FieldValues.ToDictionary(v => v.FieldId, v => v.Value))).ToList();
        var hideCompleted = selectedView?.HideCompleted == true;
        var matching = itemDtos.Where(x => activeFilters.All(f => Matches(x, f, categories)) &&
                !(hideCompleted && Category(x, categories) == StatusCategory.Done))
            .Select(x => x.Id).ToList();

        return new WorkspaceDto(itemDtos, matching,
            statuses.Select(x => new TaskStatusDto(x.Id, x.Name, x.Category, x.Color, x.SortOrder)).ToList(),
            fields.Select(x => new CustomFieldDto(x.Id, x.Name, x.Type, x.Options, x.SortOrder)).ToList(),
            views.Select(x => new ViewConfigurationDto(x.Id, x.Name, x.Kind, x.Filters, x.VisibleFields,
                x.GroupByField, x.SortField, x.SortDescending, x.HideCompleted, x.SortOrder)).ToList(),
            links.Select(x => new ItemLinkDto(x.TaskId, x.DocumentId)).ToList());
    }

    private static StatusCategory? Category(WorkspaceItemDto item, Dictionary<Guid, StatusCategory> categories) =>
        item.StatusId is Guid id && categories.TryGetValue(id, out var category) ? category : null;

    private static bool Matches(WorkspaceItemDto item, ViewFilter filter, Dictionary<Guid, StatusCategory> categories)
    {
        var value = filter.Field switch
        {
            FieldKeys.Title => item.Title,
            FieldKeys.Description => item.Description,
            FieldKeys.Kind => item.Kind.ToString(),
            // A status filter names either one status or a whole category ("category:Done").
            FieldKeys.Status when filter.Value.Trim().StartsWith(FieldKeys.StatusCategoryPrefix, StringComparison.Ordinal) =>
                Category(item, categories) is { } category ? FieldKeys.StatusCategoryPrefix + category : null,
            FieldKeys.Status => item.StatusId?.ToString(),
            FieldKeys.StartDate => item.StartDate?.ToString("yyyy-MM-dd"),
            FieldKeys.DueDate => item.DueDate?.ToString("yyyy-MM-dd"),
            _ when FieldKeys.TryGetCustomFieldId(filter.Field, out var fieldId) => item.Fields.GetValueOrDefault(fieldId),
            _ => null
        };
        var comparison = StringComparison.OrdinalIgnoreCase;
        var expected = ResolveRelativeDate(filter.Value.Trim());
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

    // Saved filters may use "@today", "@today+7" or "@today-3" so they stay current from day to day.
    private static string ResolveRelativeDate(string value)
    {
        var match = RelativeDate().Match(value);
        if (!match.Success) return value;
        var days = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        if (match.Groups[1].Value == "-") days = -days;
        return DateOnly.FromDateTime(DateTime.Now).AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^@today(?:([+-])(\d{1,4}))?$")]
    private static partial Regex RelativeDate();

    private static bool CompareNumbers(string? value, string expected, bool greater)
    {
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var left) ||
            !decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var right))
            return false;
        return greater ? left > right : left < right;
    }
}
