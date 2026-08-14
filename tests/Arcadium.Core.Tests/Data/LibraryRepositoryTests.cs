using Arcadium.Core.Data;
using Arcadium.Core.Mame;
using Arcadium.Core.Models;
using Arcadium.Core.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Tests.Data;

public sealed class LibraryRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ScanRepository _scanRepository;
    private readonly LibraryRepository _libraryRepository;

    public LibraryRepositoryTests()
    {
        _connection = TestDatabase.CreateMigrated();
        _scanRepository = new ScanRepository(_connection);
        _libraryRepository = new LibraryRepository(_connection);
    }

    [Fact]
    public void GetLibraryRom_WithMatchingMameMachine_ReturnsPopulatedMachine()
    {
        InsertRom("pacman.zip", "pacman");
        InsertMachine("pacman", "Pac-Man (Midway)");

        LibraryRom? libraryRom = _libraryRepository.GetLibraryRom("arcade", "pacman");

        Assert.NotNull(libraryRom);
        Assert.NotNull(libraryRom.Machine);
        Assert.Equal("Pac-Man (Midway)", libraryRom.Machine.Description);
    }

    [Fact]
    public void GetLibraryRom_WithoutMatchingMameMachine_ReturnsNullMachine()
    {
        InsertRom("homebrew.zip", "homebrew");

        LibraryRom? libraryRom = _libraryRepository.GetLibraryRom("arcade", "homebrew");

        Assert.NotNull(libraryRom);
        Assert.Null(libraryRom.Machine);
    }

    [Fact]
    public void RomExists_ReturnsTrueForSeededRomAndFalseForUnknownId()
    {
        long romId = InsertRom("pacman.zip", "pacman");

        Assert.True(_libraryRepository.RomExists(romId));
        Assert.False(_libraryRepository.RomExists(romId + 1000));
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

    private void InsertMachine(string name, string description)
    {
        using MameRepository mameRepository = new MameRepository(_connection);
        mameRepository.BeginTransaction();
        mameRepository.InsertMachine(new MameMachine
        {
            Name = name,
            Description = description
        });
        mameRepository.Commit();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
