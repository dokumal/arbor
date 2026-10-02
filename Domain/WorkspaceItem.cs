namespace QuireClone.Domain;

public enum TaskStatus { Todo, Doing, Done }
public enum WorkspaceItemKind { Task, Document, Folder }

public sealed class WorkspaceItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public WorkspaceItemKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid? ParentId { get; set; }
    public WorkspaceItem? ParentItem { get; set; }
    public List<WorkspaceItem> Children { get; set; } = [];
    public TaskStatus Status { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ItemFieldValue> FieldValues { get; set; } = [];
}
