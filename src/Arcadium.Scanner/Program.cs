using Arcadium.Domain.Models;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("Arcadium ROM scanner");
    Console.WriteLine("Usage: arcadium-scanner scan <init|update|verify> [--config <directory>] [--db <path>]");
    return;
}

if (args.Length < 2 || !string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Invalid command. Run with --help for usage.");
    Environment.ExitCode = 1;
    return;
}

var mode = args[1].ToLowerInvariant() switch
{
    "init" => ScanMode.Initialize,
    "update" => ScanMode.Update,
    "verify" => ScanMode.Verify,
    _ => throw new ArgumentException($"Unknown scan mode '{args[1]}'."),
};

Console.WriteLine($"Scan mode '{mode}' is scaffolded but not implemented yet.");
