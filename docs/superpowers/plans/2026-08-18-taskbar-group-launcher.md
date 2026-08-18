# Taskbar Group Launcher Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a packaged Windows 11 launcher whose pinned taskbar icon opens a compact, configurable grid of grouped desktop application shortcuts.

**Architecture:** A single-instance WinUI 3 process owns a borderless launcher window and a normal settings window. A UI-independent core library owns configuration, validation, ordering, persistence, and placement calculations; Windows-specific adapters handle activation, shell launching, icons, and MSIX packaging.

**Tech Stack:** C# 14, .NET 10, Windows App SDK 2.4.0, WinUI 3, MSIX, System.Text.Json, xUnit

---

## File structure

Create the following solution structure. Keep each file focused on one concern.

```text
GroupsOnTaskbar.sln
Directory.Build.props
.gitignore
README.md
scripts/
  New-DevelopmentPackage.ps1
src/
  GroupsOnTaskbar.Core/
    GroupsOnTaskbar.Core.csproj
    Models/
      LauncherConfiguration.cs
    Validation/
      ConfigurationValidator.cs
      ShortcutTargetValidator.cs
      ValidationIssue.cs
    Configuration/
      IGroupStore.cs
      JsonGroupStore.cs
      CorruptConfigurationException.cs
      ConfigurationEditor.cs
    Placement/
      ScreenRect.cs
      TaskbarEdge.cs
      WindowPlacementCalculator.cs
    Launch/
      LaunchResult.cs
      IconCacheKey.cs
  GroupsOnTaskbar.App/
    GroupsOnTaskbar.App.csproj
    App.xaml
    App.xaml.cs
    Program.cs
    Package.appxmanifest
    MainWindow.xaml
    MainWindow.xaml.cs
    SettingsWindow.xaml
    SettingsWindow.xaml.cs
    Activation/
      ActivationCoordinator.cs
    Interop/
      NativeMethods.cs
    Services/
      IAppLogger.cs
      IAppLaunchService.cs
      IShellExecutor.cs
      ShellAppLaunchService.cs
      ProcessShellExecutor.cs
      ShortcutIconService.cs
      LocalFileLogger.cs
    ViewModels/
      ObservableObject.cs
      LauncherViewModel.cs
      SettingsViewModel.cs
      GroupViewModel.cs
      ShortcutViewModel.cs
    Windows/
      LauncherWindowController.cs
      SettingsWindowController.cs
tests/
  GroupsOnTaskbar.Tests/
    GroupsOnTaskbar.Tests.csproj
    ConfigurationValidatorTests.cs
    ShortcutTargetValidatorTests.cs
    JsonGroupStoreTests.cs
    ConfigurationEditorTests.cs
    WindowPlacementCalculatorTests.cs
    ShellAppLaunchServiceTests.cs
    IconCacheKeyTests.cs
```

## Reference material

- WinUI CLI quick start: <https://learn.microsoft.com/windows/apps/get-started/start-here>
- App instancing: <https://learn.microsoft.com/windows/apps/windows-app-sdk/applifecycle/applifecycle-instancing>
- AppWindow management: <https://learn.microsoft.com/windows/apps/develop/ui/manage-app-windows>
- MSIX test certificates: <https://learn.microsoft.com/windows/msix/package/create-certificate-package-signing>

### Task 1: Scaffold the WinUI solution

**Files:**
- Create: `GroupsOnTaskbar.sln`
- Create: `Directory.Build.props`
- Create: `.gitignore`
- Create: `src/GroupsOnTaskbar.App/**`
- Create: `src/GroupsOnTaskbar.Core/GroupsOnTaskbar.Core.csproj`
- Create: `tests/GroupsOnTaskbar.Tests/GroupsOnTaskbar.Tests.csproj`
- Delete: `tests/GroupsOnTaskbar.Tests/UnitTest1.cs`

- [ ] **Step 1: Install the pinned WinUI CLI templates**

Run:

```powershell
dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates::2.4.0
dotnet new list winui
```

Expected: the list includes `winui`, `winui-mvvm`, and `winui-unittest`.

- [ ] **Step 2: Generate the solution and projects**

Run from the repository root:

```powershell
dotnet new sln -n GroupsOnTaskbar --format sln
dotnet new winui -n GroupsOnTaskbar.App -o src\GroupsOnTaskbar.App --dotnet-version net10.0
dotnet new classlib -n GroupsOnTaskbar.Core -o src\GroupsOnTaskbar.Core -f net10.0
dotnet new xunit -n GroupsOnTaskbar.Tests -o tests\GroupsOnTaskbar.Tests -f net10.0
dotnet new gitignore
dotnet sln GroupsOnTaskbar.sln add src\GroupsOnTaskbar.App\GroupsOnTaskbar.App.csproj
dotnet sln GroupsOnTaskbar.sln add src\GroupsOnTaskbar.Core\GroupsOnTaskbar.Core.csproj
dotnet sln GroupsOnTaskbar.sln add tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj
dotnet add src\GroupsOnTaskbar.App\GroupsOnTaskbar.App.csproj reference src\GroupsOnTaskbar.Core\GroupsOnTaskbar.Core.csproj
dotnet add tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj reference src\GroupsOnTaskbar.Core\GroupsOnTaskbar.Core.csproj
dotnet add tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj reference src\GroupsOnTaskbar.App\GroupsOnTaskbar.App.csproj
```

Expected: all three projects appear in `dotnet sln GroupsOnTaskbar.sln list`.

- [ ] **Step 3: Add shared compiler settings**

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>14.0</LangVersion>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

Change `tests/GroupsOnTaskbar.Tests/GroupsOnTaskbar.Tests.csproj` to target Windows so it can reference the WinUI project:

```xml
<TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
```

Delete `tests/GroupsOnTaskbar.Tests/UnitTest1.cs`.

- [ ] **Step 4: Verify the generated baseline**

Run:

```powershell
dotnet restore GroupsOnTaskbar.sln
dotnet build GroupsOnTaskbar.sln -c Debug --no-restore
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj -c Debug --no-build
```

Expected: restore and build succeed; the test command reports zero failed tests.

- [ ] **Step 5: Commit the scaffold**

```powershell
git add .gitignore Directory.Build.props GroupsOnTaskbar.sln src tests
git commit -m "build: scaffold WinUI launcher solution"
```

### Task 2: Define and validate the configuration model

**Files:**
- Create: `src/GroupsOnTaskbar.Core/Models/LauncherConfiguration.cs`
- Create: `src/GroupsOnTaskbar.Core/Validation/ValidationIssue.cs`
- Create: `src/GroupsOnTaskbar.Core/Validation/ConfigurationValidator.cs`
- Create: `src/GroupsOnTaskbar.Core/Validation/ShortcutTargetValidator.cs`
- Create: `tests/GroupsOnTaskbar.Tests/ConfigurationValidatorTests.cs`
- Create: `tests/GroupsOnTaskbar.Tests/ShortcutTargetValidatorTests.cs`

- [ ] **Step 1: Write failing configuration validation tests**

Create `tests/GroupsOnTaskbar.Tests/ConfigurationValidatorTests.cs`:

```csharp
using GroupsOnTaskbar.Core.Models;
using GroupsOnTaskbar.Core.Validation;

namespace GroupsOnTaskbar.Tests;

public sealed class ConfigurationValidatorTests
{
    [Fact]
    public void Empty_configuration_is_valid()
    {
        Assert.Empty(ConfigurationValidator.Validate(LauncherConfiguration.Empty));
    }

    [Fact]
    public void Unsupported_schema_is_rejected()
    {
        var configuration = new LauncherConfiguration(99, []);

        var issue = Assert.Single(ConfigurationValidator.Validate(configuration));

        Assert.Equal("schemaVersion", issue.Field);
    }

    [Fact]
    public void Invalid_names_and_targets_are_reported()
    {
        var shortcut = new AppShortcut(Guid.NewGuid(), "", @"relative\app.txt", 0);
        var group = new AppGroup(Guid.NewGuid(), " ", 0, [shortcut]);

        var issues = ConfigurationValidator.Validate(
            new LauncherConfiguration(LauncherConfiguration.CurrentSchemaVersion, [group]));

        Assert.Contains(issues, issue => issue.Field.EndsWith(".name", StringComparison.Ordinal));
        Assert.Contains(issues, issue => issue.Field.EndsWith(".targetPath", StringComparison.Ordinal));
    }
}
```

