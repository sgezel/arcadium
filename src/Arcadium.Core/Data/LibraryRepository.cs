using Arcadium.Core.Mame;
using Arcadium.Core.Models;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Data;

/// <summary>
/// Read-only lookups over the rom library for frontends. The caller owns the connection,
/// typically opened via <see cref="SqliteDatabase.OpenReadOnly"/>.
/// </summary>
public sealed class LibraryRepository
{
    private readonly SqliteConnection _connection;

    public LibraryRepository(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    /// <summary>
    /// Loads one active rom by basename, combined with its MAME machine metadata.
    /// <see cref="LibraryRom.Machine"/> is null for non-MAME systems or unmatched roms.
    /// Returns null when the system has no active rom with this basename.
    /// </summary>
    public LibraryRom? GetLibraryRom(string systemId, string baseName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);

        RomRecord? rom = ReadRom(systemId, baseName);
        if (rom is null)
        {
            return null;
        }

        using MameRepository mameRepository = new MameRepository(_connection);
        MameMachine? machine = mameRepository.GetMachineByName(rom.Basename);

        return new LibraryRom
        {
            Rom = rom,
            Machine = machine
        };
    }

    public LibraryRom? GetLibraryRom(long romId)
    {
        if (romId <= 0)
        {
            throw new ArgumentException($"Invalid rom id {romId}.");
        }

        RomRecord? rom = ReadRom(romId);
        if (rom is null)
        {
            return null;
        }

        using MameRepository mameRepository = new MameRepository(_connection);
        MameMachine? machine = mameRepository.GetMachineByName(rom.Basename);

        return new LibraryRom
        {
            Rom = rom,
            Machine = machine
        };
    }

    /// <summary>True if an active rom with this id exists. Cheaper than <see cref="GetLibraryRom(long)"/>
    /// for callers that only need to validate a rom id, since it skips the MAME join.</summary>
    public bool RomExists(long romId)
    {
        if (romId <= 0)
        {
            throw new ArgumentException($"Invalid rom id {romId}.");
        }

        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM roms WHERE id = $romId AND scan_state = 'active');";
        command.Parameters.AddWithValue("$romId", romId);

        return (long)command.ExecuteScalar()! == 1;
    }

    private RomRecord? ReadRom(long romId)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT id, system_id, filename, basename, path,
                   wheel_path, video_path, marquee_path, physical_path, game_image_path,
                   size_bytes, modified_time_utc, scan_state,
                   first_seen_at, last_seen_at, created_at, updated_at
            FROM roms
            WHERE id = $romId
              AND scan_state = 'active'
            LIMIT 1;
        ";
        command.Parameters.AddWithValue("$romId", romId);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new RomRecord
        {
            Id = reader.GetInt64(0),
            SystemId = reader.GetString(1),
            Filename = reader.GetString(2),
            Basename = reader.GetString(3),
            Path = reader.GetString(4),
            WheelPath = reader.IsDBNull(5) ? null : reader.GetString(5),
            VideoPath = reader.IsDBNull(6) ? null : reader.GetString(6),
            MarqueePath = reader.IsDBNull(7) ? null : reader.GetString(7),
            PhysicalPath = reader.IsDBNull(8) ? null : reader.GetString(8),
            GameImagePath = reader.IsDBNull(9) ? null : reader.GetString(9),
            SizeBytes = reader.GetInt64(10),
            ModifiedTimeUtc = reader.GetDateTime(11),
            ScanState = reader.GetString(12),
            FirstSeenAt = reader.GetDateTime(13),
            LastSeenAt = reader.GetDateTime(14),
            CreatedAt = reader.GetDateTime(15),
            UpdatedAt = reader.GetDateTime(16)
        };
    }

    private RomRecord? ReadRom(string systemId, string baseName)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = @"
            SELECT id, system_id, filename, basename, path,
                   wheel_path, video_path, marquee_path, physical_path, game_image_path,
                   size_bytes, modified_time_utc, scan_state,
                   first_seen_at, last_seen_at, created_at, updated_at
            FROM roms
            WHERE system_id = $systemId
              AND basename = $baseName
              AND scan_state = 'active'
            ORDER BY path
            LIMIT 1;
        ";
        command.Parameters.AddWithValue("$systemId", systemId);
        command.Parameters.AddWithValue("$baseName", baseName);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new RomRecord
        {
            Id = reader.GetInt64(0),
            SystemId = reader.GetString(1),
            Filename = reader.GetString(2),
            Basename = reader.GetString(3),
            Path = reader.GetString(4),
            WheelPath = reader.IsDBNull(5) ? null : reader.GetString(5),
            VideoPath = reader.IsDBNull(6) ? null : reader.GetString(6),
            MarqueePath = reader.IsDBNull(7) ? null : reader.GetString(7),
            PhysicalPath = reader.IsDBNull(8) ? null : reader.GetString(8),
            GameImagePath = reader.IsDBNull(9) ? null : reader.GetString(9),
            SizeBytes = reader.GetInt64(10),
            ModifiedTimeUtc = reader.GetDateTime(11),
            ScanState = reader.GetString(12),
            FirstSeenAt = reader.GetDateTime(13),
            LastSeenAt = reader.GetDateTime(14),
            CreatedAt = reader.GetDateTime(15),
            UpdatedAt = reader.GetDateTime(16)
        };
    }
}
