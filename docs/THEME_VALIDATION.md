[English](THEME_VALIDATION.md) | [简体中文](zh-CN/THEME_VALIDATION.md)

# Windows / Google theme validation

## preview.24: distinct compact layouts

Date: 2026-10-17. Approved [Windows design](https://www.figma.com/design/fChA8PN9ewvBN02v9O1OjA?node-id=49-747) and [Google design](https://www.figma.com/design/fChA8PN9ewvBN02v9O1OjA?node-id=49-1000) were adapted to existing WPF controls. Windows uses top navigation, a flat neutral six-column grid, and a bottom system bar; Google uses a capsule sidebar, rounded white content card, and five columns. Secondary actions use local icons with translated tooltips and accessible names. Navigation/app names and expanded account/power choices retain text. Search, sorting, adding, screenshots, native recovery, and update badges retain their handlers.

Checks passed for six/five-column placement, icon resources, default and compact English bounds, language switching, state preservation, and update badges. WPF renders with twelve non-executed test app entries were inspected against the design. SVG originals and derived PNGs are local; actual app icons remain dynamic. The native titlebar and OS outer-window geometry remain for compatibility. Figma fonts are adapted to installed Segoe UI / Microsoft YaHei UI fonts.

The production window retains its existing 740 by 660 DIP capacity to avoid increasing scrolling in My Computer and real app lists; the content area expands with the window. The compact design-reference renders exercise smaller content sizes. This section records pre-release checks; published binaries are linked from README. It does not establish Windows 10 acceptance. Pointer dragging, other DPI/monitors, and every account/power action remain outside these layout checks. Earlier milestone records follow.

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
