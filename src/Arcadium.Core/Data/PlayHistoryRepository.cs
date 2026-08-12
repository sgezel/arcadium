using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Data;

public class PlayHistoryRepository
{
    private readonly SqliteConnection _connection;

    public PlayHistoryRepository(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    public void AddPlayHistory(int romId)
    {
        if (romId <= 0)
        {
            throw new ArgumentException($"Invalid rom id {romId}.");
        }

        LibraryRepository libraryRepository = new LibraryRepository(_connection);

        if (libraryRepository.GetLibraryRom(romId) is null)
        {
            throw new ArgumentException($"No rom with id {romId} exists.");
        }

        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO game_play_history (rom_id, played_at)
            VALUES ($romId, $playedAt)";
        command.Parameters.AddWithValue("$romId", romId);
        command.Parameters.AddWithValue("$playedAt", DateTime.UtcNow);

        command.ExecuteNonQuery();
    }

    public void FinishPlayHistory(int romId)
    {
        if (romId <= 0)
        {
            throw new ArgumentException($"Invalid rom id {romId}.");
        }

        LibraryRepository libraryRepository = new LibraryRepository(_connection);

        if (libraryRepository.GetLibraryRom(romId) is null)
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
}
