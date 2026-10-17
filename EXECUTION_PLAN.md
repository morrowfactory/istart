[English](EXECUTION_PLAN.md) | [简体中文](docs/zh-CN/EXECUTION_PLAN.md)

# iStart implementation and execution plan

The current release is **0.2.0-preview.23**, which reduces installer size while retaining offline operation and both interface languages. Five views, activity ordering, system resources, in-app updates, screenshots, and the Chinese installer are implemented. Interface language can follow Windows or be set to Simplified Chinese/English. GitHub Actions builds and publishes installers. Safe build/core checks continue on the development machine; Windows 10/other builds, Explorer restarts, and the complete abnormal-installation matrix still require isolated system testing. Implemented code is not the same as completed acceptance. See [P0 validation](docs/P0_VALIDATION.md).

## Development preview.24

Implement online update checks, red dots on both entry points, current/target versions and release notes, and default-on automatic upgrades with a persistent manual option. Keep download verification, idle gating, visible installation progress, native recovery, and personal data. Local fixtures and localized render checks are covered by [Updates](docs/UPDATES.md); public cross-version upgrades, production relaunch, and abnormal-installation cases remain pending. This feature does not constitute a published release.

## Adopted approach

Development preview.24 also implements the approved distinct themes: Windows top navigation/six columns and Google sidebar/five columns, with local action icons, translated tooltips, and accessible names. A focused Win11 check covers stale-marker re-enable and session cleanup; real reboot, Windows 10, and hook-timeout recovery remain pending. See [Theme checks](docs/THEME_VALIDATION.md) and [Recovery checks](docs/RESIDENT_VALIDATION.md).

C# + WPF/.NET 10 in one ordinary-user desktop process; atomic JSON storage with one backup; process-owned low-level hooks and UIAutomation; an Inno Setup 7.1.0 Chinese per-user self-contained installer. There is no copied Open-Shell code, service, driver, cloud service, or whole-disk index. Appropriate Inno commercial licensing is required before commercial distribution.

The default repository documentation is English. Complete Chinese pages are separate under `docs/zh-CN/`; filenames use English names and every page links to its counterpart. Future documentation changes must update both versions. PRs, issues, and release/acceptance records continue to follow the repository's separate communication rules.

## Stage status

| Stage | Implemented and checked | Remaining gates |
| --- | --- | --- |
| P0: technical validation | Windows 11 build 26200 Start/Win, Win+R, temporary native access, disable, and abnormal test-child recovery; integration off by default; known builds may opt in after capability detection | Windows 10, other Windows 11 builds, Explorer restart, hook-timeout recovery; dual-system integration is not complete |
| P1: core menu | Five fixed views, icon/list display, search, adding/hiding, relocation, eight-second undo, keyboard opening, persistence protection, and layouts; legacy category storage retained | Full real-mouse interaction, multi-monitor/scaling, accessibility acceptance |
| P2: system integration | At most 30 activity-backed apps, up to 100 recent files from bounded sources, frequent/recent folders and websites, My Computer, tray, native switching, account/power capability checks | Complete history is not available; real account/power execution and session recovery need isolation |
| P3: installation | Chinese welcome/location/options/progress/completion, per-user setup, separate QA first install/overwrite/uninstall | Interactive cancellation, denied permissions, disk-full, running-file locks, interrupted recovery, and scaled visual checks; basic pages have evidence |
| P4: preview delivery | Windows CI, core and self-contained EXE checks, win-x64 ZIP, Chinese installer/SHA256, GitHub release provenance, seven-day artifacts | Complete compatibility matrix, signing, and formal initial-release acceptance |

## Next steps

