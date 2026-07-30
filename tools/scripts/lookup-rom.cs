#:project ../../src/Arcadium.Core/Arcadium.Core.csproj

// Looks up a rom in the database and prints the rom + MAME metadata.
// Usage (from the repo root):
//   dotnet run tools/scripts/lookup-rom.cs <systemId> <basename> [dbPath]
// Without dbPath the path is read from config/cabinet.json.

using System.Globalization;
using System.Text.Json;
using Arcadium.Core.Data;
using Arcadium.Core.Models;
using Microsoft.Data.Sqlite;

if (args.Length < 2)
{
    Console.WriteLine("Usage: dotnet run tools/scripts/lookup-rom.cs <systemId> <basename> [dbPath]");
    return 1;
}

string systemId = args[0];
string baseName = args[1];
string databasePath = args.Length > 2 ? args[2] : ReadDatabasePathFromConfig();

Console.WriteLine($"Database: {databasePath}");

var database = new SqliteDatabase(databasePath);
using SqliteConnection connection = database.OpenReadOnly();
var repository = new LibraryRepository(connection);

LibraryRom? result = repository.GetLibraryRom(systemId, baseName);
if (result is null)
{
    Console.WriteLine($"No active rom found for system '{systemId}' with basename '{baseName}'.");
    return 1;
}

RomRecord rom = result.Rom;
Console.WriteLine();
Console.WriteLine($"Rom #{rom.Id}  {rom.Filename}  ({rom.SizeBytes:N0} bytes)");
Console.WriteLine($"  Path:      {rom.Path}");
Console.WriteLine($"  Modified:  {rom.ModifiedTimeUtc:u}   State: {rom.ScanState}");
Console.WriteLine($"  Media:     wheel={rom.WheelPath ?? "-"} video={rom.VideoPath ?? "-"} marquee={rom.MarqueePath ?? "-"}");

if (result.Machine is null)
{
    Console.WriteLine();
    Console.WriteLine("No MAME machine attached (non-MAME system or no match on basename).");
    return 0;
}

var machine = result.Machine;
Console.WriteLine();
Console.WriteLine($"Machine '{machine.Name}': {machine.Description}");
Console.WriteLine($"  Year: {machine.Year ?? "?"}   Manufacturer: {machine.Manufacturer ?? "?"}");
Console.WriteLine($"  Players: {machine.Players?.ToString(CultureInfo.InvariantCulture) ?? "?"}   Coins: {machine.Coins?.ToString(CultureInfo.InvariantCulture) ?? "?"}   Driver: {machine.DriverStatus ?? "?"}");
Console.WriteLine($"  Display: {machine.DisplayType ?? "?"} {machine.DisplayWidth}x{machine.DisplayHeight} @ {machine.DisplayRefresh:F2}Hz rotate={machine.DisplayRotate}");
Console.WriteLine($"  CloneOf: {machine.CloneOf ?? "-"}   RequiresChd: {machine.RequiresChd}");

Console.WriteLine();
Console.WriteLine($"  Controls ({machine.Controls.Count}):");
foreach (var control in machine.Controls)
{
    Console.WriteLine($"    player {control.Player?.ToString(CultureInfo.InvariantCulture) ?? "?"}: {control.Type} buttons={control.Buttons?.ToString(CultureInfo.InvariantCulture) ?? "?"} ways={control.Ways ?? "-"}");
}

Console.WriteLine();
Console.WriteLine($"  Rom dumps ({machine.Roms.Count}):");
foreach (var dump in machine.Roms)
{
    Console.WriteLine($"    {dump.Name,-20} {dump.SizeBytes,10:N0}  crc={dump.Crc ?? "-"}  status={dump.Status}");
}

return 0;

static string ReadDatabasePathFromConfig()
{
    const string configPath = "config/cabinet.json";
    if (!File.Exists(configPath))
    {
        throw new FileNotFoundException(
            $"'{configPath}' not found — run from the repo root or pass a dbPath as the third argument.");
    }

    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath));
    return document.RootElement.GetProperty("database").GetString()
        ?? throw new InvalidOperationException("'database' is missing in config/cabinet.json.");
}
