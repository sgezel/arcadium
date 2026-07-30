using Arcadium.Core.Models;
using GameSystem = Arcadium.Core.Models.System;

namespace Arcadium.Core.Configuration;

public static class ConfigValidator
{
    public static List<ConfigValidationResult> ValidateConfiguration(ArcadiumConfiguration configuration)
    {
        List<ConfigValidationResult> results = new List<ConfigValidationResult>
        {
            ValidateCabinet(configuration.Cabinet)
        };

        results.AddRange(ValidateSystems(configuration.Systems, configuration.Emulators));
        results.AddRange(ValidateEmulators(configuration.Emulators));

        return results;
    }

    private static ConfigValidationResult ValidateCabinet(Cabinet cabinet)
    {
        ConfigValidationResult result = NewResult(cabinet?.ConfigFilePath, "cabinet.json");

        if (cabinet == null)
        {
            result.IsValid = false;
            result.Errors.Add("Cabinet configuration is not found.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(cabinet.Name))
        {
            result.IsValid = false;
            result.Errors.Add("Cabinet name is not specified.");
        }

        if (string.IsNullOrWhiteSpace(cabinet.Database))
        {
            result.IsValid = false;
            result.Errors.Add("Database path is not specified.");
        }

        return result;
    }

    private static List<ConfigValidationResult> ValidateSystems(IReadOnlyList<GameSystem> systems, IReadOnlyList<Emulator> emulators)
    {
        List<ConfigValidationResult> results = new List<ConfigValidationResult>();

        if (systems == null || systems.Count == 0)
        {
            ConfigValidationResult emptyResult = NewResult(null, "system-profiles");
            emptyResult.IsValid = false;
            emptyResult.Errors.Add("No system profiles found.");
            results.Add(emptyResult);
            return results;
        }

        HashSet<string> emulatorIds = new HashSet<string>(
            (emulators ?? []).Select(emulator => emulator.Id),
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenSystemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (GameSystem system in systems)
        {
            ConfigValidationResult result = NewResult(system.ConfigFilePath, $"system-profiles/{system.Id}");
            results.Add(result);

            if (string.IsNullOrWhiteSpace(system.Id))
            {
                result.IsValid = false;
                result.Errors.Add($"System profile '{system.Name}' has an empty or null ID.");
            }
            else if (!seenSystemIds.Add(system.Id))
            {
                result.IsValid = false;
                result.Errors.Add($"Duplicate system profile ID '{system.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(system.Name))
            {
                result.IsValid = false;
                result.Errors.Add($"System profile with ID '{system.Id}' has an empty or null name.");
            }

            if (system.RomPath == null || system.RomPath.Count == 0)
            {
                result.IsValid = false;
                result.Errors.Add($"System profile '{system.Name}' has no ROM paths specified.");
            }
            else
            {
                foreach (string path in system.RomPath)
                {
                    if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"System profile '{system.Name}' has a ROM path that does not exist: '{path}'");
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(system.EmulatorProfile))
            {
                result.IsValid = false;
                result.Errors.Add($"System profile '{system.Name}' has no emulator profile specified.");
            }
            else if (!emulatorIds.Contains(system.EmulatorProfile))
            {
                result.IsValid = false;
                result.Errors.Add($"System profile '{system.Name}' references unknown emulator profile '{system.EmulatorProfile}'.");
            }
        }

        return results;
    }

    private static List<ConfigValidationResult> ValidateEmulators(IReadOnlyList<Emulator> emulators)
    {
        List<ConfigValidationResult> results = new List<ConfigValidationResult>();

        if (emulators == null || emulators.Count == 0)
        {
            ConfigValidationResult emptyResult = NewResult(null, "emulator-profiles");
            emptyResult.IsValid = false;
            emptyResult.Errors.Add("No emulator profiles found.");
            results.Add(emptyResult);
            return results;
        }

        HashSet<string> seenEmulatorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Emulator emulator in emulators)
        {
            ConfigValidationResult result = NewResult(emulator.ConfigFilePath, $"emulator-profiles/{emulator.Id}");
            results.Add(result);

            if (string.IsNullOrWhiteSpace(emulator.Id))
            {
                result.IsValid = false;
                result.Errors.Add($"Emulator profile '{emulator.Name}' has an empty or null ID.");
            }
            else if (!seenEmulatorIds.Add(emulator.Id))
            {
                result.IsValid = false;
                result.Errors.Add($"Duplicate emulator profile ID '{emulator.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(emulator.Name))
            {
                result.IsValid = false;
                result.Errors.Add($"Emulator profile with ID '{emulator.Id}' has an empty or null name.");
            }

            if (emulator.DefaultExecutables == null || emulator.DefaultExecutables.Count == 0)
            {
                result.IsValid = false;
                result.Errors.Add($"Emulator profile '{emulator.Name}' has no executables specified.");
            }
            else
            {
                foreach (string executable in emulator.DefaultExecutables)
                {
                    if (string.IsNullOrWhiteSpace(executable))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Emulator profile '{emulator.Name}' has an empty executable entry.");
                    }
                    // Only absolute paths can be checked here; bare names resolve via PATH at launch time.
                    else if (Path.IsPathRooted(executable) && !File.Exists(executable))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Emulator profile '{emulator.Name}' has an executable that does not exist: {executable}");
                    }
                }
            }
        }

        return results;
    }

    private static ConfigValidationResult NewResult(string? configFilePath, string fallbackLabel)
    {
        return new ConfigValidationResult
        {
            ConfigFilePath = string.IsNullOrWhiteSpace(configFilePath) ? fallbackLabel : configFilePath,
            IsValid = true
        };
    }
}