1. Keep the current preview, English/Chinese documentation, source revision, and CI artifacts aligned; preserve native Start by default.
2. On the development machine, continue build-policy, native Start fixtures, diagnostic export, and Windows 11 input regressions. Known Windows 10/11 builds remain capability-checked trials until corresponding isolated system acceptance is recorded.
3. Test Explorer restarts, taskbar auto-hide/position, multiple monitors/scaling, lock, and sleep recovery. Retain native mode on failure and record the environment.
4. Test account/power operations and installation failures in a test account or VM. Complete upgrade-failure recovery evidence. Do not restart, sign out, or terminate Explorer in a daily work session for these tests.
5. Propose a stable release only after compatibility, recovery, signing, and licensing gates. Public installer publication and merges follow the user's requested scope and the documented GitHub workflow.

## Product recommendations and boundaries

- My Apps uses bounded local activity evidence or explicit additions, with at most 30 entries. Missing records are acknowledged rather than described as exact usage frequency. There is no separate installed-app recommendation panel.
- Preserve added items and stored order; activity ordering and recent records have explicit rules. Dragging a file adds a reference without moving its source.
- Chinese installation defaults to no desktop shortcut, no startup, and no Start takeover; users may opt in. Unknown systems retain native mode.
- Windows manages authentication and the secure desktop. Account/power actions do not force applications closed and require a real user action.
- Record compatibility by actual OS build and test evidence; one successful build or callback test does not replace real interaction.

## Repository layout

| Path | Purpose |
| --- | --- |
| src/IStart.App/ | UI, editing, storage, discovery, system integration/actions, and focused tests |
| installer/ | Inno wizard and lifecycle script |
| scripts/ | Core checks, self-contained publishing, installer builds, isolated acceptance |
| .github/workflows/ | Windows CI and GitHub preview publication; seven-day build artifacts |
| docs/ | English guides, prototypes, compatibility evidence, and acceptance records |
| docs/zh-CN/ | Separate Chinese counterparts with English filenames |

Keep the project small rather than creating empty planned modules. Acceptance logs remain in ignored artifacts; real personal items and activity are not committed. QA files are uninstalled after installation tests, retaining logs. Test cleanup does not remove the daily installation or personal configuration. There is no remote-server deployment or server-backup cleanup; public releases and verified rollback packages follow the GitHub retention policy.

## Historical milestones

These entries record their original dates and test scope. Pending items describe the state at that milestone; consult current guides for the present UI. Later previews replaced the early recommendation/category interface.

