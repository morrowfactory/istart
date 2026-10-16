[English](P0_VALIDATION.md) | [简体中文](zh-CN/P0_VALIDATION.md)

# P0 technical validation record

Date: 2026-10-13. Historical version: 0.2.0-preview.2, with a runnable menu, basic Windows 11 integration, and a Chinese installer. P0–P4 had not completed all acceptance gates; this was not a formal Windows 10/11 compatibility certification. See the [README](../README.md) for current functionality and support boundaries.

## Passed checks

| Check | Evidence |
| --- | --- |
| Current OS | Windows 11 25H2 x64, 26200.9457. The legacy `ProductName` reported Windows 10; classification used the actual build. |
| Compilation | .NET SDK 10.0.401; Release build with 0 warnings/errors. |
| Core behavior | Add, deduplication, category ordering, reload, removal without deleting source files, configuration recovery, unknown-version protection, and in-memory fallback after save failure. |
| New data behavior | Schema 1→2 preserved favorites; recent counts, disabling, 100-item limit, and clearing passed; deleting a category retained favorites. |
| Application recommendations | Synthetic Chinese-shortcut filtering, target deduplication, recommendation preference/exclusions, and ordering using iStart launch counts. |
| Layout | WPF list/icon rendering; icons obtained through Windows Shell. |
| Windows 11 integration | Start-button click and a standalone Windows-key press each triggered one custom callback without also opening the native menu. |
| Native menu and shortcuts | Win+R did not trigger the custom callback; Ctrl+Esc temporary native handoff did not recurse; disabling integration restored native Windows-key behavior. |
| Abnormal recovery | A separate child process was terminated after installing hooks; the Windows key then opened the native menu. Only that test child was terminated. |
| Packaging | Inno Setup 7.1.0 Chinese installer, self-contained win-x64 payload, SHA256 file. |
| Installer pages | Real captures of welcome, location, options, ready, file installation at 2%, and completion. Basic Chinese rendering checked; scaling remained pending. |
| Installation lifecycle | Silent first install, overwrite, and uninstall passed with a separate QA AppId and a Chinese path containing spaces. The uninstall log reported `Removed all? Yes`; no reboot required. |

Core validation: `scripts/Test-Prototype.ps1`. Start-integration results: local `artifacts/input-final/input-result.txt`. That check installed callbacks rather than opening the real menu window and did not prove complete interactive acceptance. The observed native foreground process was `SearchHost`; comparisons used the measured baseline rather than hardcoding one process name for every OS.

`scripts/Test-Installer.ps1` used a QA installer without launching the real menu, registering startup, or closing the production instance. Results: `artifacts/install-final/result.txt`. Uninstall removed the test installation; small logs were retained. No production server, production backup, or business data changed.

## Not yet passed or performed at this milestone

- No Windows 10 or other Windows 11 test environment was available. Only build 26200 was allowlisted for trial integration; unknown versions retained native behavior.
- Explorer restart, taskbar alignment/auto-hide, multiple displays, scaling, return from lock, and sleep recovery.
- Complete mouse dragging, grid ordering, context management, undo, keyboard accessibility, and high contrast. Model/event checks did not substitute for complete interactions.
- Account/power capability and policy checks used fixed arguments, and checks ensured `/f` was not used. Sign-out, restart, sleep, and other session-disrupting actions were not executed.
- Reliable historical frequency evidence was unavailable. This milestone used installed candidates and iStart records, without reading system-wide history.
- Installation cancellation, permission denial, insufficient space, files locked by a running app, interrupted upgrades, and full failure rollback.
- Code signing, automatic updates, stable release, and complete compatibility certification.

## Integration and fallback at this milestone

Process-owned low-level keyboard/mouse hooks and UI Automation located Start. There was no Explorer injection, system-file modification, service, or driver. Cached Start bounds had to be fresh and match the current taskbar. At this early milestone, a background detection failure disabled integration; an unfinished detection task was not repeatedly recreated.

Native mode was the default; enabling integration required an explicit choice and a version check. Temporary native access did not change the preference. The tray could restore default native mode, and exit released hooks. A leftover `session.active` marker restored native mode on next launch. Reliable detection of hooks silently removed after timeout was not implemented; see [Microsoft's low-level keyboard-hook documentation](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc). Later previews changed recovery behavior; see [resident validation](RESIDENT_VALIDATION.md) and [compatibility](WINDOWS_COMPATIBILITY.md).

Favorites JSON used atomic replacement with one previous copy. Older previews should not be mixed after a schema upgrade. Uninstall preserved configuration and source files. Inno's transaction/cancellation behavior did not count as acceptance of untested upgrade-failure cases.

Open-Shell's root license is [MIT](https://github.com/Open-Shell/Open-Shell-Menu/blob/master/LICENSE); no code had been copied or linked.

## Package and CI at this milestone

`iStart-Setup-0.2.0-preview.2.exe` and `.sha256` were produced in `artifacts/setup/`; a ZIP also supported running the complete directory independently. Failed compatibility checks retained native behavior. Current-user startup was optional and off by default.

Windows CI checked build, core behavior, layout, self-contained packaging, and isolated QA install/overwrite/uninstall. Artifacts were retained for 7 days. Unattended runners did not run desktop hook tests or automatically create a release. At the date of this record, development was submitted through a Chinese PR and had not yet been merged or publicly released.

## Installer evidence

These historical reference captures show the Chinese installer UI:

- [Welcome](installer/welcome.png)
- [Options](installer/options.png)
- [Actual installation progress](installer/progress.png)
- [Completion](installer/complete.png)

## Subsequent validation on the current PC

Real internal dragging, search, addition, rename, relocation, and whole-menu process recovery were later checked. See [current-PC validation](CURRENT_SYSTEM_VALIDATION.md) for its scope and remaining work. Unperformed keyboard, cross-window, and session actions must not be marked as passing.
