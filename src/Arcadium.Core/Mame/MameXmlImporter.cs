using System.Diagnostics;
using System.Globalization;
using System.Xml;
using Arcadium.Core.Data;

namespace Arcadium.Core.Mame;

/// <summary>
/// Streams MAME -listxml output (300+ MB for a full set) into the mame_* tables with a
/// forward-only <see cref="XmlReader"/>; the document is never held in memory. The whole
/// import is one transaction: existing rows are replaced only when parsing completes, so a
/// truncated or invalid stream leaves the previous import intact.
/// </summary>
public sealed class MameXmlImporter
{
    private const int ProgressBatchSize = 1000;

    private readonly MameRepository _repository;

    public MameXmlImporter(MameRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        _repository = repository;
    }

    public MameImportResult Import(
        Stream xmlStream,
        IProgress<MameImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(xmlStream);

        var stopwatch = Stopwatch.StartNew();
        var settings = new XmlReaderSettings
        {
            // The listxml output starts with an internal DTD; skip it. Attribute defaults
            // from the DTD are therefore not applied and are handled while parsing.
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreWhitespace = true,
        };

        using var reader = XmlReader.Create(xmlStream, settings);

        string build = string.Empty;
        int machineCount = 0;
        int romCount = 0;
        int controlCount = 0;

        _repository.BeginTransaction();
        try
        {
            _repository.ClearMachines();

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (reader.LocalName == "mame")
                {
                    build = reader.GetAttribute("build") ?? string.Empty;
                    continue;
                }

                if (reader.LocalName != "machine")
                {
                    continue;
                }

                MameMachine machine = ReadMachine(reader);
                _repository.InsertMachine(machine);

                machineCount++;
                romCount += machine.Roms.Count;
                controlCount += machine.Controls.Count;

                if (machineCount % ProgressBatchSize == 0)
                {
                    progress?.Report(new MameImportProgress(machineCount, machine.Name));
                }
            }

            _repository.RecordImport(build, machineCount, DateTime.UtcNow);
            _repository.Commit();
        }
        catch
        {
            if (_repository.HasActiveTransaction)
            {
                _repository.Rollback();
            }

            throw;
        }

        progress?.Report(new MameImportProgress(machineCount, null));

        return new MameImportResult(build, machineCount, romCount, controlCount, stopwatch.Elapsed);
    }

    private static MameMachine ReadMachine(XmlReader outer)
    {
        var machine = new MameMachine
        {
            Name = outer.GetAttribute("name")
                ?? throw new InvalidDataException("Encountered a <machine> element without a name attribute."),
            SourceFile = outer.GetAttribute("sourcefile"),
            CloneOf = outer.GetAttribute("cloneof"),
            RomOf = outer.GetAttribute("romof"),
            SampleOf = outer.GetAttribute("sampleof"),
            IsBios = ParseYesNo(outer.GetAttribute("isbios")),
            IsDevice = ParseYesNo(outer.GetAttribute("isdevice")),
            IsMechanical = ParseYesNo(outer.GetAttribute("ismechanical")),
            Runnable = ParseYesNo(outer.GetAttribute("runnable"), defaultValue: true),
        };

        if (outer.IsEmptyElement)
        {
            return machine;
        }

        // ReadSubtree keeps the parse of one machine contained: when the subtree reader is
        // disposed, the outer reader sits on this machine's end tag and the main loop resumes.
        using XmlReader reader = outer.ReadSubtree();
        reader.Read(); // position on the <machine> element itself
        reader.Read(); // enter the first child node

        while (!reader.EOF)
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.Read();
                continue;
            }

            // Each case must advance the reader: ReadElementContentAsString and Skip move
            // past the current element; the bare Read on <input> descends into its children
            // so the <control> elements surface in this same loop.
            switch (reader.LocalName)
            {
                case "description":
                    machine.Description = reader.ReadElementContentAsString();
                    break;
                case "year":
                    machine.Year = reader.ReadElementContentAsString();
                    break;
                case "manufacturer":
                    machine.Manufacturer = reader.ReadElementContentAsString();
                    break;
                case "rom":
                    machine.Roms.Add(ReadRom(reader));
                    reader.Skip();
                    break;
                case "disk":
                    machine.RequiresChd = true;
                    reader.Skip();
                    break;
                case "driver":
                    machine.DriverStatus = reader.GetAttribute("status");
                    machine.DriverEmulation = reader.GetAttribute("emulation");
                    machine.DriverSavestate = reader.GetAttribute("savestate");
                    reader.Skip();
                    break;
                case "input":
                    machine.Players = ParseInt(reader.GetAttribute("players"));
                    machine.Coins = ParseInt(reader.GetAttribute("coins"));
                    reader.Read();
                    break;
                case "control":
                    machine.Controls.Add(ReadControl(reader));
                    reader.Skip();
                    break;
                case "display":
                    // Only the first display is stored; multi-screen machines are rare and
                    // the frontend only needs the primary screen's orientation and type.
                    if (machine.DisplayType is null)
                    {
                        machine.DisplayType = reader.GetAttribute("type");
                        machine.DisplayRotate = ParseInt(reader.GetAttribute("rotate"));
                        machine.DisplayWidth = ParseInt(reader.GetAttribute("width"));
                        machine.DisplayHeight = ParseInt(reader.GetAttribute("height"));
                        machine.DisplayRefresh = ParseDouble(reader.GetAttribute("refresh"));
                    }

                    reader.Skip();
                    break;
                case "sound":
                    machine.SoundChannels = ParseInt(reader.GetAttribute("channels"));
                    reader.Skip();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (machine.Description.Length == 0)
        {
            machine.Description = machine.Name;
        }

        return machine;
    }

    private static MameRomDump ReadRom(XmlReader reader)
    {
        return new MameRomDump(
            reader.GetAttribute("name") ?? string.Empty,
            ParseLong(reader.GetAttribute("size")) ?? 0,
            reader.GetAttribute("crc"),
            reader.GetAttribute("sha1"),
            reader.GetAttribute("merge"),
            reader.GetAttribute("region"),
            reader.GetAttribute("status") ?? "good",
            ParseYesNo(reader.GetAttribute("optional")));
    }

    private static MameControl ReadControl(XmlReader reader)
    {
        return new MameControl(
            reader.GetAttribute("type") ?? string.Empty,
            ParseInt(reader.GetAttribute("player")),
            ParseInt(reader.GetAttribute("buttons")),
            reader.GetAttribute("ways"));
    }

    private static bool ParseYesNo(string? value, bool defaultValue = false)
        => value is null ? defaultValue : string.Equals(value, "yes", StringComparison.Ordinal);

    private static int? ParseInt(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : null;

    private static long? ParseLong(string? value)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) ? result : null;

    private static double? ParseDouble(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : null;
}

/// <summary>Progress snapshot during an import; reported once per batch of parsed machines.</summary>
public sealed record MameImportProgress(int MachinesParsed, string? CurrentMachine);

/// <summary>Result of a completed MAME metadata import.</summary>
public sealed record MameImportResult(
    string Build,
    int MachineCount,
    int RomCount,
    int ControlCount,
    TimeSpan Duration);
