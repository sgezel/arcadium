namespace Arcadium.Core.Tests.Support;

/// <summary>Writes fake rom files with a controlled size and mtime for <see cref="Arcadium.Core.Scanning.ScanEngine"/> tests.</summary>
internal static class TestRomFiles
{
    internal static string Create(string directory, string fileName, int sizeBytes, DateTime lastWriteTimeUtc)
    {
        string path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, new byte[sizeBytes]);
        File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);

        return path;
    }
}
