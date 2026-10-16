[English](ARCHITECTURE.md) | [简体中文](docs/zh-CN/ARCHITECTURE.md)

# iStart architecture

## Status and constraints

iStart is a single C#/.NET 10 WPF desktop project with no additional NuGet dependencies. Windows Forms supplies the tray, monitor information, and suspend capability. Inno Setup 7.1.0 produces a Chinese, per-user, self-contained installer.

The target is Windows 10/11 x64. Known Windows 10 builds 19041–19045 and Windows 11 builds 22000/22621/22631/26100/26200 may opt into capability-checked trials; other builds retain native Start. Only build 26200 has basic hardware integration evidence. See [P0 validation](docs/P0_VALIDATION.md).

## Module boundaries

| Module | Responsibilities and boundaries |
| --- | --- |
| MainWindow / Editing / Shell / Websites / Computer | Five fixed views, search, icon/list display, adding and hiding items, activity ordering, Settings, tray, and placement; ordinary-user execution |
| LibraryStore | Items, legacy categories, preferences, and recent records; copy before mutation, write a temporary file, atomically replace, retain one backup; a failed save does not replace in-memory state |
| Localization / Strings.en.json | Central product text and English translations; current-user Windows display language; immediate WPF binding updates; stable navigation and shortcut identifiers |
| SoftwareDiscovery / ShellShortcut | Asynchronous discovery of user/common Start-menu `.lnk` files; Unicode IShellLinkW/IPersistFile parsing; deduplicated valid EXE targets; installer, maintenance, and invalid-entry filtering |
| ShellIconConverter | Shell icon extraction and a bounded cache; generic UNC icons avoid remote icon reads |
| StartMenuAdapter / ShellCompatibility / WinInput | Build policy, UIAutomation/native Start-child discovery, standalone Win and Start-button low-level hooks, combination passthrough, and marked native injection to prevent recursion |
| NativeMenu | Marked SendInput Ctrl+Esc requests for temporary native Start |
| SystemActions | Capability/policy checks for lock, sign-out, switch user, sleep, hibernate, shutdown, and restart; fixed arguments without `/f` |
| App | Current-SID mutex and show/exit signals; bounded graceful-exit waits; background/diagnostic modes; isolated UI-test configuration without real startup changes |
| installer / scripts / CI | Chinese wizard and real file progress, graceful exit before upgrades, uninstall and checksums; separate QA AppId; no server deployment |

The process does not inject into Explorer, modify system binaries, replace authentication, install a service, or install a driver. Temporary native Start access and switching the default to native mode are separate operations.

### Presentation and focus

ShowMenu no longer depends on `adapter.Enabled` for topmost/focus behavior: desktop icons, tray actions, and repeated launches use the same path. Hiding clears Topmost; deactivation hides the menu. Focus retries are bounded to four attempts, 60 ms apart, with a presentation generation preventing a delayed retry from reopening an old window. Search focus uses Input priority so background loading does not delay it.

A repeated launch calls AllowSetForegroundWindow only for an existing iStart process in the same session and at the same path, then sends the show signal. It does not grant foreground access to arbitrary processes. Initial integration still requires Enable integration or the Settings switch; a chosen native mode is not automatically replaced.

ShowMenu restores Normal state, enables ShowActivated, raises the window temporarily, requests SetForegroundWindow, and focuses search. Every user-triggered opening stays temporarily topmost until hidden, regardless of integration mode. Synchronous Deactivated events during presentation must not hide it prematurely. Background startup hides immediately after the first Show rather than at the end of asynchronous initialization.

Once presentation completes, the UI thread sets native HWND focus and synchronizes WPF keyboard/logical focus. If the foreground request fails and no modifier is held, a marked, balanced Alt/Ctrl sequence permits a bounded retry. The program does not change Windows foreground-lock policy or attach other threads' input queues. Logs record success flags, not other apps or typed text. Placement uses the taskbar and physical work area. Shortcut icons come from their resource or target while launch paths remain unchanged.