- [ ] **Step 2: Run tests and confirm the expected failure**

Run:

```powershell
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj --filter ConfigurationValidatorTests
```

Expected: compilation fails because the configuration types do not exist.

- [ ] **Step 3: Implement the model and structural validator**

Create `src/GroupsOnTaskbar.Core/Models/LauncherConfiguration.cs`:

```csharp
namespace GroupsOnTaskbar.Core.Models;

public sealed record LauncherConfiguration(int SchemaVersion, AppGroup[] Groups)
{
    public const int CurrentSchemaVersion = 1;
    public static LauncherConfiguration Empty { get; } = new(CurrentSchemaVersion, []);
}

public sealed record AppGroup(
    Guid Id,
    string Name,
    int SortOrder,
    AppShortcut[] Shortcuts);

public sealed record AppShortcut(
    Guid Id,
    string DisplayName,
    string TargetPath,
    int SortOrder);
```

Create `src/GroupsOnTaskbar.Core/Validation/ValidationIssue.cs`:

```csharp
namespace GroupsOnTaskbar.Core.Validation;

public sealed record ValidationIssue(string Field, string Message);
```

Create `src/GroupsOnTaskbar.Core/Validation/ConfigurationValidator.cs`:

```csharp
using GroupsOnTaskbar.Core.Models;

namespace GroupsOnTaskbar.Core.Validation;

public static class ConfigurationValidator
{
    public const int MaximumGroupNameLength = 60;
    public const int MaximumShortcutNameLength = 100;

    public static IReadOnlyList<ValidationIssue> Validate(
        LauncherConfiguration configuration)
    {
        var issues = new List<ValidationIssue>();
        if (configuration.SchemaVersion != LauncherConfiguration.CurrentSchemaVersion)
        {
            issues.Add(new("schemaVersion", "The configuration schema is not supported."));
        }

        if (configuration.Groups is null)
        {
            issues.Add(new("groups", "The groups collection is required."));
            return issues;
        }

        for (var groupIndex = 0; groupIndex < configuration.Groups.Length; groupIndex++)
        {
            var group = configuration.Groups[groupIndex];
            if (group is null)
            {
                issues.Add(new($"groups[{groupIndex}]", "The group entry is required."));
                continue;
            }

            ValidateName(group.Name, MaximumGroupNameLength, $"groups[{groupIndex}].name", issues);

            if (group.Shortcuts is null)
            {
                issues.Add(new(
                    $"groups[{groupIndex}].shortcuts",
                    "The shortcuts collection is required."));
                continue;
            }

            var duplicatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var shortcutIndex = 0; shortcutIndex < group.Shortcuts.Length; shortcutIndex++)
            {
                var shortcut = group.Shortcuts[shortcutIndex];
                var prefix = $"groups[{groupIndex}].shortcuts[{shortcutIndex}]";
                if (shortcut is null)
                {
                    issues.Add(new(prefix, "The shortcut entry is required."));
                    continue;
                }

                ValidateName(
                    shortcut.DisplayName,
                    MaximumShortcutNameLength,
                    $"{prefix}.displayName",
                    issues);

                if (string.IsNullOrWhiteSpace(shortcut.TargetPath) ||
                    !Path.IsPathFullyQualified(shortcut.TargetPath) ||
                    !ShortcutTargetValidator.IsSupportedExtension(shortcut.TargetPath))
                {
                    issues.Add(new(
                        $"{prefix}.targetPath",
                        "The target must be an absolute .exe or .lnk path."));
                    continue;
                }

                var normalizedPath = Path.GetFullPath(shortcut.TargetPath);
                if (!duplicatePaths.Add(normalizedPath))
                {
                    issues.Add(new(
                        $"{prefix}.targetPath",
                        "The target already exists in this group."));
                }
            }
        }

        return issues;
    }

    private static void ValidateName(
        string value,
        int maximumLength,
        string field,
        ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new(field, "A name is required."));
        }
        else if (value.Trim().Length > maximumLength)
        {
            issues.Add(new(field, $"The name cannot exceed {maximumLength} characters."));
        }
    }
}
```

- [ ] **Step 4: Write and implement add-time target validation**

Create `tests/GroupsOnTaskbar.Tests/ShortcutTargetValidatorTests.cs`:

```csharp
using GroupsOnTaskbar.Core.Validation;

namespace GroupsOnTaskbar.Tests;

public sealed class ShortcutTargetValidatorTests
{
    [Theory]
    [InlineData(@"C:\Apps\Tool.exe", true)]
    [InlineData(@"C:\Apps\Tool.lnk", true)]
    [InlineData(@"C:\Apps\Tool.cmd", false)]
    public void Supported_extensions_are_explicit(string path, bool expected)
    {
        Assert.Equal(expected, ShortcutTargetValidator.IsSupportedExtension(path));
    }

    [Fact]
    public void Missing_file_is_rejected_when_adding()
    {
        var issues = ShortcutTargetValidator.ValidateForAdd(
            @"C:\Missing\Tool.exe",
            [],
            _ => false);

        Assert.Contains(issues, issue => issue.Message == "The selected target does not exist.");
    }
}
```

Create `src/GroupsOnTaskbar.Core/Validation/ShortcutTargetValidator.cs`:

```csharp
namespace GroupsOnTaskbar.Core.Validation;

public static class ShortcutTargetValidator
{
    public static bool IsSupportedExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<ValidationIssue> ValidateForAdd(
        string path,
        IEnumerable<string> existingPaths,
        Func<string, bool> fileExists)
    {
        var issues = new List<ValidationIssue>();
        if (!Path.IsPathFullyQualified(path))
        {
            issues.Add(new("targetPath", "The selected target must use an absolute path."));
            return issues;
        }

        if (!IsSupportedExtension(path))
        {
            issues.Add(new("targetPath", "Only .exe and .lnk targets are supported."));
        }

        if (!fileExists(path))
        {
            issues.Add(new("targetPath", "The selected target does not exist."));
        }

        var normalizedPath = Path.GetFullPath(path);
        if (existingPaths.Any(existing =>
            string.Equals(Path.GetFullPath(existing), normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(new("targetPath", "The selected target is already in this group."));
        }

        return issues;
    }
}
```

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj --filter "ConfigurationValidatorTests|ShortcutTargetValidatorTests"
```

Expected: all validation tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src\GroupsOnTaskbar.Core tests\GroupsOnTaskbar.Tests
git commit -m "feat: add launcher configuration validation"
```

### Task 3: Add atomic JSON persistence and corruption recovery

**Files:**
- Create: `src/GroupsOnTaskbar.Core/Configuration/IGroupStore.cs`
- Create: `src/GroupsOnTaskbar.Core/Configuration/CorruptConfigurationException.cs`
- Create: `src/GroupsOnTaskbar.Core/Configuration/JsonGroupStore.cs`
- Create: `tests/GroupsOnTaskbar.Tests/JsonGroupStoreTests.cs`

- [ ] **Step 1: Write failing persistence tests**

Create `tests/GroupsOnTaskbar.Tests/JsonGroupStoreTests.cs` with tests for:

