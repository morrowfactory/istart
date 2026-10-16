[English](RESIDENT_VALIDATION.md) | [简体中文](zh-CN/RESIDENT_VALIDATION.md)

# Integration recovery fix: 0.2.0-preview.4

Date: 2026-10-13; Windows 11 26200.9457. The user reported that after several Windows-key/Start-button interactions the native menu returned, without clicking Close.

## Diagnosis and fix

At the first inspection the process had exited and no corresponding crash log was found, so the exit cause was unconfirmed. The older integration logic was then run with lifecycle logging. At 10:57:31, Start-button detection returned no result; the old logic disabled the entire adapter and persisted native mode. This established one fallback cause, not the sole explanation for every exit.

After the fix, empty results and detection exceptions no longer revoke the custom preference or uninstall the keyboard hook. Mouse interception uses only the latest successful, unexpired snapshot and revalidates taskbar root and rectangle. After 1.5 seconds it passes input through instead of intercepting without reliable bounds. A single background detection task continues and updates position/recovery state on success; unfinished tasks are not repeatedly duplicated.

Close (`×`) hides to the tray. Menu/tray Exit, update/uninstall exit signals, and system session shutdown still terminate normally. Local bounded `runtime.log` records lifecycle, mode, and detection state, excluding keys, favorite paths, and file contents. It rotates above 64 KiB and retains one previous log.

## Validation

- Release build: 0 warnings/errors; existing core/layout checks passed.
- Simulated success → empty result → exception → recovered position retained the old snapshot, avoided duplicate failure notifications, updated the recovered position, and cleared the snapshot on explicit disable. No hooks or injected input were used by this check.
- WPF lifecycle: Close hid the window without `Closed`; explicit Exit raised `Closed`.
- After the fix, the user alternated Windows-key/Start-button operations for about a minute and reported consistent iStart opening. The process remained alive with `CustomMenuEnabled=true`.
- Isolated first/overwrite/uninstall and final-payload core checks were part of delivery. Logs stayed under local `artifacts`; personal runtime logs were not uploaded.

This did not prove long-term stability, every exit cause resolved, Explorer/sleep recovery, or Windows 10/other Windows 11 compatibility. Reliable health detection for hooks silently removed after timeout was not yet implemented at this milestone. Startup was not newly enabled by default; favorites and Windows login were unchanged.

README, architecture, and execution plan were synchronized. Vision was reviewed with privacy and direction unchanged.
