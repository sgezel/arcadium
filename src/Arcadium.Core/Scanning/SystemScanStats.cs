namespace Arcadium.Core.Scanning;

/// <summary>
/// Result counters for one scanned system. Also used as the totals of a whole scan run.
/// </summary>
public sealed record SystemScanStats(
    int FilesSeen,
    int Added,
    int Updated,
    int Unchanged,
    int Deleted,
    int Warnings,
    int Errors);
