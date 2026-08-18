using System.Collections.Generic;
using System.IO;
using GroupsOnTaskbar.Core.Models;

namespace GroupsOnTaskbar.Core.Validation;

public static class ConfigurationValidator
{
    public const int MaximumGroupNameLength = 60;
    public const int MaximumShortcutNameLength = 100;

    public static IReadOnlyList<ValidationIssue> Validate(LauncherConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var issues = new List<ValidationIssue>();

        if (configuration.SchemaVersion != LauncherConfiguration.CurrentSchemaVersion)
        {
            issues.Add(new ValidationIssue("schemaVersion", "The schema version is not supported."));
        }

        if (configuration.Groups is null)
        {
            issues.Add(new ValidationIssue("groups", "At least an empty groups collection is required."));
            return issues;
        }

        for (var groupIndex = 0; groupIndex < configuration.Groups.Length; groupIndex++)
        {
            var group = configuration.Groups[groupIndex];
            var groupField = $"groups[{groupIndex}]";

            if (group is null)
            {
                issues.Add(new ValidationIssue(groupField, "A group entry is required."));
                continue;
            }

            ValidateName(
                issues,
                $"{groupField}.name",
                group.Name,
                MaximumGroupNameLength,
                "group");

            if (group.Shortcuts is null)
            {
                issues.Add(new ValidationIssue($"{groupField}.shortcuts", "A shortcuts collection is required."));
                continue;
            }

            var normalizedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var shortcutIndex = 0; shortcutIndex < group.Shortcuts.Length; shortcutIndex++)
            {
                var shortcut = group.Shortcuts[shortcutIndex];
                var shortcutField = $"{groupField}.shortcuts[{shortcutIndex}]";

                if (shortcut is null)
                {
                    issues.Add(new ValidationIssue(shortcutField, "A shortcut entry is required."));
                    continue;
                }

                ValidateName(
                    issues,
                    $"{shortcutField}.displayName",
                    shortcut.DisplayName,
                    MaximumShortcutNameLength,
                    "shortcut");

                if (!TryNormalizeSupportedTargetPath(shortcut.TargetPath, out var normalizedPath))
                {
                    issues.Add(new ValidationIssue(
                        $"{shortcutField}.targetPath",
                        "The target must be an absolute .exe or .lnk path."));
                    continue;
                }

                if (!normalizedTargets.Add(normalizedPath))
                {
                    issues.Add(new ValidationIssue(
                        $"{shortcutField}.targetPath",
                        "The target already exists in this group."));
                }
            }
        }

        return issues;
    }

    private static bool TryNormalizeSupportedTargetPath(string path, out string normalizedPath)
    {
        normalizedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !ShortcutTargetValidator.IsSupportedExtension(path))
        {
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void ValidateName(
        ICollection<ValidationIssue> issues,
        string field,
        string value,
        int maximumLength,
        string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new ValidationIssue(field, $"The {label} name is required."));
            return;
        }

        if (value.Trim().Length > maximumLength)
        {
            issues.Add(new ValidationIssue(field, $"The {label} name must be {maximumLength} characters or fewer."));
        }
    }
}
