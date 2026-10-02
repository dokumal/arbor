namespace QuireClone.Domain;

public sealed class ItemLink
{
    public Guid TaskId { get; set; }
    public WorkspaceItem Task { get; set; } = null!;
    public Guid DocumentId { get; set; }
    public WorkspaceItem Document { get; set; } = null!;
}
