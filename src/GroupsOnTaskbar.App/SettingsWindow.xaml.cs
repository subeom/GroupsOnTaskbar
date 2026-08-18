using System.ComponentModel;
using System.Runtime.InteropServices;
using GroupsOnTaskbar.App.ViewModels;
using GroupsOnTaskbar.Core.Models;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace GroupsOnTaskbar_App;

public sealed partial class SettingsWindow : Window
{
    private const int LogicalWindowWidth = 1040;
    private const int LogicalWindowHeight = 760;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    private bool _isSaving;
    private bool _isSyncingSelection;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();

        Title = "Taskbar Groups Settings";
        ConfigureWindow();

        SettingsRoot.Loaded += OnSettingsRootLoaded;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnSettingsRootLoaded(object sender, RoutedEventArgs e)
    {
        GroupsListView.ItemsSource = ViewModel.Groups;
        ShortcutsListView.ItemsSource = ViewModel.Shortcuts;
        SyncSelectionFromViewModel();
    }

    public event EventHandler? CancelRequested;

    public event EventHandler<SettingsSavedEventArgs>? SaveCompleted;

    public SettingsViewModel ViewModel { get; }

    private void ConfigureWindow()
    {
        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        var hwnd = WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32(
            (int)Math.Round(LogicalWindowWidth * scale, MidpointRounding.AwayFromZero),
            (int)Math.Round(LogicalWindowHeight * scale, MidpointRounding.AwayFromZero)));
    }

    private void OnErrorInfoBarCloseButtonClick(InfoBar sender, object args)
    {
        ViewModel.ClearErrorMessage();
    }

    /// <summary>
    /// Pushes in-flight TextBox text into the view model. Two-way x:Bind updates
    /// on focus loss, and a button click does not always move focus first.
    /// </summary>
    private void CommitPendingTextInput()
    {
        ViewModel.GroupNameInput = GroupNameTextBox.Text;
        ViewModel.ShortcutDisplayNameInput = ShortcutDisplayNameTextBox.Text;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsViewModel.SelectedGroup)
            or nameof(SettingsViewModel.SelectedShortcut))
        {
            SyncSelectionFromViewModel();
        }
    }

    private void SyncSelectionFromViewModel()
    {
        _isSyncingSelection = true;

        try
        {
            GroupsListView.SelectedItem = ViewModel.SelectedGroup;
            ShortcutsListView.SelectedItem = ViewModel.SelectedShortcut;
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void OnGroupsListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        ViewModel.SelectedGroup = GroupsListView.SelectedItem as SettingsGroupItemViewModel;
    }

    private void OnShortcutsListViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        ViewModel.SelectedShortcut = ShortcutsListView.SelectedItem as SettingsShortcutItemViewModel;
    }

    private void OnAddGroupButtonClick(object sender, RoutedEventArgs e)
    {
        CommitPendingTextInput();
        ViewModel.AddGroup();
    }

    private void OnRenameGroupButtonClick(object sender, RoutedEventArgs e)
    {
        CommitPendingTextInput();
        ViewModel.ApplyGroupName();
    }

    private void OnDeleteGroupButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.DeleteSelectedGroup();
    }

    private void OnMoveGroupUpButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.MoveSelectedGroup(-1);
    }

    private void OnMoveGroupDownButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.MoveSelectedGroup(1);
    }

    private async void OnAddShortcutButtonClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".lnk");

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        ViewModel.AddShortcut(Path.GetFileNameWithoutExtension(file.Path), file.Path);
    }

    private void OnRenameShortcutButtonClick(object sender, RoutedEventArgs e)
    {
        CommitPendingTextInput();
        ViewModel.ApplyShortcutName();
    }

    private void OnDeleteShortcutButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.DeleteSelectedShortcut();
    }

    private void OnMoveShortcutUpButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.MoveSelectedShortcut(-1);
    }

    private void OnMoveShortcutDownButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.MoveSelectedShortcut(1);
    }

    private async void OnSaveButtonClick(object sender, RoutedEventArgs e)
    {
        if (_isSaving)
        {
            return;
        }

        try
        {
            _isSaving = true;
            var savedConfiguration = await ViewModel.SaveAsync();
            SaveCompleted?.Invoke(this, new SettingsSavedEventArgs(savedConfiguration));
        }
        catch (Exception exception)
        {
            ViewModel.SetErrorMessage($"Settings could not be saved. {exception.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void OnCancelButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Cancel();
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class SettingsSavedEventArgs(LauncherConfiguration configuration) : EventArgs
{
    public LauncherConfiguration Configuration { get; } = configuration;
}
