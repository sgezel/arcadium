using Arcadium.Core.Models;

namespace Arcadium.Core.Scanning;

public sealed record ScanOptions
{
    public required ScanMode Mode { get; init; }

    /// <summary>When set, only the system with this id (case-insensitive) is scanned.</summary>
    public string? SystemFilter { get; init; }

    /// <summary>Run the full scan but roll back all database changes.</summary>
    public bool DryRun { get; init; }

    /// <summary>Minimum time between two FileProgress events per system.</summary>
    public TimeSpan ProgressInterval { get; init; } = TimeSpan.FromMilliseconds(100);
}
