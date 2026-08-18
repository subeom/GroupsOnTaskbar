# Manual acceptance checklist

Automated verification (build, unit tests, packaging) runs anywhere. The checks
below require Windows Developer Mode and a machine-trusted signing certificate,
both of which need administrator rights, so they must be run by a person on a
Windows 11 desktop.

## One-time setup

1. Enable Developer Mode: Settings → System → For developers → Developer Mode.
2. Build and sign the package:

   ```powershell
   .\scripts\New-DevelopmentPackage.ps1
   ```

3. Trust the certificate from an elevated PowerShell prompt:

   ```powershell
   Import-Certificate -FilePath .\artifacts\package\TaskbarGroupsDevelopment.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```

4. Install the package:

   ```powershell
   Add-AppxPackage .\artifacts\package\TaskbarGroups_0.1.0.0_x64.msix
   ```

5. Pin **Taskbar Groups** to the taskbar from Start.

## Taskbar behavior

- [ ] Clicking the pin opens the launcher above the clicked taskbar location.
- [ ] Clicking the pin again hides the launcher instead of opening a second one.
- [ ] No second taskbar button appears while the launcher is visible.
- [ ] `Esc` hides the launcher.
- [ ] Clicking outside the launcher hides it.

## Groups and launching

- [ ] Two groups can be created, renamed, reordered, and deleted.
- [ ] One `.exe` and one `.lnk` shortcut can be added to each group.
- [ ] Shortcuts can be reordered and removed.
- [ ] Launching a valid shortcut starts the app and then hides the launcher.
- [ ] A renamed or deleted target shows an unavailable state with Edit/Remove
      actions and does not hide the launcher.
- [ ] Configuration survives restarting the app and signing out and back in.

## Display and accessibility

- [ ] Light theme and dark theme both render correctly.
- [ ] 100%, 150%, and 200% Windows text scaling remain usable; Settings scrolls
      instead of clipping.
- [ ] With two monitors, clicking the pin on each monitor keeps the launcher
      inside that monitor's work area.
- [ ] Keyboard only: group selection, arrow navigation, `Enter` to launch,
      Settings, Save, Cancel, and `Esc` all work with visible focus.

## Recovery

- [ ] Replacing `settings-v1.json` with invalid JSON shows the
      "Settings file cannot be read" dialog.
- [ ] **Back up and reset** creates a timestamped `.corrupt.json` file and
      starts with empty settings.
- [ ] **Exit** leaves the original file unchanged.
- [ ] Making the settings file unreadable (for example by denying access) shows
      "Settings could not be opened" and exits without overwriting the file.

Settings and logs live under
`%LOCALAPPDATA%\Packages\GroupsOnTaskbar.TaskbarGroups_*\LocalState\`.
