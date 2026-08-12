using System.Globalization;
using System.Reflection;
using Arcadium.Core.Logging;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Data;

public class MigrationRunner
{
    private const string ResourcePrefix = "Migrations.";

    private readonly SqliteConnection _connection;

    public MigrationRunner(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    public void RunMigrations()
    {
        EnsureSchemaVersionTable();

        long currentVersion = GetCurrentVersion();

        (int Version, string ResourceName)[] pendingMigrations = GetMigrationScripts()
            .Where(script => script.Version > currentVersion)
            .ToArray();

        if (pendingMigrations.Length == 0)
        {
            return;
        }

        if (currentVersion > 0)
        {
            BackupBeforeMigrating(currentVersion);
        }

        foreach ((int version, string resourceName) in pendingMigrations)
        {
            ApplyMigration(version, resourceName);
        }
    }

    private void BackupBeforeMigrating(long currentVersion)
    {
        string sourcePath = _connection.DataSource;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return;
        }

        string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        string backupPath = $"{sourcePath}.v{currentVersion}.{timestamp}.bak";

        Logger.LogInformation($"Backing up database to '{backupPath}' before applying migrations.");

        using SqliteConnection backupConnection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        backupConnection.Open();

        _connection.BackupDatabase(backupConnection);
    }

    private void EnsureSchemaVersionTable()
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER PRIMARY KEY,
                applied_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
        ";
        command.ExecuteNonQuery();
    }

    private long GetCurrentVersion()
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";

        return (long)command.ExecuteScalar()!;
    }

    private static (int Version, string ResourceName)[] GetMigrationScripts()
    {
        Assembly assembly = typeof(MigrationRunner).Assembly;

        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(name => (Version: ParseVersion(name), ResourceName: name))
            .OrderBy(script => script.Version)
            .ToArray();
    }

    private static int ParseVersion(string resourceName)
    {
        string fileName = resourceName[ResourcePrefix.Length..];
        int separatorIndex = fileName.IndexOf('_');

        if (separatorIndex <= 0
            || !int.TryParse(fileName[..separatorIndex], NumberStyles.None, CultureInfo.InvariantCulture, out int version))
        {
            throw new InvalidOperationException(
                $"Migration resource '{resourceName}' does not start with a numeric version prefix (expected e.g. '001_name.sql').");
        }

        return version;
    }

    private void ApplyMigration(int version, string resourceName)
    {
        Logger.LogInformation($"Applying migration {version}: {resourceName}");
        string migrationSql = ReadScript(resourceName);

        using SqliteTransaction transaction = _connection.BeginTransaction();
        using SqliteCommand command = _connection.CreateCommand();
        command.Transaction = transaction;

        command.CommandText = migrationSql;
        command.ExecuteNonQuery();

        command.CommandText = "INSERT INTO schema_version (version) VALUES ($version);";
        command.Parameters.AddWithValue("$version", version);
        command.ExecuteNonQuery();

        transaction.Commit();
    }

    private static string ReadScript(string resourceName)
    {
        Assembly assembly = typeof(MigrationRunner).Assembly;

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded migration resource '{resourceName}' not found.");
        using StreamReader reader = new(stream);

        return reader.ReadToEnd();
    }
}
