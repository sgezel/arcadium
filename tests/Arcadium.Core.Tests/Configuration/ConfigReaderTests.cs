using Arcadium.Core.Configuration;
using Arcadium.Core.Tests.Support;
using Xunit;

namespace Arcadium.Core.Tests.Configuration;

public sealed class ConfigReaderTests
{
    [Fact]
    public async Task ReadAsync_WithMinimalValidConfigDirectory_ReadsExpectedCountsAsync()
    {
        using TempDirectory tempDirectory = new TempDirectory();
        TestConfigFiles.WriteMinimalValidConfig(tempDirectory.Path);

        ConfigReader reader = new ConfigReader();
        ArcadiumConfiguration configuration = await reader.ReadAsync(tempDirectory.Path, TestContext.Current.CancellationToken);

        Assert.Equal("Test Cabinet", configuration.Cabinet.Name);
        Assert.Single(configuration.Systems);
        Assert.Single(configuration.Emulators);
    }

    [Fact]
    public async Task ReadAsync_WithMissingDirectory_ThrowsDirectoryNotFoundExceptionAsync()
    {
        ConfigReader reader = new ConfigReader();
        string missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => reader.ReadAsync(missingDirectory, TestContext.Current.CancellationToken));
    }
}
