[English](MATERIAL_VALIDATION.md) | [简体中文](zh-CN/MATERIAL_VALIDATION.md)

# Material-style preview validation

Date: 2026-10-13. Version: 0.2.0-preview.3. Environment: Windows 11 25H2 x64 26200.9457. This added no Windows 10 compatibility evidence.

## Design and implementation

The light background, blue accents, rounded search field, category selection, content cards, and buttons from the [editable Figma design](https://www.figma.com/design/fChA8PN9ewvBN02v9O1OjA?node-id=1-1245) were applied to WPF. Standard window borders retained move/resize behavior. Real local icons replaced illustration placeholders. File/folder addition, remove/undo, status messages, account, power, and native-menu access remained available. Content scrolled according to actual recommendation and favorite counts.

No dependency, JSON schema, user favorites, system hook, or login flow changed. Categories and the recommendation panel described here are historical preview.3 behavior, not the current navigation.

## Verified

- Release build: 0 warnings/errors; existing core data-protection checks passed.
- Default, icon, and compact-size rendering was checked. Compact rendering passed bounds checks for search, view controls, mode status, and favorites. Attempted real mouse resizing did not change the window size, so it was not counted as interactive resize acceptance.
- In isolated interactive configuration, actual Chinese input filtered the matching project entry; clearing search restored the list.
- Clicking switched to icon view. Dragging meeting notes before the project plan produced matching UI and JSON order.
- The test window exited normally. Default user configuration and native-menu mode were unchanged.

First install, overwrite install, and uninstall passed using a separate QA AppId and Chinese path, without reboot. Logs: `artifacts/material-install-test/`. The app was not automatically installed over the user's everyday installation. The delivery program came from this branch's commit; core checks were repeated against the self-contained payload and a SHA256 file accompanied the installer.

## Limits

Earlier manual feedback about the native menu or both menus had uncertain timing and remained pending. This UI version did not change integration and did not claim to fix that report. Automated Windows-key input, Explorer restart, lock/sign-out, and power actions were not performed. External cross-window dragging, multiple monitors/other DPI, installation cancellation, and failure rollback remained unaccepted. The installer was unsigned.

README and architecture were synchronized; vision was reviewed with product direction and privacy principles unchanged.
