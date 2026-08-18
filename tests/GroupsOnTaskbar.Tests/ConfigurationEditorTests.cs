using GroupsOnTaskbar.Core.Configuration;
using GroupsOnTaskbar.Core.Models;
using GroupsOnTaskbar.Core.Validation;

namespace GroupsOnTaskbar.Tests;

public sealed class ConfigurationEditorTests
{
    [Fact]
    public void MoveGroup_ReordersAndReindexesSortOrders_AndBoundaryMoveIsNoOp()
    {
        var alphaId = Guid.NewGuid();
        var betaId = Guid.NewGuid();
        var gammaId = Guid.NewGuid();
        var editor = CreateEditor(
            CreateGroup(alphaId, "Alpha", 0),
            CreateGroup(betaId, "Beta", 1),
            CreateGroup(gammaId, "Gamma", 2));

        editor.MoveGroup(betaId, 1);

        var moved = editor.Snapshot;
        Assert.Equal([alphaId, gammaId, betaId], moved.Groups.Select(group => group.Id));
        Assert.Equal([0, 1, 2], moved.Groups.Select(group => group.SortOrder));
        AssertValid(editor);

        editor.MoveGroup(alphaId, -1);
        editor.MoveGroup(betaId, 5);

        var unchanged = editor.Snapshot;
        Assert.Equal([alphaId, gammaId, betaId], unchanged.Groups.Select(group => group.Id));
        Assert.Equal([0, 1, 2], unchanged.Groups.Select(group => group.SortOrder));
        AssertValid(editor);
    }

    [Fact]
    public void AddRenameDeleteGroup_TrimNamesAndReindexAfterDelete()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var editor = CreateEditor(
            CreateGroup(firstId, "First", 0),
            CreateGroup(secondId, "Second", 1));

        var addedId = editor.AddGroup("  Utilities  ");
        editor.RenameGroup(secondId, "  Productivity  ");
        editor.DeleteGroup(firstId);

