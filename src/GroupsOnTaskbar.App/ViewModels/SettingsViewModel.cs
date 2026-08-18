using System.Collections.ObjectModel;
using GroupsOnTaskbar.Core.Configuration;
using GroupsOnTaskbar.Core.Models;

namespace GroupsOnTaskbar.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly SettingsSession _session;
    private SettingsGroupItemViewModel? _selectedGroup;
    private SettingsShortcutItemViewModel? _selectedShortcut;
    private string _groupNameInput = string.Empty;
    private string _shortcutDisplayNameInput = string.Empty;
    private string _selectedShortcutTargetPath = string.Empty;
    private string? _errorMessage;

    public SettingsViewModel(SettingsSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ReloadFromSession();
    }

    public ObservableCollection<SettingsGroupItemViewModel> Groups { get; } = [];

    public ObservableCollection<SettingsShortcutItemViewModel> Shortcuts { get; } = [];

    public SettingsGroupItemViewModel? SelectedGroup
    {
        get => _selectedGroup;
        set => SetSelectedGroup(value, null);
    }

    public SettingsShortcutItemViewModel? SelectedShortcut
    {
        get => _selectedShortcut;
        set => SetSelectedShortcut(value);
    }

    public string GroupNameInput
    {
        get => _groupNameInput;
        set
        {
            if (!SetProperty(ref _groupNameInput, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanApplyGroupName));
        }
    }

    public string ShortcutDisplayNameInput
    {
        get => _shortcutDisplayNameInput;
        set
        {
            if (!SetProperty(ref _shortcutDisplayNameInput, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanApplyShortcutName));
        }
    }

    public string SelectedShortcutTargetPath
    {
        get => _selectedShortcutTargetPath;
        private set => SetProperty(ref _selectedShortcutTargetPath, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (!SetProperty(ref _errorMessage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasErrorMessage));
        }
    }

    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool CanApplyGroupName => SelectedGroup is not null && !string.IsNullOrWhiteSpace(GroupNameInput);

    public bool CanDeleteGroup => SelectedGroup is not null;

    public bool CanMoveGroupUp => SelectedGroup is not null && SelectedGroup.SortOrder > 0;

    public bool CanMoveGroupDown => SelectedGroup is not null && SelectedGroup.SortOrder < Groups.Count - 1;

    public bool CanAddShortcut => SelectedGroup is not null;

    public bool CanApplyShortcutName => SelectedShortcut is not null && !string.IsNullOrWhiteSpace(ShortcutDisplayNameInput);

    public bool CanDeleteShortcut => SelectedShortcut is not null;

    public bool CanMoveShortcutUp => SelectedShortcut is not null && SelectedShortcut.SortOrder > 0;

    public bool CanMoveShortcutDown => SelectedShortcut is not null && SelectedShortcut.SortOrder < Shortcuts.Count - 1;

    public void AddGroup()
    {
        var addedGroupId = _session.AddGroup(GroupNameInput);
        SyncSessionError();
        if (addedGroupId is null)
        {
            return;
        }

        ReloadFromSession(addedGroupId, null);
    }

    public void ApplyGroupName()
    {
        if (SelectedGroup is null)
        {
            SetSelectionError("Select a group to rename.");
            return;
        }

        if (!_session.RenameGroup(SelectedGroup.Id, GroupNameInput))
        {
            SyncSessionError();
            return;
        }

        SyncSessionError();
        ReloadFromSession(SelectedGroup.Id, SelectedShortcut?.Id);
    }

    public void DeleteSelectedGroup()
    {
        if (SelectedGroup is null)
        {
            SetSelectionError("Select a group to delete.");
            return;
        }

        if (!_session.DeleteGroup(SelectedGroup.Id))
        {
            SyncSessionError();
            return;
        }

        SyncSessionError();
        ReloadFromSession();
    }

    public void MoveSelectedGroup(int offset)
    {
        if (SelectedGroup is null)
        {
            SetSelectionError("Select a group to move.");
            return;
        }

        if (!_session.MoveGroup(SelectedGroup.Id, offset))
        {
            SyncSessionError();
            return;
        }

        SyncSessionError();
        ReloadFromSession(SelectedGroup.Id, SelectedShortcut?.Id);
    }

    public void AddShortcut(string displayName, string targetPath)
    {
        if (SelectedGroup is null)
        {
            SetSelectionError("Select a group before adding an app.");
            return;
        }

        var addedShortcutId = _session.AddShortcut(SelectedGroup.Id, displayName, targetPath);
        SyncSessionError();
        if (addedShortcutId is null)
        {
            return;
        }

        ReloadFromSession(SelectedGroup.Id, addedShortcutId);
    }

    public void ApplyShortcutName()
    {
        if (SelectedGroup is null || SelectedShortcut is null)
        {
            SetSelectionError("Select an app to rename.");
            return;
        }

        if (!_session.UpdateShortcut(
                SelectedGroup.Id,
                SelectedShortcut.Id,
                ShortcutDisplayNameInput,
                SelectedShortcut.TargetPath))
        {
            SyncSessionError();
            return;
        }

        SyncSessionError();
        ReloadFromSession(SelectedGroup.Id, SelectedShortcut.Id);
    }

    public void DeleteSelectedShortcut()
    {
        if (SelectedGroup is null || SelectedShortcut is null)
        {
            SetSelectionError("Select an app to remove.");
            return;
        }

        if (!_session.DeleteShortcut(SelectedGroup.Id, SelectedShortcut.Id))
        {
            SyncSessionError();
            return;
        }

        SyncSessionError();
        ReloadFromSession(SelectedGroup.Id, null);
    }

    public void MoveSelectedShortcut(int offset)
    {
        if (SelectedGroup is null || SelectedShortcut is null)
        {
            SetSelectionError("Select an app to move.");
            return;
        }

        if (!_session.MoveShortcut(SelectedGroup.Id, SelectedShortcut.Id, offset))
        {
            SyncSessionError();
            return;
        }

        SyncSessionError();
        ReloadFromSession(SelectedGroup.Id, SelectedShortcut.Id);
    }

    public async Task<LauncherConfiguration> SaveAsync(CancellationToken cancellationToken = default)
    {
        var savedConfiguration = await _session.SaveAsync(cancellationToken);
        SyncSessionError();
        return savedConfiguration;
    }

    public void Cancel()
    {
        _session.Cancel();
        SyncSessionError();
        ReloadFromSession();
    }

    public void ClearErrorMessage()
    {
        ErrorMessage = null;
    }

    private void ReloadFromSession(Guid? preferredGroupId = null, Guid? preferredShortcutId = null)
    {
        var snapshot = _session.Snapshot;

        ReplaceCollection(
            Groups,
            snapshot.Groups.Select(group => new SettingsGroupItemViewModel(group.Id, group.Name, group.SortOrder)));

        var selectedGroup = Groups.FirstOrDefault(group => group.Id == preferredGroupId)
            ?? Groups.FirstOrDefault(group => group.Id == SelectedGroup?.Id)
            ?? Groups.FirstOrDefault();

        SetSelectedGroup(selectedGroup, preferredShortcutId);
    }

    private void SetSelectedGroup(SettingsGroupItemViewModel? group, Guid? preferredShortcutId)
    {
        SetProperty(ref _selectedGroup, group, nameof(SelectedGroup));
        GroupNameInput = group?.Name ?? string.Empty;

        var shortcuts = _session.Snapshot.Groups
            .FirstOrDefault(snapshotGroup => snapshotGroup.Id == group?.Id)?
            .Shortcuts
            .Select(shortcut => new SettingsShortcutItemViewModel(
                shortcut.Id,
                shortcut.DisplayName,
                shortcut.TargetPath,
                shortcut.SortOrder))
            ?? [];

        ReplaceCollection(Shortcuts, shortcuts);

        var selectedShortcut = Shortcuts.FirstOrDefault(shortcut => shortcut.Id == preferredShortcutId)
            ?? Shortcuts.FirstOrDefault(shortcut => shortcut.Id == SelectedShortcut?.Id)
            ?? Shortcuts.FirstOrDefault();

        SetSelectedShortcut(selectedShortcut);
        RaiseGroupCommandStateChanged();
        RaiseShortcutCommandStateChanged();
    }

    private void SetSelectedShortcut(SettingsShortcutItemViewModel? shortcut)
    {
        SetProperty(ref _selectedShortcut, shortcut, nameof(SelectedShortcut));
        ShortcutDisplayNameInput = shortcut?.DisplayName ?? string.Empty;
        SelectedShortcutTargetPath = shortcut?.TargetPath ?? string.Empty;
        RaiseShortcutCommandStateChanged();
    }

    private void SetSelectionError(string message)
    {
        ErrorMessage = message;
    }

    private void SyncSessionError()
    {
        ErrorMessage = _session.ErrorMessage;
    }

    private void RaiseGroupCommandStateChanged()
    {
        OnPropertyChanged(nameof(CanApplyGroupName));
        OnPropertyChanged(nameof(CanDeleteGroup));
        OnPropertyChanged(nameof(CanMoveGroupUp));
        OnPropertyChanged(nameof(CanMoveGroupDown));
        OnPropertyChanged(nameof(CanAddShortcut));
    }

    private void RaiseShortcutCommandStateChanged()
    {
        OnPropertyChanged(nameof(CanApplyShortcutName));
        OnPropertyChanged(nameof(CanDeleteShortcut));
        OnPropertyChanged(nameof(CanMoveShortcutUp));
        OnPropertyChanged(nameof(CanMoveShortcutDown));
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();

        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}

public sealed class SettingsGroupItemViewModel(Guid id, string name, int sortOrder)
{
    public Guid Id { get; } = id;

    public string Name { get; } = name;

    public int SortOrder { get; } = sortOrder;
}

public sealed class SettingsShortcutItemViewModel(Guid id, string displayName, string targetPath, int sortOrder)
{
    public Guid Id { get; } = id;

    public string DisplayName { get; } = displayName;

    public string TargetPath { get; } = targetPath;

    public int SortOrder { get; } = sortOrder;
}
