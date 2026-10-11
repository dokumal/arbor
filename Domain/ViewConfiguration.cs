namespace QuireClone.Domain;

public enum ViewKind { Tree, Kanban, Calendar, Timeline }

public sealed class ViewConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ViewKind Kind { get; set; }
    public List<ViewFilter> Filters { get; set; } = [];
    // Field keys as built by FieldKeys.
    public List<string> VisibleFields { get; set; } = [];
    public string? GroupByField { get; set; }
    // Null keeps the manual order from the tree.
    public string? SortField { get; set; }
    public bool SortDescending { get; set; }
    public bool HideCompleted { get; set; }
    public int SortOrder { get; set; }
}

public sealed record ViewFilter(string Field, string Operator, string Value);
