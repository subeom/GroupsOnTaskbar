using GroupsOnTaskbar.App.ViewModels;
using GroupsOnTaskbar.Core.Configuration;
using GroupsOnTaskbar.Core.Models;
using GroupsOnTaskbar_App;

namespace GroupsOnTaskbar.App.Windows;

public sealed class SettingsWindowController
{
    private readonly LauncherWindowController _launcherWindowController;
    private readonly IGroupStore _groupStore;
    private readonly Func<LauncherConfiguration> _getCurrentConfiguration;
    private readonly Func<LauncherConfiguration, Task> _applySavedConfigurationAsync;
    private SettingsWindow? _settingsWindow;

    public SettingsWindowController(
        LauncherWindowController launcherWindowController,
        IGroupStore groupStore,
        Func<LauncherConfiguration> getCurrentConfiguration,
        Func<LauncherConfiguration, Task> applySavedConfigurationAsync)
    {
        _launcherWindowController = launcherWindowController ?? throw new ArgumentNullException(nameof(launcherWindowController));
        _groupStore = groupStore ?? throw new ArgumentNullException(nameof(groupStore));
        _getCurrentConfiguration = getCurrentConfiguration ?? throw new ArgumentNullException(nameof(getCurrentConfiguration));
        _applySavedConfigurationAsync = applySavedConfigurationAsync ?? throw new ArgumentNullException(nameof(applySavedConfigurationAsync));
    }

    public void Open()
    {
        _launcherWindowController.SuppressDeactivationHide = true;

        try
        {
            _launcherWindowController.Hide();

            if (_settingsWindow is not null)
            {
                _settingsWindow.Activate();
                return;
            }

            var session = new SettingsSession(_getCurrentConfiguration(), _groupStore);
            var window = new SettingsWindow(new SettingsViewModel(session));
            window.SaveCompleted += OnSaveCompleted;
            window.CancelRequested += OnCancelRequested;
            window.Closed += (_, _) => OnWindowClosed(window);

            _settingsWindow = window;
            window.Activate();
        }
        finally
        {
            _launcherWindowController.SuppressDeactivationHide = false;
        }
    }

    private async void OnSaveCompleted(object? sender, SettingsSavedEventArgs e)
    {
        if (sender is not SettingsWindow window || !ReferenceEquals(window, _settingsWindow))
        {
            return;
        }

        await _applySavedConfigurationAsync(e.Configuration);
        window.Close();
    }

    private void OnCancelRequested(object? sender, EventArgs e)
    {
        _settingsWindow?.Close();
    }

    private void OnWindowClosed(SettingsWindow window)
    {
        if (!ReferenceEquals(window, _settingsWindow))
        {
            return;
        }

        window.ViewModel.Cancel();
        _settingsWindow = null;
    }
}
