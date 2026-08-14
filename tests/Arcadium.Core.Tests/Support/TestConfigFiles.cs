using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Arcadium.Core.Configuration;
using Arcadium.Core.Models;
using GameSystem = Arcadium.Core.Models.System;

namespace Arcadium.Core.Tests.Support;

/// <summary>Writes a minimal valid Arcadium config directory via the real <see cref="ArcadiumJsonContext"/>,
/// so fixtures stay in sync with the actual model shape instead of hand-maintained JSON strings.</summary>
internal static class TestConfigFiles
{
    internal static void WriteMinimalValidConfig(string configDirectory)
    {
        string systemsDirectory = Directory.CreateDirectory(Path.Combine(configDirectory, "system-profiles")).FullName;
        string emulatorsDirectory = Directory.CreateDirectory(Path.Combine(configDirectory, "emulator-profiles")).FullName;
        string romDirectory = Directory.CreateDirectory(Path.Combine(configDirectory, "roms")).FullName;

        Cabinet cabinet = new Cabinet
        {
            Name = "Test Cabinet",
            Database = "arcadium.db"
        };
        WriteJson(Path.Combine(configDirectory, "cabinet.json"), cabinet, ArcadiumJsonContext.Default.Cabinet);

        Emulator emulator = new Emulator
        {
            Id = "mame",
            Name = "MAME",
            DefaultExecutables = ["mame"],
            LaunchMode = "standalone"
        };
        WriteJson(Path.Combine(emulatorsDirectory, "mame.json"), emulator, ArcadiumJsonContext.Default.Emulator);

        GameSystem system = new GameSystem
        {
            Id = "arcade",
            Name = "Arcade",
            RomPath = [romDirectory],
            Extensions = [".zip"],
            EmulatorProfile = "mame"
        };
        WriteJson(Path.Combine(systemsDirectory, "arcade.json"), system, ArcadiumJsonContext.Default.System);
    }

    private static void WriteJson<T>(string path, T value, JsonTypeInfo<T> typeInfo)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, typeInfo));
    }
}
