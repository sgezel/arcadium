using Arcadium.Core.Models;
using Arcadium.Core.Scanning;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Data;

/// <summary>
/// Data access for the scanner: rom upserts and scan_runs bookkeeping.
/// The caller owns the connection; use <see cref="BeginTransaction"/> / <see cref="Commit"/> /
/// <see cref="Rollback"/> to run one transaction per system (or roll back for --dry-run).
/// </summary>
public sealed class ScanRepository
{
    private readonly SqliteConnection _connection;
    private SqliteTransaction? _transaction;

    public bool HasActiveTransaction => _transaction is not null;

    public ScanRepository(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _connection = connection;
    }

    public void BeginTransaction()
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already active.");
        }

        _transaction = _connection.BeginTransaction();
    }

    public void Commit()
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No active transaction to commit.");
        }

        _transaction.Commit();
        _transaction.Dispose();
        _transaction = null;
    }

    public void Rollback()
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No active transaction to roll back.");
        }

        _transaction.Rollback();
        _transaction.Dispose();
        _transaction = null;
    }

    public long BeginScanRun(ScanMode mode, string? systemId, DateTime startedAtUtc)
    {
        using var command = CreateCommand();
        command.CommandText = @"
            INSERT INTO scan_runs (system_id, mode, started_at)
            VALUES ($systemId, $mode, $startedAt);
            SELECT last_insert_rowid();
        ";
        command.Parameters.AddWithValue("$systemId", (object?)systemId ?? DBNull.Value);
        command.Parameters.AddWithValue("$mode", ModeName(mode));
        command.Parameters.AddWithValue("$startedAt", startedAtUtc);

        return (long)command.ExecuteScalar()!;
    }

    public void CompleteScanRun(long scanRunId, SystemScanStats totals, DateTime finishedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(totals);

        using var command = CreateCommand();
        command.CommandText = @"
            UPDATE scan_runs
            SET finished_at = $finishedAt,
                files_seen = $filesSeen,
                files_added = $filesAdded,
                files_updated = $filesUpdated,
                files_deleted = $filesDeleted,
                warnings_count = $warningsCount,
                errors_count = $errorsCount
            WHERE id = $id;
        ";
        command.Parameters.AddWithValue("$id", scanRunId);
        command.Parameters.AddWithValue("$finishedAt", finishedAtUtc);
        command.Parameters.AddWithValue("$filesSeen", totals.FilesSeen);
        command.Parameters.AddWithValue("$filesAdded", totals.Added);
        command.Parameters.AddWithValue("$filesUpdated", totals.Updated);
        command.Parameters.AddWithValue("$filesDeleted", totals.Deleted);
        command.Parameters.AddWithValue("$warningsCount", totals.Warnings);
        command.Parameters.AddWithValue("$errorsCount", totals.Errors);
        command.ExecuteNonQuery();
    }

    /// <summary>Loads all roms of a system (any scan_state), keyed by path, for change detection.</summary>
    public Dictionary<string, RomRecord> GetRomsBySystem(string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        using var command = CreateCommand();
        command.CommandText = @"
            SELECT id, system_id, filename, basename, path,
                   wheel_path, video_path, marquee_path, physical_path, game_image_path,
                   size_bytes, modified_time_utc, scan_state,
                   first_seen_at, last_seen_at, created_at, updated_at
            FROM roms
            WHERE system_id = $systemId;
        ";
        command.Parameters.AddWithValue("$systemId", systemId);

        var roms = new Dictionary<string, RomRecord>(StringComparer.Ordinal);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var rom = new RomRecord
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

            roms[rom.Path] = rom;
        }

        return roms;
    }

    /// <summary>Inserts a new rom or updates the existing row with the same (system_id, path).</summary>
    public void UpsertRom(RomRecord rom)
    {
        ArgumentNullException.ThrowIfNull(rom);

        using var command = CreateCommand();
        command.CommandText = @"
            INSERT INTO roms (
                system_id, filename, basename, path,
                wheel_path, video_path, marquee_path, physical_path, game_image_path,
                size_bytes, modified_time_utc, scan_state,
                first_seen_at, last_seen_at, created_at, updated_at)
            VALUES (
                $systemId, $filename, $basename, $path,
                $wheelPath, $videoPath, $marqueePath, $physicalPath, $gameImagePath,
                $sizeBytes, $modifiedTimeUtc, $scanState,
                $firstSeenAt, $lastSeenAt, $createdAt, $updatedAt)
            ON CONFLICT(system_id, path) DO UPDATE SET
                filename = excluded.filename,
                basename = excluded.basename,
                wheel_path = excluded.wheel_path,
                video_path = excluded.video_path,
                marquee_path = excluded.marquee_path,
                physical_path = excluded.physical_path,
                game_image_path = excluded.game_image_path,
                size_bytes = excluded.size_bytes,
                modified_time_utc = excluded.modified_time_utc,
                scan_state = excluded.scan_state,
                last_seen_at = excluded.last_seen_at,
                updated_at = excluded.updated_at;
        ";
        command.Parameters.AddWithValue("$systemId", rom.SystemId);
        command.Parameters.AddWithValue("$filename", rom.Filename);
        command.Parameters.AddWithValue("$basename", rom.Basename);
        command.Parameters.AddWithValue("$path", rom.Path);
        command.Parameters.AddWithValue("$wheelPath", (object?)rom.WheelPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$videoPath", (object?)rom.VideoPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$marqueePath", (object?)rom.MarqueePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$physicalPath", (object?)rom.PhysicalPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$gameImagePath", (object?)rom.GameImagePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$sizeBytes", rom.SizeBytes);
        command.Parameters.AddWithValue("$modifiedTimeUtc", rom.ModifiedTimeUtc);
        command.Parameters.AddWithValue("$scanState", rom.ScanState);
        command.Parameters.AddWithValue("$firstSeenAt", rom.FirstSeenAt);
        command.Parameters.AddWithValue("$lastSeenAt", rom.LastSeenAt);
        command.Parameters.AddWithValue("$createdAt", rom.CreatedAt);
        command.Parameters.AddWithValue("$updatedAt", rom.UpdatedAt);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Bumps last_seen_at of an unchanged rom so <see cref="MarkUnseenAsDeleted"/> will not touch it.
    /// </summary>
    public void TouchRom(long romId, DateTime lastSeenAtUtc)
    {
        using var command = CreateCommand();
        command.CommandText = "UPDATE roms SET last_seen_at = $lastSeenAt WHERE id = $id;";
        command.Parameters.AddWithValue("$id", romId);
        command.Parameters.AddWithValue("$lastSeenAt", lastSeenAtUtc);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Marks all active roms of a system that were not seen during this scan as deleted.
    /// Returns the number of roms that were marked.
    /// </summary>
    public int MarkUnseenAsDeleted(string systemId, DateTime scanStartedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        using var command = CreateCommand();
        command.CommandText = @"
            UPDATE roms
            SET scan_state = 'deleted',
                updated_at = $now
            WHERE system_id = $systemId
              AND scan_state <> 'deleted'
              AND last_seen_at < $scanStartedAt;
        ";
        command.Parameters.AddWithValue("$systemId", systemId);
        command.Parameters.AddWithValue("$scanStartedAt", scanStartedAtUtc);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow);

        return command.ExecuteNonQuery();
    }

    private SqliteCommand CreateCommand()
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;

        return command;
    }

    private static string ModeName(ScanMode mode) => mode switch
    {
        ScanMode.Initialize => "initialize",
        ScanMode.Update => "update",
        ScanMode.Verify => "verify",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown scan mode.")
    };
}
