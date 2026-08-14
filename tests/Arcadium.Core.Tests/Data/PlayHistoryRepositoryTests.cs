using Arcadium.Core.Data;
using Arcadium.Core.Models;
using Arcadium.Core.Tests.Support;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Arcadium.Core.Tests.Data;

public sealed class PlayHistoryRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ScanRepository _scanRepository;
    private readonly PlayHistoryRepository _playHistoryRepository;

    public PlayHistoryRepositoryTests()
    {
        _connection = TestDatabase.CreateMigrated();
        _scanRepository = new ScanRepository(_connection);
        _playHistoryRepository = new PlayHistoryRepository(_connection);
    }

    [Fact]
    public void GetPlayHistory_OnFreshlyMigratedDatabase_ReturnsEmptyList()
    {
        long romId = InsertRom("pacman.zip", "pacman");

        IReadOnlyList<PlayHistoryEntry> entries = _playHistoryRepository.GetPlayHistory(romId);

        Assert.Empty(entries);
    }

    [Fact]
    public void GetPlayHistory_OnUnmigratedDatabase_ThrowsBecauseTheTableDoesNotExist()
    {
        using SqliteConnection unmigrated = TestDatabase.CreateEmpty();
        PlayHistoryRepository repository = new PlayHistoryRepository(unmigrated);

        Assert.Throws<SqliteException>(() => repository.GetPlayHistory(1));
    }

    [Fact]
    public void AddPlayHistory_ThenGetPlayHistory_RecordsAnOpenSession()
    {
        long romId = InsertRom("pacman.zip", "pacman");

        _playHistoryRepository.AddPlayHistory(romId);

        PlayHistoryEntry entry = Assert.Single(_playHistoryRepository.GetPlayHistory(romId));
        Assert.Equal(romId, entry.RomId);
        Assert.Null(entry.ExitedAt);
    }

    [Fact]
    public void AddPlayHistory_WithUnknownRomId_Throws()
    {
        Assert.Throws<ArgumentException>(() => _playHistoryRepository.AddPlayHistory(999));
    }

    [Fact]
    public void FinishPlayHistory_ClosesTheOpenSession()
    {
        long romId = InsertRom("pacman.zip", "pacman");
        _playHistoryRepository.AddPlayHistory(romId);

        _playHistoryRepository.FinishPlayHistory(romId);

        PlayHistoryEntry entry = Assert.Single(_playHistoryRepository.GetPlayHistory(romId));
        Assert.NotNull(entry.ExitedAt);
    }

    [Fact]
    public void FinishPlayHistory_WithNoOpenSession_IsANoOp()
    {
        long romId = InsertRom("pacman.zip", "pacman");

        _playHistoryRepository.FinishPlayHistory(romId);

        Assert.Empty(_playHistoryRepository.GetPlayHistory(romId));
    }

    [Fact]
    public void FinishPlayHistory_WithMultipleOpenSessions_OnlyClosesTheMostRecentOne()
    {
        long romId = InsertRom("pacman.zip", "pacman");
        _playHistoryRepository.AddPlayHistory(romId);
        _playHistoryRepository.AddPlayHistory(romId);

        _playHistoryRepository.FinishPlayHistory(romId);

        IReadOnlyList<PlayHistoryEntry> entries = _playHistoryRepository.GetPlayHistory(romId);
        Assert.Equal(2, entries.Count);
        Assert.NotNull(entries[0].ExitedAt);
        Assert.Null(entries[1].ExitedAt);
    }

    private long InsertRom(string filename, string basename)
    {
        DateTime timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        RomRecord rom = new RomRecord
        {
            SystemId = "arcade",
            Filename = filename,
            Basename = basename,
            Path = $"/roms/arcade/{filename}",
            SizeBytes = 4096,
            ModifiedTimeUtc = timestamp,
            ScanState = "active",
            FirstSeenAt = timestamp,
            LastSeenAt = timestamp,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };

        _scanRepository.BeginTransaction();
        _scanRepository.UpsertRom(rom);
        _scanRepository.Commit();

        return _scanRepository.GetRomsBySystem("arcade")[rom.Path].Id;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
