using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;

namespace QuireClone.Services;

/// <summary>
/// Starts an isolated PostgreSQL cluster for the local app when no connection string is configured.
/// PostgreSQL binaries must already be installed on the computer.
/// </summary>
public sealed class LocalPostgres
{
    private const string User = "quireclone";
    private const string Database = "quireclone";
    private readonly string _pgCtl;
    private readonly string _dataDirectory;
    private readonly bool _startedHere;

    private LocalPostgres(string pgCtl, string dataDirectory, bool startedHere, string connectionString)
    {
        _pgCtl = pgCtl;
        _dataDirectory = dataDirectory;
        _startedHere = startedHere;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static string? TryGetExistingConnectionString(string contentRoot)
    {
        var settingsPath = Path.Combine(contentRoot, ".local", "postgres", "settings.json");
        if (!File.Exists(settingsPath)) return null;
        var settings = JsonSerializer.Deserialize<LocalPostgresSettings>(File.ReadAllText(settingsPath));
        return settings is null ? null : BuildConnectionString(settings);
    }

    public static async Task<LocalPostgres> StartAsync(string contentRoot, CancellationToken cancellationToken = default)
    {
        var root = Path.Combine(contentRoot, ".local", "postgres");
        var dataDirectory = Path.Combine(root, "data");
        var settingsPath = Path.Combine(root, "settings.json");
        Directory.CreateDirectory(root);

        LocalPostgresSettings settings;
        if (File.Exists(settingsPath))
        {
            settings = JsonSerializer.Deserialize<LocalPostgresSettings>(
                await File.ReadAllTextAsync(settingsPath, cancellationToken))
                ?? throw new InvalidOperationException("Die lokalen PostgreSQL-Einstellungen sind ungültig.");
            if (!Directory.Exists(dataDirectory))
                throw new InvalidOperationException("Der lokale PostgreSQL-Datenordner fehlt.");
        }
        else
        {
            if (Directory.Exists(dataDirectory))
                throw new InvalidOperationException("Ein PostgreSQL-Datenordner ohne Einstellungen existiert bereits. Bitte den Ordner prüfen.");

            var binDirectory = FindBinDirectory() ?? throw new InvalidOperationException(
                "PostgreSQL wurde nicht gefunden. Bitte PostgreSQL installieren oder POSTGRES_BIN auf den bin-Ordner setzen.");
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            settings = new LocalPostgresSettings(binDirectory, FindAvailablePort(), password);
            var passwordFile = Path.Combine(root, "init-password.tmp");
            try
            {
                await File.WriteAllTextAsync(passwordFile, password + Environment.NewLine, cancellationToken);
                await RunCheckedAsync(Binary(binDirectory, "initdb"),
                    ["-D", dataDirectory, "-U", User, "-E", "UTF8",
                        "--auth-host=scram-sha-256", "--auth-local=scram-sha-256",
                        "--pwfile", passwordFile], cancellationToken);
                await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(settings), cancellationToken);
            }
            finally
            {
                if (File.Exists(passwordFile)) File.Delete(passwordFile);
            }
        }

        var pgCtl = Binary(settings.BinDirectory, "pg_ctl");
        if (!File.Exists(pgCtl))
            throw new InvalidOperationException($"PostgreSQL-Werkzeug fehlt: {pgCtl}");

        var status = await RunAsync(pgCtl, ["status", "-D", dataDirectory], cancellationToken);
        var startedHere = status.ExitCode != 0;
        if (startedHere)
        {
            if (!PortAvailable(settings.Port))
            {
                settings = settings with { Port = FindAvailablePort() };
                await File.WriteAllTextAsync(settingsPath, JsonSerializer.Serialize(settings), cancellationToken);
            }
            await RunCheckedAsync(pgCtl,
                ["start", "-D", dataDirectory, "-l", Path.Combine(root, "server.log"),
                    "-o", $"-p {settings.Port} -h 127.0.0.1", "-w"], cancellationToken,
                captureOutput: false);
        }

        var connectionString = BuildConnectionString(settings);
        var instance = new LocalPostgres(pgCtl, dataDirectory, startedHere, connectionString);
        try
        {
            await instance.EnsureDatabaseAsync(cancellationToken);
            return instance;
        }
        catch
        {
            await instance.StopAsync();
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (!_startedHere) return;
        try { await RunCheckedAsync(_pgCtl, ["stop", "-D", _dataDirectory, "-m", "fast", "-w"]); }
        catch (Exception exception) { Console.Error.WriteLine($"Lokales PostgreSQL konnte nicht gestoppt werden: {exception.Message}"); }
    }

    private async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var existsCommand = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'quireclone')", connection);
        if ((bool)(await existsCommand.ExecuteScalarAsync(cancellationToken) ?? false)) return;
        await using var createCommand = new NpgsqlCommand("CREATE DATABASE quireclone", connection);
        await createCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? FindBinDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("POSTGRES_BIN");
        if (!string.IsNullOrWhiteSpace(configured) && HasBinaries(configured))
            return Path.GetFullPath(configured);

        var pathDirectories = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var directory in pathDirectories)
            if (HasBinaries(directory)) return Path.GetFullPath(directory);

        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PostgreSQL"),
            "/usr/lib/postgresql", "/opt/homebrew/opt/postgresql@17", "/opt/homebrew/opt/postgresql@16"
        };
        foreach (var root in roots)
        {
            if (HasBinaries(Path.Combine(root, "bin"))) return Path.Combine(root, "bin");
            if (!Directory.Exists(root)) continue;
            foreach (var versionDirectory in Directory.GetDirectories(root).OrderByDescending(VersionNumber))
            {
                var bin = Path.Combine(versionDirectory, "bin");
                if (HasBinaries(bin)) return bin;
            }
        }
        return null;
    }

    private static int VersionNumber(string directory) =>
        int.TryParse(Path.GetFileName(directory), out var version) ? version : 0;

    private static string BuildConnectionString(LocalPostgresSettings settings) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1", Port = settings.Port, Database = Database,
            Username = User, Password = settings.Password
        }.ConnectionString;

    private static bool HasBinaries(string directory) =>
        File.Exists(Binary(directory, "initdb")) && File.Exists(Binary(directory, "pg_ctl"));

    private static string Binary(string directory, string name) =>
        Path.Combine(directory, OperatingSystem.IsWindows() ? name + ".exe" : name);

    private static int FindAvailablePort()
    {
        for (var port = 5433; port <= 5499; port++)
            if (PortAvailable(port)) return port;
        throw new InvalidOperationException("Kein freier Port für lokales PostgreSQL zwischen 5433 und 5499 gefunden.");
    }

    private static bool PortAvailable(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException) { return false; }
    }

    private static async Task RunCheckedAsync(string fileName, string[] arguments,
        CancellationToken cancellationToken = default, bool captureOutput = true)
    {
        var result = await RunAsync(fileName, arguments, cancellationToken, captureOutput);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{Path.GetFileName(fileName)} ist fehlgeschlagen: {result.Output}");
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string fileName, string[] arguments,
        CancellationToken cancellationToken = default, bool captureOutput = true)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"{fileName} konnte nicht gestartet werden.");
        var output = captureOutput ? process.StandardOutput.ReadToEndAsync(cancellationToken) : Task.FromResult("");
        var error = captureOutput ? process.StandardError.ReadToEndAsync(cancellationToken) : Task.FromResult("");
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, (await output + Environment.NewLine + await error).Trim());
    }

    private sealed record LocalPostgresSettings(string BinDirectory, int Port, string Password);
}
