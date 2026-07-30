using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Arcadium.Core.Configuration;
using Arcadium.Core.Data;
using Arcadium.Core.Logging;
using Arcadium.Core.Mame;
using Arcadium.Core.Models;
using Arcadium.Core.Scanning;
using Microsoft.Data.Sqlite;

Logger.Initialize(LogType.Console);

string configDirectory = Path.Combine(AppContext.BaseDirectory, "config");
string? databasePathOverride = null;
string? systemFilter = null;
bool checkConfiguration = false;
bool dryRun = false;
bool jsonOutput = false;
bool importMame = false;
string? mameExecutableOverride = null;
string? mameXmlPath = null;
ScanMode mode = ScanMode.Update;

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    Logger.LogInformation("Arcadium ROM scanner");
    Logger.LogInformation("Usage: arcadium-scanner scan [init|update|verify] [options]");
    Logger.LogInformation("       arcadium-scanner import-mame [options]");
    Logger.LogInformation("Options:");
    Logger.LogInformation("  --config, -c <directory>   Config directory (default: <exe>/config)");
    Logger.LogInformation("  --db <path>                Database path (default: cabinet.json 'database')");
    Logger.LogInformation("  --system, -s <id>          Only scan the system with this id");
    Logger.LogInformation("  --dry-run                  Scan and report, but persist nothing");
    Logger.LogInformation("  --json                     Emit NDJSON scan events on stdout");
    Logger.LogInformation("  --check-configuration, -cc Validate the configuration and exit");
    Logger.LogInformation("import-mame options:");
    Logger.LogInformation("  --mame <path>              MAME executable (default: emulator profile 'mame')");
    Logger.LogInformation("  --xml <path>               Read an existing -listxml dump instead of running MAME");
    return;
}

for (int i = 0; i < args.Length; i++)
{
    if (args[i] is "scan")
    {
        if (i + 1 < args.Length && args[i + 1].ToLower(CultureInfo.CurrentCulture) is "init" or "update" or "verify")
        {
            mode = args[i + 1].ToLower(CultureInfo.CurrentCulture) switch
            {
                "init" => ScanMode.Initialize,
                "verify" => ScanMode.Verify,
                _ => ScanMode.Update,
            };

            i++; // skip the next argument since it's the scan mode
        }
    }
    else if (args[i] is "import-mame")
    {
        importMame = true;
    }
    else if (args[i] is "--mame" && i + 1 < args.Length)
    {
        mameExecutableOverride = args[i + 1];
        i++;
    }
    else if (args[i] is "--xml" && i + 1 < args.Length)
    {
        mameXmlPath = args[i + 1];
        i++;
    }
    else if (args[i] is "--config" or "-c" && i + 1 < args.Length)
    {
        configDirectory = args[i + 1];
        i++;
    }
    else if (args[i] is "--db" && i + 1 < args.Length)
    {
        databasePathOverride = args[i + 1];
        i++;
    }
    else if (args[i] is "--system" or "-s" && i + 1 < args.Length)
    {
        systemFilter = args[i + 1];
        i++;
    }
    else if (args[i] is "--dry-run")
    {
        dryRun = true;
    }
    else if (args[i] is "--json")
    {
        jsonOutput = true;
    }
    else if (args[i] is "--check-configuration" or "-cc")
    {
        checkConfiguration = true;
    }
    else
    {
        Logger.LogError($"Unknown argument '{args[i]}'. Run with --help for usage.");
        Environment.ExitCode = 1;
        return;
    }
}

// In --json mode stdout is reserved for NDJSON events; human output goes to stderr.
void Info(string message)
{
    if (jsonOutput)
    {
        Console.Error.WriteLine(message);
    }
    else
    {
        Logger.LogInformation(message);
    }
}

void Error(string message)
{
    if (jsonOutput)
    {
        Console.Error.WriteLine(message);
    }
    else
    {
        Logger.LogError(message);
    }
}

ConfigReader configReader = new();
ArcadiumConfiguration configuration;
try
{
    configuration = await configReader.ReadAsync(configDirectory);
}
catch (Exception exception)
{
    Error($"Failed to read configuration: {exception.Message}");
    Environment.ExitCode = 1;
    return;
}

if (checkConfiguration)
{
    List<ConfigValidationResult> validationResults = ConfigValidator.ValidateConfiguration(configuration);

    if (!validationResults.All(r => r.IsValid))
    {
        foreach (ConfigValidationResult? result in validationResults.Where(r => !r.IsValid))
        {
            Error($"Configuration file '{result.ConfigFilePath}' has validation errors:");
            foreach (string error in result.Errors)
            {
                Error($"  - {error}");
            }
        }

        Error("Configuration validation failed.");
        Environment.ExitCode = 1;
    }
    else
    {
        Info("Configuration is valid.");
    }

    return;
}

// Ctrl+C cancels gracefully: the engine finishes its current transaction handling and reports Aborted.
using CancellationTokenSource cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

string databasePath = databasePathOverride ?? configuration.Cabinet.Database;

SqliteDatabase database = new(databasePath);