```csharp
[Fact]
public async Task Missing_file_loads_empty_configuration()
{
    using var directory = new TemporaryDirectory();
    var store = new JsonGroupStore(directory.Path);

    Assert.Equal(LauncherConfiguration.Empty, await store.LoadAsync());
}

[Fact]
public async Task Save_then_load_round_trips()
{
    using var directory = new TemporaryDirectory();
    var expected = new LauncherConfiguration(
        1,
        [new AppGroup(Guid.NewGuid(), "Work", 0, [])]);
    var store = new JsonGroupStore(directory.Path);

    await store.SaveAsync(expected);

    Assert.Equal(expected, await store.LoadAsync());
}

[Fact]
public async Task Corrupt_file_is_preserved_until_reset_is_confirmed()
{
    using var directory = new TemporaryDirectory();
    var settingsPath = Path.Combine(directory.Path, JsonGroupStore.SettingsFileName);
    await File.WriteAllTextAsync(settingsPath, "{not-json");
    var store = new JsonGroupStore(
        directory.Path,
        new FixedTimeProvider(new DateTimeOffset(2026, 8, 18, 4, 0, 0, TimeSpan.Zero)));

    await Assert.ThrowsAsync<CorruptConfigurationException>(() => store.LoadAsync());
    var backupPath = await store.BackUpAndResetAsync();

    Assert.True(File.Exists(backupPath));
    Assert.Equal(LauncherConfiguration.Empty, await store.LoadAsync());
}
```

Add these test-only helpers at the bottom of the same file:

```csharp
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "GroupsOnTaskbar.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, true);
        }
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => value;
}
```

- [ ] **Step 2: Run the tests and confirm failure**

```powershell
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj --filter JsonGroupStoreTests
```

Expected: compilation fails because `JsonGroupStore` does not exist.

- [ ] **Step 3: Implement the store contract and corruption exception**

Create `src/GroupsOnTaskbar.Core/Configuration/IGroupStore.cs`:

```csharp
using GroupsOnTaskbar.Core.Models;

namespace GroupsOnTaskbar.Core.Configuration;

public interface IGroupStore
{
    Task<LauncherConfiguration> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(
        LauncherConfiguration configuration,
        CancellationToken cancellationToken = default);
    Task<string> BackUpAndResetAsync(CancellationToken cancellationToken = default);
}
```

Create `src/GroupsOnTaskbar.Core/Configuration/CorruptConfigurationException.cs`:

```csharp
namespace GroupsOnTaskbar.Core.Configuration;

public sealed class CorruptConfigurationException(
    string settingsPath,
    IReadOnlyList<string> reasons,
    Exception? innerException = null)
    : Exception("The launcher configuration is damaged.", innerException)
{
    public string SettingsPath { get; } = settingsPath;
    public IReadOnlyList<string> Reasons { get; } = reasons;
}
```

- [ ] **Step 4: Implement atomic JSON storage**

Create `src/GroupsOnTaskbar.Core/Configuration/JsonGroupStore.cs`:

```csharp
using System.Text.Json;
using GroupsOnTaskbar.Core.Models;
using GroupsOnTaskbar.Core.Validation;

namespace GroupsOnTaskbar.Core.Configuration;

public sealed class JsonGroupStore : IGroupStore
{
    public const string SettingsFileName = "settings-v1.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _rootPath;
    private readonly string _settingsPath;
    private readonly TimeProvider _timeProvider;

    public JsonGroupStore(string rootPath, TimeProvider? timeProvider = null)
    {
        _rootPath = rootPath;
        _settingsPath = Path.Combine(rootPath, SettingsFileName);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<LauncherConfiguration> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsPath))
        {
            return LauncherConfiguration.Empty;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var configuration = await JsonSerializer.DeserializeAsync<LauncherConfiguration>(
                stream,
                JsonOptions,
                cancellationToken);
            if (configuration is null)
            {
                throw new JsonException("The settings document is empty.");
            }

            var issues = ConfigurationValidator.Validate(configuration);
            if (issues.Count > 0)
            {
                throw new CorruptConfigurationException(
                    _settingsPath,
                    issues.Select(issue => $"{issue.Field}: {issue.Message}").ToArray());
            }

            return configuration;
        }
        catch (CorruptConfigurationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new CorruptConfigurationException(
                _settingsPath,
                [exception.Message],
                exception);
        }
    }

    public async Task SaveAsync(
        LauncherConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var issues = ConfigurationValidator.Validate(configuration);
        if (issues.Count > 0)
        {
            throw new ArgumentException(
                string.Join(Environment.NewLine, issues.Select(issue => issue.Message)),
                nameof(configuration));
        }

        Directory.CreateDirectory(_rootPath);
        var temporaryPath = Path.Combine(_rootPath, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough | FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    configuration,
                    JsonOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _settingsPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public async Task<string> BackUpAndResetAsync(
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_rootPath);
        var timestamp = _timeProvider.GetUtcNow().ToString("yyyyMMdd-HHmmss");
        var backupPath = Path.Combine(_rootPath, $"settings-v1.{timestamp}.corrupt.json");
        if (File.Exists(_settingsPath))
        {
            File.Move(_settingsPath, backupPath);
        }

        await SaveAsync(LauncherConfiguration.Empty, cancellationToken);
        return backupPath;
    }
}
```

- [ ] **Step 5: Run persistence tests and the full test project**

```powershell
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj --filter JsonGroupStoreTests
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj
```

Expected: all tests pass and no `.tmp` file remains in the test directories.

- [ ] **Step 6: Commit**

```powershell
git add src\GroupsOnTaskbar.Core\Configuration tests\GroupsOnTaskbar.Tests\JsonGroupStoreTests.cs
git commit -m "feat: persist launcher groups atomically"
```

### Task 4: Add deterministic group and shortcut editing

**Files:**
- Create: `src/GroupsOnTaskbar.Core/Configuration/ConfigurationEditor.cs`
- Create: `tests/GroupsOnTaskbar.Tests/ConfigurationEditorTests.cs`

- [ ] **Step 1: Write failing editor tests**

Cover these exact behaviors in `ConfigurationEditorTests.cs`:

```csharp
[Fact]
public void Moving_group_reindexes_all_groups()
{
    var editor = new ConfigurationEditor(new LauncherConfiguration(
        1,
        [
            new AppGroup(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "A", 0, []),
            new AppGroup(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "B", 1, [])
        ]));

    editor.MoveGroup(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), -1);

    Assert.Equal(["B", "A"], editor.Snapshot.Groups.Select(group => group.Name));
    Assert.Equal([0, 1], editor.Snapshot.Groups.Select(group => group.SortOrder));
}

[Fact]
public void Adding_duplicate_shortcut_to_same_group_fails()
{
    var groupId = Guid.NewGuid();
    var editor = new ConfigurationEditor(
        new LauncherConfiguration(1, [new AppGroup(groupId, "Work", 0, [])]),
        _ => true);

    editor.AddShortcut(groupId, "Tool", @"C:\Apps\Tool.exe");

    Assert.Throws<ArgumentException>(() =>
        editor.AddShortcut(groupId, "Tool Again", @"c:\apps\tool.exe"));
}
```

Also test add, rename, delete, and boundary moves for both groups and shortcuts.

- [ ] **Step 2: Run tests and confirm failure**

```powershell
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj --filter ConfigurationEditorTests
```

Expected: compilation fails because `ConfigurationEditor` does not exist.

- [ ] **Step 3: Implement the editor**

Create `ConfigurationEditor` with this public API:

```csharp
public sealed class ConfigurationEditor
{
    public ConfigurationEditor(
        LauncherConfiguration configuration,
        Func<string, bool>? fileExists = null);

    public LauncherConfiguration Snapshot { get; }

    public Guid AddGroup(string name);
    public void RenameGroup(Guid groupId, string name);
    public void DeleteGroup(Guid groupId);
    public void MoveGroup(Guid groupId, int offset);

    public Guid AddShortcut(Guid groupId, string displayName, string targetPath);
    public void UpdateShortcut(
        Guid groupId,
        Guid shortcutId,
        string displayName,
        string targetPath);
    public void DeleteShortcut(Guid groupId, Guid shortcutId);
    public void MoveShortcut(Guid groupId, Guid shortcutId, int offset);
}
```

Implement the class as follows:

