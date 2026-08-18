# Taskbar Group Launcher Design

**Date:** 2026-08-18  
**Product name:** Taskbar Groups  
**Workspace:** GroupsOnTaskbar  
**Status:** Approved for implementation planning

## 1. Purpose

Taskbar Groups is a Windows 11 launcher that lets a user pin one launcher icon
to the taskbar, click it, and open a compact panel containing grouped
application shortcuts. It provides the experience without modifying or
injecting code into Windows Explorer.

The MVP optimizes for a fast, predictable interaction:

1. Click the pinned Taskbar Groups icon.
2. Choose a group.
3. Click an application icon.
4. The selected application starts and the launcher closes.

## 2. Goals

- Provide a left-click launcher experience adjacent to the Windows taskbar.
- Let users create, rename, delete, and reorder groups.
- Let users add, edit, remove, and reorder desktop application shortcuts.
- Support `.exe` and `.lnk` targets selected by the user.
- Preserve configuration across launches without requiring an account.
- Work correctly on Windows 11 with multiple monitors and common taskbar
  positions.
- Use supported Windows application APIs and avoid Explorer injection.

## 3. Non-goals for the MVP

- Rendering custom controls inside the Windows taskbar.
- Creating a separate taskbar pin for every group.
- Automatically enumerating Microsoft Store applications.
- System tray integration or start-at-login behavior.
- Cloud synchronization, accounts, telemetry, or network access.
- Arbitrary scripts, elevated processes, or administrator-only targets.
- Custom command-line arguments or environment variables per shortcut.

These items can be evaluated after the core launcher is proven reliable.

## 4. Approaches considered

### 4.1 Recommended: packaged WinUI 3 flyout launcher

A packaged C# WinUI 3 application is pinned to the taskbar. Activation opens a
borderless tool window above the taskbar near the pointer. A single long-lived
instance receives later activation requests and toggles the flyout.

This approach supports left-click behavior, modern Windows visuals, package
identity, and safe Windows integration. It is the selected approach.

### 4.2 Jump List only

A packaged application can populate the Windows taskbar Jump List with groups
and launch actions. This is stable and simple, but users must right-click the
taskbar icon and the UI cannot provide the requested visual app grid.

This remains a possible secondary accessibility feature, not the primary MVP.

### 4.3 Explorer/taskbar injection

Injecting controls into Explorer could resemble a native taskbar folder, but
Windows 11 does not provide a supported extension API for this behavior.
Explorer updates could break the integration or prevent Explorer from working.

This approach is explicitly rejected.

## 5. Technical baseline

- C# and .NET 10
- Windows App SDK 2.4.0 Stable
- WinUI 3 desktop application
- MSIX package identity
- Minimum supported OS: Windows 11
- x64 first; ARM64 can be added after the x64 MVP is stable

The solution is divided into:

- `GroupsOnTaskbar.App`: WinUI views, activation, window management, and shell
  adapters.
- `GroupsOnTaskbar.Core`: configuration models, validation, ordering, and
  placement calculations without WinUI dependencies.
- `GroupsOnTaskbar.Tests`: unit tests for core behavior.

## 6. Architecture

### 6.1 ActivationCoordinator

`ActivationCoordinator` enforces a single application instance with
`Microsoft.Windows.AppLifecycle.AppInstance`.

On the first launch, it initializes the application and opens the launcher. On
later taskbar activations, the temporary process redirects activation to the
registered instance and exits. The registered instance samples the pointer
position, then shows or hides the launcher.

The launcher window uses tool-window behavior so it does not create a second
taskbar button. Hiding the launcher keeps the process available for fast
subsequent activation. An explicit **Exit** command in Settings terminates it.

### 6.2 LauncherWindow

`LauncherWindow` is a compact, borderless, non-resizable WinUI window with:

- A group selector at the top.
- A responsive application icon grid.
- An empty state when no shortcuts exist.
- A Settings button and an Exit command.

The window is topmost only while visible. It hides when:

- An application launches successfully.
- The user presses `Esc`.
- The window loses activation to another application.
- A new taskbar activation arrives while it is already visible.

The window exposes accessible names for all controls, supports keyboard
navigation, and preserves visible focus indicators.

### 6.3 WindowPlacementService

`WindowPlacementService` receives the pointer location, monitor bounds, monitor
work area, and desired flyout size. It returns a clamped window rectangle.

It infers the taskbar edge from the difference between monitor bounds and the
monitor work area. The launcher is placed inside the work area, adjacent to the
inferred taskbar edge, and as close to the pointer as possible. If the taskbar
auto-hides and no reserved edge can be inferred, the service uses the bottom
edge as a deterministic fallback.

Placement calculations live in `GroupsOnTaskbar.Core` so they can be tested
without a desktop session.

### 6.4 GroupStore

`GroupStore` reads and writes one versioned JSON file under the application's
local data directory. The initial document is:

```json
{
  "schemaVersion": 1,
  "groups": []
}
```

Each group contains:

- Stable GUID
- Display name
- Integer sort order
- Ordered application entries

Each application entry contains:

- Stable GUID
- Display name
- Absolute target path
- Integer sort order

