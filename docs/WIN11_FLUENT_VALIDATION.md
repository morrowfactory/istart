[English](WIN11_FLUENT_VALIDATION.md) | [简体中文](zh-CN/WIN11_FLUENT_VALIDATION.md)

# Windows 11 Fluent visual validation: preview.13

Date: 2026-10-13.

## Design and implementation

The [editable Figma design](https://www.figma.com/design/fChA8PN9ewvBN02v9O1OjA?node-id=10-173) was added beside the retained Material design. It included local Button, Navigation, and EntryTile component families, 16 color/spacing/corner variables, four text styles, and a menu-shadow style. Editable nodes and 34 instances formed the UI. Twelve image nodes were original application icons; the interface was not replaced with a full-screen screenshot.

Windows colors used background `#F3F6FB`, white content, accent `#0067C0`, selection `#EAF2FA`, and light borders. Inputs/buttons had 6 DIP corners; content had 12 DIP corners. Gray navigation icons, a blue selection indicator, search-focus underline, account icon, and lighter status area were added. Default width fit six columns; narrower windows wrapped. Google retained its previous color, size, and font values with immediate switching.

App/file dividers, hidden recommendations, favorites order, recent-file sources, dragging/ordering, account/power, native recovery, and Start integration were retained at this milestone. The new-category field gained a Windows placeholder. App/folder icons still came from actual targets; example directories were not written to user configuration. Category creation and the recommendation panel were later removed from current navigation.

## Asset evidence

Navigation `all` / `recent` / `folder` / `apps` came from Figma nodes `10:130`, `10:135`, `10:144`, `10:153`; search from `10:182`, account from `10:329`. Source SVGs remain in `Assets/Fluent`. Derived PNGs cover 18 DIP navigation/search and 22 DIP account slots, exported at 2× pixels. Nontransparent-pixel and dimension checks passed. PNGs are embedded WPF Resources without temporary Figma URLs or runtime network dependencies. The brand reused the existing four-pane geometry at this milestone.

## Validation and differences

- Build, data protection, recent history, grouping, and both-theme regressions passed; frozen navigation images and 36×36 dimensions were checked.
- Default 712×604 content and compact 592×484 previews were rendered. Search, view controls, mode status, and list stayed in bounds. A minimum account-button width avoided icon/text crowding.
- The real Windows 11 preview used public application names and sample directories, without personal file paths in captures.
- Figma provided only Noto Sans SC; all design text was checked against that substitute. Pixel identity with system fonts was not claimed. The Windows app uses Segoe UI Variable / Microsoft YaHei UI, with standard title/status bars. The OS provides window shadow; native Mica blur was not implemented.
- Actual directory counts, names, and app titles depend on configuration. Long names truncate with tooltips; excess content scrolls.
- No automated Windows-key input or power action was performed in this visual check. Final installation updates and manual UI feedback were recorded separately.

README, architecture, and execution plan were synchronized; vision was reviewed without a direction change.
