using Arcadium.Core.Data;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Tests.Support;

/// <summary>Creates an open, fully migrated in-memory SQLite connection for a single test.</summary>
internal static class TestDatabase
{
    internal static SqliteConnection CreateMigrated()
    {
        SqliteConnection connection = CreateEmpty();

        new MigrationRunner(connection).RunMigrations();

        return connection;
    }

    /// <summary>Opens an in-memory SQLite connection with no migrations applied (no tables at all).</summary>
    internal static SqliteConnection CreateEmpty()
    {
        SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using (SqliteCommand pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        return connection;
    }
}
