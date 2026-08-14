using System.Text;
using Arcadium.Core.Data;
using Arcadium.Core.Mame;
using Arcadium.Core.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Arcadium.Core.Tests.Mame;

public sealed class MameXmlImporterTests : IDisposable
{
    private const string TwoMachineXml = """
        <?xml version="1.0"?>
        <mame build="0.260 (mame0260)">
          <machine name="pacman" sourcefile="pacman.cpp">
            <description>Pac-Man (Midway)</description>
            <year>1980</year>
            <manufacturer>Midway</manufacturer>
            <rom name="pacman.6e" size="4096" crc="c1e6ab10" region="maincpu"/>
            <driver status="good" emulation="good" savestate="supported"/>
            <input players="2" coins="2">
              <control type="joy4way"/>
            </input>
            <display type="raster" rotate="90" width="224" height="288" refresh="60.606060"/>
            <sound channels="1"/>
          </machine>
          <machine name="mspacman" cloneof="pacman" romof="pacman">
            <description>Ms. Pac-Man</description>
            <year>1981</year>
            <manufacturer>Midway (Bally license)</manufacturer>
            <rom name="boot1" size="2048" crc="5e77ec6b" region="maincpu"/>
          </machine>
        </mame>
        """;

    private const string OneMachineXml = """
        <?xml version="1.0"?>
        <mame build="0.260 (mame0260)">
          <machine name="galaga">
            <description>Galaga</description>
          </machine>
        </mame>
        """;

    private readonly SqliteConnection _connection;
    private readonly MameRepository _repository;
    private readonly MameXmlImporter _importer;

    public MameXmlImporterTests()
    {
        _connection = TestDatabase.CreateMigrated();
        _repository = new MameRepository(_connection);
        _importer = new MameXmlImporter(_repository);
    }

    [Fact]
    public void Import_WithTwoMachineXml_MachinesAreRetrievableAfterImport()
    {
        using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(TwoMachineXml));

        MameImportResult result = _importer.Import(stream, progress: null, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.MachineCount);

        MameMachine? pacman = _repository.GetMachineByName("pacman");
        Assert.NotNull(pacman);
        Assert.Equal("Pac-Man (Midway)", pacman.Description);
        MameRomDump rom = Assert.Single(pacman.Roms);
        Assert.Equal("pacman.6e", rom.Name);
        MameControl control = Assert.Single(pacman.Controls);
        Assert.Equal("joy4way", control.Type);

        MameMachine? clone = _repository.GetMachineByName("mspacman");
        Assert.NotNull(clone);
        Assert.Equal("pacman", clone.CloneOf);
    }

    [Fact]
    public void Import_CalledAgainWithDifferentXml_ReplacesThePreviousImport()
    {
        using (MemoryStream first = new MemoryStream(Encoding.UTF8.GetBytes(TwoMachineXml)))
        {
            _importer.Import(first, progress: null, TestContext.Current.CancellationToken);
        }

        using (MemoryStream second = new MemoryStream(Encoding.UTF8.GetBytes(OneMachineXml)))
        {
            _importer.Import(second, progress: null, TestContext.Current.CancellationToken);
        }

        Assert.Null(_repository.GetMachineByName("pacman"));
        Assert.NotNull(_repository.GetMachineByName("galaga"));
    }

    public void Dispose()
    {
        _repository.Dispose();
        _connection.Dispose();
    }
}
