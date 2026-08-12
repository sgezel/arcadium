using Arcadium.Core.Models;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Data;

/// <summary>
/// Data access for the game_play_history table: recording emulator launches and exits.
/// The caller owns the connection, which must be opened via
/// <see cref="SqliteDatabase.OpenReadWrite"/> since this repository writes.
/// </summary>
public sealed class PlayHistoryRepository
{
    private readonly SqliteConnection _connection;

    public PlayHistoryRepository(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    /// <summary>Records a new emulator launch for a rom. Throws if no rom with this id exists.</summary>
    public void AddPlayHistory(long romId)
    {
        if (romId <= 0)
        {
            throw new ArgumentException($"Invalid rom id {romId}.");
        }

        LibraryRepository libraryRepository = new LibraryRepository(_connection);

        if (!libraryRepository.RomExists(romId))
        {
            throw new ArgumentException($"No rom with id {romId} exists.");
        }

        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO game_play_history (rom_id, launched_at)
            VALUES ($romId, $launchedAt)";
        command.Parameters.AddWithValue("$romId", romId);
        command.Parameters.AddWithValue("$launchedAt", DateTime.UtcNow);

        command.ExecuteNonQuery();
    }

    /// <summary>Marks the rom's most recent open play session as exited. No-op if there is no open session.</summary>
    public void FinishPlayHistory(long romId)
    {
        if (romId <= 0)
        {
            throw new ArgumentException($"Invalid rom id {romId}.");
        }

        LibraryRepository libraryRepository = new LibraryRepository(_connection);

        if (!libraryRepository.RomExists(romId))
        {
            throw new ArgumentException($"No rom with id {romId} exists.");
        }

        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = @"
            UPDATE game_play_history
            SET exited_at = $finishedAt
            WHERE rom_id = $romId AND exited_at IS NULL";
        command.Parameters.AddWithValue("$romId", romId);
        command.Parameters.AddWithValue("$finishedAt", DateTime.UtcNow);

        command.ExecuteNonQuery();
    }

    /// <summary>All play history entries for a rom, most recently launched first.</summary>
    public IReadOnlyList<PlayHistoryEntry> GetPlayHistory(long romId)
    {
        if (romId <= 0)
        {
            throw new ArgumentException($"Invalid rom id {romId}.");
        }

        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT id, rom_id, launched_at, exited_at, exit_code
            FROM game_play_history
            WHERE rom_id = $romId
            ORDER BY launched_at DESC";
        command.Parameters.AddWithValue("$romId", romId);

        List<PlayHistoryEntry> entries = new List<PlayHistoryEntry>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries.Add(new PlayHistoryEntry
            {
                Id = reader.GetInt64(0),
                RomId = reader.GetInt64(1),
                LaunchedAt = reader.GetDateTime(2),
                ExitedAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                ExitCode = reader.IsDBNull(4) ? null : reader.GetInt32(4)
            });
        }

        return entries;
    }
}
