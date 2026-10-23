using System.ComponentModel.DataAnnotations;
using QuireClone.Domain;
using QuireClone.Services;

namespace QuireClone.Contracts;

public sealed record WorkspaceItemDto(Guid Id, WorkspaceItemKind Kind, string Title, string Description, Guid? ParentId,
    Guid? StatusId, DateOnly? StartDate, DateOnly? DueDate, int SortOrder,
    DateTimeOffset UpdatedAt, Dictionary<Guid, string> Fields);

public sealed record TaskStatusDto(Guid Id, string Name, StatusCategory Category, string Color, int SortOrder);

public sealed record CustomFieldDto(Guid Id, string Name, CustomFieldType Type,
    List<SelectOption> Options, int SortOrder);

public sealed record ViewConfigurationDto(Guid Id, string Name, ViewKind Kind,
    List<ViewFilter> Filters, List<string> VisibleFields, string? GroupByField, string? SortField,
    bool SortDescending, bool HideCompleted, int SortOrder);

public sealed record WorkspaceDto(List<WorkspaceItemDto> Items, List<Guid> MatchingItemIds,
    List<TaskStatusDto> Statuses, List<CustomFieldDto> CustomFields, List<ViewConfigurationDto> Views);

public sealed class ItemSaveRequest
{
    public WorkspaceItemKind Kind { get; set; }
    [Required, StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? ParentId { get; set; }
    public Guid? StatusId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public Dictionary<Guid, string?> Fields { get; set; } = [];
}

public sealed class TaskDatesRequest
{
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
}

public sealed class ItemFieldValueRequest
{
    public string? Value { get; set; }
}

public sealed class ItemStatusRequest
{
    public Guid StatusId { get; set; }
}

public sealed class TaskStatusChange
{
    // The id of an existing status, or null for a newly added status.
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
    public StatusCategory Category { get; set; }
    public string Color { get; set; } = StatusColors.Default;
}

public sealed class TaskStatusesRequest
{
    // The complete list in display order; statuses left out are removed.
    public List<TaskStatusChange> Statuses { get; set; } = [];
    // Removed status → existing status that takes over its tasks. Without an entry the first status of the same category is used.
    public Dictionary<Guid, Guid> Replacements { get; set; } = [];
}

public sealed class TaskMoveRequest
{
    public Guid? ParentId { get; set; }
    public Guid? BeforeTaskId { get; set; }
}

public sealed class CustomFieldSaveRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = "";
    public CustomFieldType Type { get; set; }
    public List<string> Options { get; set; } = [];
}

public sealed class SelectOptionChange
{
    // The id of an existing option, or null for a newly added option.
    public Guid? Id { get; set; }
    public string Name { get; set; } = "";
}

public sealed class CustomFieldOptionsRequest
{
    public List<SelectOptionChange> Options { get; set; } = [];
}

public sealed record StorageInfoDto(string DatabasePath, string ImagesDirectory, string ResolvedDatabasePath,
    string ResolvedImagesDirectory, long DatabaseSize, int ImageCount, StoragePaths Defaults);

public sealed class StorageSettingsRequest
{
    public string DatabasePath { get; set; } = "";
    public string ImagesDirectory { get; set; } = "";
}

public sealed class ViewConfigurationSaveRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = "";
    public ViewKind Kind { get; set; }
    public List<ViewFilter> Filters { get; set; } = [];
    public List<string> VisibleFields { get; set; } = [];
    public string? GroupByField { get; set; }
    public string? SortField { get; set; }
    public bool SortDescending { get; set; }
    public bool HideCompleted { get; set; }
}

public sealed record TimeEntryDto(Guid Id, DateOnly Date, TimeOnly Start, TimeOnly End, string Description);

public sealed class TimeEntrySaveRequest
{
    public DateOnly? Date { get; set; }
    public TimeOnly? Start { get; set; }
    public TimeOnly? End { get; set; }
    public string Description { get; set; } = "";
}

public sealed record ProjectDto(Guid Id, string Name, int SortOrder);

public sealed class ProjectSaveRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = "";
}

// Location holds the titles from the top level down to the former parent. ContainedCount counts the items
// that went into the trash together with the entry.
public sealed record TrashEntryDto(Guid Id, Guid ProjectId, string ProjectName, WorkspaceItemKind Kind, string Title,
    DateTimeOffset DeletedAt, int ContainedCount, List<string> Location, bool ParentInTrash);

public sealed record TrashRestoreResultDto(Guid ProjectId, string Message);
