using GroupsOnTaskbar.App.Activation;
using GroupsOnTaskbar.App.Windows;
using Microsoft.UI.Xaml;

namespace GroupsOnTaskbar_App;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private LauncherWindowController? _launcherWindowController;
    private bool _isActivationHandlerRegistered;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        EnsureLauncherWindow();
        RegisterActivationHandler();
        _launcherWindowController!.Toggle();
    }

    private void EnsureLauncherWindow()
    {
        if (_mainWindow is not null && _launcherWindowController is not null)
        {
            return;
        }

        _mainWindow = new MainWindow();
        _mainWindow.Activate();

        _launcherWindowController = new LauncherWindowController(_mainWindow);
        _mainWindow.AppWindow.Hide();
    }

    private void RegisterActivationHandler()
    {
        if (_isActivationHandlerRegistered)
        {
            return;
        }

        ActivationCoordinator.RegisterActivationHandler(() =>
        {
            EnsureLauncherWindow();
            _launcherWindowController!.Toggle();
        });

        _isActivationHandlerRegistered = true;
    }
}