```csharp
using GroupsOnTaskbar.Core.Models;
using GroupsOnTaskbar.Core.Validation;

namespace GroupsOnTaskbar.Core.Configuration;

public sealed class ConfigurationEditor
{
    private readonly Func<string, bool> _fileExists;
    private LauncherConfiguration _snapshot;

    public ConfigurationEditor(
        LauncherConfiguration configuration,
        Func<string, bool>? fileExists = null)
    {
        _fileExists = fileExists ?? File.Exists;
        _snapshot = Clone(configuration);
    }

    public LauncherConfiguration Snapshot => Clone(_snapshot);

    public Guid AddGroup(string name)
    {
        var id = Guid.NewGuid();
        var groups = _snapshot.Groups.ToList();
        groups.Add(new AppGroup(id, ValidateName(name, 60), groups.Count, []));
        SetGroups(groups);
        return id;
    }

    public void RenameGroup(Guid groupId, string name)
    {
        var groups = _snapshot.Groups.ToList();
        var index = FindGroupIndex(groups, groupId);
        groups[index] = groups[index] with { Name = ValidateName(name, 60) };
        SetGroups(groups);
    }

    public void DeleteGroup(Guid groupId)
    {
        var groups = _snapshot.Groups.ToList();
        groups.RemoveAt(FindGroupIndex(groups, groupId));
        SetGroups(groups);
    }

    public void MoveGroup(Guid groupId, int offset)
    {
        var groups = _snapshot.Groups.ToList();
        Move(groups, FindGroupIndex(groups, groupId), offset);
        SetGroups(groups);
    }

    public Guid AddShortcut(Guid groupId, string displayName, string targetPath)
    {
        var groups = _snapshot.Groups.ToList();
        var groupIndex = FindGroupIndex(groups, groupId);
        var shortcuts = groups[groupIndex].Shortcuts.ToList();
        ValidateTarget(targetPath, shortcuts.Select(item => item.TargetPath));

        var id = Guid.NewGuid();
        shortcuts.Add(new AppShortcut(
            id,
            ValidateName(displayName, 100),
            Path.GetFullPath(targetPath),
            shortcuts.Count));
        groups[groupIndex] = groups[groupIndex] with
        {
            Shortcuts = ReindexShortcuts(shortcuts)
        };
        SetGroups(groups);
        return id;
    }

    public void UpdateShortcut(
        Guid groupId,
        Guid shortcutId,
        string displayName,
        string targetPath)
    {
        var groups = _snapshot.Groups.ToList();
        var groupIndex = FindGroupIndex(groups, groupId);
        var shortcuts = groups[groupIndex].Shortcuts.ToList();
        var shortcutIndex = shortcuts.FindIndex(item => item.Id == shortcutId);
        if (shortcutIndex < 0)
        {
            throw new KeyNotFoundException($"Shortcut '{shortcutId}' was not found.");
        }

        ValidateTarget(
            targetPath,
            shortcuts.Where(item => item.Id != shortcutId).Select(item => item.TargetPath));
        shortcuts[shortcutIndex] = shortcuts[shortcutIndex] with
        {
            DisplayName = ValidateName(displayName, 100),
            TargetPath = Path.GetFullPath(targetPath)
        };
        groups[groupIndex] = groups[groupIndex] with
        {
            Shortcuts = ReindexShortcuts(shortcuts)
        };
        SetGroups(groups);
    }

    public void DeleteShortcut(Guid groupId, Guid shortcutId)
    {
        var groups = _snapshot.Groups.ToList();
        var groupIndex = FindGroupIndex(groups, groupId);
        var shortcuts = groups[groupIndex].Shortcuts.ToList();
        var shortcutIndex = shortcuts.FindIndex(item => item.Id == shortcutId);
        if (shortcutIndex < 0)
        {
            throw new KeyNotFoundException($"Shortcut '{shortcutId}' was not found.");
        }

        shortcuts.RemoveAt(shortcutIndex);
        groups[groupIndex] = groups[groupIndex] with
        {
            Shortcuts = ReindexShortcuts(shortcuts)
        };
        SetGroups(groups);
    }

    public void MoveShortcut(Guid groupId, Guid shortcutId, int offset)
    {
        var groups = _snapshot.Groups.ToList();
        var groupIndex = FindGroupIndex(groups, groupId);
        var shortcuts = groups[groupIndex].Shortcuts.ToList();
        var shortcutIndex = shortcuts.FindIndex(item => item.Id == shortcutId);
        if (shortcutIndex < 0)
        {
            throw new KeyNotFoundException($"Shortcut '{shortcutId}' was not found.");
        }

        Move(shortcuts, shortcutIndex, offset);
        groups[groupIndex] = groups[groupIndex] with
        {
            Shortcuts = ReindexShortcuts(shortcuts)
        };
        SetGroups(groups);
    }

    private static LauncherConfiguration Clone(LauncherConfiguration source) =>
        new(
            source.SchemaVersion,
            source.Groups
                .Select(group => group with
                {
                    Shortcuts = group.Shortcuts.Select(shortcut => shortcut with { }).ToArray()
                })
                .ToArray());

    private static int FindGroupIndex(IReadOnlyList<AppGroup> groups, Guid groupId)
    {
        for (var index = 0; index < groups.Count; index++)
        {
            if (groups[index].Id == groupId)
            {
                return index;
            }
        }

        throw new KeyNotFoundException($"Group '{groupId}' was not found.");
    }

    private static string ValidateName(string name, int maximumLength)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0 || trimmed.Length > maximumLength)
        {
            throw new ArgumentException(
                $"The name must contain 1 to {maximumLength} characters.",
                nameof(name));
        }

        return trimmed;
    }

    private void ValidateTarget(string path, IEnumerable<string> existingPaths)
    {
        var issues = ShortcutTargetValidator.ValidateForAdd(
            path,
            existingPaths,
            _fileExists);
        if (issues.Count > 0)
        {
            throw new ArgumentException(
                string.Join(Environment.NewLine, issues.Select(issue => issue.Message)),
                nameof(path));
        }
    }

    private void SetGroups(List<AppGroup> groups)
    {
        _snapshot = new LauncherConfiguration(
            LauncherConfiguration.CurrentSchemaVersion,
            groups.Select((group, index) => group with
            {
                SortOrder = index,
                Shortcuts = ReindexShortcuts(group.Shortcuts)
            }).ToArray());
    }

    private static AppShortcut[] ReindexShortcuts(IEnumerable<AppShortcut> shortcuts) =>
        shortcuts.Select((shortcut, index) => shortcut with { SortOrder = index }).ToArray();

    private static void Move<T>(List<T> items, int index, int offset)
    {
        var destination = index + offset;
        if (index < 0 || destination < 0 || destination >= items.Count)
        {
            return;
        }

        var item = items[index];
        items.RemoveAt(index);
        items.Insert(destination, item);
    }
}
```

- [ ] **Step 4: Run editor and full tests**

```powershell
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj --filter ConfigurationEditorTests
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src\GroupsOnTaskbar.Core\Configuration\ConfigurationEditor.cs tests\GroupsOnTaskbar.Tests\ConfigurationEditorTests.cs
git commit -m "feat: edit and order launcher groups"
```

### Task 5: Calculate monitor-relative launcher placement

**Files:**
- Create: `src/GroupsOnTaskbar.Core/Placement/ScreenRect.cs`
- Create: `src/GroupsOnTaskbar.Core/Placement/TaskbarEdge.cs`
- Create: `src/GroupsOnTaskbar.Core/Placement/WindowPlacementCalculator.cs`
- Create: `tests/GroupsOnTaskbar.Tests/WindowPlacementCalculatorTests.cs`

- [ ] **Step 1: Write table-driven failing placement tests**

Create tests for bottom, top, left, right, auto-hide fallback, and corner
clamping. A bottom-taskbar example:

```csharp
[Fact]
public void Bottom_taskbar_places_launcher_above_work_area_edge()
{
    var monitor = new ScreenRect(0, 0, 1920, 1080);
    var workArea = new ScreenRect(0, 0, 1920, 1040);

    var result = WindowPlacementCalculator.Calculate(
        monitor,
        workArea,
        pointerX: 960,
        pointerY: 1060,
        windowWidth: 440,
        windowHeight: 360,
        gap: 8);

    Assert.Equal(new ScreenRect(740, 672, 440, 360), result);
}
```

- [ ] **Step 2: Run tests and confirm failure**

