using Arcadium.Core.Data;
using Arcadium.Core.Models;
using Arcadium.Core.Tests.Support;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Arcadium.Core.Tests.Data;

public sealed class MigrationRunnerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public MigrationRunnerTests()
    {
        _connection = TestDatabase.CreateMigrated();
    }

    [Fact]
    public void RunMigrations_WithPendingMigrationsOnANonEmptyDatabase_BacksUpThePreMigrationState()
    {
        using TempDirectory tempDirectory = new TempDirectory();
        string databasePath = Path.Combine(tempDirectory.Path, "arcadium.db");
        SqliteDatabase database = new SqliteDatabase(databasePath);
        DateTime timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using (SqliteConnection setupConnection = database.OpenReadWrite())
        {
            new MigrationRunner(setupConnection).RunMigrations();

            ScanRepository scanRepository = new ScanRepository(setupConnection);
            scanRepository.BeginTransaction();
            scanRepository.UpsertRom(new RomRecord
            {
                SystemId = "arcade",
                Filename = "pacman.zip",
                Basename = "pacman",
                Path = "/roms/arcade/pacman.zip",
                SizeBytes = 4096,
                ModifiedTimeUtc = timestamp,
                ScanState = "active",
                FirstSeenAt = timestamp,
                LastSeenAt = timestamp,
                CreatedAt = timestamp,
                UpdatedAt = timestamp
            });
            scanRepository.Commit();

            // Roll the recorded version back to 1 so the next run sees every migration above it as
            // pending, without touching the tables those migrations already created (their scripts
            // are idempotent) or the rom row seeded above.
            using SqliteCommand rollback = setupConnection.CreateCommand();
            rollback.CommandText = "DELETE FROM schema_version WHERE version > 1;";
            rollback.ExecuteNonQuery();
        }

        using (SqliteConnection secondRunConnection = database.OpenReadWrite())
        {
            new MigrationRunner(secondRunConnection).RunMigrations();
        }

        string[] backupFiles = Directory.GetFiles(tempDirectory.Path, "arcadium.db.v*.bak");
        string backupPath = Assert.Single(backupFiles);
        Assert.Contains(".v1.", Path.GetFileName(backupPath));

        using SqliteConnection backupConnection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
        backupConnection.Open();

        using SqliteCommand versionCommand = backupConnection.CreateCommand();
        versionCommand.CommandText = "SELECT MAX(version) FROM schema_version;";
        long backedUpVersion = (long)versionCommand.ExecuteScalar()!;
        Assert.Equal(1, backedUpVersion);

        using SqliteCommand romCommand = backupConnection.CreateCommand();
        romCommand.CommandText = "SELECT basename FROM roms WHERE path = $path;";
        romCommand.Parameters.AddWithValue("$path", "/roms/arcade/pacman.zip");
        Assert.Equal("pacman", (string)romCommand.ExecuteScalar()!);
    }

    [Fact]
    public void RunMigrations_OnFreshFileDatabase_CreatesNoBackup()
    {
        using TempDirectory tempDirectory = new TempDirectory();
        string databasePath = Path.Combine(tempDirectory.Path, "arcadium.db");
        SqliteDatabase database = new SqliteDatabase(databasePath);

        using (SqliteConnection connection = database.OpenReadWrite())
        {
            new MigrationRunner(connection).RunMigrations();
        }

        Assert.Empty(Directory.GetFiles(tempDirectory.Path, "*.bak"));
    }

    [Fact]
    public void RunMigrations_OnFreshDatabase_CreatesExpectedTablesAndSchemaVersion()
    {
        List<string> tableNames = QueryTableNames();

        Assert.Contains("roms", tableNames);
        Assert.Contains("scan_runs", tableNames);
        Assert.Contains("mame_machines", tableNames);
        Assert.Contains("mame_machine_roms", tableNames);
        Assert.Contains("mame_machine_controls", tableNames);
        Assert.Contains("schema_version", tableNames);
        Assert.Equal(2, CountSchemaVersionRows());
    }

    [Fact]
    public void RunMigrations_CalledTwice_IsANoOp()
    {
        new MigrationRunner(_connection).RunMigrations();

        Assert.Equal(2, CountSchemaVersionRows());
    }

    private List<string> QueryTableNames()
    {
        List<string> names = new List<string>();

        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private long CountSchemaVersionRows()
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM schema_version;";

        return (long)command.ExecuteScalar()!;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
