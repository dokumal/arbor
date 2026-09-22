namespace QuireClone.Domain;

public enum TaskStatus { Todo, Doing, Done }

public sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? ParentTaskId { get; set; }
    public TaskItem? ParentTask { get; set; }
    public List<TaskItem> Children { get; set; } = [];
    public TaskStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<TaskFieldValue> FieldValues { get; set; } = [];
}
