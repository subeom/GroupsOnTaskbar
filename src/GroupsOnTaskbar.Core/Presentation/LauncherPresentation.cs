namespace GroupsOnTaskbar.Core.Presentation;

public sealed record LauncherPresentation(
    IReadOnlyList<GroupPresentation> Groups,
    Guid? SelectedGroupId)
{
    public GroupPresentation? SelectedGroup => Groups.FirstOrDefault(group => group.Id == SelectedGroupId);

    public bool HasShortcuts => SelectedGroup?.Shortcuts.Count > 0;
}

public sealed record GroupPresentation(
    Guid Id,
    string Name,
    IReadOnlyList<ShortcutPresentation> Shortcuts);

public sealed record ShortcutPresentation(
    Guid Id,
    string DisplayName,
    string TargetPath,
    bool IsAvailable);
