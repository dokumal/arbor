using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuireClone.Contracts;
using QuireClone.Data;

namespace QuireClone.Services;

/// <summary>
/// Changes where the database file and the images are stored.
/// A database is moved to a new path that holds no file yet; an existing Arbor database at the new path is used instead.
/// </summary>
public sealed class StorageLocationService(StorageSettings settings)
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public StorageInfoDto GetInfo()
    {
        var database = new FileInfo(settings.DatabasePath);
        var images = settings.ImagesDirectory;
        var imageCount = Directory.Exists(images)
            ? Directory.EnumerateFiles(images).Count(file => TaskImageStorage.IsStoredImageName(Path.GetFileName(file)))
            : 0;
        return new StorageInfoDto(settings.Paths.DatabasePath, settings.Paths.ImagesDirectory,
            database.FullName, images, database.Exists ? database.Length : 0, imageCount, StoragePaths.Defaults);
    }

    public async Task<string> ChangeAsync(StoragePaths requested, CancellationToken cancellationToken)
    {
        var requestedPaths = new StoragePaths(requested.DatabasePath.Trim(), requested.ImagesDirectory.Trim());
        if (requestedPaths.DatabasePath.Length == 0 || requestedPaths.ImagesDirectory.Length == 0)
            throw new ArgumentException("Bitte beide Speicherorte angeben.");
        string newDatabase, newImages;
        try
        {
            newDatabase = settings.Resolve(requestedPaths.DatabasePath);
            newImages = settings.Resolve(requestedPaths.ImagesDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("Mindestens ein Pfad ist ungültig.");
        }
        if (Directory.Exists(newDatabase))
            throw new ArgumentException("Für die Datenbank bitte einen Dateinamen angeben, keinen Ordner.");
        if (File.Exists(newImages))
            throw new ArgumentException("Der Bilder-Pfad zeigt auf eine Datei. Bitte einen Ordner angeben.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var oldDatabase = settings.DatabasePath;
            var oldImages = settings.ImagesDirectory;
            var messages = new List<string>();
            var moveDatabase = false;

            if (!SamePath(oldDatabase, newDatabase))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(newDatabase)!);
                if (File.Exists(newDatabase))
                {
                    await UseExistingDatabaseAsync(newDatabase, cancellationToken);
                    messages.Add("Die vorhandene Datenbank am neuen Ort wird jetzt verwendet. Die bisherige Datei bleibt am alten Ort erhalten.");
                }
                else
                {
                    await CopyDatabaseAsync(oldDatabase, newDatabase, cancellationToken);
                    moveDatabase = true;
                }
            }

            if (!SamePath(oldImages, newImages))
            {
                Directory.CreateDirectory(newImages);
                var moved = MoveImages(oldImages, newImages);
                messages.Add(moved == 1 ? "1 Bild wurde in den neuen Ordner verschoben." : $"{moved} Bilder wurden in den neuen Ordner verschoben.");
            }

            settings.Save(requestedPaths);

            if (moveDatabase)
            {
                // The copy is complete and in use; release pooled handles so the old file can be removed.
                SqliteConnection.ClearAllPools();
                try
                {
                    File.Delete(oldDatabase);
                    messages.Insert(0, "Die Datenbank wurde an den neuen Ort verschoben.");
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    messages.Insert(0, $"Die Datenbank wurde an den neuen Ort kopiert. Die alte Datei konnte nicht gelöscht werden: {oldDatabase}");
                }
            }
            return messages.Count > 0 ? string.Join(" ", messages) : "Einstellungen gespeichert.";
        }
        finally { _gate.Release(); }
    }

    private static async Task CopyDatabaseAsync(string source, string target, CancellationToken cancellationToken)
    {
        // The SQLite backup API produces a consistent copy even while the app holds open connections.
        try
        {
            await using var from = new SqliteConnection(StorageSettings.BuildConnectionString(source));
            await using var to = new SqliteConnection(StorageSettings.BuildConnectionString(target));
            await from.OpenAsync(cancellationToken);
            await to.OpenAsync(cancellationToken);
            from.BackupDatabase(to);
        }
        catch
        {
            SqliteConnection.ClearAllPools();
            File.Delete(target);
            throw;
        }
    }

    private static async Task UseExistingDatabaseAsync(string path, CancellationToken cancellationToken)
    {
        var connectionString = StorageSettings.BuildConnectionString(path);
        var tables = new List<string>();
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) tables.Add(reader.GetString(0));
        }
        catch (SqliteException)
        {
            throw new ArgumentException("Die Datei am neuen Ort ist keine SQLite-Datenbank.");
        }
        // An empty file is fine; a database of another application is left untouched.
        if (tables.Count > 0 && !tables.Contains("__EFMigrationsHistory"))
            throw new ArgumentException("Die Datei am neuen Ort ist keine Arbor-Datenbank.");

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
        await using var db = new AppDbContext(options);
        await DatabaseInitializer.InitializeAsync(db, cancellationToken);
    }

    private static int MoveImages(string from, string to)
    {
        if (!Directory.Exists(from)) return 0;
        var moved = 0;
        foreach (var file in Directory.EnumerateFiles(from).ToList())
        {
            var name = Path.GetFileName(file);
            var target = Path.Combine(to, name);
            if (!TaskImageStorage.IsStoredImageName(name) || File.Exists(target)) continue;
            File.Move(file, target);
            moved++;
        }
        if (!Directory.EnumerateFileSystemEntries(from).Any())
        {
            try { Directory.Delete(from); }
            catch (IOException) { /* An empty leftover folder is harmless. */ }
        }
        return moved;
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), PathComparison);
}