```powershell
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj --filter WindowPlacementCalculatorTests
```

Expected: compilation fails because placement types do not exist.

- [ ] **Step 3: Implement placement primitives**

Create `ScreenRect.cs`:

```csharp
namespace GroupsOnTaskbar.Core.Placement;

public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}
```

Create `TaskbarEdge.cs`:

```csharp
namespace GroupsOnTaskbar.Core.Placement;

public enum TaskbarEdge
{
    Bottom,
    Top,
    Left,
    Right
}
```

- [ ] **Step 4: Implement deterministic placement**

Create `WindowPlacementCalculator.cs`:

```csharp
namespace GroupsOnTaskbar.Core.Placement;

public static class WindowPlacementCalculator
{
    public static ScreenRect Calculate(
        ScreenRect monitor,
        ScreenRect workArea,
        int pointerX,
        int pointerY,
        int windowWidth,
        int windowHeight,
        int gap)
    {
        if (windowWidth <= 0 || windowHeight <= 0 || gap < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowWidth),
                "Window dimensions must be positive and gap cannot be negative.");
        }

        if (windowWidth > workArea.Width || windowHeight > workArea.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowWidth),
                "The launcher must fit inside the monitor work area.");
        }

        var edge = InferTaskbarEdge(monitor, workArea);
        var x = pointerX - (windowWidth / 2);
        var y = pointerY - (windowHeight / 2);

        switch (edge)
        {
            case TaskbarEdge.Top:
                y = workArea.Y + gap;
                break;
            case TaskbarEdge.Bottom:
                y = workArea.Bottom - windowHeight - gap;
                break;
            case TaskbarEdge.Left:
                x = workArea.X + gap;
                break;
            case TaskbarEdge.Right:
                x = workArea.Right - windowWidth - gap;
                break;
        }

        x = Clamp(x, workArea.X, workArea.Right - windowWidth);
        y = Clamp(y, workArea.Y, workArea.Bottom - windowHeight);
        return new ScreenRect(x, y, windowWidth, windowHeight);
    }

    private static TaskbarEdge InferTaskbarEdge(
        ScreenRect monitor,
        ScreenRect workArea)
    {
        var candidates = new (TaskbarEdge Edge, int Inset)[]
        {
            (TaskbarEdge.Left, workArea.X - monitor.X),
            (TaskbarEdge.Top, workArea.Y - monitor.Y),
            (TaskbarEdge.Right, monitor.Right - workArea.Right),
            (TaskbarEdge.Bottom, monitor.Bottom - workArea.Bottom)
        };

        var selected = candidates.MaxBy(candidate => candidate.Inset);
        return selected.Inset > 0 ? selected.Edge : TaskbarEdge.Bottom;
    }

    private static int Clamp(int value, int minimum, int maximum) =>
        Math.Min(Math.Max(value, minimum), maximum);
}
```

- [ ] **Step 5: Run placement and full tests**

```powershell
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj --filter WindowPlacementCalculatorTests
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src\GroupsOnTaskbar.Core\Placement tests\GroupsOnTaskbar.Tests\WindowPlacementCalculatorTests.cs
git commit -m "feat: position launcher beside the taskbar"
```

### Task 6: Add shell launching and icon caching

**Files:**
- Create: `src/GroupsOnTaskbar.Core/Launch/LaunchResult.cs`
- Create: `src/GroupsOnTaskbar.Core/Launch/IconCacheKey.cs`
- Create: `src/GroupsOnTaskbar.App/Services/IAppLaunchService.cs`
- Create: `src/GroupsOnTaskbar.App/Services/IAppLogger.cs`
- Create: `src/GroupsOnTaskbar.App/Services/IShellExecutor.cs`
- Create: `src/GroupsOnTaskbar.App/Services/ProcessShellExecutor.cs`
- Create: `src/GroupsOnTaskbar.App/Services/ShellAppLaunchService.cs`
- Create: `src/GroupsOnTaskbar.App/Services/ShortcutIconService.cs`
- Create: `src/GroupsOnTaskbar.App/Services/LocalFileLogger.cs`
- Create: `tests/GroupsOnTaskbar.Tests/ShellAppLaunchServiceTests.cs`
- Create: `tests/GroupsOnTaskbar.Tests/IconCacheKeyTests.cs`

- [ ] **Step 1: Write failing launch-result tests**

Inject a fake file probe and fake `IShellExecutor`. Verify:

- existing `.exe` returns `Started` and calls the executor once;
- missing path returns `TargetMissing`;
- `Win32Exception` with native error code 5 returns `AccessDenied`;
- any other `Win32Exception` returns `LaunchFailed`;
- unsupported extensions return `LaunchFailed` without invoking the executor.

Use this result model:

```csharp
public enum LaunchStatus
{
    Started,
    TargetMissing,
    AccessDenied,
    LaunchFailed
}

public sealed record LaunchResult(LaunchStatus Status, string? UserMessage = null);
```

- [ ] **Step 2: Run tests and confirm failure**

```powershell
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj --filter ShellAppLaunchServiceTests
```

Expected: compilation fails because launch types do not exist.

- [ ] **Step 3: Implement shell launching**

Create `LaunchResult.cs` from the result model in Step 1. Create the service
contracts:

```csharp
using GroupsOnTaskbar.Core.Launch;

namespace GroupsOnTaskbar.App.Services;

public interface IAppLaunchService
{
    LaunchResult Launch(string targetPath);
}

public interface IShellExecutor
{
    void Execute(string targetPath);
}
```

Create `ProcessShellExecutor.cs`:

```csharp
using System.Diagnostics;

namespace GroupsOnTaskbar.App.Services;

public sealed class ProcessShellExecutor : IShellExecutor
{
    public void Execute(string targetPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = targetPath,
            UseShellExecute = true
        });
    }
}
```

Implement `ShellAppLaunchService`:

```csharp
using System.ComponentModel;
using GroupsOnTaskbar.Core.Launch;
using GroupsOnTaskbar.Core.Validation;

namespace GroupsOnTaskbar.App.Services;

public sealed class ShellAppLaunchService(
    IShellExecutor shellExecutor,
    Func<string, bool>? fileExists = null) : IAppLaunchService
{
    private readonly Func<string, bool> _fileExists = fileExists ?? File.Exists;

    public LaunchResult Launch(string targetPath)
    {
        if (!Path.IsPathFullyQualified(targetPath) ||
            !ShortcutTargetValidator.IsSupportedExtension(targetPath))
        {
            return new(
                LaunchStatus.LaunchFailed,
                "This shortcut is not a supported .exe or .lnk target.");
        }

        if (!_fileExists(targetPath))
        {
            return new(LaunchStatus.TargetMissing, "The shortcut target no longer exists.");
        }

        try
        {
            shellExecutor.Execute(targetPath);
            return new(LaunchStatus.Started);
        }
        catch (UnauthorizedAccessException)
        {
            return new(
                LaunchStatus.AccessDenied,
                "Windows denied access to this shortcut.");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 5)
        {
            return new(
                LaunchStatus.AccessDenied,
                "Windows denied access to this shortcut.");
        }
        catch (Win32Exception)
        {
            return new(
                LaunchStatus.LaunchFailed,
                "Windows could not start this shortcut.");
        }
    }
}
```

- [ ] **Step 4: Write and implement deterministic cache-key tests**

Create `IconCacheKeyTests.cs` and verify that path casing normalizes to the same
key while a changed last-write timestamp changes the key.

Implement `IconCacheKey.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;

namespace GroupsOnTaskbar.Core.Launch;

public static class IconCacheKey
{
    public static string Create(string path, DateTimeOffset lastWriteUtc)
    {
        var input = string.Concat(
            Path.GetFullPath(path).ToUpperInvariant(),
            "|",
            lastWriteUtc.UtcTicks);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"{Convert.ToHexStringLower(digest)}.png";
    }
}
```

- [ ] **Step 5: Implement thumbnail extraction and local caching**

Create this logging contract:

```csharp
public interface IAppLogger
{
    Task WriteAsync(
        string category,
        Exception exception,
        CancellationToken cancellationToken = default);
}
```

