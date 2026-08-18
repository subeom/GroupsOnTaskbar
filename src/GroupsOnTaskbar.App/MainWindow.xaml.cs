using GroupsOnTaskbar.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace GroupsOnTaskbar_App;

public sealed partial class MainWindow : Window
{
    public MainWindow(LauncherViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
    }

    public event EventHandler<ShortcutInvokedEventArgs>? ShortcutInvoked;

    public event EventHandler? SettingsRequested;

    public event EventHandler? ExitRequested;

    public LauncherViewModel ViewModel { get; }

    public ContentControl RootHost => RootHostControl;

    public KeyboardAccelerator EscapeKeyboardAccelerator => EscapeKeyboardAcceleratorControl;

    public static Visibility BoolToVisibility(bool value)
        => value ? Visibility.Visible : Visibility.Collapsed;

    public void ShowStatus(string title, string message, InfoBarSeverity severity)
    {
        StatusInfoBar.Title = title;
        StatusInfoBar.Message = message;
        StatusInfoBar.Severity = severity;
        StatusInfoBar.IsOpen = true;
    }

    public void ClearStatus()
    {
        StatusInfoBar.IsOpen = false;
        StatusInfoBar.Title = string.Empty;
        StatusInfoBar.Message = string.Empty;
    }

    private void OnShortcutGridViewItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ShortcutViewModel shortcut || !shortcut.IsAvailable)
        {
            return;
        }

        ShortcutInvoked?.Invoke(this, new ShortcutInvokedEventArgs(shortcut));
    }

    private void OnSettingsButtonClick(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnExitButtonClick(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class ShortcutInvokedEventArgs(ShortcutViewModel shortcut) : EventArgs
{
    public ShortcutViewModel Shortcut { get; } = shortcut;
}