### Input thread and taskbar recovery

InputHookThread installs and removes low-level keyboard/mouse hooks in a dedicated STA message loop. Callbacks recognize gestures, validate local HWNDs, and dispatch UI work. Pending open requests are coalesced. Lifecycle generations reject work queued before a disable/re-enable cycle.

If the UI heartbeat is older than two seconds, keyboard input passes through and the Win gesture is cleared. Subsequent mouse-down events are not intercepted; an already-intercepted mouse-up is still consumed so Explorer does not receive half a gesture. Windows 10 StartTarget records the actual ButtonWindow and rechecks its class, parent taskbar, visibility, and current bounds on click. UIAutomation snapshots retain a 1.5-second freshness limit.

TaskbarCreated, WM_DISPLAYCHANGE, and WM_SETTINGCHANGE clear snapshots. Snapshot generations reject discovery results from before the change. A COM timeout tries native-window enumeration without accumulating blocked automation tasks. Enable requests share one pending task; bounded retries that still fail leave native Start available.

### Screenshots

ScreenshotHotkey registers a global combination with RegisterHotKey/MOD_NOREPEAT after the main HWND is created. HwndSource routes WM_HOTKEY to capture. Normal shutdown unregisters the hotkey and removes its hook independently of Start integration. A missing ScreenshotShortcut defaults to Alt+Shift+A; unknown values are not registered. WeCom configuration is neither read nor modified. Conflicts keep the menu button available and are shown in Settings, with alternative combinations or Off. Interactive acceptance mode does not register global hotkeys; pure UI tests do not install global hooks.

MainWindow.Screenshot hides the menu and waits for repaint. Screenshot uses GDI CopyFromScreen for the selected monitor's physical pixels. ScreenshotWindow / CaptureSurface provide region movement/resizing and a floating annotation toolbar. Start cannot reopen the menu while editing. Export crops the original pixels and annotations and excludes the mask, selection border, and toolbar. Text and strokes remain in local memory; mosaic uses block sampling. Only Copy or Save outputs an image. PNG encoding completes before a temporary file replaces the destination; temporary files are cleaned. Capture is local and never enters activity history. Single-monitor capture is supported; real desktop hiding, clipboard behavior, and different DPI settings still require interactive acceptance.

### Appearance and test environment

Appearance supplies Windows/Google colors and shapes through DynamicResource in App.xaml and MainWindow.xaml. Settings saves the theme independently and calls SetTheme without restarting the adapter or changing privacy/startup preferences. WPF, native Shell icons, and the existing fonts are retained without an additional UI library. Inputs retain PART_ContentHost; list items retain drag borders and keyboard-focus feedback. Themes do not change storage, integration, or recovery. See [Material validation](docs/MATERIAL_VALIDATION.md).

`scripts/Test-WindowsVm.ps1` is a test-environment tool outside the product runtime. It invokes existing probes/regressions/input tests only in an identified Windows 10 x64 VM, isolating configuration and reports by run. The host may run read-only preflight; actual input/focus/recovery tests belong in the VM. VirtualBox, VM images/disks, account credentials, and local reports are not distributed with iStart or committed to the repository.

## Local storage and localization

`%LOCALAPPDATA%\iStart\library.json` uses schemaVersion 2 and migrates schema 1. Unknown versions are rejected. Invalid JSON may fall back to `.bak`; an unknown newer schema is not silently replaced with an older backup.

- Entries: id, target, title, categoryId; array order retains manual ordering.
- Categories: id and name; legacy order and the undeletable uncategorized identity are retained for compatibility, although category-editing UI was removed.
- Launches: target, title, openedAt, count; at most 100 unique targets; a record means launch was successfully handed to Windows.
- Preferences cover hidden apps, history reads, frequent/recent ordering, integration, startup, screenshot shortcut, icon view, and Theme. Windows/Google are valid themes; missing or unknown values fall back to Google.
- Language: `System`, `zh-CN`, or `en`, within schema 2. Missing, empty, or unknown values become System. GetUserDefaultUILanguage reads the current user's display language; Chinese maps to Simplified Chinese, other unsupported languages to English. Windows language and regional formatting are not changed.