`LocalFileLogger` writes UTF-8 lines to the supplied local-data root under
`Logs\taskbar-groups.log`. Serialize writes with `SemaphoreSlim`, include UTC
timestamp, category, exception type, HRESULT, and message, and rotate to
`taskbar-groups.previous.log` when the current file exceeds 1 MiB. Never write
configuration JSON.

`ShortcutIconService` takes the package local folder and an `IAppLogger`, then:

1. Creates an `IconCache` child folder.
2. Uses `StorageFile.GetFileFromPathAsync`.
3. Calls `GetThumbnailAsync(ThumbnailMode.SingleItem, 64, ThumbnailOptions.UseCurrentScale)`.
4. Copies a non-empty thumbnail stream into the cache file named by
   `IconCacheKey`.
5. Loads a `BitmapImage` from the cache file.
6. Returns `null` for an unavailable thumbnail so XAML can show the generic
   `SymbolIcon`.

Catch only `FileNotFoundException`, `UnauthorizedAccessException`, and
`COMException` at this boundary. Log the exception through `IAppLogger`;
do not convert storage failures into successful cache writes.

- [ ] **Step 6: Run tests and build**

```powershell
dotnet test tests\GroupsOnTaskbar.Tests\GroupsOnTaskbar.Tests.csproj --filter "ShellAppLaunchServiceTests|IconCacheKeyTests"
dotnet build GroupsOnTaskbar.sln -c Debug
```

Expected: all tests and the WinUI build pass.

- [ ] **Step 7: Commit**

```powershell
git add src\GroupsOnTaskbar.Core\Launch src\GroupsOnTaskbar.App\Services tests\GroupsOnTaskbar.Tests
git commit -m "feat: launch shortcuts and cache icons"
```

### Task 7: Implement single-instance activation and flyout window behavior

**Files:**
- Modify: `src/GroupsOnTaskbar.App/GroupsOnTaskbar.App.csproj`
- Create: `src/GroupsOnTaskbar.App/Program.cs`
- Create: `src/GroupsOnTaskbar.App/Activation/ActivationCoordinator.cs`
- Create: `src/GroupsOnTaskbar.App/Interop/NativeMethods.cs`
- Create: `src/GroupsOnTaskbar.App/Windows/LauncherWindowController.cs`
- Modify: `src/GroupsOnTaskbar.App/App.xaml.cs`
- Modify: `src/GroupsOnTaskbar.App/MainWindow.xaml.cs`

- [ ] **Step 1: Enable a custom entry point**

Add this property to the WinUI project:

```xml
<DefineConstants>$(DefineConstants);DISABLE_XAML_GENERATED_MAIN</DefineConstants>
```

Create `Program.cs`:

```csharp
using Microsoft.Windows.AppLifecycle;

namespace GroupsOnTaskbar.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        if (ActivationCoordinator.RedirectToMainInstance())
        {
            return;
        }

        XamlGeneratedProgram.XamlGeneratedMain();
    }
}
```

Add `using GroupsOnTaskbar.App.Activation;` to `Program.cs`.

- [ ] **Step 2: Implement activation redirection**

`ActivationCoordinator` uses the key `GroupsOnTaskbar.Main`. It obtains current
activation arguments before registering. If the returned instance is not
current, call:

```csharp
mainInstance
    .RedirectActivationToAsync(activationArguments)
    .AsTask()
    .GetAwaiter()
    .GetResult();
```

Return `true` only for the redirecting process. Expose
`RegisterActivationHandler(Action handler)`, which subscribes to
`AppInstance.GetCurrent().Activated` and enqueues `handler` on the current
`DispatcherQueue`.

- [ ] **Step 3: Add pointer and display-area adapters**

In `NativeMethods.cs`, P/Invoke `GetCursorPos` from `user32.dll` with
`SetLastError = true`. Throw `Win32Exception` when it fails.

`LauncherWindowController` obtains:

```csharp
var displayArea = DisplayArea.GetFromPoint(
    new PointInt32(cursor.X, cursor.Y),
    DisplayAreaFallback.Nearest);
```

Convert `displayArea.OuterBounds` and `displayArea.WorkArea` to `ScreenRect`,
call `WindowPlacementCalculator.Calculate`, then call
`AppWindow.MoveAndResize`.

- [ ] **Step 4: Configure the window as a flyout**

In the controller constructor:

```csharp
var presenter = (OverlappedPresenter)_window.AppWindow.Presenter;
presenter.SetBorderAndTitleBar(false, false);
presenter.IsResizable = false;
presenter.IsMaximizable = false;
presenter.IsMinimizable = false;
presenter.IsAlwaysOnTop = true;
_window.AppWindow.IsShownInSwitchers = false;
```

Use a logical target size of 560 by 460 and convert it with the root
`XamlRoot.RasterizationScale` before placement. `ToggleAtCursor` hides a
visible window; otherwise it moves, shows, and activates it.

Handle `Window.Activated`. On `WindowActivationState.Deactivated`, hide the
window unless a settings transition is in progress. Handle `Esc` at the root
element and hide the window.

- [ ] **Step 5: Wire lifecycle in `App.xaml.cs`**

`OnLaunched` uses `ApplicationData.Current.LocalFolder.Path` to construct
`JsonGroupStore`, `LocalFileLogger`, and `ShortcutIconService`. It creates one
`MainWindow`, one `LauncherWindowController`, registers reactivation, and
invokes the first toggle. Do not create a second launcher window for later
activations.

Run:

```powershell
dotnet build src\GroupsOnTaskbar.App\GroupsOnTaskbar.App.csproj
dotnet run --project src\GroupsOnTaskbar.App\GroupsOnTaskbar.App.csproj
```

Expected: the first launch shows one borderless window; launching the same
project again toggles that existing window rather than creating another
process-owned window.

- [ ] **Step 6: Commit**

```powershell
git add src\GroupsOnTaskbar.App
git commit -m "feat: add single-instance launcher flyout"
```

### Task 8: Build the launcher user interface

**Files:**
- Create: `src/GroupsOnTaskbar.App/ViewModels/ObservableObject.cs`
- Create: `src/GroupsOnTaskbar.App/ViewModels/LauncherViewModel.cs`
- Create: `src/GroupsOnTaskbar.App/ViewModels/GroupViewModel.cs`
- Create: `src/GroupsOnTaskbar.App/ViewModels/ShortcutViewModel.cs`
- Modify: `src/GroupsOnTaskbar.App/MainWindow.xaml`
- Modify: `src/GroupsOnTaskbar.App/MainWindow.xaml.cs`
- Modify: `src/GroupsOnTaskbar.App/Windows/LauncherWindowController.cs`

- [ ] **Step 1: Add focused view models**

`ObservableObject` implements `INotifyPropertyChanged` with a protected
`SetProperty<T>` helper.

`GroupViewModel` exposes `Id`, `Name`, and
`ObservableCollection<ShortcutViewModel> Shortcuts`.

`ShortcutViewModel` exposes `Id`, `DisplayName`, `TargetPath`,
`ImageSource? Icon`, `bool IsAvailable`, and `string AvailabilityText`.

`LauncherViewModel` exposes `ObservableCollection<GroupViewModel> Groups`,
`GroupViewModel? SelectedGroup`, and `bool HasShortcuts`. Its
`LoadAsync(LauncherConfiguration, ShortcutIconService)` sorts by `SortOrder`,
checks `File.Exists`, and loads icons without blocking the UI thread.

- [ ] **Step 2: Replace the generated window content**

Use this visual structure in `MainWindow.xaml`:

