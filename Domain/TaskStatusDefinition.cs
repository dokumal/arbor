namespace QuireClone.Domain;

/// <summary>
/// Every status belongs to a fixed category. The application only looks at the category,
/// e.g. to hide completed tasks or to highlight overdue ones.
/// </summary>
public enum StatusCategory { Open, Active, Done }

public sealed class TaskStatusDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = "";
    public StatusCategory Category { get; set; }
    // A key from StatusColors.All; the frontend maps it to a colour.
    public string Color { get; set; } = StatusColors.Default;
    // Order within the category.
    public int SortOrder { get; set; }
}

public static class StatusColors
{
    public const string Default = "gray";

    public static IReadOnlySet<string> All { get; } =
        new HashSet<string> { "gray", "slate", "blue", "teal", "green", "orange", "purple", "pink" };
}

/// <summary>The statuses of the first project; they replace the former fixed values Todo, Doing and Done.</summary>
public static class DefaultStatuses
{
    public static readonly Guid Open = new("3b1f6c0e-5a4d-4e8b-9a52-0c6f1d2e7a01");
    public static readonly Guid Active = new("3b1f6c0e-5a4d-4e8b-9a52-0c6f1d2e7a02");
    public static readonly Guid Done = new("3b1f6c0e-5a4d-4e8b-9a52-0c6f1d2e7a03");
}
