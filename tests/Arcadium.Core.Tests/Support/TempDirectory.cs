namespace Arcadium.Core.Tests.Support;

/// <summary>A temp folder that recursively deletes itself on disposal. Used for config/rom-tree fixtures.</summary>
internal sealed class TempDirectory : IDisposable
{
    internal string Path { get; }

    internal TempDirectory()
    {
        Path = Directory.CreateTempSubdirectory("arcadium-tests-").FullName;
    }

    internal string CreateSubdirectory(string relativePath)
    {
        string fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(fullPath);

        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
