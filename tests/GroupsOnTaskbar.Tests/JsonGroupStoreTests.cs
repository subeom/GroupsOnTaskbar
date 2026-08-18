using GroupsOnTaskbar.Core.Configuration;
using GroupsOnTaskbar.Core.Models;

namespace GroupsOnTaskbar.Tests;

public sealed class JsonGroupStoreTests
{
    [Fact]
    public async Task LoadAsync_WhenSettingsFileDoesNotExist_ReturnsEmptyConfiguration()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new JsonGroupStore(temporaryDirectory.Path);

        var configuration = await store.LoadAsync();

        Assert.Equivalent(LauncherConfiguration.Empty, configuration);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsConfiguration()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new JsonGroupStore(temporaryDirectory.Path);
        var configuration = CreateConfiguration();

        await store.SaveAsync(configuration);

        var loaded = await store.LoadAsync();

        Assert.Equivalent(configuration, loaded);
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsCorrupt_ThrowsAndBackupAndResetRestoresEmptyConfiguration()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var settingsPath = Path.Combine(temporaryDirectory.Path, JsonGroupStore.SettingsFileName);
        await File.WriteAllTextAsync(settingsPath, "{ not-valid-json");

        var store = new JsonGroupStore(
            temporaryDirectory.Path,
            new FixedTimeProvider(new DateTimeOffset(2026, 08, 18, 11, 52, 08, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<CorruptConfigurationException>(() => store.LoadAsync());

        Assert.Equal(settingsPath, exception.SettingsPath);
        Assert.NotEmpty(exception.Reasons);

        var backupPath = await store.BackUpAndResetAsync();

        Assert.Equal(
            Path.Combine(temporaryDirectory.Path, "settings-v1.20260818-115208.corrupt.json"),
            backupPath);
        Assert.True(File.Exists(backupPath));

        var recovered = await store.LoadAsync();

        Assert.Equivalent(LauncherConfiguration.Empty, recovered);
    }

    [Fact]
    public async Task SaveAsync_WhenConfigurationIsInvalid_ThrowsArgumentException()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new JsonGroupStore(temporaryDirectory.Path);
        var invalidConfiguration = new LauncherConfiguration(
            LauncherConfiguration.CurrentSchemaVersion,
            [
                new AppGroup(
                    Guid.NewGuid(),
                    "Utilities",
                    0,
                    [
                        new AppShortcut(Guid.NewGuid(), "Broken", "relative.exe", 0)
                    ])
            ]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(invalidConfiguration));

        Assert.Equal("configuration", exception.ParamName);
    }

    [Fact]
    public async Task SaveAsync_WhenSuccessful_DoesNotLeaveTemporaryFilesBehind()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var store = new JsonGroupStore(temporaryDirectory.Path);

        await store.SaveAsync(CreateConfiguration());

        Assert.Empty(Directory.GetFiles(temporaryDirectory.Path, "*.tmp"));
    }

    private static LauncherConfiguration CreateConfiguration()
    {
        return new LauncherConfiguration(
            LauncherConfiguration.CurrentSchemaVersion,
            [
                new AppGroup(
                    Guid.Parse("5B984737-D92A-47C6-BBFE-D958E0A8C5D7"),
                    "Utilities",
                    0,
                    [
                        new AppShortcut(
                            Guid.Parse("9E3ADFD1-D692-4D8A-867A-CF55F8AD6BB9"),
                            "Tool",
                            @"C:\Apps\Tool.exe",
                            0)
                    ])
            ]);
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "GroupsOnTaskbar.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => value;
}
