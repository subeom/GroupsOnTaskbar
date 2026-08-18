using GroupsOnTaskbar.Core.Configuration;
using GroupsOnTaskbar.Core.Models;

namespace GroupsOnTaskbar.Tests;

public sealed class SettingsSessionTests
{
    [Fact]
    public async Task SaveAsync_PersistsEditsAndReturnsSavedSnapshot()
    {
        var store = new FakeGroupStore();
        var session = CreateSession(LauncherConfiguration.Empty, store);

        var groupId = session.AddGroup("Utilities");
        Assert.NotNull(groupId);

        var shortcutId = session.AddShortcut(groupId.Value, "Paint", @"C:\Apps\Paint.exe");
        Assert.NotNull(shortcutId);

        var saved = await session.SaveAsync();

        Assert.Null(session.ErrorMessage);
        Assert.Equal(1, store.SaveCallCount);
        var persisted = Assert.Single(store.SavedConfigurations);
        Assert.Equal(saved.Groups.Select(group => group.Id), persisted.Groups.Select(group => group.Id));
        Assert.Equal(saved.Groups.Select(group => group.Name), persisted.Groups.Select(group => group.Name));
        Assert.Equal(saved.Groups.SelectMany(group => group.Shortcuts).Select(shortcut => shortcut.Id), persisted.Groups.SelectMany(group => group.Shortcuts).Select(shortcut => shortcut.Id));
        Assert.Equal(saved.Groups.SelectMany(group => group.Shortcuts).Select(shortcut => shortcut.DisplayName), persisted.Groups.SelectMany(group => group.Shortcuts).Select(shortcut => shortcut.DisplayName));
        Assert.Contains(saved.Groups.Single().Shortcuts, shortcut => shortcut.Id == shortcutId.Value);
    }

    [Fact]
    public async Task SaveAsync_WhenStoreThrows_SetsErrorAndKeepsEdits()
    {
        var store = new FakeGroupStore
        {
            SaveException = new InvalidOperationException("Disk is unavailable.")
        };

        var session = CreateSession(LauncherConfiguration.Empty, store);
        var groupId = session.AddGroup("Utilities");
        Assert.NotNull(groupId);
        session.AddShortcut(groupId.Value, "Paint", @"C:\Apps\Paint.exe");
        var pendingSnapshot = session.Snapshot;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => session.SaveAsync());

        Assert.Equal("Disk is unavailable.", exception.Message);
        Assert.Contains("Disk is unavailable.", session.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(1, store.SaveCallCount);
        Assert.Equal(pendingSnapshot.Groups.Select(group => group.Name), session.Snapshot.Groups.Select(group => group.Name));
        Assert.Equal(
            pendingSnapshot.Groups.SelectMany(group => group.Shortcuts).Select(shortcut => shortcut.DisplayName),
            session.Snapshot.Groups.SelectMany(group => group.Shortcuts).Select(shortcut => shortcut.DisplayName));
    }

    [Fact]
    public void AddGroup_WhenNameIsInvalid_CapturesErrorMessage()
    {
        var session = CreateSession(LauncherConfiguration.Empty);

        var addedGroupId = session.AddGroup("   ");

        Assert.Null(addedGroupId);
        Assert.Equal("The group name is required.", session.ErrorMessage);
        Assert.Empty(session.Snapshot.Groups);
    }

    [Fact]
    public void AddShortcut_WhenTargetAlreadyExists_CapturesErrorMessage()
    {
        var groupId = Guid.NewGuid();
        var configuration = new LauncherConfiguration(
            LauncherConfiguration.CurrentSchemaVersion,
            [
                new AppGroup(
                    groupId,
                    "Utilities",
                    0,
                    [new AppShortcut(Guid.NewGuid(), "Paint", @"C:\Apps\Paint.exe", 0)])
            ]);

        var session = CreateSession(configuration);

        var addedShortcutId = session.AddShortcut(groupId, "Paint copy", @"c:\apps\PAINT.exe");

        Assert.Null(addedShortcutId);
        Assert.Equal("The selected target is already in this group.", session.ErrorMessage);
        Assert.Single(session.Snapshot.Groups[0].Shortcuts);
    }

    [Fact]
    public void Cancel_DiscardsPendingEditsWithoutSaving()
    {
        var originalGroupId = Guid.NewGuid();
        var originalConfiguration = new LauncherConfiguration(
            LauncherConfiguration.CurrentSchemaVersion,
            [new AppGroup(originalGroupId, "Original", 0, [])]);

        var store = new FakeGroupStore();
        var session = CreateSession(originalConfiguration, store);

        session.AddGroup("Utilities");
        session.Cancel();

        Assert.Null(session.ErrorMessage);
        Assert.Equal(0, store.SaveCallCount);
        Assert.Single(session.Snapshot.Groups);
        Assert.Equal(originalGroupId, session.Snapshot.Groups[0].Id);
        Assert.Equal("Original", session.Snapshot.Groups[0].Name);
    }

    private static SettingsSession CreateSession(LauncherConfiguration configuration, FakeGroupStore? store = null)
    {
        return new SettingsSession(configuration, store ?? new FakeGroupStore(), _ => true);
    }

    private sealed class FakeGroupStore : IGroupStore
    {
        public List<LauncherConfiguration> SavedConfigurations { get; } = [];

        public int SaveCallCount { get; private set; }

        public Exception? SaveException { get; init; }

        public Task<string> BackUpAndResetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<LauncherConfiguration> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(LauncherConfiguration.Empty);

        public Task SaveAsync(LauncherConfiguration configuration, CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            SavedConfigurations.Add(configuration);

            if (SaveException is not null)
            {
                throw SaveException;
            }

            return Task.CompletedTask;
        }
    }
}