Writes are atomic: serialize to a temporary file, flush it, and replace the
current file. A malformed document is never silently discarded. The app keeps
the original file with a timestamped `.corrupt` suffix and presents recovery
choices to the user before creating a new empty configuration.

### 6.5 ShortcutIconService

`ShortcutIconService` obtains a display icon through Windows Shell APIs. It
uses the target file icon and stores a derived PNG in the app-local cache. A
generic application icon is shown if Windows cannot produce an icon; the
shortcut remains usable.

The cache key includes the normalized target path and the target's last-write
timestamp so updated application icons are refreshed.

### 6.6 AppLaunchService

`AppLaunchService` accepts only validated `.exe` and `.lnk` paths from the
configuration. It verifies that the path exists, then launches through Windows
Shell execution without elevation.

The service returns an explicit result:

- `Started`
- `TargetMissing`
- `AccessDenied`
- `LaunchFailed` with a user-safe Windows error message

The UI hides only after `Started`. Other results leave the launcher open and
show an inline error with **Edit shortcut** and **Remove shortcut** actions.

### 6.7 SettingsView

Settings opens in a normal app window and provides:

- Create, rename, delete, move up, and move down for groups.
- Add, edit, remove, move up, and move down for shortcuts.
- A file picker restricted to `.exe` and `.lnk`.
- Duplicate-path detection within a group.
- An explicit **Exit Taskbar Groups** action.

Opening Settings hides the launcher flyout and activates the existing Settings
window if one is already open. Closing Settings returns the app to its hidden
background state; it does not terminate the launcher process.

The first launch displays an empty-state action, **Create your first group**.
The app does not install sample shortcuts.

## 7. Data and interaction flow

```text
Taskbar click
  -> packaged app activation
  -> ActivationCoordinator redirects to the registered instance
  -> GroupStore supplies current groups
  -> WindowPlacementService calculates the monitor-relative location
  -> LauncherWindow displays the selected group
  -> user selects an application
  -> AppLaunchService starts the target
  -> LauncherWindow hides after confirmed start
```

Configuration changes flow through a settings view model into `GroupStore`.
After a successful atomic save, the launcher refreshes its in-memory snapshot.
If saving fails, the previous snapshot remains active and the settings window
shows the failure.

## 8. Validation and error behavior

- Group names are trimmed, required, and limited to 60 characters.
- Shortcut display names are trimmed, required, and limited to 100 characters.
- Target paths must be absolute, exist when added, and end in `.exe` or `.lnk`.
- Duplicate group names are allowed; stable IDs distinguish groups.
- Duplicate normalized target paths within one group are rejected using a
  case-insensitive comparison and a clear message.
- Targets removed after configuration are displayed as unavailable rather than
  silently removed.
- Storage, activation, icon, and launch failures are logged locally with no
  personal data beyond paths already present in the local configuration.
- No operation requests administrator privileges.

## 9. Accessibility and visual behavior

- Follow the current Windows 11 light/dark theme.
- Use standard WinUI typography, spacing, and selection states.
- Display application names below icons and expose full names to accessibility
  APIs.
- Support keyboard group selection, arrow-key grid navigation, `Enter` to
  launch, and `Esc` to close.
- Do not communicate missing targets by color alone; use an icon and text state.
- Respect Windows text scaling and keep the settings window usable at 200%.

## 10. Testing strategy

### Automated unit tests

- JSON round-trip and schema validation.
- Atomic-save behavior and preservation after a failed save.
- Group and shortcut validation.
- Stable ordering after add, delete, and move operations.
- Placement for bottom, top, left, and right taskbar work areas.
- Placement clamping near every monitor corner.
- Missing and unsupported launch target classification.

### Integration tests

- A second activation redirects to the registered instance.
- Activation toggles a visible launcher and shows a hidden launcher.
- Saved settings are reflected in the next launcher display.
- Icon cache invalidates when the target timestamp changes.

### Windows 11 acceptance checks

- Taskbar pin launches the flyout on first and subsequent clicks.
- The flyout appears on the monitor containing the clicked taskbar icon.
- The flyout does not create another taskbar button.
- Outside click, `Esc`, and successful app launch close the flyout.
- Light theme, dark theme, 100-200% scaling, and two-monitor layouts work.
- Missing targets and access-denied launches produce actionable errors.
- Uninstall removes packaged binaries while user configuration follows the
  standard MSIX local-data lifecycle.

## 11. MVP acceptance criteria

The MVP is complete when a clean Windows 11 user can:

1. Install the MSIX package without administrator privileges.
2. Pin Taskbar Groups to the taskbar.
3. Create at least two groups.
4. Add `.exe` and `.lnk` shortcuts to each group.
5. Click the pinned icon and see a correctly positioned launcher.
6. Launch every valid configured shortcut.
7. Reorder and remove groups and shortcuts.
8. Restart Windows and retain the saved configuration.
9. Understand and recover from missing targets or a damaged settings file.

## 12. Deferred extensions

After the MVP, separate design work can evaluate:

- One taskbar pin per group.
- Microsoft Store app discovery and activation by AppUserModelID.
- Drag-and-drop shortcut registration.
- Optional Jump List mirroring.
- Start-at-login and notification-area integration.
- Configuration import/export.

Each extension must preserve the supported, non-injected Windows integration
boundary established by this design.
