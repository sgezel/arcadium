namespace Arcadium.Core.Models;

/// <summary>
/// Mirrors a row in the "roms" table. A ROM is uniquely identified by (SystemId, Path).
/// </summary>
public class RomRecord
{
    public long Id { get; set; }

    public string SystemId { get; set; } = string.Empty;

    /// <summary>File name including extension, e.g. "pacman.zip".</summary>
    public string Filename { get; set; } = string.Empty;

    /// <summary>File name without extension, e.g. "pacman". Used to match media files.</summary>
    public string Basename { get; set; } = string.Empty;

    /// <summary>Full path to the ROM file.</summary>
    public string Path { get; set; } = string.Empty;

    public string? WheelPath { get; set; }

    public string? VideoPath { get; set; }

    public string? MarqueePath { get; set; }

    public string? PhysicalPath { get; set; }

    public string? GameImagePath { get; set; }

    public long SizeBytes { get; set; }

    /// <summary>Last write time of the ROM file, in UTC.</summary>
    public DateTime ModifiedTimeUtc { get; set; }

    /// <summary>"active" or "deleted" (file no longer found on disk).</summary>
    public string ScanState { get; set; } = "active";

    public DateTime FirstSeenAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
