using System.Collections.ObjectModel;
using GroupsOnTaskbar.App.Services;
using GroupsOnTaskbar.Core.Presentation;

namespace GroupsOnTaskbar.App.ViewModels;

public sealed class LauncherViewModel(ShortcutIconService shortcutIconService) : ObservableObject
{
    private static readonly ObservableCollection<ShortcutViewModel> EmptyShortcuts = [];

    private readonly ShortcutIconService _shortcutIconService = shortcutIconService;
    private GroupViewModel? _selectedGroup;

    public ObservableCollection<GroupViewModel> Groups { get; } = [];

    public GroupViewModel? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (!SetProperty(ref _selectedGroup, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasShortcuts));
            OnPropertyChanged(nameof(HasNoShortcuts));
            OnPropertyChanged(nameof(VisibleShortcuts));
        }
    }

    public bool HasShortcuts => SelectedGroup?.Shortcuts.Count > 0;

    public bool HasNoShortcuts => !HasShortcuts;

    public ObservableCollection<ShortcutViewModel> VisibleShortcuts => SelectedGroup?.Shortcuts ?? EmptyShortcuts;

    public async Task LoadAsync(LauncherPresentation presentation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(presentation);

        var groupViewModels = presentation.Groups
            .Select(group => new GroupViewModel(
                group.Id,
                group.Name,
                group.Shortcuts.Select(shortcut => new ShortcutViewModel(
                    shortcut.Id,
                    shortcut.DisplayName,
                    shortcut.TargetPath,
                    shortcut.IsAvailable))))
            .ToArray();

        Groups.Clear();
        foreach (var groupViewModel in groupViewModels)
        {
            Groups.Add(groupViewModel);
        }

        SelectedGroup = Groups.FirstOrDefault(group => group.Id == presentation.SelectedGroupId);

        var iconTasks = groupViewModels
            .SelectMany(group => group.Shortcuts)
            .Select(shortcut => LoadIconAsync(shortcut, cancellationToken));

        await Task.WhenAll(iconTasks);
    }

    private async Task LoadIconAsync(ShortcutViewModel shortcut, CancellationToken cancellationToken)
    {
        if (!shortcut.IsAvailable)
        {
            return;
        }

        var lastWriteTimeUtc = new DateTimeOffset(File.GetLastWriteTimeUtc(shortcut.TargetPath));
        shortcut.Icon = await _shortcutIconService.GetIconAsync(shortcut.TargetPath, lastWriteTimeUtc, cancellationToken);
    }
}
