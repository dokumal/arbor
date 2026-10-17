using QuireClone.Domain;

namespace QuireClone.Services;

/// <summary>What a new project starts with: one status per category and the usual views.</summary>
public static class ProjectDefaults
{
    public static IEnumerable<TaskStatusDefinition> Statuses(Guid projectId) =>
    [
        new() { ProjectId = projectId, Name = "Offen", Category = StatusCategory.Open, Color = "slate" },
        new() { ProjectId = projectId, Name = "In Arbeit", Category = StatusCategory.Active, Color = "blue" },
        new() { ProjectId = projectId, Name = "Erledigt", Category = StatusCategory.Done, Color = "green" }
    ];

    // Custom fields to show in the views, e.g. the sample fields of the first project.
    public static IEnumerable<ViewConfiguration> Views(Guid projectId, params Guid[] fieldIds)
    {
        var fields = fieldIds.Select(FieldKeys.ForCustomField).ToArray();
        var first = fields.Take(1).ToArray();
        return
        [
            View(projectId, "Alle Einträge", ViewKind.Tree, 0, [FieldKeys.Kind, FieldKeys.Status, FieldKeys.Title, FieldKeys.DueDate, .. fields]),
            View(projectId, "Kanban", ViewKind.Kanban, 1, [FieldKeys.Title, FieldKeys.DueDate, .. first]),
            View(projectId, "Kalender", ViewKind.Calendar, 2, [FieldKeys.Title, FieldKeys.Status, .. first]),
            View(projectId, "Zeitachse", ViewKind.Timeline, 3, [FieldKeys.Title, FieldKeys.Status, FieldKeys.StartDate, FieldKeys.DueDate, .. first]),
            UpcomingView(projectId, 4)
        ];
    }

    // Open tasks that are overdue or due within the next seven days.
    private static ViewConfiguration UpcomingView(Guid projectId, int order) => new()
    {
        ProjectId = projectId, Name = "Anstehend", Kind = ViewKind.Tree, SortOrder = order,
        VisibleFields = [FieldKeys.Title, FieldKeys.Status, FieldKeys.DueDate],
        Filters = [new ViewFilter(FieldKeys.DueDate, "before", "@today+7")],
        SortField = FieldKeys.DueDate, HideCompleted = true
    };

    private static ViewConfiguration View(Guid projectId, string name, ViewKind kind, int order, string[] fields) => new()
    {
        ProjectId = projectId, Name = name, Kind = kind, SortOrder = order,
        VisibleFields = [.. fields]
    };
}
