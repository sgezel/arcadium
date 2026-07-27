using System.Text.Json;
using System.Text.Json.Serialization;
using Arcadium.Core.Models;
using GameSystem = Arcadium.Core.Models.System;

namespace Arcadium.Core.Configuration;

/// <summary>
/// Source-generated JSON metadata for Arcadium configuration models.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(Cabinet))]
[JsonSerializable(typeof(GameSystem))]
[JsonSerializable(typeof(Emulator))]
public partial class ArcadiumJsonContext : JsonSerializerContext
{
}