```xml
<Window
    x:Class="GroupsOnTaskbar.App.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid x:Name="RootGrid"
          Padding="12"
          CornerRadius="12"
          Background="{ThemeResource LayerFillColorDefaultBrush}">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <ListView x:Name="GroupList"
                  SelectionMode="Single"
                  SelectionChanged="GroupList_SelectionChanged"
                  IsItemClickEnabled="True"
                  ItemsSource="{Binding Groups}">
            <ListView.ItemsPanel>
                <ItemsPanelTemplate>
                    <ItemsStackPanel Orientation="Horizontal" />
                </ItemsPanelTemplate>
            </ListView.ItemsPanel>
            <ListView.ItemTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding Name}" />
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>

        <GridView x:Name="ShortcutGrid"
                  Grid.Row="1"
                  Margin="0,12,0,8"
                  IsItemClickEnabled="True"
                  ItemClick="ShortcutGrid_ItemClick"
                  ItemsSource="{Binding SelectedGroup.Shortcuts}">
            <GridView.ItemTemplate>
                <DataTemplate>
                    <StackPanel Width="88" Spacing="6">
                        <Grid Width="48" Height="48">
                            <Image Source="{Binding Icon}" Stretch="Uniform" />
                            <SymbolIcon Symbol="OpenFile"
                                        Visibility="{Binding IconFallbackVisibility}" />
                        </Grid>
                        <TextBlock Text="{Binding DisplayName}"
                                   MaxLines="2"
                                   TextAlignment="Center"
                                   TextTrimming="CharacterEllipsis" />
                        <TextBlock Text="{Binding AvailabilityText}"
                                   Style="{StaticResource CaptionTextBlockStyle}" />
                    </StackPanel>
                </DataTemplate>
            </GridView.ItemTemplate>
        </GridView>

        <StackPanel x:Name="EmptyState"
                    Grid.Row="1"
                    HorizontalAlignment="Center"
                    VerticalAlignment="Center"
                    Spacing="8">
            <SymbolIcon Symbol="Add" />
            <TextBlock Text="Create your first group in Settings." />
        </StackPanel>

        <Grid Grid.Row="2">
            <Button Content="Settings"
                    Click="Settings_Click"
                    AutomationProperties.Name="Open Taskbar Groups settings" />
            <Button Content="Exit"
                    HorizontalAlignment="Right"
                    Click="Exit_Click"
                    AutomationProperties.Name="Exit Taskbar Groups" />
        </Grid>
    </Grid>
</Window>
```

Add `IconFallbackVisibility` to `ShortcutViewModel` as a computed
`Visibility` property, and update it when `Icon` changes.

- [ ] **Step 3: Wire launcher actions**

`MainWindow` exposes these events:

```csharp
public event Func<ShortcutViewModel, Task>? ShortcutInvoked;
public event EventHandler? SettingsRequested;
public event EventHandler? ExitRequested;
```

The item-click handler ignores unavailable shortcuts. The controller invokes
`IAppLaunchService`, hides only on `Started`, and leaves the panel open with an
inline `InfoBar` for every other status.

Settings marks a settings transition before opening the settings window. Exit
calls `Application.Current.Exit()`.

- [ ] **Step 4: Verify keyboard and empty-state behavior**

Run the app with an empty configuration. Verify `Tab` reaches groups, shortcuts,
Settings, and Exit; `Esc` hides the window; and no shortcut grid is shown for an
empty group.

- [ ] **Step 5: Commit**

```powershell
git add src\GroupsOnTaskbar.App
git commit -m "feat: add grouped launcher interface"
```

### Task 9: Build settings and persist edits

**Files:**
- Create: `src/GroupsOnTaskbar.App/ViewModels/SettingsViewModel.cs`
- Create: `src/GroupsOnTaskbar.App/SettingsWindow.xaml`
- Create: `src/GroupsOnTaskbar.App/SettingsWindow.xaml.cs`
- Create: `src/GroupsOnTaskbar.App/Windows/SettingsWindowController.cs`
- Modify: `src/GroupsOnTaskbar.App/Windows/LauncherWindowController.cs`
- Modify: `src/GroupsOnTaskbar.App/App.xaml.cs`

- [ ] **Step 1: Implement an editable settings view model**

`SettingsViewModel` wraps `ConfigurationEditor` and exposes:

```csharp
ObservableCollection<GroupViewModel> Groups
GroupViewModel? SelectedGroup
ShortcutViewModel? SelectedShortcut
string? ErrorMessage

void AddGroup()
void RenameSelectedGroup(string name)
void DeleteSelectedGroup()
void MoveSelectedGroup(int offset)
void AddShortcut(string displayName, string targetPath)
void UpdateSelectedShortcut(string displayName, string targetPath)
void DeleteSelectedShortcut()
void MoveSelectedShortcut(int offset)
Task<LauncherConfiguration> SaveAsync()
```

`SaveAsync` calls `IGroupStore.SaveAsync` first. It returns the saved snapshot
only after that call succeeds. Storage exceptions set `ErrorMessage` and are
re-thrown so the window remains open.

- [ ] **Step 2: Add a two-pane settings window**

Create a normal, resizable `SettingsWindow` with:

- left `ListView` for groups;
- group name `TextBox`;
- add, delete, move-up, and move-down group buttons;
- right `ListView` for shortcuts in the selected group;
- shortcut display-name `TextBox`;
- Add app, remove, move-up, and move-down shortcut buttons;
- Save and Cancel buttons;
- an `InfoBar` bound to `ErrorMessage`.

Give every icon-only button an `AutomationProperties.Name`. Disable move and
delete actions when no corresponding selection exists.

- [ ] **Step 3: Implement the `.exe` and `.lnk` picker**

In `SettingsWindow.xaml.cs`:

```csharp
var picker = new FileOpenPicker();
picker.FileTypeFilter.Add(".exe");
picker.FileTypeFilter.Add(".lnk");
InitializeWithWindow.Initialize(
    picker,
    WindowNative.GetWindowHandle(this));
var file = await picker.PickSingleFileAsync();
```

If a file is returned, use `Path.GetFileNameWithoutExtension(file.Path)` as the
initial display name and call `SettingsViewModel.AddShortcut`. Show validation
messages in the `InfoBar`; do not silently ignore invalid selections.

- [ ] **Step 4: Coordinate settings and launcher windows**

`SettingsWindowController` maintains only one settings window. Opening it hides
the launcher and activates the existing settings window if one is already open.

On successful Save:

1. close Settings;
2. reload `LauncherViewModel` from the returned snapshot;
3. return to the hidden background state.

On Cancel, close without mutating the active launcher snapshot. Closing the
window with the title-bar close button has the same behavior as Cancel.

- [ ] **Step 5: Verify persistence manually**

Run:

```powershell
dotnet run --project src\GroupsOnTaskbar.App\GroupsOnTaskbar.App.csproj
```

Create two groups, add one `.exe` and one `.lnk`, reorder both groups and
shortcuts, save, exit, and restart. Expected: the exact saved order and names
return.

- [ ] **Step 6: Commit**

```powershell
git add src\GroupsOnTaskbar.App
git commit -m "feat: configure launcher groups and shortcuts"
```

### Task 10: Add explicit recovery, logging, and accessibility polish

**Files:**
- Modify: `src/GroupsOnTaskbar.App/Services/LocalFileLogger.cs`
- Modify: `src/GroupsOnTaskbar.App/App.xaml.cs`
- Modify: `src/GroupsOnTaskbar.App/MainWindow.xaml`
- Modify: `src/GroupsOnTaskbar.App/SettingsWindow.xaml`

- [ ] **Step 1: Add bounded local logging**

Confirm that `LocalFileLogger` writes UTF-8 lines to
`ApplicationData.Current.LocalFolder\Logs\taskbar-groups.log` with UTC
timestamp, category, exception type, HRESULT, and message. It must:

- serialize writes with `SemaphoreSlim`;
- rotate the log to `.previous.log` when it exceeds 1 MiB;
- never include configuration JSON;
- catch only file I/O failures at the final logging boundary.

- [ ] **Step 2: Add corrupt-settings recovery**

When initial `IGroupStore.LoadAsync` throws `CorruptConfigurationException`,
show a `ContentDialog` with:

- title: `Settings file cannot be read`;
- body: the settings path and validation reasons;
- primary: `Back up and reset`;
- close: `Exit`.

Primary invokes `BackUpAndResetAsync`, logs the backup path, and continues with
an empty configuration. Close exits the app. Do not reset before the user makes
that choice.

