[English](README.md) | [简体中文](docs/zh-CN/README.md)

# iStart

A compact, personal Windows Start menu for frequently used apps, recent files, folders, and websites. Keep everyday shortcuts together and your desktop clear.

**Version: 0.2.0-preview.27**

This version opens iStart at the bottom left of the selected monitor's working area, including the first launch, app icon, tray, repeated opens, and native-menu mode. It retains background icon loading, activity-backed apps and files, Windows/Google layouts, and automatic/manual upgrades. The installer includes the offline runtime and both interface languages. See [Package size](docs/PACKAGE_SIZE.md).

The interface follows the Windows display language by default. You can select Simplified Chinese or English in Settings and apply the change immediately. Chinese display languages use Simplified Chinese; English and other currently unsupported languages use English. The menu, Settings, tray, screenshot editor, and updater are localized. App names, filenames, URLs, and release notes retain their original text. See [Interface localization](docs/LOCALIZATION.md).

[Download the Windows x64 installer](https://github.com/morrowfactory/istart/releases/download/v0.2.0-preview.27/iStart-Setup-0.2.0-preview.27.exe) · [Release and checksums](https://github.com/morrowfactory/istart/releases/tag/v0.2.0-preview.27) · [All releases](https://github.com/morrowfactory/istart/releases)

**Fixed in preview.25:** discover used apps without Start-menu shortcuts, include FreeCAD's saved recent documents, prevent stale paths from consuming valid discovery slots, isolate history-source errors, search folders before limiting results, and refresh activity when returning to My Apps. See [Activity views](docs/ACTIVITY_VIEWS.md). Existing [themes](docs/THEME_VALIDATION.md), [updates](docs/UPDATES.md), and [integration recovery](docs/RESIDENT_VALIDATION.md) remain available.

## Features

| View | Contents | Ordering and limits |
| --- | --- | --- |
| My Apps | Installed or portable apps with local activity evidence and apps you add | Most used first; at most 30 apps, including search results |
| Recently Opened | Valid files recorded by iStart, Windows, WPS, LibreOffice, FreeCAD, and other supported sources | Most recent first, deduplicated, at most 100 files; folders and programs are excluded |
| Frequent Folders | Added folders and folders found in local activity | Frequent first by default; switch to recent folders on the same page; suggestions fill available space |
| My Websites | Bounded Chrome and Edge history, combined by domain | Frequent first by default; switch to recent websites on the same page; at most 30 sites |
| My Computer | Disk partitions, available space, Recycle Bin, Network, and seven expandable Control Panel categories, excluding Ease of Access | Open the corresponding Windows page; search within this view |

- Switch between Windows 11 Fluent and Google styles in Settings. The choice is applied immediately and saved.
- A consistent iStart icon is used for the desktop shortcut, taskbar, window, tray, installer, and menu.
- Drag apps and folders into the menu; switch between icons and a list, search, and hide items.
- Windows Start menu recovery, account actions, power options, and the tray remain available.
- The separate app recommendation panel, Uncategorized view, category creation, and category-name field have been removed. Legacy item data remains compatible.
- While iStart remains running, reopening shows the retained page, rows, search, selection, and scroll position first. Activity refresh starts after a 200 ms idle delay; file checks and ranking for that refresh run in the background. Unchanged rows are reused, and a failed refresh keeps the previous content. This memory cache does not survive exiting the application.

Content comes from the current user's local system and activity records. Documentation screenshots use test data, including synthetic disk capacities. See [Activity views](docs/ACTIVITY_VIEWS.md) and the [documentation index](docs/INDEX.md).

Activity records are bounded local evidence, not complete system-wide statistics. FreeCAD reads use its saved MRU list; missing or unsaved records cannot be reconstructed. See [source limits](docs/ACTIVITY_VIEWS.md).

### Start integration and foreground behavior

**Reliability in preview.26:** repeated opening/focus failures were reported on the current Windows 11 PC, including in native-menu mode. This version moves Shell icon reads off the UI thread, coalesces software scans with a 30-second successful-result cache, protects restored menu dimensions, and removes simulated modifier keys from focus retries. Isolated checks do not establish live Win-key acceptance or the cause of the reported unintended text deletion. Keep the affected copy exited during investigation; installation alone does not complete interaction acceptance. Build 26300 remains in native-menu mode because it is outside the existing integration trial list. See [current investigation and checks](docs/FOREGROUND_VALIDATION.md#current-reliability-investigation--2026-10-29).

Since preview.21, input hooks run on a dedicated message thread to reduce failures caused by a busy UI. On Windows 10, native Start-button clicks revalidate the button bounds and HWND. Taskbar recreation and display or system-setting changes invalidate cached locations. A native-window fallback handles automation timeouts, and startup uses bounded retries. If the UI is unresponsive for about two seconds, subsequent native input is allowed through. These changes target known Windows 10/11 builds; they do not establish Windows 10 hardware acceptance.

Opening iStart from its icon also uses a temporary topmost window and bounded focus retries, even in native-menu mode. Switching to another window hides it. Reopening the shortcut hands foreground permission to the existing instance. Use Enable integration at the bottom of the menu to opt in; otherwise, the system Start button continues to open Windows Start.

**Placement in preview.27:** the first launch, app icon/repeated launch, tray, and integrated Start entry points open at the bottom left of the selected monitor's working area, with up to eight physical pixels of spacing. Placement also applies in native-menu mode and does not follow a centered Start button or a previously dragged window position. Start/tray clicks select their monitor; other opens capture the foreground monitor before activating iStart. Background startup remains hidden.

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

Starting with preview.24, installed copies check GitHub at startup while online and periodically thereafter. New versions add red dots to Settings and the upgrade button. The upgrade section is at the top of Settings; its page shows the current version, target version, release notes, and download progress. **Automatic updates is on by default**, including for older configurations: download and verify in the background, then install when the menu is hidden, no Settings or screenshot window is open, and the current Windows session has been idle for a minute. Uncheck it for notification-only manual upgrades; the preference saves immediately. Installation shows progress, preserves personal items/settings, and does not reboot Windows or force other apps closed. Only the fixed `morrowfactory/istart` Releases are accepted. Preview copies can follow newer previews/stable versions; stable copies follow stable releases. Development/test runs cannot start background upgrades or execute an installer. See [Updates and validation](docs/UPDATES.md).

The installer is currently unsigned. Upgrades retain items, history, and valid settings. Legacy category data remains stored, although category editing is no longer shown. Hiding an item does not uninstall an app, move a file, or delete browser history.

## Windows Start and exit behavior

- Windows Start menu at the bottom temporarily opens the system menu.
- Switch to Windows Start menu in the tray disables integration and restores native mode as the default.
- The window's × button hides iStart to the tray. Exit in the menu or tray ends the process and releases integration.
- After an abnormal exit, an ordinary launch restores native mode. An explicit `--enable-menu` request can re-enable integration, including in an existing background instance. Normal session ending releases integration and clears its marker while retaining the preference. Uninstallation keeps personal configuration by default.

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
- Activity is processed locally. App lists, paths, and browsing history are not uploaded, and the entire disk is not scanned. The updater checks the fixed public GitHub repository automatically; automatic downloads can be disabled in Settings. These requests do not send local activity.
- App ordering uses cumulative Windows app-switch counts and iStart launches. Folder ordering uses opens and recent-file activity. These are neither complete system frequency records nor exact monthly statistics.
- Website history is bounded to records Chrome and Edge retain. Cookies, passwords, page contents, and private sessions are not read. A combined domain opens its site root.
- Settings can disable external file/folder activity or browser-history reading. Clear iStart history does not clear other applications' history.
- Browser-history snapshots are temporary and local, with cleanup after normal reads. Unreadable, partially failed, or timed-out sources are reported.
- Update downloads use `%LOCALAPPDATA%\iStart\Updates`. Cancellation or failure removes the current download. After success, known caches older than seven days are cleaned while retaining at least the latest two downloads; personal data is untouched.
- `runtime.log` records lifecycle and recovery diagnostics, not keystrokes, URLs, or file contents. It rotates above 64 KiB and keeps one old log.

## Local workspace

The local project root is `E:\workspace\istart`. Keep development tools in `.istart-tools/`, virtual machines and private test materials in `.istart-testlab/`, and design previews in `artifacts/design-previews/`. All three are ignored by Git and excluded from release assets. Run development commands from the project root and write generated output under `artifacts/`.

The former adjacent tool/test-lab paths and the original preview directory are compatibility junctions pointing into this project, with no duplicate data. Installed application files and `%LOCALAPPDATA%\iStart` remain in their normal locations.

Local release storage retains the latest **five published versions**, one verified installer with its SHA256 and source manifest per version, under `artifacts/releases/<version>/`. Remove superseded local/QA installers and obsolete test runtimes after verifying the retained copies. Source/Git history, developer tools, VM baselines, personal recovery backups and necessary test evidence are separate from this limit. GitHub Releases retention is unchanged.

## Build and validate

Requires Windows and the .NET 10 SDK. Installer builds also require Inno Setup 7.1.0.

```powershell
dotnet run --project src/IStart.App/IStart.App.csproj
./scripts/Test-Prototype.ps1
./scripts/Publish-Preview.ps1
./scripts/Build-Installer.ps1 -Compiler 'C:\path\ISCC.exe' -SkipPublish
./scripts/Build-Installer.ps1 -Compiler 'C:\path\ISCC.exe' -SkipPublish -TestInstall
./scripts/Test-Installer.ps1 -Package "$PWD\artifacts\setup\iStart-QA-Setup-0.2.0-preview.27.exe" -Output "$PWD\artifacts\install-test"
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
