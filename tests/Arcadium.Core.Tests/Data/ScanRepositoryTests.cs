using Arcadium.Core.Data;
using Arcadium.Core.Models;
using Arcadium.Core.Scanning;
using Arcadium.Core.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Tests.Data;

public sealed class ScanRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ScanRepository _repository;

    public ScanRepositoryTests()
    {
        _connection = TestDatabase.CreateMigrated();
        _repository = new ScanRepository(_connection);
    }

    [Fact]
    public void UpsertRom_ThenGetRomsBySystem_RoundTripsTheRom()
    {
        DateTime timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        RomRecord rom = NewRom("arcade", "/roms/arcade/pacman.zip", "pacman.zip", "pacman", 4096, timestamp);

        _repository.BeginTransaction();
        _repository.UpsertRom(rom);
        _repository.Commit();

        Dictionary<string, RomRecord> roms = _repository.GetRomsBySystem("arcade");
        RomRecord stored = Assert.Single(roms.Values);

        Assert.Equal(rom.Path, stored.Path);
        Assert.Equal(rom.SizeBytes, stored.SizeBytes);
        Assert.Equal(rom.ModifiedTimeUtc, stored.ModifiedTimeUtc);
        Assert.Equal("active", stored.ScanState);
    }

    [Fact]
    public void MarkUnseenAsDeleted_OnlyFlipsRomsNotTouchedSinceScanStarted()
    {
        DateTime before = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime scanStart = before.AddMinutes(1);

        RomRecord touched = NewRom("arcade", "/roms/arcade/pacman.zip", "pacman.zip", "pacman", 4096, before);
        RomRecord untouched = NewRom("arcade", "/roms/arcade/galaga.zip", "galaga.zip", "galaga", 8192, before);

        _repository.BeginTransaction();
        _repository.UpsertRom(touched);
        _repository.UpsertRom(untouched);
        _repository.Commit();

        long touchedId = _repository.GetRomsBySystem("arcade")[touched.Path].Id;
        _repository.TouchRom(touchedId, scanStart.AddSeconds(1));

        int deletedCount = _repository.MarkUnseenAsDeleted("arcade", scanStart);

        Dictionary<string, RomRecord> roms = _repository.GetRomsBySystem("arcade");
        Assert.Equal(1, deletedCount);
        Assert.Equal("active", roms[touched.Path].ScanState);
        Assert.Equal("deleted", roms[untouched.Path].ScanState);
    }

    [Fact]
    public void BeginScanRun_ThenCompleteScanRun_PersistsRunMetadata()
    {
        DateTime startedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime finishedAt = startedAt.AddSeconds(5);
        SystemScanStats stats = new SystemScanStats(10, 3, 2, 5, 0, 1, 0);

        long scanRunId = _repository.BeginScanRun(ScanMode.Update, "arcade", startedAt);
        _repository.CompleteScanRun(scanRunId, stats, finishedAt);

        (string mode, int filesSeen, int filesAdded) = ReadScanRun(scanRunId);

        Assert.Equal("update", mode);
        Assert.Equal(10, filesSeen);
        Assert.Equal(3, filesAdded);
    }

    private (string Mode, int FilesSeen, int FilesAdded) ReadScanRun(long scanRunId)
    {
        using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT mode, files_seen, files_added FROM scan_runs WHERE id = $id;";
        command.Parameters.AddWithValue("$id", scanRunId);

        using SqliteDataReader reader = command.ExecuteReader();
        reader.Read();

        return (reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2));
    }

    private static RomRecord NewRom(string systemId, string path, string filename, string basename, long sizeBytes, DateTime timestamp)
    {
        return new RomRecord
        {
            SystemId = systemId,
            Filename = filename,
            Basename = basename,
            Path = path,
            SizeBytes = sizeBytes,
            ModifiedTimeUtc = timestamp,
            ScanState = "active",
            FirstSeenAt = timestamp,
            LastSeenAt = timestamp,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
