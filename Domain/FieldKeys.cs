namespace QuireClone.Domain;

/// <summary>
/// Views refer to fields by key: a built-in name such as "dueDate", or "field:{id}" for a custom field.
/// </summary>
public static class FieldKeys
{
    public const string Title = "title";
    public const string Description = "description";
    public const string Kind = "kind";
    public const string Status = "status";
    public const string StartDate = "startDate";
    public const string DueDate = "dueDate";

    private const string CustomPrefix = "field:";

    public static IReadOnlySet<string> BuiltIn { get; } =
        new HashSet<string> { Title, Description, Kind, Status, StartDate, DueDate };

    public static string ForCustomField(Guid fieldId) => $"{CustomPrefix}{fieldId}";

    public static bool TryGetCustomFieldId(string key, out Guid fieldId)
    {
        fieldId = default;
        return key.StartsWith(CustomPrefix, StringComparison.Ordinal) &&
            Guid.TryParse(key.AsSpan(CustomPrefix.Length), out fieldId);
    }
}
