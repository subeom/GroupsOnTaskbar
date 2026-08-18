using GroupsOnTaskbar.App.Interop;
using GroupsOnTaskbar.Core.Launch;
using GroupsOnTaskbar.Core.Placement;
using GroupsOnTaskbar_App;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;

namespace GroupsOnTaskbar.App.Windows;

public sealed class LauncherWindowController
{
    private const int LogicalWindowWidth = 560;
    private const int LogicalWindowHeight = 460;
    private const int LogicalLauncherGap = 8;

    private readonly MainWindow _window;
    private readonly AppWindow _appWindow;
    private readonly ContentControl _rootHost;
    private readonly IAppLaunchService _appLaunchService;
    private readonly Action? _settingsRequestHandler;

    private bool _isVisible;

    public LauncherWindowController(
        MainWindow window,
        IAppLaunchService appLaunchService,
        Action? settingsRequestHandler = null)
    {
        _window = window;
        _appWindow = window.AppWindow;
        _rootHost = window.RootHost;
        _appLaunchService = appLaunchService;
        _settingsRequestHandler = settingsRequestHandler;

        ConfigureWindow();
        HookEvents();
    }

    public bool SuppressDeactivationHide { get; set; }

    public void Toggle()
    {
        if (_isVisible)
        {
            Hide();
            return;
        }

        ShowAdjacentToTaskbar();
    }

    public void Hide()
    {
        _appWindow.Hide();
        _isVisible = false;
    }

    private void ConfigureWindow()
    {
        _window.ExtendsContentIntoTitleBar = false;
        _window.SetTitleBar(null);

        _appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        if (_appWindow.Presenter is not OverlappedPresenter presenter)
        {
            throw new InvalidOperationException("The launcher window requires an overlapped presenter.");
        }

        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;

        _appWindow.IsShownInSwitchers = false;
    }

    private void HookEvents()
    {
        _window.Activated += OnWindowActivated;
        _window.ShortcutInvoked += OnShortcutInvoked;
        _window.SettingsRequested += OnSettingsRequested;
        _window.ExitRequested += OnExitRequested;
        _rootHost.KeyDown += OnRootHostKeyDown;
        _window.EscapeKeyboardAccelerator.Invoked += OnEscapeKeyboardAcceleratorInvoked;
    }

    private void ShowAdjacentToTaskbar()
    {
        var cursor = NativeMethods.GetCursorPosition();
        var displayArea = DisplayArea.GetFromPoint(cursor, DisplayAreaFallback.Nearest);
        var monitor = ToScreenRect(displayArea.OuterBounds);
        var workArea = ToScreenRect(displayArea.WorkArea);
        var scale = _rootHost.XamlRoot?.RasterizationScale ?? 1.0;
        var windowWidth = Math.Max(1, (int)Math.Round(LogicalWindowWidth * scale, MidpointRounding.AwayFromZero));
        var windowHeight = Math.Max(1, (int)Math.Round(LogicalWindowHeight * scale, MidpointRounding.AwayFromZero));
        var gap = Math.Max(0, (int)Math.Round(LogicalLauncherGap * scale, MidpointRounding.AwayFromZero));
        var placement = WindowPlacementCalculator.Calculate(
            monitor,
            workArea,
            cursor.X,
            cursor.Y,
            windowWidth,
            windowHeight,
            gap);

        _appWindow.MoveAndResize(new RectInt32(placement.X, placement.Y, placement.Width, placement.Height));
        _appWindow.Show();
        _window.Activate();
        _rootHost.Focus(FocusState.Programmatic);
        _isVisible = true;
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated && !SuppressDeactivationHide)
        {
            Hide();
        }
    }

    private void OnShortcutInvoked(object? sender, ShortcutInvokedEventArgs e)
    {
        var result = _appLaunchService.Launch(e.Shortcut.TargetPath);
        if (result.Status == LaunchStatus.Started)
        {
            _window.ClearStatus();
            Hide();
            return;
        }

        _window.ShowStatus(
            "Launch failed",
            result.UserMessage ?? "Windows could not start this shortcut.",
            MapSeverity(result.Status));
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        _settingsRequestHandler?.Invoke();
    }

    private static void OnExitRequested(object? sender, EventArgs e)
    {
        Application.Current.Exit();
    }

    private void OnRootHostKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key != VirtualKey.Escape)
        {
            return;
        }

        Hide();
        args.Handled = true;
    }

    private void OnEscapeKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        Hide();
        args.Handled = true;
    }

    private static InfoBarSeverity MapSeverity(LaunchStatus status)
        => status == LaunchStatus.TargetMissing ? InfoBarSeverity.Warning : InfoBarSeverity.Error;

    private static ScreenRect ToScreenRect(RectInt32 rect)
        => new(rect.X, rect.Y, rect.Width, rect.Height);
}
