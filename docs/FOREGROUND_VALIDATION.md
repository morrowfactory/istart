[English](FOREGROUND_VALIDATION.md) | [简体中文](zh-CN/FOREGROUND_VALIDATION.md)

# Foreground and background presentation: 0.2.0-preview.6

## Bottom-left placement correction — 2026-10-30, preview.27

The installed preview.26 only positioned the window while Start integration was enabled, and its initial startup still used CenterScreen/Show. In native mode, including unsupported build 26300, opening the app could therefore retain a centered or manually dragged position. This correction uses Manual startup and routes initial visible startup through ShowMenu. A shared placement helper runs in both modes before showing and after any resulting DPI change, anchored to the selected work area's bottom left. The monitor is selected before activation. Tray actions supply a monitor point; other opens use the foreground monitor. Background startup uses the same helper and remains hidden without requesting activation. The integration allowlist is unchanged.

On a separate inactive desktop, 100 Windows/Google show/hide cycles passed in native mode, including the first visible bounds, centered previous positions, explicit monitor points, and ten minimized restores. Background startup stayed hidden without topmost/activation. Core data, localization, themes, screenshots, and update-fixture regressions passed. Pure geometry checks cover work-area offsets, negative monitor coordinates, and DPI/clamping. No input was synthesized and no daily installation/configuration was replaced. Real Win interception, foreground search input, physical multiple monitors/mixed DPI, and the earlier text-deletion cause are outside this check.

## Current reliability investigation — 2026-10-29

The preview.25 installed copy again showed intermittent opening/focus failures. Logs recorded foreground success with native/WPF search focus false, including after a restart in native-menu mode. The user also reported unintended text deletion, which stopped after iStart exited. The cause is unconfirmed; this is not evidence of a particular Delete-key sender or a completed fix. Earlier manual acceptance below does not establish current long-running reliability.

The changes assigned to preview.26 move Shell icons to two bounded background workers, share software scans and reuse success for 30 seconds, cache shortcut classification between searches, protect restored dimensions, use actual foreground ownership for toggling, and remove Alt/Ctrl injection from focus retries. Bounded direct retries and content-rendered focusing remain. Standalone Win cancellation and explicit native-menu access still use synthetic input and need isolated integration acceptance.

Local checks passed with no input injection: blocked/failing/duplicate icons, bounded queue, real async image assignment, discovery reuse, minimum sizes at 100/125/150/200%, and taskbar snapshot recovery. On a separate inactive desktop, 100 Windows/Google show/hide cycles including ten minimized restores passed; display dispatch/layout measured median 3.9 ms, p95 8.2 ms, and maximum 287.0 ms in that run. Core data, localization, themes, screenshots, and updater-fixture regressions passed there too. These timings exclude Start-key interception and do not measure perceived live-desktop latency.

Run `--reliability-test <directory>` for checks without shown windows; use `scripts/Test-MenuWindow.ps1` for the inactive-desktop check. Keep the affected installed copy exited during investigation. Publication or installation of preview.26 does not complete actual Win/Start opening, direct input under other applications, long-running operation, or unintended-text-deletion cause acceptance. Publication provenance is recorded in the release manifest; local installation is recorded separately. Do not run `--input-test` or `--presentation-test` on a working desktop during this investigation.

The Alt/Ctrl focus fallback described in the historical records below was removed in this development change. Historical standalone Win tests still apply only to their named versions and systems.

Release preparation on 2026-10-30 found that the workstation now reports build 26300. The previous cancellation fixture stopped at the unsupported-version gate, so its in-flight-task assertion failed before reaching the simulated scan. It now supplies build 26200 explicitly and separately checks build 26300 rejection before scanning/hooks. The safe checks pass after that correction; production policy is unchanged and this PC retains native Start on build 26300.

Date: 2026-10-13; Windows 11 26200.9457. The user's capture showed iStart appearing only on the taskbar after Start was clicked, behind another maximized window. The process was still running and integration was enabled; this evidence did not establish a startup failure or crash.

## Changes

- Start-button, Windows-key, tray, and repeated-launch entry points share `ShowMenu`, restoring a minimized window before showing it and requesting foreground focus.
- A visible custom menu uses a temporary topmost layer. Synchronous `Deactivated` events during presentation do not immediately hide it; hiding clears `Topmost`. Since preview.21, opening from the app icon in native mode also keeps this layer until dismissal.
- Pressing the Windows key can hide a menu that is visible but inactive.
- Background startup does not request focus and hides immediately after its initial `Show`. The later asynchronous integration initialization no longer hides a menu that the user has already opened.
- `menu-shown` diagnostics record foreground success and topmost flags, without other app names, keystrokes, or file contents.

[Windows foreground activation rules](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow) allow the OS to refuse focus requests. iStart does not change foreground-lock policy or attach to other apps' input threads. Only after a user opens the menu, if the foreground request fails and no modifiers are held, it retries with a marked, balanced Alt/Ctrl sequence. This sends no text or commands and does not guarantee activation over the secure desktop or every full-screen/topmost app.

## Validation

Build, core data protection, both themes, discovery recovery, and lifecycle checks passed. Added checks covered minimized → `ShowMenu` → normal visible state and clearing `Topmost` on hide. Those checks did not install integration hooks or automatically send the Windows key.

The user was asked to manually test Start, Windows-key toggling, and direct search input with another app maximized. Manual feedback is recorded separately from code checks. First/overwrite/uninstall checks and the final payload continued through the established packaging and CI path.

README, architecture, and execution-plan documentation were updated. Vision was reviewed; product direction and privacy principles were unchanged.

## Final manual feedback

On 2026-10-13, the user confirmed menu placement and application icons. After native `SetFocus` was added, the user confirmed direct English and Chinese input without clicking the search box. Logs showed foreground, WPF keyboard, and native focus all true, with no modifiers or Windows key held. Two earlier rounds that showed only WPF focus true still failed actual input and were not accepted.

Placement uses the clicked Start button's taskbar and monitor work area; Windows-key activation selects the foreground window's monitor. Physical-pixel clamping checks cover top/bottom/left/right taskbars and negative coordinates. Shortcut icons come from their icon resource or target to avoid shortcut arrows, while launching still uses the original shortcut to preserve arguments.

Logs record focus and modifier state, not typed characters. Manual acceptance passed on this PC; multiple monitors, other OS versions, and long-running behavior still need validation.

## Combined preview.21 validation — 2026-10-16

The fix retained the temporary topmost layer and focus retries when opening in native mode. Search focus uses Input dispatcher priority. A second launch grants foreground permission to the existing process in the same path and session before signaling it to show. The footer offers an explicit integration-enable button.

On the current Windows 11 26200 PC, isolated tests passed: three opens over a maximized background reached the foreground with search focus; switching away hid the menu and cleared topmost; minimized windows restored; after the enable button installed integration, the Windows key and system Start button opened the actual iStart window. An initial fixed-delay check missed its focus condition. Diagnostics, Input-priority focusing, and a bounded wait were added before the combined check passed.

Actual Windows 10, foreground-permission handoff from repeated shortcut launches, and other topmost/full-screen applications remain untested. `--presentation-test <directory>` creates a temporary maximized background and sends Windows-key/Start-button test input. Run it manually only on an idle desktop; it is excluded from CI. It uses isolated configuration and cleans up its test windows and hooks.