if (importMame)
{
    try
    {
        using SqliteConnection connection = database.OpenReadWrite();
        new MigrationRunner(connection).RunMigrations();

        using MameRepository repository = new MameRepository(connection);
        MameXmlImporter importer = new MameXmlImporter(repository);

        bool importProgressLineOpen = false;
        InlineProgress<MameImportProgress> importProgress = new InlineProgress<MameImportProgress>(p =>
        {
            if (!jsonOutput)
            {
                Console.Write($"\r  {p.MachinesParsed} machines {(p.CurrentMachine is null ? "" : $"({p.CurrentMachine})")}          ");
                importProgressLineOpen = true;
            }
        });

        MameImportResult result;
        if (mameXmlPath is not null)
        {
            Info($"Importing MAME metadata from {mameXmlPath}");
            using FileStream xmlStream = File.OpenRead(mameXmlPath);
            result = importer.Import(xmlStream, importProgress, cancellation.Token);
        }
        else
        {
            string mameExecutable = mameExecutableOverride
                ?? configuration.Emulators
                    .FirstOrDefault(emulator => string.Equals(emulator.Id, "mame", StringComparison.OrdinalIgnoreCase))
                    ?.DefaultExecutables.FirstOrDefault()
                ?? "mame";

            Info($"Importing MAME metadata from '{mameExecutable} -listxml'");

            using Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = mameExecutable,
                ArgumentList = { "-listxml" },
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };

            if (!process.Start())
            {
                throw new InvalidOperationException($"Could not start '{mameExecutable}'.");
            }

            try
            {
                result = importer.Import(process.StandardOutput.BaseStream, importProgress, cancellation.Token);
            }
            catch
            {
                // Cancellation or a parse error: stop MAME too instead of letting it
                // keep writing XML to a closed pipe.
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                throw;
            }

            process.WaitForExit();
        }

        if (importProgressLineOpen)
        {
            Console.WriteLine();
        }

        Info($"Import completed in {result.Duration.TotalSeconds:F1}s: MAME {result.Build}, " +
             $"{result.MachineCount} machines, {result.RomCount} rom dumps, {result.ControlCount} controls");
    }
    catch (OperationCanceledException)
    {
        Error("MAME import aborted; no changes were committed.");
        Environment.ExitCode = 3;
    }
    catch (Exception exception)
    {
        Error($"MAME import failed: {exception.Message}");
        Environment.ExitCode = 1;
    }

    return;
}

ScanSummary summary;
try
{
    using SqliteConnection connection = database.OpenReadWrite();
    new MigrationRunner(connection).RunMigrations();

    ScanRepository repository = new ScanRepository(connection);
    ScanEngine engine = new ScanEngine(repository);

    ScanOptions options = new ScanOptions
    {
        Mode = mode,
        SystemFilter = systemFilter,
        DryRun = dryRun,
    };

    bool progressLineOpen = false;

    void CloseProgressLine()
    {
        if (progressLineOpen)
        {
            Console.WriteLine();
            progressLineOpen = false;
        }
    }

    InlineProgress<ScanEvent> progress = new InlineProgress<ScanEvent>(scanEvent =>
    {
        if (jsonOutput)
        {
            Console.WriteLine(JsonSerializer.Serialize(scanEvent, ArcadiumJsonContext.Default.ScanEvent));
            Console.Out.Flush();
            return;
        }

        switch (scanEvent)
        {
            case ScanStarted started:
                Info($"Scan started: mode={started.Mode}, systems={started.SystemCount}{(started.DryRun ? ", dry-run" : "")}");
                break;
            case SystemScanStarted systemStarted:
                Info($"[{systemStarted.SystemIndex}/{systemStarted.SystemCount}] Scanning {systemStarted.SystemName} ({systemStarted.SystemId})");
                break;
            case FileProgress fileProgress:
                Console.Write($"\r  {fileProgress.Processed}/{fileProgress.Total} {fileProgress.CurrentFile ?? ""}          ");
                progressLineOpen = true;
                if (fileProgress.Processed == fileProgress.Total)
                {
                    CloseProgressLine();
                }
                break;
            case ScanWarning warning:
                CloseProgressLine();
                Logger.LogWarning($"{warning.Message}{(warning.Path is null ? "" : $" ({warning.Path})")}");
                break;
            case SystemScanCompleted systemCompleted:
                CloseProgressLine();
                SystemScanStats stats = systemCompleted.Stats;
                Info($"  Done: {stats.FilesSeen} seen, {stats.Added} added, {stats.Updated} updated, {stats.Unchanged} unchanged, {stats.Deleted} deleted, {stats.Warnings} warnings");
                break;
            case ScanCompleted completed:
                CloseProgressLine();
                Info($"Scan {(completed.Summary.Aborted ? "aborted" : "completed")} in {completed.Summary.Duration.TotalSeconds:F1}s: " +
                     $"{completed.Summary.Totals.Added} added, {completed.Summary.Totals.Updated} updated, " +
                     $"{completed.Summary.Totals.Deleted} deleted, {completed.Summary.Totals.Warnings} warnings");
                break;
        }
    });

    summary = await engine.RunAsync(configuration, options, progress, cancellation.Token);
}
catch (Exception exception)
{
    Error($"Scan failed: {exception.Message}");
    Environment.ExitCode = 1;
    return;
}

Environment.ExitCode = summary switch
{
    { Aborted: true } => 3,
    { Totals.Errors: > 0 } => 2,
    _ => 0,
};
