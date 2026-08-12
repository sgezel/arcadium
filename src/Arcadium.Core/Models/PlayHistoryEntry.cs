namespace Arcadium.Core.Models;

/// <summary>Mirrors a row in the "game_play_history" table: one emulator launch of a rom.</summary>
public class PlayHistoryEntry
{
    public long Id { get; set; }

    public long RomId { get; set; }

    public DateTime LaunchedAt { get; set; }

    /// <summary>Null while the emulator session is still running.</summary>
    public DateTime? ExitedAt { get; set; }

    public int? ExitCode { get; set; }
}
