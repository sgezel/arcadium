using Arcadium.Core.Models;
using Arcadium.Core.Configuration;
using System.Globalization;

string configDirectory = Path.Combine(AppContext.BaseDirectory, "config");
bool checkConfiguration = false;
ScanMode mode = ScanMode.Update;

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("Arcadium ROM scanner");
    Console.WriteLine("Usage: arcadium-scanner scan <init|update|verify> [--config <directory>] [--db <path>]");
    return;
}

if (args.Length < 2)
{
    Console.Error.WriteLine("Invalid command. Run with --help for usage.");
    Environment.ExitCode = 1;
    return;
}

for (int i = 0; i < args.Length; i++)
{
    if(args[i] is "scan" && i + 1 < args.Length)
    {
        mode = args[i + 1].ToLower(CultureInfo.CurrentCulture) switch
        {
            "init" => ScanMode.Initialize,
            "update" => ScanMode.Update,
            "verify" => ScanMode.Verify,
            _ => throw new ArgumentException($"Unknown scan mode '{args[i + 1]}'."),
        };

        i++; //skip the next argument since it's the scan mode
    }
    else if (args[i] is "--config" or "-c" && i + 1 < args.Length)
    {
        configDirectory = args[i + 1];
        i++; // Skip the next argument since it's the config path
    }
    else if (args[i] is "--check-configuration" or "-cc")
    {
        checkConfiguration = true;
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
    Console.Error.WriteLine($"Failed to read configuration: {exception.Message}");
    Environment.ExitCode = 1;
    return;
}

if (checkConfiguration)
{
    List<ConfigValidationResult> validationResults = ConfigValidator.ValidateConfiguration(configuration);

    if (!validationResults.All(r => r.IsValid))
    {
        foreach (var result in validationResults)
        {
            if (!result.IsValid)
            {
                Console.Error.WriteLine($"Configuration file '{result.ConfigFilePath}' has validation errors:");
                foreach (var error in result.Errors)
                {
                    Console.Error.WriteLine($"  - {error}");
                }
            }
        }

        Console.Error.WriteLine("Configuration validation failed.");
        Environment.ExitCode = 1;
        return;
    }
    else
    {
        Console.WriteLine("Configuration is valid.");
        return;
    }
}

Console.WriteLine($"Scan mode '{mode}' is scaffolded but not implemented yet.");

foreach (var system in configuration.Systems)
{
    Console.WriteLine($"System: {system.Name}");

    Console.WriteLine($"  ROM Path: {string.Join(", ", system.RomPath)}");
}