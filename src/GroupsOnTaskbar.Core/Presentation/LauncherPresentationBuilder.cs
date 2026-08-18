using GroupsOnTaskbar.Core.Models;

namespace GroupsOnTaskbar.Core.Presentation;

public static class LauncherPresentationBuilder
{
    public static LauncherPresentation Create(
        LauncherConfiguration configuration,
        Guid? previousSelectedGroupId = null,
        Func<string, bool>? fileExists = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var targetExists = fileExists ?? File.Exists;
        var groups = configuration.Groups
            .OrderBy(group => group.SortOrder)
            .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Id)
            .Select(group => new GroupPresentation(
                group.Id,
                group.Name,
                group.Shortcuts
                    .OrderBy(shortcut => shortcut.SortOrder)
                    .ThenBy(shortcut => shortcut.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(shortcut => shortcut.Id)
                    .Select(shortcut => new ShortcutPresentation(
                        shortcut.Id,
                        shortcut.DisplayName,
                        shortcut.TargetPath,
                        targetExists(shortcut.TargetPath)))
                    .ToArray()))
            .ToArray();

        var selectedGroupId = ResolveSelectedGroupId(groups, previousSelectedGroupId);
        return new LauncherPresentation(groups, selectedGroupId);
    }

    private static Guid? ResolveSelectedGroupId(
        IReadOnlyList<GroupPresentation> groups,
        Guid? previousSelectedGroupId)
    {
        if (groups.Count == 0)
        {
            return null;
        }

        if (previousSelectedGroupId is Guid groupId
            && groups.Any(group => group.Id == groupId))
        {
            return groupId;
        }

        return groups[0].Id;
    }
}
