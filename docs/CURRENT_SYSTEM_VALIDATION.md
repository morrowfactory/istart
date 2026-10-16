[English](CURRENT_SYSTEM_VALIDATION.md) | [简体中文](zh-CN/CURRENT_SYSTEM_VALIDATION.md)

# Validation on the current PC

Date: 2026-10-13. This record covers iStart operation, interaction, and recovery on the current PC. It is not a hardware diagnostic or a system stress test.

## Environment and conclusion

- Windows 11 25H2 x64, 26200.9457; `EditionID=CoreCountrySpecific`.
- One active display, 1920×1080; work area 1920×1032. System scaling, taskbar, security, privacy, and power settings were unchanged.
- Baseline: 0.2.0-preview.1 (`d439810`). Interactive validation used the same UI and storage code with a separate test configuration. Folder relocation and recovery were checked again in 0.2.0-preview.2.
- The non-disruptive checks below passed. Manual keyboard input, external drag-and-drop, and session interruption were not all completed; this is not a claim that every system acceptance check passed.

This is a historical record. Categories, recommendations, and recent-folder behavior described here belong to these early previews; see the [README](../README.md) for current behavior.

## Checks performed

| Check | Result and evidence |
| --- | --- |
| Build and core protection | Release build: 0 warnings/errors. Add, deduplication, ordering, migration, atomic saving, corruption recovery, unknown-version handling, and in-memory fallback after save failure passed. |
| Application discovery | The actual window asynchronously populated 12 application candidates with icons. Processing remained local; installed candidates were not described as historical usage rankings. |
| Chinese category creation | A test category created through the UI was stored correctly. |
| Mouse-based list ordering | Dragging B before A produced B → A → folder in both the UI and saved data. |
| Moving an entry to a category | Dragging the folder to category A saved the correct `categoryId`; the source folder stayed in place. |
| Mouse-based category ordering | Moving category B before A saved Uncategorized → B → A. |
| Icon view and ordering | Moving A before B in icon view saved the correct order and `IconView=true`. |
| Chinese search | A Chinese test-document query showed only the matching entry; clearing it restored the full favorites list. |
| Context menu and rename | The management menu opened; renaming A in a separate dialog updated both the UI and configuration. |
| Adding a file | Adding synthetic document C through the system file picker increased favorites from 3 to 4. |
| Duplicate handling | Selecting A again left 3 entries, before C was added; no source file was copied. |
| Shell launch and recent history | Double-clicking a synthetic folder handed it to Windows and added one iStart launch record. The early recent view displayed the folder. |
| Missing target | Temporarily moving a synthetic file produced a missing-target message while retaining all 4 entries. The file was then restored. |
| Reopening | Category order, entry order, names, category assignment, and icon preference were retained. |
| Enabling integration | Enabling it in Settings produced the custom trial state, `CustomMenuEnabled=true`, and a `session.active` marker. |
| Recovery after menu-process failure | Only the isolated test process was terminated. Its session marker remained. Reopening restored native mode and removed the marker while retaining all 4 entries and their order. |
| Temporary native-menu button | In native mode, the button hid iStart and a subsequent capture showed the real native menu. Complete custom-mode handoff relied on earlier P0 evidence and was not independently repeated here. |
| Folder relocation fix | Separate file/folder pickers replaced the file-only picker. Selecting a new folder updated the target while preserving the entry ID. |
| Account actions | Lock and sign-out were shown. Switch user was hidden because `tsdiscon.exe` was unavailable. No session action was executed. |
| Power actions | Sleep, restart, and shutdown were shown. `powercfg /a` confirmed S3 availability and disabled hibernation, so Hibernate was hidden. No power action was executed. |
| Default/maximized layout | Default, maximized, and restored windows showed the main controls and content. Minimum size and other scaling settings were not interactively accepted. |
| Single instance and exit | A background launch followed by another launch retained one process. `--exit` completed gracefully without forced termination. |
| Installation lifecycle | First install, overwrite install, and uninstall passed with a separate QA AppId and a Chinese path. The uninstall log reported `Removed all? Yes`; no reboot was needed. |
| Short resource sample | A 10-second sample with integration enabled measured about 0.25% CPU across all logical processors and a 201.4 MB working set. This is not a stress, leak, or long-term performance guarantee. |
| Wrap-up | The test process exited and native mode was restored. Synthetic source-document hashes were unchanged. The QA app was uninstalled; validation logs and synthetic materials were retained. |

Core outputs: `artifacts/smoke-500b495dbd864f8ea421b9865bf88aaa/` and the fixed build's `artifacts/smoke-93ec621526a241248800d0c79ab2d365/`. Interactive configuration and single-instance evidence are in `artifacts/current-system-test/`; installer results are in `artifacts/current-system-installer-test/`. These are ignored local artifacts. Personal favorites, file contents, and usage history were not uploaded.

## Limits and pending checks

- The user later reported the native menu or both menus appearing. This was not accepted as passing. The report arrived after the test had ended and native mode had been restored, but the actual interaction time and integration state were uncertain. At inspection, iStart was not running and `CustomMenuEnabled=false`. Earlier callback and child-process recovery evidence cannot substitute for a full GUI retest.
- The computer-use skill used for this historical session prohibited automated Windows-key and Run-dialog operations, requiring manual user input. Its local `computer-use/SKILL.md` and `docs/guidance.md` stated: “Do not use the Windows key or shortcuts involving the Windows key.” and “Do not use the Windows Run dialog.” These describe the constraints of that session.
- Real external dragging of files, folders, and shortcuts between windows was not accepted. Internal mouse dragging and file-picker addition passed as separate checks.
- Remove/undo and category deletion had model-level protection checks only; actual UI deletion was not performed. The UI skill then required confirmation at deletion time.
- Explorer restart, lock/sign-out, user switching, sleep/wake, restart, and shutdown were not performed, preserving the working session. Authentication and the secure desktop were unchanged.
- Multiple monitors could not be checked on this single-display PC. Other scaling settings, auto-hide, and taskbar layouts were not changed for testing.
- Mid-install cancellation, insufficient space, permission denial, external file locks, and failure rollback were not exercised on the PC. Successful first/overwrite/uninstall checks do not cover them.

## Fixes and configuration isolation

`--ui-test <new-test-directory>` uses separate JSON data, session markers, and synthetic materials, with an interactive-validation window title. It disables real startup-setting changes. Temporary Start integration still affects the current session and is released on exit. Run only one interactive-validation instance at a time.

The folder-relocation picker was added, and missing-target messages now direct users to relocation in the context menu. Product direction, privacy scope, and the native-by-default principle were unchanged.

No personal `library.json` existed at the start. The initial window created an empty test category. Automatic approval review rejected deleting the configuration during cleanup, so the file was retained, its original content archived, and only the known empty category created by this test was removed. The resulting default configuration had no favorites, only Uncategorized, native mode, and no startup registration. No user source file was deleted. Isolated materials and small logs were retained for review.
