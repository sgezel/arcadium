using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Arcadium.Core.Models
{
    /// <summary>
    /// Represents a system profile (e.g. MAME) as defined in the system-profiles JSON files.
    /// </summary>
    public class System
    {
        /// <summary>Path of the JSON file this profile was loaded from. Not part of the JSON itself.</summary>
        [JsonIgnore]
        public string ConfigFilePath { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; set; }= string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("romPath")]
        public List<string> RomPath { get; set; } = [];

        [JsonPropertyName("mediaPath")]
        public string MediaPath { get; set; } = string.Empty;

        [JsonPropertyName("extensions")]
        public List<string> Extensions { get; set; } = [];

        [JsonPropertyName("emulatorProfile")]
        public string EmulatorProfile { get; set; } = string.Empty;

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("sortOrder")]
        public int SortOrder { get; set; } = 0;
    }
}
