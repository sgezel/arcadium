namespace Arcadium.Core.Mame;

/// <summary>One parsed &lt;machine&gt; element from MAME -listxml output.</summary>
public sealed class MameMachine
{
    public required string Name { get; init; }

    public string? SourceFile { get; init; }

    public string Description { get; set; } = string.Empty;

    public string? Year { get; set; }

    public string? Manufacturer { get; set; }

    public string? CloneOf { get; init; }

    public string? RomOf { get; init; }

    public string? SampleOf { get; init; }

    public bool IsBios { get; init; }

    public bool IsDevice { get; init; }

    public bool IsMechanical { get; init; }

    public bool Runnable { get; init; } = true;

    public string? DriverStatus { get; set; }

    public string? DriverEmulation { get; set; }

    public string? DriverSavestate { get; set; }

    public int? Players { get; set; }

    public int? Coins { get; set; }

    public string? DisplayType { get; set; }

    public int? DisplayRotate { get; set; }

    public int? DisplayWidth { get; set; }

    public int? DisplayHeight { get; set; }

    public double? DisplayRefresh { get; set; }

    public int? SoundChannels { get; set; }

    public bool RequiresChd { get; set; }

    public List<MameRomDump> Roms { get; } = [];

    public List<MameControl> Controls { get; } = [];
}

/// <summary>One &lt;rom&gt; dump entry of a machine.</summary>
public sealed record MameRomDump(
    string Name,
    long SizeBytes,
    string? Crc,
    string? Sha1,
    string? MergeName,
    string? Region,
    string Status,
    bool IsOptional);

/// <summary>One &lt;control&gt; entry of a machine's input definition.</summary>
public sealed record MameControl(
    string Type,
    int? Player,
    int? Buttons,
    string? Ways);
