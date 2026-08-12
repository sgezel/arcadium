using Arcadium.Core.Models;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Data;

/// <summary>
/// Data access for the favorites table: marking/unmarking roms as favorite and querying
/// favorite membership. The caller owns the connection, which must be opened via
/// <see cref="SqliteDatabase.OpenReadWrite"/> since this repository writes.
/// </summary>
public sealed class FavoriteRepository
{
    private readonly SqliteConnection _connection;

    public FavoriteRepository(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    /// <summary>Marks a rom as favorite. No-op if already favorited. Throws if no rom with this id exists.</summary>
    public void AddFavorite(long romId)
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
            INSERT OR IGNORE INTO favorites (rom_id)
            VALUES ($romId)";
        command.Parameters.AddWithValue("$romId", romId);

        command.ExecuteNonQuery();
    }

    /// <summary>Marks a rom as favorite. See <see cref="AddFavorite(long)"/>.</summary>
    public void AddFavorite(RomRecord rom)
    {
        ArgumentNullException.ThrowIfNull(rom);

        AddFavorite(rom.Id);
    }

    /// <summary>Unmarks a rom as favorite. No-op if it was not favorited.</summary>
    public void RemoveFavorite(long romId)
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

    /// <summary>Unmarks a rom as favorite. See <see cref="RemoveFavorite(long)"/>.</summary>
    public void RemoveFavorite(RomRecord rom)
    {
        ArgumentNullException.ThrowIfNull(rom);

        RemoveFavorite(rom.Id);
    }

    /// <summary>True if the rom is currently marked as favorite.</summary>
    public bool IsFavorite(long romId)
    {
        if (romId <= 0)
        {
            throw new ArgumentException($"Invalid rom id {romId}.");
        }

        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM favorites WHERE rom_id = $romId);";
        command.Parameters.AddWithValue("$romId", romId);

        return (long)command.ExecuteScalar()! == 1;
    }

    /// <summary>All favorited rom IDs, most recently favorited first.</summary>
    public IReadOnlyList<long> GetFavoriteRomIds()
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT rom_id FROM favorites ORDER BY created_at DESC;";

        List<long> romIds = new List<long>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            romIds.Add(reader.GetInt64(0));
        }

        return romIds;
    }
}