- [ ] **Step 3: Complete accessibility states**

Add:

- tooltips for all icon buttons;
- accessible group and shortcut names;
- explicit unavailable text and warning glyph for missing targets;
- visible focus states;
- `KeyboardAccelerator Key="Escape"` on launcher close;
- `AutomationProperties.HelpText` containing the full target path on shortcut
  tiles.

At 200% Windows text scaling, Settings must scroll rather than clip controls.

- [ ] **Step 4: Run the full automated suite**

```powershell
dotnet build GroupsOnTaskbar.sln -c Debug
dotnet test GroupsOnTaskbar.sln -c Debug --no-build
```

Expected: zero warnings from project code and zero failed tests.

- [ ] **Step 5: Commit**

```powershell
git add src\GroupsOnTaskbar.App
git commit -m "feat: recover settings and improve accessibility"
```

### Task 11: Package the x64 development build

**Files:**
- Modify: `src/GroupsOnTaskbar.App/Package.appxmanifest`
- Create: `scripts/New-DevelopmentPackage.ps1`
- Modify: `.gitignore`
- Create: `README.md`

- [ ] **Step 1: Set package identity and display metadata**

Use these manifest values:

```xml
<Identity
  Name="GroupsOnTaskbar.TaskbarGroups"
  Publisher="CN=Taskbar Groups Development"
  Version="0.1.0.0" />
```

Set package and application display names to `Taskbar Groups`, description to
`Open grouped application shortcuts from the Windows taskbar`, and keep the
template-generated visual assets until production artwork is approved.

- [ ] **Step 2: Ignore generated signing and package artifacts**

Append:

```gitignore
artifacts/
*.cer
*.pfx
AppPackages/
```

- [ ] **Step 3: Create a reproducible development packaging script**

`scripts/New-DevelopmentPackage.ps1` must:

1. create or reuse a `CurrentUser\My` code-signing certificate whose subject is
   `CN=Taskbar Groups Development`;
2. export its public `.cer` under `artifacts\package`;
3. import that certificate into `CurrentUser\TrustedPeople`;
4. run:

```powershell
dotnet publish src\GroupsOnTaskbar.App\GroupsOnTaskbar.App.csproj `
  -c Release `
  -r win-x64 `
  -p:Platform=x64 `
  -p:GenerateAppxPackageOnBuild=true `
  -p:AppxPackageSigningEnabled=true `
  -p:PackageCertificateThumbprint=$certificate.Thumbprint
```

5. find the newest `.msix` beneath the Release output, copy it to
   `artifacts\package\TaskbarGroups_0.1.0.0_x64.msix`, and print that absolute
   path;
6. fail with a non-zero exit code if no package exists.

Do not export or commit the private key.

Use this complete script:

```powershell
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$subject = 'CN=Taskbar Groups Development'
$friendlyName = 'Taskbar Groups Development'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifactRoot = Join-Path $repoRoot 'artifacts\package'
$project = Join-Path $repoRoot 'src\GroupsOnTaskbar.App\GroupsOnTaskbar.App.csproj'
$outputPackage = Join-Path $artifactRoot 'TaskbarGroups_0.1.0.0_x64.msix'

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

$certificate = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object {
        $_.Subject -eq $subject -and
        $_.FriendlyName -eq $friendlyName -and
        $_.HasPrivateKey -and
        $_.NotAfter -gt (Get-Date).AddDays(30)
    } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if (-not $certificate) {
    $certificate = New-SelfSignedCertificate `
        -Type Custom `
        -KeyUsage DigitalSignature `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -TextExtension @(
            '2.5.29.37={text}1.3.6.1.5.5.7.3.3',
            '2.5.29.19={text}'
        ) `
        -Subject $subject `
        -FriendlyName $friendlyName `
        -NotAfter (Get-Date).AddYears(2)
}

$cerPath = Join-Path $artifactRoot 'TaskbarGroupsDevelopment.cer'
Export-Certificate -Cert $certificate -FilePath $cerPath -Force | Out-Null

$trusted = Get-ChildItem Cert:\CurrentUser\TrustedPeople |
    Where-Object Thumbprint -eq $certificate.Thumbprint
if (-not $trusted) {
    Import-Certificate `
        -FilePath $cerPath `
        -CertStoreLocation 'Cert:\CurrentUser\TrustedPeople' | Out-Null
}

& dotnet publish $project `
    -c Release `
    -r win-x64 `
    '-p:Platform=x64' `
    '-p:GenerateAppxPackageOnBuild=true' `
    '-p:AppxPackageSigningEnabled=true' `
    "-p:PackageCertificateThumbprint=$($certificate.Thumbprint)"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$package = Get-ChildItem `
    (Join-Path $repoRoot 'src\GroupsOnTaskbar.App\bin\Release') `
    -Recurse `
    -Filter *.msix |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
if (-not $package) {
    throw 'The build completed without producing an MSIX package.'
}

Copy-Item $package.FullName $outputPackage -Force
Write-Output (Resolve-Path $outputPackage).Path
```

- [ ] **Step 4: Document local development and installation**

`README.md` must contain:

- prerequisites: Windows 11, .NET 10, Developer Mode;
- template installation command;
- restore, build, test, and `dotnet run` commands;
- development package command;
- `Add-AppxPackage .\artifacts\package\TaskbarGroups_0.1.0.0_x64.msix`;
- how to pin Taskbar Groups from Start;
- where configuration and logs are stored;
- MVP limitations from the design spec.

- [ ] **Step 5: Build and install the development package**

Run:

```powershell
.\scripts\New-DevelopmentPackage.ps1
Add-AppxPackage .\artifacts\package\TaskbarGroups_0.1.0.0_x64.msix
```

Expected: Windows installs the signed x64 MSIX for the current user without an
administrator prompt.

- [ ] **Step 6: Commit**

```powershell
git add .gitignore README.md scripts src\GroupsOnTaskbar.App\Package.appxmanifest
git commit -m "build: package Taskbar Groups for Windows"
```

### Task 12: Execute end-to-end Windows 11 acceptance checks

**Files:**
- Modify only files required to fix an observed acceptance failure.

- [ ] **Step 1: Run clean automated validation**

```powershell
dotnet clean GroupsOnTaskbar.sln
dotnet restore GroupsOnTaskbar.sln
dotnet build GroupsOnTaskbar.sln -c Release --no-restore
dotnet test GroupsOnTaskbar.sln -c Release --no-build
```

Expected: build succeeds and every test passes.

- [ ] **Step 2: Verify taskbar behavior**

Use the installed package:

1. Pin Taskbar Groups to the Windows 11 taskbar.
2. Click the pin; verify the flyout opens above the clicked taskbar location.
3. Click the pin again; verify the same process hides the flyout.
4. Confirm no second taskbar button appears.
5. Press `Esc` and click outside; verify both hide the flyout.

- [ ] **Step 3: Verify group and launch behavior**

1. Create two groups.
2. Add one `.exe` and one `.lnk`.
3. Reorder groups and shortcuts.
4. Launch both targets and confirm the flyout hides only after a successful
   launch.
5. Rename or remove one target file and verify the unavailable state and
   Edit/Remove actions.
6. Restart the package and verify persistence.

- [ ] **Step 4: Verify display and accessibility behavior**

Check light theme, dark theme, 100%, 150%, and 200% text scaling. If a second
monitor is available, click the taskbar pin on each monitor and verify the
flyout remains inside that monitor's work area.

Keyboard-only verification must cover group selection, arrow navigation,
`Enter` launch, Settings, Save, Cancel, and `Esc`.

- [ ] **Step 5: Verify corruption recovery**

Back up `settings-v1.json`, replace its contents with invalid JSON, and start
the app. Verify that:

- the app does not silently reset;
- `Back up and reset` creates a timestamped `.corrupt.json`;
- `Exit` leaves the original file unchanged.

Restore the valid configuration after the check.

- [ ] **Step 6: Commit any acceptance fixes**

If acceptance required code changes:

```powershell
git add --all
git commit -m "fix: address Windows acceptance findings"
```

If no changes were required, do not create an empty commit.
