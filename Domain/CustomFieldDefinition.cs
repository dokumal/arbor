namespace QuireClone.Domain;

public enum CustomFieldType { Text, Number, Date, Select, Boolean }

public sealed class CustomFieldDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public CustomFieldType Type { get; set; }
    public string OptionsJson { get; set; } = "[]";
    public int SortOrder { get; set; }
    public List<TaskFieldValue> Values { get; set; } = [];
}

public sealed class TaskFieldValue
{
    public Guid TaskId { get; set; }
    public TaskItem Task { get; set; } = null!;
    public Guid FieldId { get; set; }
    public CustomFieldDefinition Field { get; set; } = null!;
    public string Value { get; set; } = "";
}
