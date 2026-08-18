using GroupsOnTaskbar.App.Activation;
using GroupsOnTaskbar.App.Services;
using GroupsOnTaskbar.App.ViewModels;
using GroupsOnTaskbar.App.Windows;
using GroupsOnTaskbar.Core.Configuration;
using GroupsOnTaskbar.Core.Launch;
using GroupsOnTaskbar.Core.Models;
using GroupsOnTaskbar.Core.Presentation;
using Microsoft.UI.Xaml;
using Windows.Storage;

namespace GroupsOnTaskbar_App;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private LauncherWindowController? _launcherWindowController;
    private SettingsWindowController? _settingsWindowController;
    private LauncherViewModel? _launcherViewModel;
    private IGroupStore? _groupStore;
    private IAppLogger? _logger;
    private bool _isActivationHandlerRegistered;
    private LauncherConfiguration _currentConfiguration = LauncherConfiguration.Empty;

    public App()
    {
        InitializeComponent();
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
            && _logger is not null)
        {
            return Task.CompletedTask;
        }

        var localFolderPath = ApplicationData.Current.LocalFolder.Path;
        _logger = new LocalFileLogger(localFolderPath);
        _groupStore = new JsonGroupStore(localFolderPath);

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
            LoadLauncherConfigurationAsync);
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
        await LoadLauncherConfigurationAsync();
        _launcherWindowController!.Toggle();
    }

    private async Task LoadLauncherConfigurationAsync(LauncherConfiguration? configuration = null)
    {
        if (_groupStore is null || _launcherViewModel is null)
        {
            return;
        }

        if (configuration is null)
        {
            try
            {
                configuration = await _groupStore.LoadAsync();
            }
            catch (Exception exception)
            {
                if (_logger is not null)
                {
                    await _logger.WriteAsync(nameof(App), exception);
                }

                configuration = LauncherConfiguration.Empty;
            }
        }

        _currentConfiguration = configuration;
        var previousSelectedGroupId = _launcherViewModel.SelectedGroup?.Id;
        var presentation = await Task.Run(
            () => LauncherPresentationBuilder.Create(configuration, previousSelectedGroupId),
            CancellationToken.None);

        await _launcherViewModel.LoadAsync(presentation);
    }
}
