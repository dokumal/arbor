using System.ComponentModel.DataAnnotations;
using QuireClone.Domain;
using TaskStatus = QuireClone.Domain.TaskStatus;

namespace QuireClone.Contracts;

public sealed record TaskDto(Guid Id, string Title, string Description, Guid? ParentTaskId,
    TaskStatus Status, DateOnly? StartDate, DateOnly? DueDate, int SortOrder,
    DateTimeOffset UpdatedAt, Dictionary<Guid, string> Fields);

public sealed record CustomFieldDto(Guid Id, string Name, CustomFieldType Type,
    List<string> Options, int SortOrder);

public sealed record ViewFilter(string Field, string Operator, string Value);

public sealed record ViewConfigurationDto(Guid Id, string Name, ViewKind Kind,
    List<ViewFilter> Filters, List<string> VisibleFields, string? GroupByField, int SortOrder);

public sealed record WorkspaceDto(List<TaskDto> Tasks, List<Guid> MatchingTaskIds,
    List<CustomFieldDto> CustomFields, List<ViewConfigurationDto> Views);

public sealed class TaskSaveRequest
{
    [Required, StringLength(300, MinimumLength = 1)]
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? ParentTaskId { get; set; }
    public TaskStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public Dictionary<Guid, string?> Fields { get; set; } = [];
}

public sealed class TaskMoveRequest
{
    public Guid? ParentTaskId { get; set; }
    public Guid? BeforeTaskId { get; set; }
}

public sealed class CustomFieldSaveRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = "";
    public CustomFieldType Type { get; set; }
    public List<string> Options { get; set; } = [];
}

public sealed class ViewConfigurationSaveRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = "";
    public ViewKind Kind { get; set; }
    public List<ViewFilter> Filters { get; set; } = [];
    public List<string> VisibleFields { get; set; } = [];
    public string? GroupByField { get; set; }
}
