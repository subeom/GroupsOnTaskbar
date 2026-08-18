using System.Collections.Generic;
using System.IO;

namespace GroupsOnTaskbar.Core.Validation;

public static class ShortcutTargetValidator
{
    public static bool IsSupportedExtension(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<ValidationIssue> ValidateForAdd(
        string path,
        IEnumerable<string> existingPaths,
        Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return [new ValidationIssue("targetPath", "The selected target must use an absolute path.")];
        }

        if (!IsSupportedExtension(path))
        {
            return [new ValidationIssue("targetPath", "Only .exe and .lnk targets are supported.")];
        }

        string normalizedPath;

        try
        {
            normalizedPath = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return [new ValidationIssue("targetPath", "The selected target must use an absolute path.")];
        }

        if (!fileExists(normalizedPath))
        {
            return [new ValidationIssue("targetPath", "The selected target does not exist.")];
        }

        foreach (var existingPath in existingPaths)
        {
            if (string.IsNullOrWhiteSpace(existingPath) || !Path.IsPathFullyQualified(existingPath))
            {
                continue;
            }

            try
            {
                if (string.Equals(Path.GetFullPath(existingPath), normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return [new ValidationIssue("targetPath", "The selected target is already in this group.")];
                }
            }
            catch (Exception)
            {
            }
        }

        return [];
    }
}
