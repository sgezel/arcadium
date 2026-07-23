using System.Text.Json.Serialization;

namespace Arcadium.Shared.Models
{
    public class Cabinet
    {
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
    }
}
