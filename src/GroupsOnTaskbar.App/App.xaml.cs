using GroupsOnTaskbar.App.Activation;
using GroupsOnTaskbar.App.Services;
using GroupsOnTaskbar.App.ViewModels;
using GroupsOnTaskbar.App.Windows;
using GroupsOnTaskbar.Core.Configuration;
using GroupsOnTaskbar.Core.Launch;
using GroupsOnTaskbar.Core.Logging;
using GroupsOnTaskbar.Core.Models;
using GroupsOnTaskbar.Core.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;

namespace GroupsOnTaskbar_App;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private LauncherWindowController? _launcherWindowController;
    private SettingsWindowController? _settingsWindowController;
    private LauncherViewModel? _launcherViewModel;
    private IGroupStore? _groupStore;
    private StartupConfigurationLoader? _startupConfigurationLoader;
    private IAppLogger? _logger;
    private bool _isActivationHandlerRegistered;
    private LauncherConfiguration _currentConfiguration = LauncherConfiguration.Empty;

    public App()
    {
        InitializeComponent();

        // The launcher hides its only window between activations, so the app must
        // not shut down when the last window disappears.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await EnsureLauncherWindowAsync();
        RegisterActivationHandler();
        await ToggleLauncherAsync();
    }

    private Task EnsureLauncherWindowAsync()
    {
        if (_mainWindow is not null
            && _launcherWindowController is not null
            && _launcherViewModel is not null
            && _groupStore is not null
            && _startupConfigurationLoader is not null
            && _logger is not null)
        {
            return Task.CompletedTask;
        }

        var localFolderPath = ApplicationData.Current.LocalFolder.Path;
        _logger = new LocalFileLogger(localFolderPath);
        _groupStore = new JsonGroupStore(localFolderPath);
        _startupConfigurationLoader = new StartupConfigurationLoader(_groupStore);

        var iconService = new ShortcutIconService(localFolderPath, _logger);
        _launcherViewModel = new LauncherViewModel(iconService);
        var launchService = new ShellAppLaunchService(new ProcessShellExecutor());

        _mainWindow = new MainWindow(_launcherViewModel);
        _mainWindow.Activate();

        _launcherWindowController = new LauncherWindowController(
            _mainWindow,
            launchService,
            () => _settingsWindowController?.Open());
        _settingsWindowController = new SettingsWindowController(
            _launcherWindowController,
            _groupStore,
            () => _currentConfiguration,
            async configuration => _ = await LoadLauncherConfigurationAsync(configuration));
        _mainWindow.AppWindow.Hide();

        return Task.CompletedTask;
    }

    private void RegisterActivationHandler()
    {
        if (_isActivationHandlerRegistered)
        {
            return;
        }

        ActivationCoordinator.RegisterActivationHandler(() => _ = ToggleLauncherAsync());
        _isActivationHandlerRegistered = true;
    }

    private async Task ToggleLauncherAsync()
    {
        await EnsureLauncherWindowAsync();
        if (!await LoadLauncherConfigurationAsync())
        {
            return;
        }

        _launcherWindowController!.Toggle();
    }

    private async Task<bool> LoadLauncherConfigurationAsync(LauncherConfiguration? configuration = null)
    {
        if (_launcherViewModel is null || _startupConfigurationLoader is null)
        {
            return false;
        }

        if (configuration is null)
        {
            try
            {
                var loadResult = await _startupConfigurationLoader.LoadAsync();
                if (loadResult.Status == StartupConfigurationLoadStatus.Loaded)
                {
                    configuration = loadResult.Configuration ?? LauncherConfiguration.Empty;
                }
                else if (loadResult.Recovery is not null)
                {
                    StartupConfigurationRecoveryResult recoveryResult;

                    try
                    {
                        recoveryResult = await ShowCorruptSettingsDialogAsync(loadResult.Recovery);
                    }
                    catch (Exception exception)
                    {
                        if (_logger is not null)
                        {
                            await _logger.WriteAsync(nameof(App), exception);
                        }

                        Application.Current.Exit();
                        return false;
                    }

                    if (recoveryResult.Choice == StartupConfigurationRecoveryChoice.Exit)
                    {
                        Application.Current.Exit();
                        return false;
                    }

                    configuration = recoveryResult.Configuration ?? LauncherConfiguration.Empty;

                    if (_logger is not null)
                    {
                        await _logger.WriteAsync(
                            nameof(App),
                            $"Backed up corrupt settings to '{recoveryResult.BackupPath}'.");
                    }
                }
            }
            catch (Exception exception)
            {
                if (_logger is not null)
                {
                    await _logger.WriteAsync(nameof(App), exception);
                }

                await ShowStartupLoadFailureDialogAsync(exception);
                Application.Current.Exit();
                return false;
            }
        }

        configuration ??= LauncherConfiguration.Empty;
        _currentConfiguration = configuration;
        var previousSelectedGroupId = _launcherViewModel.SelectedGroup?.Id;
        var presentation = await Task.Run(
            () => LauncherPresentationBuilder.Create(configuration, previousSelectedGroupId),
            CancellationToken.None);

        await _launcherViewModel.LoadAsync(presentation);
        return true;
    }

    private async Task<StartupConfigurationRecoveryResult> ShowCorruptSettingsDialogAsync(
        StartupConfigurationRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);

        if (_mainWindow is null)
        {
            return recovery.Exit();
        }

        _mainWindow.AppWindow.Show();
        _mainWindow.Activate();

        var reasonsText = string.Join(
            Environment.NewLine,
            recovery.Reasons.Select(reason => $"• {reason}"));

        var dialog = new ContentDialog
        {
            Title = "Settings file cannot be read",
            PrimaryButtonText = "Back up and reset",
            CloseButtonText = "Exit",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = _mainWindow.RootHost.XamlRoot,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Taskbar Groups could not read its settings file. You can back up the unreadable file and start with empty settings, or exit the app.",
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    new TextBlock
                    {
                        Text = $"Path: {recovery.SettingsPath}",
                        TextWrapping = TextWrapping.WrapWholeWords
                    },
                    new TextBlock
                    {
                        Text = reasonsText,
                        TextWrapping = TextWrapping.WrapWholeWords
                    }
                }
            }
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary
            ? await recovery.BackUpAndResetAsync()
            : recovery.Exit();
    }

    private async Task ShowStartupLoadFailureDialogAsync(Exception exception)
    {
        if (_mainWindow is null)
        {
            return;
        }

        try
        {
            _mainWindow.AppWindow.Show();
            _mainWindow.Activate();

            var dialog = new ContentDialog
            {
                Title = "Settings could not be opened",
                CloseButtonText = "Exit",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = _mainWindow.RootHost.XamlRoot,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Taskbar Groups could not open its settings file, so it stopped instead of replacing your saved groups. Check that the file is accessible and start the app again.",
                            TextWrapping = TextWrapping.WrapWholeWords
                        },
                        new TextBlock
                        {
                            Text = exception.Message,
                            TextWrapping = TextWrapping.WrapWholeWords
                        }
                    }
                }
            };

            await dialog.ShowAsync();
        }
        catch (Exception dialogException)
        {
            if (_logger is not null)
            {
                await _logger.WriteAsync(nameof(App), dialogException);
            }
        }
    }
}
