using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Data;

public sealed class SqliteDatabase
{
    private const int BusyTimeoutMilliseconds = 5000;

    private readonly string _databasePath;

    public SqliteDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        _databasePath = Path.GetFullPath(databasePath);

        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public SqliteConnection OpenReadWrite()
    {
        var connection = CreateConnection(SqliteOpenMode.ReadWriteCreate);
        connection.Open();

        try
        {
            ExecutePragma(connection, "PRAGMA journal_mode = WAL;");
            ConfigureConnection(connection);

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public SqliteConnection OpenReadOnly()
    {
        var connection = CreateConnection(SqliteOpenMode.ReadOnly);
        connection.Open();

        try
        {
            ConfigureConnection(connection);

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private SqliteConnection CreateConnection(SqliteOpenMode mode)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = mode
        }.ToString();

        return new SqliteConnection(connectionString);
    }

    private static void ConfigureConnection(SqliteConnection connection)
    {
        ExecutePragma(connection, "PRAGMA foreign_keys = ON;");
        ExecutePragma(
            connection,
            $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds};");
    }

    private static void ExecutePragma(
        SqliteConnection connection,
        string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }
}