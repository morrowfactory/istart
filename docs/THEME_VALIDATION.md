[English](THEME_VALIDATION.md) | [简体中文](zh-CN/THEME_VALIDATION.md)

# Windows / Google theme validation

Version: 0.2.0-preview.5. Date: 2026-10-13. Environment: Windows 11 26200.9457.

## Usage at this milestone

In Settings, choose Windows or Google under Appearance. Selection applies and saves immediately, without pressing the menu-settings Save button. The early Windows theme used a light gray background, small corners, outlined buttons, selection indicator, and four-pane mark. Google retained the rounded Material appearance. Windows visuals were later refined in [preview.13](WIN11_FLUENT_VALIDATION.md).

Theme changes affected appearance only: Start/Windows-key integration, startup, recommendation/history preferences, favorites, and order were unchanged. The optional `Theme` field kept schema 2; old configuration or unknown values fell back to Google.

## Validation

- Release build: 0 warnings/errors.
- The shared settings `SetTheme` path updated the same window Google → Windows → Google immediately, including input-template corners.
- Saving/reloading each theme retained selection, favorites order, and custom-integration preference.
- Old configuration defaulted to Google; unknown values fell back without discarding favorites.
- Actual WPF rendering was checked: [Google reference capture (Chinese UI)](prototypes/theme-google.png), [Windows reference capture (Chinese UI)](prototypes/theme-windows.png). Existing icon/compact layout and bounds checks passed.
- Core protection, detection recovery, hide-on-close, and explicit-exit checks continued to pass.
- Installation lifecycle and final self-contained payload used this version's packaging/CI checks; logs stayed in local `artifacts`.

Actual dropdown clicking was not completed: the Windows interaction tool reported “window id 5642440 no longer belongs to …; current owner is …” despite matching paths. Reselecting still failed. Input stopped and only the isolated validation process was ended. Code checks were not counted as real mouse acceptance. Personal favorites and integration state were untouched.

Multiple monitors/other DPI, Windows 10/other Windows 11 builds, long-term hook health, and installation-failure cases retained existing limitations. The installer was unsigned.

README, architecture, and execution plan were synchronized; vision was reviewed with direction and privacy unchanged.
