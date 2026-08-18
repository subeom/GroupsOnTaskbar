using GroupsOnTaskbar.Core.Models;

namespace GroupsOnTaskbar.Core.Configuration;

public sealed class StartupConfigurationLoader(IGroupStore groupStore)
{
    private readonly IGroupStore _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));

    public async Task<StartupConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var configuration = await _groupStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            return StartupConfigurationLoadResult.Loaded(configuration);
        }
        catch (CorruptConfigurationException exception)
        {
            return StartupConfigurationLoadResult.RequiresDecision(
                new StartupConfigurationRecovery(
                    _groupStore,
                    exception.SettingsPath,
                    exception.Reasons));
        }
    }
}

public enum StartupConfigurationLoadStatus
{
    Loaded,
    RequiresDecision
}

public sealed class StartupConfigurationLoadResult
{
    private StartupConfigurationLoadResult(
        StartupConfigurationLoadStatus status,
        LauncherConfiguration? configuration,
        StartupConfigurationRecovery? recovery)
    {
        Status = status;
        Configuration = configuration;
        Recovery = recovery;
    }

    public StartupConfigurationLoadStatus Status { get; }

    public LauncherConfiguration? Configuration { get; }

    public StartupConfigurationRecovery? Recovery { get; }

    public static StartupConfigurationLoadResult Loaded(LauncherConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new StartupConfigurationLoadResult(StartupConfigurationLoadStatus.Loaded, configuration, null);
    }

    public static StartupConfigurationLoadResult RequiresDecision(StartupConfigurationRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        return new StartupConfigurationLoadResult(StartupConfigurationLoadStatus.RequiresDecision, null, recovery);
    }
}

public enum StartupConfigurationRecoveryChoice
{
    BackUpAndReset,
    Exit
}

public sealed class StartupConfigurationRecovery
{
    private readonly IGroupStore _groupStore;

    internal StartupConfigurationRecovery(
        IGroupStore groupStore,
        string settingsPath,
        IReadOnlyList<string> reasons)
    {
        _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));
        SettingsPath = settingsPath ?? throw new ArgumentNullException(nameof(settingsPath));
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    public string SettingsPath { get; }

    public IReadOnlyList<string> Reasons { get; }

    public async Task<StartupConfigurationRecoveryResult> BackUpAndResetAsync(CancellationToken cancellationToken = default)
    {
        var backupPath = await _groupStore.BackUpAndResetAsync(cancellationToken).ConfigureAwait(false);
        return StartupConfigurationRecoveryResult.FromBackUpAndReset(LauncherConfiguration.Empty, backupPath);
    }

    public StartupConfigurationRecoveryResult Exit()
        => StartupConfigurationRecoveryResult.FromExit();
}

public sealed class StartupConfigurationRecoveryResult
{
    private StartupConfigurationRecoveryResult(
        StartupConfigurationRecoveryChoice choice,
        LauncherConfiguration? configuration,
        string? backupPath)
    {
        Choice = choice;
        Configuration = configuration;
        BackupPath = backupPath;
    }

    public StartupConfigurationRecoveryChoice Choice { get; }

    public LauncherConfiguration? Configuration { get; }

    public string? BackupPath { get; }

    public static StartupConfigurationRecoveryResult FromBackUpAndReset(
        LauncherConfiguration configuration,
        string backupPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return new StartupConfigurationRecoveryResult(
            StartupConfigurationRecoveryChoice.BackUpAndReset,
            configuration,
            backupPath);
    }

    public static StartupConfigurationRecoveryResult FromExit()
        => new(StartupConfigurationRecoveryChoice.Exit, null, null);
}
