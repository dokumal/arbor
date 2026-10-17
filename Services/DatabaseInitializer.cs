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

    // Applies migrations and seeds the first project of a new, empty database with sample fields and views.
    // The migrations already create that project and its statuses.
    public static async Task InitializeAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        // EF creates SQLite files in WAL mode; a rollback journal keeps all data in the one .db file, so copying it is a complete backup.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=DELETE;", cancellationToken);
        if (await db.ViewConfigurations.AnyAsync(cancellationToken)) return;

        var projectId = await db.Projects.OrderBy(x => x.SortOrder).Select(x => x.Id).FirstAsync(cancellationToken);
        var label = new CustomFieldDefinition { ProjectId = projectId, Name = "Label", Type = CustomFieldType.Text };
        var priority = new CustomFieldDefinition { ProjectId = projectId, Name = "Priorität", Type = CustomFieldType.Select,
            Options = [new(Guid.NewGuid(), "Niedrig"), new(Guid.NewGuid(), "Mittel"), new(Guid.NewGuid(), "Hoch")],
            SortOrder = 1 };
        db.CustomFields.AddRange(label, priority);
        db.ViewConfigurations.AddRange(ProjectDefaults.Views(projectId, label.Id, priority.Id));
        await db.SaveChangesAsync(cancellationToken);
    }
}
