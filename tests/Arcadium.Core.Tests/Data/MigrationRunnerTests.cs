using Arcadium.Core.Data;
using Arcadium.Core.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Tests.Data;

public sealed class MigrationRunnerTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public MigrationRunnerTests()
    {
        _connection = TestDatabase.CreateMigrated();
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
