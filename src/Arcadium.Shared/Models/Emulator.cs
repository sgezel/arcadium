using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Arcadium.Shared.Models
{
    public class Emulator
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("defaultExecutables")]
        public List<string> DefaultExecutables { get; set; } = [];

        [JsonPropertyName("extraConfig")]
        public string ExtraConfig { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public List<string> Arguments { get; set; } = [];

        [JsonPropertyName("launchMode")]
        public string LaunchMode { get; set; } = string.Empty;
    }
}
