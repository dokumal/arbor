using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace QuireClone.Services;

public sealed record StoragePaths(string DatabasePath, string ImagesDirectory)
{
    public static StoragePaths Defaults { get; } = new(Path.Combine(".local", "arbor.db"), Path.Combine(".local", "images"));
}

/// <summary>
/// Where the database file and the image folder live. Kept in a small JSON file outside the database,
/// because the database itself can be moved. Relative paths are resolved against the app folder.
/// </summary>
public sealed class StorageSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _contentRoot;
    private readonly string _file;
    private volatile StoragePaths _paths;

    public StorageSettings(string contentRoot)
    {
        _contentRoot = contentRoot;
        _file = Path.Combine(contentRoot, ".local", "storage.json");
        _paths = Load(_file) ?? StoragePaths.Defaults;
    }

    public StoragePaths Paths => _paths;
    public string DatabasePath => Resolve(_paths.DatabasePath);
    public string ImagesDirectory => Resolve(_paths.ImagesDirectory);
    public string ConnectionString => BuildConnectionString(DatabasePath);

    public string Resolve(string path) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()), _contentRoot);

    public static string BuildConnectionString(string databasePath) =>
        new SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true }.ToString();

    public void Save(StoragePaths paths)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(paths, JsonOptions));
        _paths = paths;
    }

    private static StoragePaths? Load(string file)
    {
        if (!File.Exists(file)) return null;
        try
        {
            var paths = JsonSerializer.Deserialize<StoragePaths>(File.ReadAllText(file), JsonOptions);
            return paths is null || string.IsNullOrWhiteSpace(paths.DatabasePath) || string.IsNullOrWhiteSpace(paths.ImagesDirectory)
                ? null : paths;
        }
        catch (JsonException) { return null; }
    }
}
