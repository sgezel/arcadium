using System.Text.Json.Serialization;
using Arcadium.Core.Models;

namespace Arcadium.Core.Scanning;

/// <summary>
/// Progress event emitted by the <see cref="ScanEngine"/>. Also the NDJSON wire format
/// consumed by the Godot frontend, so changes here change the machine protocol.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "event")]
[JsonDerivedType(typeof(ScanStarted), "scanStarted")]
[JsonDerivedType(typeof(SystemScanStarted), "systemStarted")]
[JsonDerivedType(typeof(FileProgress), "progress")]
[JsonDerivedType(typeof(ScanWarning), "warning")]
[JsonDerivedType(typeof(SystemScanCompleted), "systemCompleted")]
[JsonDerivedType(typeof(ScanCompleted), "scanCompleted")]
public abstract record ScanEvent
{
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

public sealed record ScanStarted(ScanMode Mode, int SystemCount, bool DryRun) : ScanEvent;

public sealed record SystemScanStarted(string SystemId, string SystemName, int SystemIndex, int SystemCount) : ScanEvent;

/// <summary>Throttled by the engine; the final event per system always has Processed == Total.</summary>
public sealed record FileProgress(string SystemId, int Processed, int Total, string? CurrentFile) : ScanEvent;

public sealed record ScanWarning(string? SystemId, string Message, string? Path) : ScanEvent;

public sealed record SystemScanCompleted(string SystemId, SystemScanStats Stats) : ScanEvent;

public sealed record ScanCompleted(ScanSummary Summary) : ScanEvent;
