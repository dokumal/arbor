using Microsoft.EntityFrameworkCore;
using QuireClone.Data;
using QuireClone.Domain;

namespace QuireClone.Services;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        Directory.CreateDirectory(Path.GetDirectoryName(scope.ServiceProvider.GetRequiredService<StorageSettings>().DatabasePath)!);
        await InitializeAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    // Applies migrations and seeds a new, empty database with sample fields and views.
    public static async Task InitializeAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        // EF creates SQLite files in WAL mode; a rollback journal keeps all data in the one .db file, so copying it is a complete backup.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE;", cancellationToken);
        if (await db.ViewConfigurations.AnyAsync(cancellationToken)) return;

        var label = new CustomFieldDefinition { Name = "Label", Type = CustomFieldType.Text };
        var priority = new CustomFieldDefinition { Name = "Priorität", Type = CustomFieldType.Select,
            Options = [new(Guid.NewGuid(), "Niedrig"), new(Guid.NewGuid(), "Mittel"), new(Guid.NewGuid(), "Hoch")],
            SortOrder = 1 };
        db.CustomFields.AddRange(label, priority);
        db.ViewConfigurations.AddRange(
            View("Alle Einträge", ViewKind.Tree, 0, ["kind", "status", "title", "dueDate", FieldKeys.ForCustomField(label.Id), FieldKeys.ForCustomField(priority.Id)]),
            View("Kanban", ViewKind.Kanban, 1, ["title", "dueDate", FieldKeys.ForCustomField(label.Id)]),
            View("Kalender", ViewKind.Calendar, 2, ["title", "status", FieldKeys.ForCustomField(label.Id)]),
            View("Zeitachse", ViewKind.Timeline, 3, ["title", "status", "startDate", "dueDate", FieldKeys.ForCustomField(label.Id)]),
            UpcomingView(4));
        await db.SaveChangesAsync(cancellationToken);
    }

    // Open tasks that are overdue or due within the next seven days.
    private static ViewConfiguration UpcomingView(int order) => new()
    {
        Name = "Anstehend", Kind = ViewKind.Tree, SortOrder = order,
        VisibleFields = [FieldKeys.Title, FieldKeys.Status, FieldKeys.DueDate],
        Filters = [new ViewFilter(FieldKeys.DueDate, "before", "@today+7")],
        SortField = FieldKeys.DueDate, HideCompleted = true
    };

    private static ViewConfiguration View(string name, ViewKind kind, int order, string[] fields) => new()
    {
        Name = name, Kind = kind, SortOrder = order,
        VisibleFields = [.. fields]
    };
}
