using System.Text.Json.Serialization;

namespace Arcadium.Core.Models
{
    public class Cabinet
    {
        /// <summary>Path of the JSON file this configuration was loaded from. Not part of the JSON itself.</summary>
        [JsonIgnore]
        public string ConfigFilePath { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("fullscreen")]
        public bool Fullscreen { get; set; } = true;

        [JsonPropertyName("hideCursor")]
        public bool HideCursor { get; set; } = true;

        [JsonPropertyName("idleAttractModeSeconds")]
        public int IdleAttractModeSeconds { get; set; } = 120;

        [JsonPropertyName("maintenanceCombo")]
        public string MaintenanceCombo { get; set; } = string.Empty;

        [JsonPropertyName("returnToLibraryAfterGame")]
        public bool ReturnToLibraryAfterGame { get; set; } = true;

        [JsonPropertyName("database")]
        public string Database { get; set; } = "arcadium.db";
    }
}
