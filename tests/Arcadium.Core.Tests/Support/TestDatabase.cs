using Arcadium.Core.Data;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Tests.Support;

/// <summary>Creates an open, fully migrated in-memory SQLite connection for a single test.</summary>
internal static class TestDatabase
{
    internal static SqliteConnection CreateMigrated()
    {
        SqliteConnection connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using (SqliteCommand pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        new MigrationRunner(connection).RunMigrations();

        return connection;
    }
}
