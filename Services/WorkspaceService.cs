using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public sealed partial class WorkspaceService(AppDbContext db)
{
    public async Task<WorkspaceDto> GetAsync(Guid? viewId, CancellationToken cancellationToken)
    {
        var items = await db.Items.AsNoTracking().Include(x => x.FieldValues)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync(cancellationToken);
        var fields = await db.CustomFields.AsNoTracking().OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var views = await db.ViewConfigurations.AsNoTracking().OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var selectedView = viewId is null ? views.FirstOrDefault() : views.FirstOrDefault(x => x.Id == viewId);
        var activeFilters = selectedView?.Filters ?? [];

        var links = await db.ItemLinks.AsNoTracking().ToListAsync(cancellationToken);
        var itemDtos = items.Select(x => new WorkspaceItemDto(x.Id, x.Kind, x.Title, x.Description,
            x.ParentId, x.Status, x.StartDate, x.DueDate, x.SortOrder,
            x.UpdatedAt, x.FieldValues.ToDictionary(v => v.FieldId, v => v.Value))).ToList();
        var hideCompleted = selectedView?.HideCompleted == true;
        var matching = itemDtos.Where(x => activeFilters.All(f => Matches(x, f)) &&
                !(hideCompleted && ItemKindRules.HasSchedule(x.Kind) && x.Status == Domain.TaskStatus.Done))
            .Select(x => x.Id).ToList();

        return new WorkspaceDto(itemDtos, matching,
            fields.Select(x => new CustomFieldDto(x.Id, x.Name, x.Type, x.Options, x.SortOrder)).ToList(),
            views.Select(x => new ViewConfigurationDto(x.Id, x.Name, x.Kind, x.Filters, x.VisibleFields,
                x.GroupByField, x.SortField, x.SortDescending, x.HideCompleted, x.SortOrder)).ToList(),
            links.Select(x => new ItemLinkDto(x.TaskId, x.DocumentId)).ToList());
    }

    private static bool Matches(WorkspaceItemDto item, ViewFilter filter)
    {
        var value = filter.Field switch
        {
            FieldKeys.Title => item.Title,
            FieldKeys.Description => item.Description,
            FieldKeys.Kind => item.Kind.ToString(),
            FieldKeys.Status => ItemKindRules.HasSchedule(item.Kind) ? item.Status.ToString() : null,
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
