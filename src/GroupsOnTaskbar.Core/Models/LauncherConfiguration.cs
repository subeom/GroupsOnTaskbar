namespace GroupsOnTaskbar.Core.Models;

public sealed record LauncherConfiguration(int SchemaVersion, AppGroup[] Groups)
{
    public const int CurrentSchemaVersion = 1;

    public static LauncherConfiguration Empty { get; } = new(CurrentSchemaVersion, []);
}

public sealed record AppGroup(Guid Id, string Name, int SortOrder, AppShortcut[] Shortcuts);

public sealed record AppShortcut(Guid Id, string DisplayName, string TargetPath, int SortOrder);