Language changes first persist through LibraryStore, then update bindings, navigation, search results, tray, and system content. The existing Settings controls and unsaved checkboxes are retained. StartMenuAdapter and screenshot registration are not restarted; item names, privacy, and startup are unchanged. Shortcut labels are separate from stored identifiers; navigation icons use fixed GUIDs. The seven Control Panel labels are translated by category ID; volume labels retain user text. Shell names, native dialogs, the Chinese installer, and GitHub release notes retain their own language. See [Localization](docs/LOCALIZATION.md).

Data remains local. Removing an item never deletes its source. Legacy category operations retain migration/recovery support; deleting a category through the storage layer moves its items to the uncategorized identity. Store-app discovery supplements traditional shortcuts; apps without a valid Shell entry are not guaranteed to appear. Current app ordering and source limits are described in [Activity views](docs/ACTIVITY_VIEWS.md).

## Start integration and recovery

StartMenuAdapter runs one background taskbar scan and caches StartButton bounds. Callbacks check events and geometry and dispatch UI work; they do not read configuration or scan synchronously. Mouse validation includes the root window, live taskbar bounds, and snapshot freshness. Empty results or discovery exceptions update retry status without withdrawing the user's preference or disabling keyboard hooks. Old snapshots still require freshness and window validation; stale clicks pass through. Successful scans restore button interception. A timed-out pending scan is not repeatedly duplicated.

Standalone Win release suppresses native Start and shows iStart; Win combinations pass through. Marked temporary-native injection prevents recursion. Enable writes `session.active`; normal exit removes it. A leftover marker on the next launch releases integration, saves native mode, and explains recovery. Windows removes process-owned hooks at exit. Native recovery after terminating a separate test child has been checked.

