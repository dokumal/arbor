namespace QuireClone.Domain;

/// <summary>
/// A project holds its own tree of items, statuses, custom fields and views. Projects are not nested
/// and share no data with each other.
/// </summary>
public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class DefaultProject
{
    // Data from before projects existed belongs to this project.
    public static readonly Guid Id = new("7c2e4b1a-9d3f-4a6e-8b05-2f1d6c9e4a01");
    public const string Name = "Mein Projekt";
}
