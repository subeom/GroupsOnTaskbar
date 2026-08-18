using GroupsOnTaskbar.Core.Configuration;
using GroupsOnTaskbar.Core.Models;

namespace GroupsOnTaskbar.Tests;

public sealed class StartupConfigurationLoaderTests
{
    [Fact]
    public async Task LoadAsync_WhenStoreLoadsSuccessfully_ReturnsConfiguration()
    {
        var expectedConfiguration = new LauncherConfiguration(
            LauncherConfiguration.CurrentSchemaVersion,
            [new AppGroup(Guid.NewGuid(), "Utilities", 0, [])]);
        var store = new StubGroupStore
        {
            LoadResult = expectedConfiguration
        };
        var loader = new StartupConfigurationLoader(store);

        var result = await loader.LoadAsync();

        Assert.Equal(StartupConfigurationLoadStatus.Loaded, result.Status);
        Assert.Same(expectedConfiguration, result.Configuration);
        Assert.Null(result.Recovery);
        Assert.Equal(1, store.LoadCallCount);
        Assert.Equal(0, store.BackUpAndResetCallCount);
    }

    [Fact]
    public async Task BackUpAndResetAsync_WhenConfigurationIsCorrupt_ReturnsEmptyConfigurationAndBackupPath()
    {
        var store = new StubGroupStore
        {
            LoadException = new CorruptConfigurationException(
                @"C:\Users\Test\AppData\Local\TaskbarGroups\settings-v1.json",
                ["document: Unexpected end of JSON input."]),
            BackupPath = @"C:\Users\Test\AppData\Local\TaskbarGroups\settings-v1.20260818-131500.corrupt.json"
        };
        var loader = new StartupConfigurationLoader(store);

        var result = await loader.LoadAsync();

        Assert.Equal(StartupConfigurationLoadStatus.RequiresDecision, result.Status);
        Assert.NotNull(result.Recovery);
        Assert.Equal(0, store.BackUpAndResetCallCount);

        var recoveryResult = await result.Recovery!.BackUpAndResetAsync();

        Assert.Equal(StartupConfigurationRecoveryChoice.BackUpAndReset, recoveryResult.Choice);
        Assert.Equivalent(LauncherConfiguration.Empty, recoveryResult.Configuration);
        Assert.Equal(store.BackupPath, recoveryResult.BackupPath);
        Assert.Equal(1, store.BackUpAndResetCallCount);
    }

    [Fact]
    public async Task Exit_WhenConfigurationIsCorrupt_DoesNotResetStore()
    {
        var store = new StubGroupStore
        {
            LoadException = new CorruptConfigurationException(
                @"C:\Users\Test\AppData\Local\TaskbarGroups\settings-v1.json",
                ["groups[0].name: The group name is required."])
        };
        var loader = new StartupConfigurationLoader(store);

        var result = await loader.LoadAsync();

        Assert.Equal(StartupConfigurationLoadStatus.RequiresDecision, result.Status);
        Assert.NotNull(result.Recovery);
        Assert.Equal(0, store.BackUpAndResetCallCount);

        var exitResult = result.Recovery!.Exit();

        Assert.Equal(StartupConfigurationRecoveryChoice.Exit, exitResult.Choice);
        Assert.Null(exitResult.Configuration);
        Assert.Null(exitResult.BackupPath);
        Assert.Equal(0, store.BackUpAndResetCallCount);
    }

    private sealed class StubGroupStore : IGroupStore
    {
        public LauncherConfiguration LoadResult { get; init; } = LauncherConfiguration.Empty;

        public Exception? LoadException { get; init; }

        public string BackupPath { get; init; } = string.Empty;

        public int LoadCallCount { get; private set; }

        public int BackUpAndResetCallCount { get; private set; }

        public Task<string> BackUpAndResetAsync(CancellationToken cancellationToken = default)
        {
            BackUpAndResetCallCount++;
            return Task.FromResult(BackupPath);
        }

        public Task<LauncherConfiguration> LoadAsync(CancellationToken cancellationToken = default)
        {
            LoadCallCount++;

            if (LoadException is not null)
            {
                throw LoadException;
            }

            return Task.FromResult(LoadResult);
        }

        public Task SaveAsync(LauncherConfiguration configuration, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
