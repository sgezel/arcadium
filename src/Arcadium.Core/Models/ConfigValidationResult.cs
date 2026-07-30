namespace Arcadium.Core.Models;

public class ConfigValidationResult
{
    public string ConfigFilePath { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new List<string>();

}
