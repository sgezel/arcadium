using System.Text.Json;
using System.Text.Json.Serialization;
using Arcadium.Core.Models;
using Arcadium.Core.Scanning;
using GameSystem = Arcadium.Core.Models.System;

namespace Arcadium.Core.Configuration;

/// <summary>
/// Source-generated JSON metadata for Arcadium configuration models and scanner events.
/// The camelCase policy and string enums define the NDJSON wire format of scan events;
/// the configuration models are unaffected because they carry explicit JsonPropertyName attributes.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(Cabinet))]
[JsonSerializable(typeof(GameSystem))]
[JsonSerializable(typeof(Emulator))]
[JsonSerializable(typeof(ScanEvent))]
public partial class ArcadiumJsonContext : JsonSerializerContext
{
}
