using System.ComponentModel.DataAnnotations;
using QuireClone.Domain;
using QuireClone.Services;
using TaskStatus = QuireClone.Domain.TaskStatus;

namespace QuireClone.Contracts;

public sealed record WorkspaceItemDto(Guid Id, WorkspaceItemKind Kind, string Title, string Description, Guid? ParentId,
    TaskStatus Status, DateOnly? StartDate, DateOnly? DueDate, int SortOrder,
    DateTimeOffset UpdatedAt, Dictionary<Guid, string> Fields);

public sealed record CustomFieldDto(Guid Id, string Name, CustomFieldType Type,
    List<SelectOption> Options, int SortOrder);

public sealed record ViewConfigurationDto(Guid Id, string Name, ViewKind Kind,
    List<ViewFilter> Filters, List<string> VisibleFields, string? GroupByField, string? SortField,
    bool SortDescending, bool HideCompleted, int SortOrder);

public sealed record ItemLinkDto(Guid TaskId, Guid DocumentId);

public sealed record WorkspaceDto(List<WorkspaceItemDto> Items, List<Guid> MatchingItemIds,
    List<CustomFieldDto> CustomFields, List<ViewConfigurationDto> Views, List<ItemLinkDto> Links);

public sealed class ItemSaveRequest
{
    public WorkspaceItemKind Kind { get; set; }
    [Required, StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? ParentId { get; set; }
    public TaskStatus Status { get; set; }
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

public sealed class TaskMoveRequest
{
    public Guid? ParentId { get; set; }
    public Guid? BeforeTaskId { get; set; }
}

public sealed class ItemLinkRequest
{
    public Guid RelatedItemId { get; set; }
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
