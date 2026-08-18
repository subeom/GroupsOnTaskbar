using GroupsOnTaskbar.Core.Models;
using GroupsOnTaskbar.Core.Validation;

namespace GroupsOnTaskbar.Core.Configuration;

public sealed class ConfigurationEditor
{
    private readonly Func<string, bool> _fileExists;
    private readonly int _schemaVersion;
    private AppGroup[] _groups;

    public ConfigurationEditor(LauncherConfiguration configuration, Func<string, bool>? fileExists = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var issues = ConfigurationValidator.Validate(configuration);
        if (issues.Count > 0)
        {
            throw new ArgumentException(
                $"The configuration is invalid: {string.Join("; ", issues.Select(issue => $"{issue.Field}: {issue.Message}"))}",
                nameof(configuration));
        }

        _schemaVersion = configuration.SchemaVersion;
        _fileExists = fileExists ?? File.Exists;
        _groups = CloneGroups(configuration.Groups);
    }

    public LauncherConfiguration Snapshot => new(_schemaVersion, CloneGroups(_groups));

    public Guid AddGroup(string name)
    {
        var groupId = Guid.NewGuid();
        var trimmedName = ValidateAndTrimName(name, ConfigurationValidator.MaximumGroupNameLength, nameof(name), "group");
        AppGroup[] updatedGroups = [.. _groups, new AppGroup(groupId, trimmedName, 0, [])];
        _groups = NormalizeGroups(updatedGroups);
        return groupId;
    }

    public void RenameGroup(Guid groupId, string name)
    {
        var groupIndex = FindGroupIndex(groupId);
        var trimmedName = ValidateAndTrimName(name, ConfigurationValidator.MaximumGroupNameLength, nameof(name), "group");
        var updatedGroups = CloneGroups(_groups);
        updatedGroups[groupIndex] = updatedGroups[groupIndex] with { Name = trimmedName };
        _groups = NormalizeGroups(updatedGroups);
    }

    public void DeleteGroup(Guid groupId)
    {
        var groupIndex = FindGroupIndex(groupId);
        var updatedGroups = _groups.Where((_, index) => index != groupIndex).ToArray();
        _groups = NormalizeGroups(updatedGroups);
    }

    public void MoveGroup(Guid groupId, int offset)
    {
        var groupIndex = FindGroupIndex(groupId);
        var destinationIndex = groupIndex + offset;
        if (destinationIndex < 0 || destinationIndex >= _groups.Length)
        {
            return;
        }

        _groups = NormalizeGroups(MoveItem(_groups, groupIndex, destinationIndex));
    }

    public Guid AddShortcut(Guid groupId, string displayName, string targetPath)
    {
        var groupIndex = FindGroupIndex(groupId);
        var trimmedName = ValidateAndTrimName(
            displayName,
            ConfigurationValidator.MaximumShortcutNameLength,
            nameof(displayName),
            "shortcut");
        var normalizedTargetPath = ValidateAndNormalizeTargetPath(targetPath, _groups[groupIndex].Shortcuts.Select(shortcut => shortcut.TargetPath));
        var shortcutId = Guid.NewGuid();

        var updatedGroups = CloneGroups(_groups);
        AppShortcut[] updatedShortcuts = [.. updatedGroups[groupIndex].Shortcuts, new AppShortcut(shortcutId, trimmedName, normalizedTargetPath, 0)];
        updatedGroups[groupIndex] = updatedGroups[groupIndex] with { Shortcuts = updatedShortcuts };
        _groups = NormalizeGroups(updatedGroups);
        return shortcutId;
    }

    public void UpdateShortcut(Guid groupId, Guid shortcutId, string displayName, string targetPath)
    {
        var groupIndex = FindGroupIndex(groupId);
        var shortcutIndex = FindShortcutIndex(_groups[groupIndex], shortcutId);
        var trimmedName = ValidateAndTrimName(
            displayName,
            ConfigurationValidator.MaximumShortcutNameLength,
            nameof(displayName),
            "shortcut");
        var normalizedTargetPath = ValidateAndNormalizeTargetPath(
            targetPath,
            _groups[groupIndex].Shortcuts
                .Where(shortcut => shortcut.Id != shortcutId)
                .Select(shortcut => shortcut.TargetPath));

        var updatedGroups = CloneGroups(_groups);
        var updatedShortcuts = NormalizeShortcuts(updatedGroups[groupIndex].Shortcuts);
        updatedShortcuts[shortcutIndex] = updatedShortcuts[shortcutIndex] with
        {
            DisplayName = trimmedName,
            TargetPath = normalizedTargetPath
        };

        updatedGroups[groupIndex] = updatedGroups[groupIndex] with { Shortcuts = updatedShortcuts };
        _groups = NormalizeGroups(updatedGroups);
    }

