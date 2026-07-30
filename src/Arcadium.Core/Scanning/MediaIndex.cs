namespace Arcadium.Core.Scanning;

/// <summary>
/// Pre-built basename → path lookup per media type for one system, so media is never
/// searched on disk per ROM. Matching is case-insensitive on the file name without extension.
/// </summary>
public sealed class MediaIndex
{
    private const string WheelFolder = "wheel";
    private const string VideosFolder = "videos";
    private const string MarqueeFolder = "marquee";
    private const string PhysicalFolder = "physical";
    private const string GameFolder = "game";

    private readonly Dictionary<string, string> _wheel;
    private readonly Dictionary<string, string> _videos;
    private readonly Dictionary<string, string> _marquee;
    private readonly Dictionary<string, string> _physical;
    private readonly Dictionary<string, string> _game;

    private MediaIndex(
        Dictionary<string, string> wheel,
        Dictionary<string, string> videos,
        Dictionary<string, string> marquee,
        Dictionary<string, string> physical,
        Dictionary<string, string> game)
    {
        _wheel = wheel;
        _videos = videos;
        _marquee = marquee;
        _physical = physical;
        _game = game;
    }

    /// <summary>Builds the index for a system's media path. Missing folders yield empty lookups.</summary>
    public static MediaIndex Build(string mediaPath)
    {
        return new MediaIndex(
            IndexFolder(mediaPath, WheelFolder),
            IndexFolder(mediaPath, VideosFolder),
            IndexFolder(mediaPath, MarqueeFolder),
            IndexFolder(mediaPath, PhysicalFolder),
            IndexFolder(mediaPath, GameFolder));
    }

    public MediaPaths Resolve(string basename)
    {
        return new MediaPaths(
            Lookup(_wheel, basename),
            Lookup(_videos, basename),
            Lookup(_marquee, basename),
            Lookup(_physical, basename),
            Lookup(_game, basename));
    }

    private static Dictionary<string, string> IndexFolder(string mediaPath, string folderName)
    {
        Dictionary<string, string> index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(mediaPath))
        {
            return index;
        }

        string folder = Path.Combine(mediaPath, folderName);
        if (!Directory.Exists(folder))
        {
            return index;
        }

        EnumerationOptions enumeration = new EnumerationOptions { IgnoreInaccessible = true };
        foreach (string path in Directory.EnumerateFiles(folder, "*", enumeration))
        {
            index.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        }

        return index;
    }

    private static string? Lookup(Dictionary<string, string> index, string basename)
        => index.TryGetValue(basename, out string? path) ? path : null;
}

/// <summary>Resolved media file paths for one ROM basename; null when no match exists.</summary>
public sealed record MediaPaths(string? Wheel, string? Video, string? Marquee, string? Physical, string? GameImage);
