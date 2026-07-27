using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Arcadium.Core.Models;
using GameSystem = Arcadium.Core.Models.System;

namespace Arcadium.Core.Configuration;

/// <summary>
/// Loads Arcadium's JSON configuration files into their strongly typed models.
/// </summary>
public sealed class ConfigReader
{
    private readonly ArcadiumJsonContext _jsonContext = ArcadiumJsonContext.Default;

    public async Task<ArcadiumConfiguration> ReadAsync(string configDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configDirectory);

        if (!Directory.Exists(configDirectory))
        {
            throw new DirectoryNotFoundException($"Config directory not found: {configDirectory}");
        }

        var cabinetPath = Path.Combine(configDirectory, "cabinet.json");
        var systemsDirectory = Path.Combine(configDirectory, "system-profiles");
        var emulatorsDirectory = Path.Combine(configDirectory, "emulator-profiles");

        return new ArcadiumConfiguration
        {
            Cabinet = await ReadCabinetAsync(cabinetPath, cancellationToken),
            Systems = await ReadSystemsAsync(systemsDirectory, cancellationToken),
            Emulators = await ReadEmulatorsAsync(emulatorsDirectory, cancellationToken)
        };
    }

    private async Task<Cabinet> ReadCabinetAsync(string path, CancellationToken cancellationToken)
    {
        var cabinet = await DeserializeFileAsync(path, _jsonContext.Cabinet, "Cabinet configuration", cancellationToken);
        cabinet.ConfigFilePath = path;
        return cabinet;
    }

    private async Task<IReadOnlyList<GameSystem>> ReadSystemsAsync( string directory, CancellationToken cancellationToken)
    {
        var systems = new List<GameSystem>();

        foreach (var path in EnumerateJsonFiles(directory))
        {
            var system = await DeserializeFileAsync(path, _jsonContext.System, "System profile", cancellationToken);
            system.ConfigFilePath = path;
            systems.Add(system);
        }

        return systems;
    }

    private async Task<IReadOnlyList<Emulator>> ReadEmulatorsAsync(string directory, CancellationToken cancellationToken)
    {
        var emulators = new List<Emulator>();

        foreach (var path in EnumerateJsonFiles(directory))
        {
            var emulator = await DeserializeFileAsync(path, _jsonContext.Emulator, "Emulator profile", cancellationToken);
            emulator.ConfigFilePath = path;
            emulators.Add(emulator);
        }

        return emulators;
    }

    private static async Task<T> DeserializeFileAsync<T>(string path, JsonTypeInfo<T> typeInfo, string description, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);

        try
        {
            return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken)
                ?? throw new InvalidDataException($"{description} is empty: {path}");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{description} '{path}' contains invalid JSON: {exception.Message}", exception);
        }
    }

    private static IEnumerable<string> EnumerateJsonFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Config directory not found: {directory}");
        }

        return Directory.EnumerateFiles(directory, "*.json").Where(path => !path.Contains(".example."))
                                                            .OrderBy(path => path);
    }
}
