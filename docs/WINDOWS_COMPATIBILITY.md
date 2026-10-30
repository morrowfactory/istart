[English](WINDOWS_COMPATIBILITY.md) | [简体中文](zh-CN/WINDOWS_COMPATIBILITY.md)

# Windows compatibility trials and validation

## Build 26300 development correction — 2026-10-30

The reported slow/unavailable click was on the system Start button. Public preview.28 rejects the current PC's build 26300 before integration, so that click and the Windows key can continue to invoke Windows Start; the resident-menu cache does not participate in this path. Menu display-call timings do not measure this native path or end-to-end input latency.

Development code admits build 26300 to the existing explicit, capability-checked trial, without changing hook gestures, foreground policy, or recovery. Unknown builds such as 26400 remain rejected. Both Windows 10 and 11 prefer a valid native Start child when one exists, otherwise use UI Automation. Read-only discovery on the current 26300 PC found one UI Automation button at 0,1032,55,48; this does not establish live Win-key/button acceptance. Isolated tests cover cancellation before hooks, missing-button rejection on 26300, and unknown-build rejection. No hooks or input were installed/sent on the working desktop for this check.

preview.21 combined foreground and integration fixes: opening from the app icon in native mode retains a temporary topmost layer and retries focus; a second launch grants foreground permission to the existing instance; the footer provides Enable Integration. On the current Windows 11 PC, three opens over a maximized background reached the foreground with search focus, switching away dismissed the menu, and minimized state restored. After using the actual enable button, the Windows key and system Start button opened the real iStart window. Isolated test configuration/windows were closed and the previous preview.20 was restored. Second-launch permission handoff was code-reviewed, not validated on Windows 10. These results do not establish a Windows 10 fix.

Hardening on 2026-10-16 moved low-level keyboard/mouse hooks to a dedicated message thread, validated native Windows 10 Start clicks at event time, invalidated snapshots on taskbar/display changes, used native fallback on UI Automation timeout, and bounded startup retries. Window fixtures covered moved/destroyed buttons, rejection of stale detection results, and recovery. Current Windows 11 input validation included a 1,300 ms UI-blocking probe. There was no real Windows 10 acceptance evidence for the user's intermittent opening problem.

[Microsoft's low-level keyboard-hook documentation](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc) describes silent removal after timeout and recommends a dedicated thread; [taskbar documentation](https://learn.microsoft.com/en-us/windows/win32/shell/taskbar) defines `TaskbarCreated`. System hook timeouts, foreground-lock policy, and Explorer files were not modified.

## Trial eligibility versus actual validation

The version-policy baseline below originated in preview.18 on 2026-10-15. Public version, source, and installer hash are established by GitHub Releases.

| OS build | Integration policy | Evidence |
| --- | --- | --- |
| Windows 10 19041–19045 | Explicit opt-in; integrate after successful button detection | Version-policy and native Start-structure fixtures passed; no real Windows 10 application run accepted. |
| Windows 11 22000 / 22621 / 22631 / 26100 | Same opt-in policy | Version branches passed; no real run on these builds accepted. |
| Previously checked Windows 11 26200 | Same opt-in policy | One UI Automation button discovered in the historical run; input/recovery checks passed. |
| Current Windows 11 26300 (development code) | Same opt-in and valid-button requirements; preview.28 retains native behavior | One UI Automation button discovered read-only; live input/recovery acceptance pending. |
| Older/unknown builds, including tested Server build 20348 | Retain native Start | Rejection-policy tests passed. |

The runtime's official support and this app's trial-integration policy are separate questions. This table is not a complete compatibility claim.

## Validation

- Release build had no warnings/errors; data, themes, compact layout, and recovery regressions passed.
- Off-screen, nonactivating, process-owned windows simulated a traditional taskbar's Start child. Real native enumeration excluded hidden/out-of-bounds windows; cleanup touched only these fixtures.
- Current Windows 11 read-only discovery retained UI Automation and found one button without guessed coordinates.
- Local input tests passed for standalone Windows key, Start button, preserved Win+R, temporary native access, explicit disable, and recovery after an isolated child process exited abnormally. Everyday iStart exited normally before testing and was restored afterward. Other user software was not terminated; Explorer and personal configuration were unchanged.
- Local diagnostics record OS version, policy, and button geometry without personal files/URLs or uploads. Timeouts do not accumulate parallel hung scans.

## Remaining work

A Windows 10 22H2 evaluation VM was created and reached its desktop. An isolated acceptance script and preparation workflow were arranged; see the [Windows test lab](WINDOWS_TEST_LAB.md) for the dated preparation snapshot. No Windows 10 application acceptance result had been produced. Version simulation, window fixtures, and VM setup do not establish compatibility. Real Windows 10 19041–19045, other Windows 11 builds, multiple displays/scaling, auto-hide, Explorer restart, and lock/sleep recovery still need testing. Detection failure retains native behavior; tray and exit recovery remain available.

The current Windows 11 26200 run passed the 1,300 ms UI-blocking probe, standalone Windows key, Start button, Win+R, temporary native menu, explicit disable, and abnormal test-child recovery. The previous preview.20 was restored afterward. Explorer restart, lock, sleep, and real Windows 10 execution were not performed.