- **preview.3:** Material styling. Build, core protection, Chinese input/search, icon switching, and real dragging checked. Default/icon/compact rendering and install lifecycle are in [Material validation](docs/MATERIAL_VALIDATION.md). Styling did not itself establish a fix for reports of native or simultaneous menus.
- **preview.4:** prevented a briefly missing taskbar from globally restoring native mode; added hide-on-close and bounded lifecycle logging. The user reported one minute of normal use. Empty-result/scan-error/recovery and close/explicit-exit checks passed; long-running hooks, Explorer restart, and other builds remained pending. See [Resident validation](docs/RESIDENT_VALIDATION.md).
- **preview.5:** Windows/Google themes with immediate persistence. Round trips, reopen, legacy defaults, unknown-value fallback, and unchanged items/integration passed. Actual dropdown clicking was not accepted because the window-control tool could not bind. See [Theme validation](docs/THEME_VALIDATION.md).
- **preview.6:** unified explicit opening paths, addressing obscured windows and delayed background hides. Minimize recovery and clearing topmost on hide were checked; real foreground/search behavior under another maximized app depended on user feedback.
- **2026-10-13, preview.6:** the user confirmed placement, arrow-free icons, and direct English/Chinese typing. Final packaging and isolated-installation regressions continued separately.
- **2026-10-13, preview.7:** expanded search beyond saved items, adding packaged-app discovery and name/ID matching. A real read-only scan found 166 entries, including one Codex ID. Case-insensitivity, results beyond the former 12-item recommendation limit, search with recommendations disabled, empty results, and store aliases passed. Final installation/UI confirmation was still separate.
- **2026-10-13, preview.8:** store-app icons. A real 32×32 Codex icon was visually checked against the original app mark. Regression passed; final packaging/current-installation replacement continued separately.
- **2026-10-13, preview.9:** Windows Recent reading, ordering/deduplicated merge, a separate preference, and empty states. The machine had zero Recent document shortcuts and zero iStart launches, so historical recovery was not claimed. Reading/filtering/merge/persistence fixtures passed.
- **2026-10-13, preview.10:** cross-app recent files. Valid Jump List entries: 51, including 13 images and 20 office documents; LibreOffice: 24; WPS: 31. The deduplicated result was capped at 100, including 13 images and 45 office documents. Build, real read-only sources, and fixtures passed; final UI display needed user confirmation.
- **2026-10-13, preview.11:** recommendations and their headings hidden by default, enabled only in Settings; explicit search retained. Default-off, visible search, and collapse-on-clear checks passed; obsolete menu-switch handlers were removed.
- **2026-10-13, preview.12:** programs separated from files/folders in All Favorites, on separate rows with internal order retained. Icon/list rendering, grouping/order protection, and core regression passed. Real dragging after grouping still needed feedback.
- **2026-10-13, preview.13:** editable Figma Fluent screen with local components, variables, and text styles; Windows WPF styling implemented. Default/compact boundaries, theme round trips, and navigation resources passed. Google resource values remained unchanged. Standard chrome/status diagnostics, font substitution, and dynamic-content differences were documented.
- **2026-10-13, first GitHub release:** the user authorized PR #1 and v0.2.0-preview.13. Added an exact-main/CI-provenance release workflow and artifact hashes. The destination was GitHub preview installers, not a server. Merge, main build, public publication, and download acceptance were separate gates.
- **2026-10-15, preview.18 development:** Windows 10 19041–19045/known Windows 11 trials, native Start children, per-taskbar isolation, bounded geometry, and exported diagnostics. Current Windows 11 Win/Start/combinations/native recovery/test-child termination passed. Windows 10 had policy/window fixtures only. See [Compatibility](docs/WINDOWS_COMPATIBILITY.md).
- **2026-10-16, preview.19 development:** local region screenshots, selection changes, floating annotations, pen/text/mosaic, copy, and PNG save. Synthetic-image/export tests passed; real desktop interaction remained pending. See [Screenshot validation](docs/SCREENSHOT_VALIDATION.md).
- **2026-10-16, preview.20 development:** WeCom-style interaction and global Alt+Shift+A, alternatives/Off/conflict reporting, icon toolbar, double-click completion, right-click reselection, Ctrl+Y, and Ctrl+S. Shortcut mapping/persistence and annotation regression passed. A first full run hit an existing packaged-app scan timeout; the rerun passed. Real background hotkeys alongside WeCom remained pending.
- **2026-10-16, preview.21 development:** dedicated input thread, current native Start HWND/geometry checks, bounded startup retry, automation-timeout fallback, taskbar generation protection, and native passthrough when UI stalls. Fixtures and current Windows 11 input integration passed. Windows 10 build/trigger details and system acceptance remained pending.
- **2026-10-16, preview.21 combined fixes:** icon opening in native mode also used temporary topmost/focus retries; repeated launches handed off foreground permission; an explicit enable action was added. Current Windows 11 presentation/input validation passed. Windows 10 was untested and this entry preceded merge/publication.
- **2026-10-16, preview.22:** immediate system/Simplified Chinese/English preference. Product surfaces were localized using stable navigation/shortcut IDs without translating user content. Fallback, legacy configuration, persistence, unsaved options, compact/default English layouts, core, and isolated installation passed. Actual English Windows sessions and Windows 10 localization remained pending. See [Localization](docs/LOCALIZATION.md).
- **2026-10-16, preview.23:** excluded unused framework languages and published PDBs and increased installer compression. Local and self-contained regressions plus isolated installation passed. PR #12 was merged and GitHub published the exact-main CI package. The public installer was 50,957,124 bytes, 5.01% below preview.22; anonymous-download hashes/provenance and old-version update discovery were verified. See [Package size](docs/PACKAGE_SIZE.md).
