using Arcadium.Core.Data;
using Arcadium.Core.Models;
using Arcadium.Core.Tests.Support;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Arcadium.Core.Tests.Data;

public sealed class FavoriteRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ScanRepository _scanRepository;
    private readonly FavoriteRepository _favoriteRepository;

    public FavoriteRepositoryTests()
    {
        _connection = TestDatabase.CreateMigrated();
        _scanRepository = new ScanRepository(_connection);
        _favoriteRepository = new FavoriteRepository(_connection);
    }

    [Fact]
    public void GetFavoriteRomIds_OnFreshlyMigratedDatabase_ReturnsEmptyList()
    {
        IReadOnlyList<long> favoriteRomIds = _favoriteRepository.GetFavoriteRomIds();

        Assert.Empty(favoriteRomIds);
    }

    [Fact]
    public void IsFavorite_OnFreshlyMigratedDatabase_ReturnsFalse()
    {
        Assert.False(_favoriteRepository.IsFavorite(1));
    }

    [Fact]
    public void GetFavoriteRomIds_OnUnmigratedDatabase_ThrowsBecauseTheTableDoesNotExist()
    {
        using SqliteConnection unmigrated = TestDatabase.CreateEmpty();
        FavoriteRepository repository = new FavoriteRepository(unmigrated);

        Assert.Throws<SqliteException>(() => repository.GetFavoriteRomIds());
    }

    [Fact]
    public void AddFavorite_ThenIsFavorite_ReturnsTrue()
    {
        long romId = InsertRom("pacman.zip", "pacman");

        _favoriteRepository.AddFavorite(romId);

        Assert.True(_favoriteRepository.IsFavorite(romId));
        Assert.Equal(romId, Assert.Single(_favoriteRepository.GetFavoriteRomIds()));
    }

    [Fact]
    public void AddFavorite_CalledTwiceForSameRom_IsANoOp()
    {
        long romId = InsertRom("pacman.zip", "pacman");

        _favoriteRepository.AddFavorite(romId);
        _favoriteRepository.AddFavorite(romId);

        Assert.Equal(romId, Assert.Single(_favoriteRepository.GetFavoriteRomIds()));
    }

    [Fact]
    public void AddFavorite_WithUnknownRomId_Throws()
    {
        Assert.Throws<ArgumentException>(() => _favoriteRepository.AddFavorite(999));
    }

    [Fact]
    public void RemoveFavorite_UnmarksRom()
    {
        long romId = InsertRom("pacman.zip", "pacman");
        _favoriteRepository.AddFavorite(romId);

        _favoriteRepository.RemoveFavorite(romId);

        Assert.False(_favoriteRepository.IsFavorite(romId));
    }

    [Fact]
    public void RemoveFavorite_WhenNotFavorited_IsANoOp()
    {
        long romId = InsertRom("pacman.zip", "pacman");

        _favoriteRepository.RemoveFavorite(romId);

        Assert.False(_favoriteRepository.IsFavorite(romId));
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
