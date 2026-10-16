[English](README.md) | [简体中文](docs/zh-CN/README.md)

# iStart

A compact, personal Windows Start menu for frequently used apps, recent files, folders, and websites. Keep everyday shortcuts together and your desktop clear.

**Current release: 0.2.0-preview.23**

This release reduces the offline installer size by excluding unused framework language resources and debug symbols and increasing compression. The complete runtime, both interface languages, and existing features are retained. A local comparison decreased from 53.6 MB to 50.9 MB, approximately 5%; the published package size is recorded on the release page and in its manifest. See [Package size](docs/PACKAGE_SIZE.md).

The interface follows the Windows display language by default. You can select Simplified Chinese or English in Settings and apply the change immediately. Chinese display languages use Simplified Chinese; English and other currently unsupported languages use English. The menu, Settings, tray, screenshot editor, and updater are localized. App names, filenames, URLs, and release notes retain their original text. See [Interface localization](docs/LOCALIZATION.md).

[Download the Windows x64 installer](https://github.com/morrowfactory/istart/releases/download/v0.2.0-preview.23/iStart-Setup-0.2.0-preview.23.exe) · [Release and checksums](https://github.com/morrowfactory/istart/releases/tag/v0.2.0-preview.23) · [All releases](https://github.com/morrowfactory/istart/releases)

## Features

| View | Contents | Ordering and limits |
| --- | --- | --- |
| My Apps | Installed apps with local activity evidence and apps you add | Most used first; at most 30 apps, including search results |
| Recently Opened | Valid files recorded by iStart, Windows, WPS, LibreOffice, and other supported sources | Most recent first, deduplicated, at most 100 files; folders and programs are excluded |
| Frequent Folders | Added folders and folders found in local activity | Frequent first by default; switch to recent folders on the same page; suggestions fill available space |
| My Websites | Bounded Chrome and Edge history, combined by domain | Frequent first by default; switch to recent websites on the same page; at most 30 sites |
| My Computer | Disk partitions, available space, Recycle Bin, Network, and seven expandable Control Panel categories, excluding Ease of Access | Open the corresponding Windows page; search within this view |

- Switch between Windows 11 Fluent and Google styles in Settings. The choice is applied immediately and saved.
- A consistent iStart icon is used for the desktop shortcut, taskbar, window, tray, installer, and menu.
- Drag apps and folders into the menu; switch between icons and a list, search, and hide items.
- Windows Start menu recovery, account actions, power options, and the tray remain available.
- The separate app recommendation panel, Uncategorized view, category creation, and category-name field have been removed. Legacy item data remains compatible.

Content comes from the current user's local system and activity records. Documentation screenshots use test data, including synthetic disk capacities. See [Activity views](docs/ACTIVITY_VIEWS.md) and the [documentation index](docs/INDEX.md).

### Start integration and foreground behavior

Since preview.21, input hooks run on a dedicated message thread to reduce failures caused by a busy UI. On Windows 10, native Start-button clicks revalidate the button bounds and HWND. Taskbar recreation and display or system-setting changes invalidate cached locations. A native-window fallback handles automation timeouts, and startup uses bounded retries. If the UI is unresponsive for about two seconds, subsequent native input is allowed through. These changes target known Windows 10/11 builds; they do not establish Windows 10 hardware acceptance.

Opening iStart from its icon also uses a temporary topmost window and bounded focus retries, even in native-menu mode. Switching to another window hides it. Reopening the shortcut hands foreground permission to the existing instance. Use Enable integration at the bottom of the menu to opt in; otherwise, the system Start button continues to open Windows Start.

### Screenshots

The default global shortcut is **Alt+Shift+A**, including while iStart runs in the background. Settings also offers Ctrl+Alt+A, Ctrl+Shift+S, or Off. Shortcut conflicts are reported instead of taking another app's shortcut.

The menu's screenshot button hides iStart before capture. Drag a region, move it, or resize its edges and corners. Add colored arrows, ellipses, rectangles, freehand strokes, text, and rectangular mosaic blocks. The compact toolbar follows the selection. Complete copies the image; PNG saving is also available.

- Double-click the selection or press Enter to finish and copy.
- Right-click once to reselect, then again to cancel. Esc cancels.
- Ctrl+Z undoes, Ctrl+Y redoes, Ctrl+C copies, and Ctrl+S saves.
- Enter completes text entry; Shift+Enter inserts a line break.
- The global shortcut captures the monitor under the pointer; the menu button captures the menu's monitor.

Screenshots are not automatically saved or uploaded. Scrolling capture, OCR, and translation are not included.

## Install and use

1. Download and run the installer. Its wizard is currently in Chinese and shows the selected location, options, and actual installation progress.
2. Installation is per user, requires no administrator elevation, and includes .NET. A fresh installation does not create a desktop shortcut, enable startup, or replace Start by default; select those options if desired.
3. Use the five views and type directly to search. Drag app shortcuts and folders into the menu to add them.
4. Choose Windows style or Google style under Settings → Appearance. Folder and website views save their frequent/recent ordering choices.
5. Choose System, Simplified Chinese, or English under Settings → Interface language. No restart is needed; older configurations default to System after upgrading.

Settings displays the current version. Check for updates / Upgrade opens an update page with release notes, download progress, and verification results. After downloading, click Install to open the upgrade wizard. Updates come only from `morrowfactory/istart` GitHub Releases. Preview installations can follow newer previews or stable releases; stable installations only follow stable releases. Installation is not automatic or silent. A failed download or closed update window leaves the current program usable. Development and acceptance-test runs cannot launch an upgrade over a real installation.

The installer is currently unsigned. Upgrades retain items, history, and valid settings. Legacy category data remains stored, although category editing is no longer shown. Hiding an item does not uninstall an app, move a file, or delete browser history.

## Windows Start and exit behavior

- Windows Start menu at the bottom temporarily opens the system menu.
- Switch to Windows Start menu in the tray disables integration and restores native mode as the default.
- The window's × button hides iStart to the tray. Exit in the menu or tray ends the process and releases integration.
- After an abnormal exit, the next launch restores native mode. Uninstallation keeps personal configuration by default.

Account and power actions follow system capabilities and policies. Sign-out and shutdown require confirmation. Windows continues to manage authentication, Windows Hello, and the secure desktop.

## Compatibility and limitations

The target is Windows 10/11 x64. This remains a preview, with incomplete cross-version acceptance. Known builds can opt into capability-checked integration and export diagnostics; preview.17 and earlier only allowed build 26200.

| System | Verified scope |
| --- | --- |
| Windows 11 25H2 x64, 26200.9457 | Start-button and standalone Win-key behavior, Win+R passthrough, and basic recovery checked on the current machine; UIAutomation remains the preferred locator |
| Windows 10 19041–19045; Windows 11 22000, 22621, 22631, 26100 | Opt-in compatibility trials; hooks are installed only after a valid Start button is found; hardware acceptance is pending |
| Other builds, failed detection, or abnormal recovery | Keep native Start; do not guess button coordinates |

Export Start menu compatibility report in Settings saves the build policy, monitor count, button count/bounds, and detection method. It excludes app lists, file paths, and URLs and is not uploaded. See [Windows compatibility](docs/WINDOWS_COMPATIBILITY.md).

Explorer restarts, multi-monitor scaling, taskbar auto-hide, long-running stability, and interactive installer cancellation/failure recovery need further validation. Runtime support and application acceptance are separate: see the [.NET supported-OS matrix](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

## Local data and privacy

- Configuration is stored in `%LOCALAPPDATA%\iStart\library.json`, with one `.bak` backup. Uninstallation does not delete personal items or records.
- Activity is processed locally. App lists, paths, and browsing history are not uploaded, and the entire disk is not scanned. Update checks and downloads are user-initiated GitHub requests.
- App ordering uses cumulative Windows app-switch counts and iStart launches. Folder ordering uses opens and recent-file activity. These are neither complete system frequency records nor exact monthly statistics.
- Website history is bounded to records Chrome and Edge retain. Cookies, passwords, page contents, and private sessions are not read. A combined domain opens its site root.
- Settings can disable external file/folder activity or browser-history reading. Clear iStart history does not clear other applications' history.
- Browser-history snapshots are temporary and local, with cleanup after normal reads. Unreadable, partially failed, or timed-out sources are reported.
- Update downloads use `%LOCALAPPDATA%\iStart\Updates`. Cancellation or failure removes the current download. After success, known caches older than seven days are cleaned while retaining at least the latest two downloads; personal data is untouched.
- `runtime.log` records lifecycle and recovery diagnostics, not keystrokes, URLs, or file contents. It rotates above 64 KiB and keeps one old log.

## Build and validate

Requires Windows and the .NET 10 SDK. Installer builds also require Inno Setup 7.1.0.

```powershell
dotnet run --project src/IStart.App/IStart.App.csproj
./scripts/Test-Prototype.ps1
./scripts/Publish-Preview.ps1
./scripts/Build-Installer.ps1 -Compiler 'C:\path\ISCC.exe' -SkipPublish
./scripts/Build-Installer.ps1 -Compiler 'C:\path\ISCC.exe' -SkipPublish -TestInstall
./scripts/Test-Installer.ps1 -Package "$PWD\artifacts\setup\iStart-QA-Setup-0.2.0-preview.23.exe" -Output "$PWD\artifacts\install-test"
```

Installers and SHA256 files are written to `artifacts/setup/`. The QA installer uses a separate identity. Its output directory must be inside `artifacts/` and must not already exist.

preview.23 publishes only the currently supported framework language resources, excludes PDB symbols, and uses `lzma2/ultra64` solid compression. A bounded output-directory cleanup prevents stale resources from entering a new package. CI additionally runs the self-contained executable. See [Package size](docs/PACKAGE_SIZE.md).

Diagnostic and acceptance entry points:

- `--probe-system <directory>` reads drive and Control Panel counts and checks the available GitHub version; it does not launch system items or an installer.
- `--ui-test <directory>` uses isolated configuration and does not alter real startup settings.
- `--probe-activity <directory>` outputs source counts only.
- `--input-test <directory>` operates native Start and Run and terminates only its own test child. Run it manually on an idle interactive desktop; it is excluded from CI.

GitHub CI checks the build, core behavior, layouts, and isolated install/upgrade/uninstall, retaining temporary artifacts for seven days. Public installers follow the [release procedure](docs/GITHUB_RELEASE.md): validated latest-main CI artifacts, exact source SHA, and verified hashes. Personal configuration and history are excluded.

Windows 10 desktop acceptance uses `scripts/Test-WindowsVm.ps1` in a dedicated VM. The host can use `-PreflightOnly` for a read-only check. See [Windows test lab](docs/WINDOWS_TEST_LAB.md). Preparing scripts or the environment does not establish Windows 10 acceptance.

Inno Setup 7 requires an appropriate commercial license for commercial distribution; see [Inno licensing](https://jrsoftware.org/isorder.php). This project does not copy Open-Shell code.

## Documentation

The default documentation is English. Each page links to its separate Simplified Chinese counterpart; filenames and directories use English names or language codes. See the [complete documentation index](docs/INDEX.md).

- [Architecture](ARCHITECTURE.md): modules, storage, activity sources, and recovery boundaries.
- [Vision](VISION.md): product direction and privacy principles.
- [Execution plan](EXECUTION_PLAN.md): delivery stages and remaining acceptance work.
- [Activity views](docs/ACTIVITY_VIEWS.md): app, folder, and website ordering and source limits.
- [Brand icon](docs/BRAND_ICON.md): Figma source and asset maintenance.
- [Foreground validation](docs/FOREGROUND_VALIDATION.md) and [current-system validation](docs/CURRENT_SYSTEM_VALIDATION.md).
- [My Computer and updates](docs/COMPUTER_UPDATE_VALIDATION.md).
- [Interface localization](docs/LOCALIZATION.md) and [Windows compatibility](docs/WINDOWS_COMPATIBILITY.md).
- [GitHub releases](docs/GITHUB_RELEASE.md) and [package size](docs/PACKAGE_SIZE.md).