        var snapshot = editor.Snapshot;
        Assert.Equal(["Productivity", "Utilities"], snapshot.Groups.Select(group => group.Name));
        Assert.Equal([secondId, addedId], snapshot.Groups.Select(group => group.Id));
        Assert.Equal([0, 1], snapshot.Groups.Select(group => group.SortOrder));
        AssertValid(editor);
    }

    [Fact]
    public void AddShortcut_AssignsSequentialSortOrder_AndRejectsDuplicatePathIgnoringCase()
    {
        var groupId = Guid.NewGuid();
        var editor = CreateEditor(
            CreateGroup(
                groupId,
                "Utilities",
                0,
                CreateShortcut(Guid.NewGuid(), "Tool A", @"C:\Apps\ToolA.exe", 0)));

        var addedShortcutId = editor.AddShortcut(groupId, "  Tool B  ", @"C:\Apps\Folder\..\ToolB.exe");

        var snapshot = editor.Snapshot;
        var group = Assert.Single(snapshot.Groups);
        Assert.Equal([0, 1], group.Shortcuts.Select(shortcut => shortcut.SortOrder));
        Assert.Equal(addedShortcutId, group.Shortcuts[1].Id);
        Assert.Equal("Tool B", group.Shortcuts[1].DisplayName);
        Assert.Equal(Path.GetFullPath(@"C:\Apps\Folder\..\ToolB.exe"), group.Shortcuts[1].TargetPath);
        AssertValid(editor);

        var duplicate = Assert.Throws<ArgumentException>(() =>
            editor.AddShortcut(groupId, "Tool B copy", @"c:\apps\TOOLB.exe"));
        Assert.Contains("already in this group", duplicate.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddShortcut_AllowsSamePathInDifferentGroup()
    {
        var firstGroupId = Guid.NewGuid();
        var secondGroupId = Guid.NewGuid();
        var editor = CreateEditor(
            CreateGroup(firstGroupId, "First", 0),
            CreateGroup(secondGroupId, "Second", 1));

        editor.AddShortcut(firstGroupId, "Shared", @"C:\Apps\Shared.exe");
        editor.AddShortcut(secondGroupId, "Shared", @"c:\apps\shared.exe");

        var snapshot = editor.Snapshot;
        Assert.All(snapshot.Groups, group => Assert.Single(group.Shortcuts));
        AssertValid(editor);
    }

    [Fact]
    public void UpdateShortcut_CanKeepItsOwnPath_AndRejectsUnsupportedExtension()
    {
        var groupId = Guid.NewGuid();
        var firstShortcutId = Guid.NewGuid();
        var secondShortcutId = Guid.NewGuid();
        var editor = CreateEditor(
            CreateGroup(
                groupId,
                "Utilities",
                0,
                CreateShortcut(firstShortcutId, "Tool A", @"C:\Apps\ToolA.exe", 0),
                CreateShortcut(secondShortcutId, "Tool B", @"C:\Apps\ToolB.exe", 1)));

        editor.UpdateShortcut(groupId, firstShortcutId, "  Tool A Updated  ", @"c:\apps\toola.exe");

        var snapshot = editor.Snapshot;
        var updatedShortcut = snapshot.Groups[0].Shortcuts.Single(shortcut => shortcut.Id == firstShortcutId);
        Assert.Equal("Tool A Updated", updatedShortcut.DisplayName);
        Assert.Equal(Path.GetFullPath(@"c:\apps\toola.exe"), updatedShortcut.TargetPath);
        AssertValid(editor);

        var unsupported = Assert.Throws<ArgumentException>(() =>
            editor.UpdateShortcut(groupId, firstShortcutId, "Tool A", @"C:\Apps\ToolA.cmd"));
        Assert.Contains("Only .exe and .lnk targets are supported.", unsupported.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MoveShortcut_ReordersAndBoundaryMoveIsNoOp()
    {
        var groupId = Guid.NewGuid();
        var firstShortcutId = Guid.NewGuid();
        var secondShortcutId = Guid.NewGuid();
        var thirdShortcutId = Guid.NewGuid();
        var editor = CreateEditor(
            CreateGroup(
                groupId,
                "Utilities",
                0,
                CreateShortcut(firstShortcutId, "Tool A", @"C:\Apps\ToolA.exe", 0),
                CreateShortcut(secondShortcutId, "Tool B", @"C:\Apps\ToolB.exe", 1),
                CreateShortcut(thirdShortcutId, "Tool C", @"C:\Apps\ToolC.exe", 2)));

        editor.MoveShortcut(groupId, secondShortcutId, -1);

        var moved = editor.Snapshot.Groups[0];
        Assert.Equal([secondShortcutId, firstShortcutId, thirdShortcutId], moved.Shortcuts.Select(shortcut => shortcut.Id));
        Assert.Equal([0, 1, 2], moved.Shortcuts.Select(shortcut => shortcut.SortOrder));
        AssertValid(editor);

        editor.MoveShortcut(groupId, secondShortcutId, -1);
        editor.MoveShortcut(groupId, thirdShortcutId, 2);

        var unchanged = editor.Snapshot.Groups[0];
        Assert.Equal([secondShortcutId, firstShortcutId, thirdShortcutId], unchanged.Shortcuts.Select(shortcut => shortcut.Id));
        Assert.Equal([0, 1, 2], unchanged.Shortcuts.Select(shortcut => shortcut.SortOrder));
        AssertValid(editor);
    }

    [Fact]
    public void DeleteShortcut_ReindexesRemainingShortcuts()
    {
        var groupId = Guid.NewGuid();
        var firstShortcutId = Guid.NewGuid();
        var secondShortcutId = Guid.NewGuid();
        var thirdShortcutId = Guid.NewGuid();
        var editor = CreateEditor(
            CreateGroup(
                groupId,
                "Utilities",
                0,
                CreateShortcut(firstShortcutId, "Tool A", @"C:\Apps\ToolA.exe", 0),
                CreateShortcut(secondShortcutId, "Tool B", @"C:\Apps\ToolB.exe", 1),
                CreateShortcut(thirdShortcutId, "Tool C", @"C:\Apps\ToolC.exe", 2)));

        editor.DeleteShortcut(groupId, secondShortcutId);

        var shortcuts = editor.Snapshot.Groups[0].Shortcuts;
        Assert.Equal([firstShortcutId, thirdShortcutId], shortcuts.Select(shortcut => shortcut.Id));
        Assert.Equal([0, 1], shortcuts.Select(shortcut => shortcut.SortOrder));
        AssertValid(editor);
    }

    [Fact]
    public void UnknownIds_ThrowKeyNotFoundException()
    {
        var groupId = Guid.NewGuid();
        var shortcutId = Guid.NewGuid();
        var editor = CreateEditor(
            CreateGroup(
                groupId,
                "Utilities",
                0,
                CreateShortcut(shortcutId, "Tool A", @"C:\Apps\ToolA.exe", 0)));

        Assert.Throws<KeyNotFoundException>(() => editor.RenameGroup(Guid.NewGuid(), "Missing"));
        Assert.Throws<KeyNotFoundException>(() => editor.DeleteGroup(Guid.NewGuid()));
        Assert.Throws<KeyNotFoundException>(() => editor.MoveGroup(Guid.NewGuid(), 1));
        Assert.Throws<KeyNotFoundException>(() => editor.AddShortcut(Guid.NewGuid(), "Tool B", @"C:\Apps\ToolB.exe"));
        Assert.Throws<KeyNotFoundException>(() => editor.UpdateShortcut(groupId, Guid.NewGuid(), "Tool A", @"C:\Apps\ToolA.exe"));
        Assert.Throws<KeyNotFoundException>(() => editor.DeleteShortcut(groupId, Guid.NewGuid()));
        Assert.Throws<KeyNotFoundException>(() => editor.MoveShortcut(groupId, Guid.NewGuid(), 1));
    }

    [Fact]
    public void Snapshot_ReturnsDeepCopiesThatDoNotAffectEditorState()
    {
        var groupId = Guid.NewGuid();
        var shortcutId = Guid.NewGuid();
        var editor = CreateEditor(
            CreateGroup(
                groupId,
                "Utilities",
                0,
                CreateShortcut(shortcutId, "Tool A", @"C:\Apps\ToolA.exe", 0)));

        var snapshot = editor.Snapshot;
        snapshot.Groups[0] = snapshot.Groups[0] with { Name = "Tampered" };
        snapshot.Groups[0].Shortcuts[0] = snapshot.Groups[0].Shortcuts[0] with { DisplayName = "Mutated" };

        var freshSnapshot = editor.Snapshot;
        Assert.Equal("Utilities", freshSnapshot.Groups[0].Name);
        Assert.Equal("Tool A", freshSnapshot.Groups[0].Shortcuts[0].DisplayName);
        AssertValid(editor);
    }

    [Fact]
    public void AddGroup_WhenNameIsInvalid_ThrowsArgumentException()
    {
        var editor = CreateEditor();

        Assert.Throws<ArgumentException>(() => editor.AddGroup("   "));
        Assert.Throws<ArgumentException>(() => editor.AddGroup(new string('G', ConfigurationValidator.MaximumGroupNameLength + 1)));
    }

    private static ConfigurationEditor CreateEditor(params AppGroup[] groups)
    {
        var configuration = new LauncherConfiguration(LauncherConfiguration.CurrentSchemaVersion, groups);
        return new ConfigurationEditor(configuration, _ => true);
    }

    private static AppGroup CreateGroup(Guid id, string name, int sortOrder, params AppShortcut[] shortcuts)
    {
        return new AppGroup(id, name, sortOrder, shortcuts);
    }

    private static AppShortcut CreateShortcut(Guid id, string displayName, string targetPath, int sortOrder)
    {
        return new AppShortcut(id, displayName, targetPath, sortOrder);
    }

    private static void AssertValid(ConfigurationEditor editor)
    {
        Assert.Empty(ConfigurationValidator.Validate(editor.Snapshot));
    }
}
