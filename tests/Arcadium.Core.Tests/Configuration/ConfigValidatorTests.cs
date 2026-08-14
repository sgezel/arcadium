using Arcadium.Core.Configuration;
using Arcadium.Core.Models;
using Arcadium.Core.Tests.Support;
using Xunit;
using GameSystem = Arcadium.Core.Models.System;

namespace Arcadium.Core.Tests.Configuration;

public sealed class ConfigValidatorTests
{
    [Fact]
    public void ValidateConfiguration_WithDuplicateSystemIds_ReportsAnError()
    {
        Emulator emulator = NewEmulator("mame");
        GameSystem systemA = NewSystem("arcade", "mame", romPath: ".");
        GameSystem systemB = NewSystem("arcade", "mame", romPath: ".");
        ArcadiumConfiguration configuration = NewConfiguration([systemA, systemB], [emulator]);

        List<ConfigValidationResult> results = ConfigValidator.ValidateConfiguration(configuration);

        Assert.Contains(results, result => result.Errors.Any(
            error => error.Contains("Duplicate system profile ID", StringComparison.Ordinal)));
    }

    [Fact]
    public void ValidateConfiguration_WithUnknownEmulatorReference_ReportsAnError()
    {
        GameSystem system = NewSystem("arcade", "does-not-exist", romPath: ".");
        ArcadiumConfiguration configuration = NewConfiguration([system], []);

        List<ConfigValidationResult> results = ConfigValidator.ValidateConfiguration(configuration);

        Assert.Contains(results, result => result.Errors.Any(
            error => error.Contains("references unknown emulator profile", StringComparison.Ordinal)));
    }

    [Fact]
    public void ValidateConfiguration_WithMissingRomPath_ReportsAnError()
    {
        using TempDirectory tempDirectory = new TempDirectory();
        string missingPath = Path.Combine(tempDirectory.Path, "does-not-exist");

        Emulator emulator = NewEmulator("mame");
        GameSystem system = NewSystem("arcade", "mame", romPath: missingPath);
        ArcadiumConfiguration configuration = NewConfiguration([system], [emulator]);

        List<ConfigValidationResult> results = ConfigValidator.ValidateConfiguration(configuration);

        Assert.Contains(results, result => result.Errors.Any(
            error => error.Contains("ROM path that does not exist", StringComparison.Ordinal)));
    }

    private static ArcadiumConfiguration NewConfiguration(List<GameSystem> systems, List<Emulator> emulators)
    {
        return new ArcadiumConfiguration
        {
            Cabinet = new Cabinet { Name = "Test Cabinet", Database = "arcadium.db" },
            Systems = systems,
            Emulators = emulators
        };
    }

    private static GameSystem NewSystem(string id, string emulatorProfile, string romPath)
    {
        return new GameSystem
        {
            Id = id,
            Name = id,
            RomPath = [romPath],
            Extensions = [".zip"],
            EmulatorProfile = emulatorProfile
        };
    }

    private static Emulator NewEmulator(string id)
    {
        return new Emulator
        {
            Id = id,
            Name = id,
            DefaultExecutables = [id]
        };
    }
}
