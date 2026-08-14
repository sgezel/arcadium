using Arcadium.Core.Data;
using Arcadium.Core.Mame;
using Arcadium.Core.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Tests.Data;

public sealed class MameRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MameRepository _repository;

    public MameRepositoryTests()
    {
        _connection = TestDatabase.CreateMigrated();
        _repository = new MameRepository(_connection);
    }

    [Fact]
    public void InsertMachine_ThenGetMachineByName_RoundTripsMachineWithChildren()
    {
        _repository.BeginTransaction();
        _repository.InsertMachine(NewMachine());
        _repository.Commit();

        MameMachine? loaded = _repository.GetMachineByName("pacman");

        Assert.NotNull(loaded);
        Assert.Equal("Pac-Man (Midway)", loaded.Description);
        Assert.Equal("1980", loaded.Year);
        MameRomDump rom = Assert.Single(loaded.Roms);
        Assert.Equal("pacman.6e", rom.Name);
        MameControl control = Assert.Single(loaded.Controls);
        Assert.Equal("joy4way", control.Type);
    }

    [Fact]
    public void ClearMachines_RemovesPreviouslyInsertedMachines()
    {
        _repository.BeginTransaction();
        _repository.InsertMachine(NewMachine());
        _repository.Commit();

        _repository.ClearMachines();

        Assert.Null(_repository.GetMachineByName("pacman"));
    }

    private static MameMachine NewMachine()
    {
        MameMachine machine = new MameMachine
        {
            Name = "pacman",
            Description = "Pac-Man (Midway)",
            Year = "1980",
            Manufacturer = "Midway"
        };
        machine.Roms.Add(new MameRomDump("pacman.6e", 4096, "c1e6ab10", null, null, "maincpu", "good", false));
        machine.Controls.Add(new MameControl("joy4way", 1, 2, null));

        return machine;
    }

    public void Dispose()
    {
        _repository.Dispose();
        _connection.Dispose();
    }
}
