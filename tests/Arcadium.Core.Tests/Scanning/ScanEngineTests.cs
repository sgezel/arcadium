using Arcadium.Core.Configuration;
using Arcadium.Core.Data;
using Arcadium.Core.Models;
using Arcadium.Core.Scanning;
using Arcadium.Core.Tests.Support;
using Microsoft.Data.Sqlite;
using GameSystem = Arcadium.Core.Models.System;

namespace Arcadium.Core.Tests.Scanning;

public sealed class ScanEngineTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ScanRepository _repository;
    private readonly ScanEngine _engine;
    private readonly TempDirectory _romDirectory;

    public ScanEngineTests()
    {
        _connection = TestDatabase.CreateMigrated();
        _repository = new ScanRepository(_connection);
        _engine = new ScanEngine(_repository);
        _romDirectory = new TempDirectory();
    }

    [Fact]
    public void Run_WithNewRomFile_RecordsItAsAdded()
    {
        TestRomFiles.Create(_romDirectory.Path, "pacman.zip", 4096, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        ScanSummary summary = _engine.Run(NewConfiguration(), NewOptions(), progress: null, TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.Totals.Added);
        RomRecord rom = Assert.Single(_repository.GetRomsBySystem("arcade").Values);
        Assert.Equal("active", rom.ScanState);
    }

    [Fact]
    public void Run_WhenAKnownFileChangesSizeAndMtime_RecordsItAsUpdated()
    {
        string path = TestRomFiles.Create(_romDirectory.Path, "pacman.zip", 4096, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _engine.Run(NewConfiguration(), NewOptions(), progress: null, TestContext.Current.CancellationToken);

        TestRomFiles.Create(_romDirectory.Path, "pacman.zip", 8192, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        ScanSummary summary = _engine.Run(NewConfiguration(), NewOptions(), progress: null, TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.Totals.Updated);
        Assert.Equal(8192, _repository.GetRomsBySystem("arcade")[path].SizeBytes);
    }

    [Fact]
    public void Run_WhenAKnownFileIsRemovedFromDisk_MarksItDeleted()
    {
        string path = TestRomFiles.Create(_romDirectory.Path, "pacman.zip", 4096, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _engine.Run(NewConfiguration(), NewOptions(), progress: null, TestContext.Current.CancellationToken);

        File.Delete(path);
        ScanSummary summary = _engine.Run(NewConfiguration(), NewOptions(), progress: null, TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.Totals.Deleted);
        Assert.Equal("deleted", _repository.GetRomsBySystem("arcade")[path].ScanState);
    }

    private ArcadiumConfiguration NewConfiguration()
    {
        GameSystem system = new GameSystem
        {
            Id = "arcade",
            Name = "Arcade",
            RomPath = [_romDirectory.Path],
            Extensions = [".zip"],
            EmulatorProfile = "mame"
        };

        return new ArcadiumConfiguration
        {
            Cabinet = new Cabinet { Name = "Test Cabinet", Database = "arcadium.db" },
            Systems = [system],
            Emulators = []
        };
    }

    private static ScanOptions NewOptions()
    {
        return new ScanOptions { Mode = ScanMode.Update };
    }

    public void Dispose()
    {
        _connection.Dispose();
        _romDirectory.Dispose();
    }
}
