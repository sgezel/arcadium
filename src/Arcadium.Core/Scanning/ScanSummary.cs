namespace Arcadium.Core.Scanning;

/// <summary>End result of a whole scan run across all scanned systems.</summary>
public sealed record ScanSummary(
    bool Aborted,
    TimeSpan Duration,
    IReadOnlyDictionary<string, SystemScanStats> Systems,
    SystemScanStats Totals);
