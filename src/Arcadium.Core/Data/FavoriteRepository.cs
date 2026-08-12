using Arcadium.Core.Models;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Data;

public class FavoriteRepository
{
    private readonly SqliteConnection _connection;

    public FavoriteRepository(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    public void AddFavorite(int romId)
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
            INSERT OR IGNORE INTO favorites (rom_id)
            VALUES ($romId)";
        command.Parameters.AddWithValue("$romId", romId);

        command.ExecuteNonQuery();
    }

    public void AddFavorite(RomRecord rom)
    {
        ArgumentNullException.ThrowIfNull(rom);

        int romId = Convert.ToInt32(rom.Id);

        if (romId <= 0)
        {
            throw new ArgumentException($"Invalid rom id {romId}.");
        }

        AddFavorite(romId);
    }

    public void RemoveFavorite(int romId)
    {
        if (romId <= 0)
        {
            throw new ArgumentException($"Invalid rom id {romId}.");
        }

        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM favorites
            WHERE rom_id = $romId";
        command.Parameters.AddWithValue("$romId", romId);

        command.ExecuteNonQuery();
    }

    public void RemoveFavorite(RomRecord rom)
    {
        ArgumentNullException.ThrowIfNull(rom);

        int romId = Convert.ToInt32(rom.Id);

        if (romId <= 0)
        {
            throw new ArgumentException($"Invalid rom id {romId}.");
        }

        RemoveFavorite(romId);
    }
}