Windows can remove low-level hooks after callback timeouts; reliable detection of every such removal is not yet established. Explorer restarts, multi-monitor/auto-hide/scaling, lock, and sleep recovery still need isolated acceptance. A build allowlist is not a complete compatibility guarantee. See [Microsoft's low-level keyboard hook guidance](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc).

Closing normally cancels window closure and hides to the tray. Explicit RequestExit from the menu, tray, upgrade/uninstall signal, or system session ending allows exit. RuntimeLog records lifecycle/adapter state, rotates at about 64 KiB, retains one old log, and does not record keystrokes or file paths. See [Resident recovery](docs/RESIDENT_VALIDATION.md).

## Account and power actions

Policies are read from current-user/computer policy keys; sleep/hibernate query system capability. Unsupported actions are hidden. Shutdown/restart use fixed `/t 0` arguments without `/f`; session-changing actions require product confirmation. Parameter construction and capability queries are tested. Disruptive actions have not been exercised in the daily work session, so complete account/power acceptance is not claimed.

## Installation lifecycle and packaging

The Chinese wizard covers welcome, location, options, actual file progress, and completion. Installation is per user without elevation. Desktop shortcuts, startup, and integration are off by default and opt-in. Installation success is distinct from Start-integration success: capability checks run in the real user process and failures keep native Start.

preview.23 uses `SatelliteResourceLanguages=en;zh-Hans`: English neutral framework resources, Simplified Chinese satellites, and embedded product English translations. PDB symbols stay in development output. The full self-contained WPF/WinForms runtime, COM, regional formatting, and recovery remain intact; unsupported WPF trimming and manual framework-DLL removal are not used. `lzma2/ultra64` solid compression needs an approximately 64 MiB decompression dictionary during installation. Publish-Preview checks the absolute output path is inside repository artifacts and is not a reparse-point directory before cleaning only `preview-win-x64`. CI runs the actual published EXE before isolated installation checks. See [Package size](docs/PACKAGE_SIZE.md).

Before upgrade or uninstall, the installer calls its own `--exit` and waits for the current user's instance to release the mutex. A timeout stops installation with an explanation instead of killing a process. Uninstall removes installed program files and the startup entry but keeps personal items. QA uses a separate AppId and skips real exit/startup operations so it does not close the daily installation.

Inno supplies cancellation, error, and rollback mechanisms, but disk-full, permission-denied, locked-file, and interrupted-upgrade cases still need hardware validation. Arbitrary upgrade failures are not guaranteed to restore every old file. Code signing is not implemented; commercial distribution must meet Inno and bundled .NET licensing requirements.

## My Computer and in-app updates

ComputerItems uses DriveInfo for partitions and capacity of ready local disks. Mapped network drives are listed without remote capacity queries. A current-user Shell.Application enumeration reads at most 16 Control Panel category-root entries and keeps seven category IDs: System and Security, Network and Internet, Hardware and Sound, Programs, User Accounts, Appearance and Personalization, and Clock and Region. Ease of Access and All Control Panel Items are excluded. COM objects are released in finally.

MainWindow.Computer reads asynchronously with a five-second waiting budget and does not duplicate an unfinished task. Compact three-column disks, Recycle Bin, Network, and expanded Control Panel entries support name search. System items open through one explorer.exe ArgumentList argument, do not enter personal file history, and do not empty Recycle Bin or perform other management operations. ShellIconConverter uses PIDLs for virtual-entry icons.

GitHubUpdater accesses only the fixed repository's public Releases API when the user opens updates: at most 100 releases, 2 MiB response size, and a 30-second budget. Versions are compared numerically. Preview users can follow newer preview/stable versions; stable users do not follow previews. Drafts are excluded. Downloads require an exact versioned x64 asset name, the fixed repository URL, a size below 256 MiB, and GitHub's SHA256 digest.

Downloads have a 15-minute budget, show actual byte progress, and write `.part` files inside a separate GUID directory. Size and SHA256 must match before renaming to EXE. Closing cancels; failures remove the current temporary data while keeping the current installation. After success, cleanup is limited to known updater-owned caches older than seven days and outside the latest two downloads; unknown contents are not recursively removed.

UpdateWindow shows AssemblyInformationalVersion without its SHA suffix, notes, progress, and success/failure. A further user click starts the Chinese Inno wizard with the existing directory/startup preference. PrepareToInstall requests graceful exit; cancelling before installation does not preemptively terminate the running program. Only installed copies with `unins000.exe` can launch updates; development/UI-test runs cannot. Configuration/schema remain unchanged. There is no account token, activity upload, or automatic silent installation. TLS and hashes do not replace publisher signing.

SmokeTest.Updater uses isolated HTTP fixtures for channels, version comparison, source allowlists, successful downloads, hash failures, and cancellation cleanup; it never executes fixture installers. `--probe-system` outputs version and system-item/update counts without real drive paths or Control Panel contents. QA verifies first install/upgrade/uninstall. Complete future-version in-app upgrades, interactive cancellation, and abnormal installation recovery remain separate acceptance work. See [My Computer/update validation](docs/COMPUTER_UPDATE_VALIDATION.md).

## Compatibility trials

ShellCompatibility separates permission to try from hardware acceptance. Known Windows 10 19041–19045 and Windows 11 22000/22621/22631/26100/26200 require explicit opt-in. Unknown builds are rejected. Even an allowed build must discover a valid button before installing keyboard/mouse hooks.

Windows 10 prefers visible native Start children belonging to the taskbar root, with a UIAutomation StartButton/Button fallback. Windows 11 prefers UIAutomation and falls back to the same native enumeration. Taskbars fail independently. All candidates require finite coordinates, valid sizes, and taskbar bounds; detection does not depend on a localized Start name or guessed screen corners. Mouse callbacks validate the current root handle, window rectangle, and 1.5-second freshness. Combination passthrough, native access, and abnormal-exit recovery remain unchanged.

ShellProbe shares one asynchronous scan with a three-second wait. A timeout does not accumulate blocked scans. Settings can save a read-only report with app/OS versions, trial policy, monitor/button counts, methods, and geometry. It excludes usernames, file/browsing history, and full logs and is not uploaded. `--probe-shell` uses the same reader.

Tests cover known/unknown build policy, negative coordinates, invalid geometry, an off-screen nonactivating native Start fixture owned by the test process, hidden/out-of-bounds exclusion, recovery, and layouts. Current Windows 11 build 26200 input checks cover Win, Start, Win+R, temporary native mode, disable, and abnormal termination of a separate child. Windows 10 branches/fixtures are not Windows 10 system acceptance. Other builds, multi-monitor scaling, and Explorer restarts remain pending. See [Windows compatibility](docs/WINDOWS_COMPATIBILITY.md).

## Validation and publishing

SmokeTest checks persistence protection, migration, ordering, discovery filtering, recent records, and WPF list/icon rendering. InputIntegrationTest is an interactive-desktop specialist test, including native baseline, Win/button/combinations, temporary native mode, disable, and child-process recovery. Test-Installer only accepts the QA identity and new directories inside artifacts, checking silent first install, overwrite, and uninstall.

Windows CI retains core results, layouts, checksums, and installation results for seven days. It does not automatically publish a stable release. Complete Windows 10/other Windows 11, multi-monitor, and installation-failure matrices remain prerequisites for full release acceptance. See the [execution plan](EXECUTION_PLAN.md) and [current-system record](docs/CURRENT_SYSTEM_VALIDATION.md); outstanding manual inputs and session-disruption cases must remain explicit.

build.yml binds ProductVersion to the checkout SHA. release.yml takes the latest main SHA, a successful push/main run, and an authorization note, downloading only the installer allowlist. Publish-GitHubRelease.ps1 checks version, source workflow, and hashes, creates a draft, publishes it, and downloads the public files again. release-manifest.json records exact provenance and actual protection state. Releases are serialized with minimal GitHub Token permissions, no server, and no new production secret. Conversation authorization was used for recorded releases without claiming enforced GitHub approval. See [GitHub release procedure](docs/GITHUB_RELEASE.md).

## Historical implementation notes

These describe earlier previews. Current activity views supersede the old recommendation/category UI.

- **Store discovery:** read-only Shell.Application / AppsFolder enumeration retains IDs containing `!`. COM objects are released per item, with cancellation and a five-second budget. Traditional and store apps form the search candidates. Store entries launch via one explorer.exe ArgumentList argument and are not treated as disk files. Early recommendations were limited to 12; current My Apps uses the limits in Activity views.
- **Store icons:** SHParseDisplayName resolves AppsFolder to a PIDL; SHGetFileInfo with SHGFI_PIDL/SHGFI_ICON extracts the icon into a frozen bitmap. The 256-item cache is retained, with HICON/PIDL cleanup in finally. Failures use a generic EXE icon.
- **Windows recent files:** RecentDocuments reads up to 200 newest current-user Recent `.lnk` files with a five-second wait. It resolves targets and shortcut-modification times, filters network/missing/folder/EXE/LNK targets, deduplicates, and merges with Launches to 100 files. External history stays in memory; ShowWindowsRecentDocuments independently controls reading/display and discards late results when disabled. Shortcut times are approximate ordering evidence.
- **Cross-app history, preview.10:** JumpListDocuments reads the latest 60 AutomaticDestinations structured-storage files: at most 16 MiB each, 200 streams, and 1 MiB per stream. IPersistStream loads embedded Shell Link targets without Resolve or execution. IStorage/enumerator/IStream are released in finally. Stream/container modification times are approximate; DestList versions and CustomDestinations are not parsed.
- **LibreOffice:** read-only `4/user/registrymodifications.xcu`, at most 8 MiB, with XML DTD/external resolution disabled. Only PickList/OrderList/HistoryItemRef file URIs are used. Sequence order is preserved; configuration modification time is approximate.
- **WPS:** only HKCU/Software/Kingsoft/Office/6.0/{wps,et,wpp,pdf,ofd}/RecentFiles/files, up to 100 numbered subkeys' path values. No account/device/cloud records. Unknown open times remain unknown rather than fabricated; source sequence is preserved.
- **History safety:** sources fail independently. Network, missing, directory, executable, and script targets are excluded from documents. The merged limit is 100. Disabling external reads clears caches and rejects late results. Only configuration/link metadata is read, not document contents; external software/system history is not modified.
- **Recommendation display, preview.11:** new RecommendationsEnabled defaulted false; the entire SoftwarePanel collapsed without space. Settings was the only enable path. Search results remained independent. This UI and preference use were superseded by preview.16.
- **Mixed-item separator, preview.12:** EntryGroups classified apps through Shell IDs, executables/scripts, and shortcut targets, with a bounded cache. In unsearched All Favorites, a ListCollectionView grouped programs above files/folders, drawing a line only for the second group. Source Entries were not rewritten. Single-kind, category, recent, and search views did not add a separator. The mixed view was later replaced.
- **Windows 11 Fluent, preview.13:** Appearance added theme-specific spacing, search borders, button text, fonts, icon size, and status resources. Windows tiles became 64 DIP, typically six columns with container margins; smaller windows wrap. Google values were retained. CategoryIconConverter loads frozen navigation PNGs; search/account use embedded resources. SVGs remain maintenance sources, with no SVG renderer or network dependency. PART_ContentHost and focus/integration behavior were preserved.
- **Window rendering:** standard Windows titlebar, border, and shadow remain; the light layers do not enable native Mica. Figma supplied Noto Sans SC, while Windows uses Segoe UI Variable with Microsoft YaHei UI fallback. Google keeps its prior font. Runtime names and content are real, not mockup sample replacements.
- **Brand, preview.14:** Assets/Brand contains Figma SVG, transparent PNG, and nine-size ICO. Both themes use the PNG; window/tray use the embedded ICO; ApplicationIcon embeds it in the EXE. The installer uses the same ICO and installs an independent file for shortcuts/uninstall. Tray clones are disposed. SVG conversion is maintenance-only. See [Brand icon](docs/BRAND_ICON.md).
- **Files/folders, preview.15:** RecentDocuments.Files filters before merging. FrequentFolders reuses ShellShortcut and JumpListDocuments directory mode, asynchronously refreshing file/folder caches within the existing five-second work. Added directories retain order; activity/time suggestions fill space. Suggestions and session hides stay in memory; pinning persists an item. The external-history switch clears both caches without a schema change. See [Recent files/folders](docs/RECENT_FILES_FOLDERS.md).
- **Activity views, preview.16:** MySoftware matches cumulative AppSwitched/iStart activity, at most 30 apps. SoftwareDiscovery adds EXE/AppUserModel.ID metadata. Four fixed views replaced recommendations/categories while Entries/Categories remained compatible. FrequentFolders adds direct-folder recent ordering without rewriting storage. WebsiteHistory reads bounded Chrome/Edge snapshots using system winsqlite3; MainWindow.Websites handles refresh and same-page ordering. Preferences persist, external suggestions/history stay in memory, and snapshots are cleaned in finally. No new NuGet or runtime-network dependency was added. My Computer later became the fifth view.

## Documentation languages

Root documents and `docs/*.md` are the English defaults. Separate Chinese counterparts live in `docs/zh-CN/` with the same English basenames. Every page links to the other language; shared assets and source paths remain unchanged. [The index](docs/INDEX.md) groups current guides and historical acceptance records. This layout is documentation only and is not part of the application payload.
