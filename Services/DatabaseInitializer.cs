using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        if (await db.ViewConfigurations.AnyAsync()) return;

        var label = new CustomFieldDefinition { Name = "Label", Type = CustomFieldType.Text };
        var priority = new CustomFieldDefinition { Name = "Priorität", Type = CustomFieldType.Select,
            OptionsJson = JsonSerializer.Serialize(new[] { "Niedrig", "Mittel", "Hoch" }), SortOrder = 1 };
        db.CustomFields.AddRange(label, priority);
        db.ViewConfigurations.AddRange(
            View("Alle Einträge", ViewKind.Tree, 0, ["kind", "status", "title", "dueDate", $"field:{label.Id}", $"field:{priority.Id}"]),
            View("Kanban", ViewKind.Kanban, 1, ["title", "dueDate", $"field:{label.Id}"]),
            View("Kalender", ViewKind.Calendar, 2, ["title", "status", $"field:{label.Id}"]),
            View("Zeitachse", ViewKind.Timeline, 3, ["title", "status", "startDate", "dueDate", $"field:{label.Id}"]),
            UpcomingView(4));
        await db.SaveChangesAsync();
    }

    // Open tasks that are overdue or due within the next seven days; the migration adds the same view to existing workspaces.
    public static ViewConfiguration UpcomingView(int order) => new()
    {
        Name = "Anstehend", Kind = ViewKind.Tree, SortOrder = order,
        VisibleFieldsJson = JsonSerializer.Serialize(new[] { "title", "status", "dueDate" }),
        FiltersJson = JsonSerializer.Serialize(new[] { new ViewFilter("dueDate", "before", "@today+7") }),
        SortField = "dueDate", HideCompleted = true
    };

    private static ViewConfiguration View(string name, ViewKind kind, int order, string[] fields) => new()
    {
        Name = name, Kind = kind, SortOrder = order,
        VisibleFieldsJson = JsonSerializer.Serialize(fields)
    };
}