    public void DeleteShortcut(Guid groupId, Guid shortcutId)
    {
        var groupIndex = FindGroupIndex(groupId);
        var shortcutIndex = FindShortcutIndex(_groups[groupIndex], shortcutId);

        var updatedGroups = CloneGroups(_groups);
        updatedGroups[groupIndex] = updatedGroups[groupIndex] with
        {
            Shortcuts = updatedGroups[groupIndex].Shortcuts.Where((_, index) => index != shortcutIndex).ToArray()
        };

        _groups = NormalizeGroups(updatedGroups);
    }

    public void MoveShortcut(Guid groupId, Guid shortcutId, int offset)
    {
        var groupIndex = FindGroupIndex(groupId);
        var shortcutIndex = FindShortcutIndex(_groups[groupIndex], shortcutId);
        var shortcuts = _groups[groupIndex].Shortcuts;
        var destinationIndex = shortcutIndex + offset;
        if (destinationIndex < 0 || destinationIndex >= shortcuts.Length)
        {
            return;
        }

        var updatedGroups = CloneGroups(_groups);
        updatedGroups[groupIndex] = updatedGroups[groupIndex] with
        {
            Shortcuts = MoveItem(updatedGroups[groupIndex].Shortcuts, shortcutIndex, destinationIndex)
        };

        _groups = NormalizeGroups(updatedGroups);
    }

    private string ValidateAndNormalizeTargetPath(string targetPath, IEnumerable<string> existingPaths)
    {
        var issues = ShortcutTargetValidator.ValidateForAdd(targetPath, existingPaths, _fileExists);
        if (issues.Count > 0)
        {
            throw new ArgumentException(
                string.Join("; ", issues.Select(issue => issue.Message)),
                nameof(targetPath));
        }

        return Path.GetFullPath(targetPath);
    }

    private static string ValidateAndTrimName(string value, int maximumLength, string paramName, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"The {label} name is required.", paramName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maximumLength)
        {
            throw new ArgumentException(
                $"The {label} name must be {maximumLength} characters or fewer.",
                paramName);
        }

        return trimmed;
    }

    private static AppGroup[] CloneGroups(IEnumerable<AppGroup> groups)
    {
        return NormalizeGroups(groups.ToArray());
    }

    private static AppGroup[] NormalizeGroups(IReadOnlyList<AppGroup> groups)
    {
        var normalizedGroups = new AppGroup[groups.Count];

        for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            normalizedGroups[groupIndex] = groups[groupIndex] with
            {
                SortOrder = groupIndex,
                Shortcuts = NormalizeShortcuts(groups[groupIndex].Shortcuts)
            };
        }

        return normalizedGroups;
    }

    private static AppShortcut[] NormalizeShortcuts(IReadOnlyList<AppShortcut> shortcuts)
    {
        var normalizedShortcuts = new AppShortcut[shortcuts.Count];

        for (var shortcutIndex = 0; shortcutIndex < shortcuts.Count; shortcutIndex++)
        {
            normalizedShortcuts[shortcutIndex] = shortcuts[shortcutIndex] with { SortOrder = shortcutIndex };
        }

        return normalizedShortcuts;
    }

    private static T[] MoveItem<T>(IReadOnlyList<T> items, int sourceIndex, int destinationIndex)
    {
        var reordered = items.ToList();
        var item = reordered[sourceIndex];
        reordered.RemoveAt(sourceIndex);
        reordered.Insert(destinationIndex, item);
        return reordered.ToArray();
    }

    private int FindGroupIndex(Guid groupId)
    {
        var groupIndex = Array.FindIndex(_groups, group => group.Id == groupId);
        if (groupIndex < 0)
        {
            throw new KeyNotFoundException($"The group '{groupId}' was not found.");
        }

        return groupIndex;
    }

    private static int FindShortcutIndex(AppGroup group, Guid shortcutId)
    {
        var shortcutIndex = Array.FindIndex(group.Shortcuts, shortcut => shortcut.Id == shortcutId);
        if (shortcutIndex < 0)
        {
            throw new KeyNotFoundException($"The shortcut '{shortcutId}' was not found.");
        }

        return shortcutIndex;
    }
}
