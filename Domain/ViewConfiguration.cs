namespace QuireClone.Domain;

public enum ViewKind { Tree, Kanban, Calendar, Timeline }

public sealed class ViewConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ViewKind Kind { get; set; }
    public string FiltersJson { get; set; } = "[]";
    public string VisibleFieldsJson { get; set; } = "[]";
    public int SortOrder { get; set; }
}
