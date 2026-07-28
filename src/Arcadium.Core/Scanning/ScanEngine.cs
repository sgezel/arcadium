using System.Diagnostics;
using Arcadium.Core.Configuration;
using Arcadium.Core.Data;
using Arcadium.Core.Models;
using GameSystem = Arcadium.Core.Models.System;

namespace Arcadium.Core.Scanning;

/// <summary>
/// Host-agnostic ROM scanner. Reports progress through <see cref="IProgress{ScanEvent}"/>;
/// warnings never abort a scan. One database transaction per system: a crash mid-scan
/// leaves previously scanned systems consistent, and dry-run/verify roll back instead of commit.
/// </summary>
public sealed class ScanEngine
{
    private const string ActiveScanState = "active";

    private readonly ScanRepository _repository;

    public ScanEngine(ScanRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);

        _repository = repository;
    }

    public Task<ScanSummary> RunAsync(
        ArcadiumConfiguration configuration,
        ScanOptions options,
        IProgress<ScanEvent>? progress,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Run(configuration, options, progress, cancellationToken), CancellationToken.None);
    }

    public ScanSummary Run(
        ArcadiumConfiguration configuration,
        ScanOptions options,
        IProgress<ScanEvent>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        var stopwatch = Stopwatch.StartNew();

        GameSystem[] systems = configuration.Systems
            .Where(system => system.Enabled)
            .Where(system => options.SystemFilter is null
                || string.Equals(system.Id, options.SystemFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(system => system.SortOrder)
            .ToArray();

        progress?.Report(new ScanStarted(options.Mode, systems.Length, options.DryRun));

        var perSystem = new Dictionary<string, SystemScanStats>();
        bool aborted = false;

        for (int index = 0; index < systems.Length; index++)
        {
            GameSystem system = systems[index];
            progress?.Report(new SystemScanStarted(system.Id, system.Name, index + 1, systems.Length));

            try
            {
                perSystem[system.Id] = ScanSystem(system, options, progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                aborted = true;
                break;
            }
            catch (Exception exception)
            {
                // A failing system must not abort the scan of the remaining systems.
                progress?.Report(new ScanWarning(system.Id, $"System scan failed: {exception.Message}", null));
                perSystem[system.Id] = new SystemScanStats(0, 0, 0, 0, 0, 0, 1);
            }
        }

        SystemScanStats totals = Accumulate(perSystem.Values);
        var summary = new ScanSummary(aborted, stopwatch.Elapsed, perSystem, totals);
        progress?.Report(new ScanCompleted(summary));

        return summary;
    }

    private SystemScanStats ScanSystem(
        GameSystem system,
        ScanOptions options,
        IProgress<ScanEvent>? progress,
        CancellationToken cancellationToken)
    {
        int warnings = 0;

        void Warn(string message, string? path = null)
        {
            warnings++;
            progress?.Report(new ScanWarning(system.Id, message, path));
        }

        List<FileInfo> files = CollectRomFiles(system, Warn, cancellationToken);

        if (!string.IsNullOrWhiteSpace(system.MediaPath) && !Directory.Exists(system.MediaPath))
        {
            Warn($"Media path does not exist: {system.MediaPath}", system.MediaPath);
        }

        MediaIndex mediaIndex = MediaIndex.Build(system.MediaPath);

        DateTime scanStart = DateTime.UtcNow;
        bool persist = !options.DryRun && options.Mode != ScanMode.Verify;

        _repository.BeginTransaction();
        try
        {
            long scanRunId = _repository.BeginScanRun(options.Mode, system.Id, scanStart);
            Dictionary<string, RomRecord> existing = _repository.GetRomsBySystem(system.Id);

            progress?.Report(new FileProgress(system.Id, 0, files.Count, null));

            int added = 0;
            int updated = 0;
            int unchanged = 0;
            var throttle = Stopwatch.StartNew();

            for (int index = 0; index < files.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileInfo file = files[index];

                if (throttle.Elapsed >= options.ProgressInterval)
                {
                    progress?.Report(new FileProgress(system.Id, index, files.Count, file.Name));
                    throttle.Restart();
                }

                long sizeBytes;
                DateTime modifiedTimeUtc;
                try
                {
                    sizeBytes = file.Length;
                    modifiedTimeUtc = file.LastWriteTimeUtc;
                }
                catch (IOException exception)
                {
                    Warn($"Cannot read file info: {exception.Message}", file.FullName);
                    continue;
                }
                catch (UnauthorizedAccessException exception)
                {
                    Warn($"Access denied: {exception.Message}", file.FullName);
                    continue;
                }

                string basename = Path.GetFileNameWithoutExtension(file.Name);
                MediaPaths media = mediaIndex.Resolve(basename);
                DateTime now = DateTime.UtcNow;

                if (existing.TryGetValue(file.FullName, out RomRecord? known)
                    && known.ScanState == ActiveScanState
                    && known.SizeBytes == sizeBytes
                    && known.ModifiedTimeUtc == modifiedTimeUtc
                    && known.WheelPath == media.Wheel
                    && known.VideoPath == media.Video
                    && known.MarqueePath == media.Marquee
                    && known.PhysicalPath == media.Physical
                    && known.GameImagePath == media.GameImage)
                {
                    _repository.TouchRom(known.Id, now);
                    unchanged++;
                    continue;
                }

                _repository.UpsertRom(new RomRecord
                {
                    SystemId = system.Id,
                    Filename = file.Name,
                    Basename = basename,
                    Path = file.FullName,
                    WheelPath = media.Wheel,
                    VideoPath = media.Video,
                    MarqueePath = media.Marquee,
                    PhysicalPath = media.Physical,
                    GameImagePath = media.GameImage,
                    SizeBytes = sizeBytes,
                    ModifiedTimeUtc = modifiedTimeUtc,
                    ScanState = ActiveScanState,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                if (known is null)
                {
                    added++;
                }
                else
                {
                    updated++;
                }
            }

            int deleted = _repository.MarkUnseenAsDeleted(system.Id, scanStart);

            var stats = new SystemScanStats(files.Count, added, updated, unchanged, deleted, warnings, 0);
            _repository.CompleteScanRun(scanRunId, stats, DateTime.UtcNow);

            if (persist)
            {
                _repository.Commit();
            }
            else
            {
                _repository.Rollback();
            }

            progress?.Report(new FileProgress(system.Id, files.Count, files.Count, null));
            progress?.Report(new SystemScanCompleted(system.Id, stats));

            return stats;
        }
        catch
        {
            if (_repository.HasActiveTransaction)
            {
                _repository.Rollback();
            }

            throw;
        }
    }

    private static List<FileInfo> CollectRomFiles(
        GameSystem system,
        Action<string, string?> warn,
        CancellationToken cancellationToken)
    {
        var extensions = new HashSet<string>(
            system.Extensions.Select(extension => extension.StartsWith('.') ? extension : "." + extension),
            StringComparer.OrdinalIgnoreCase);

        var files = new List<FileInfo>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string root in system.RomPath)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(root))
            {
                warn($"ROM path does not exist: {root}", root);
                continue;
            }

            var enumeration = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true
            };

            foreach (string path in Directory.EnumerateFiles(root, "*", enumeration))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (extensions.Contains(Path.GetExtension(path)) && seenPaths.Add(path))
                {
                    files.Add(new FileInfo(path));
                }
            }
        }

        return files;
    }

    private static SystemScanStats Accumulate(IEnumerable<SystemScanStats> allStats)
    {
        var totals = new SystemScanStats(0, 0, 0, 0, 0, 0, 0);

        foreach (SystemScanStats stats in allStats)
        {
            totals = new SystemScanStats(
                totals.FilesSeen + stats.FilesSeen,
                totals.Added + stats.Added,
                totals.Updated + stats.Updated,
                totals.Unchanged + stats.Unchanged,
                totals.Deleted + stats.Deleted,
                totals.Warnings + stats.Warnings,
                totals.Errors + stats.Errors);
        }

        return totals;
    }
}
