[English](COMPUTER_UPDATE_VALIDATION.md) | [简体中文](zh-CN/COMPUTER_UPDATE_VALIDATION.md)

# My Computer and update validation

Date: 2026-10-14. Version: 0.2.0-preview.17. This is a historical development-validation record; GitHub Releases supplies the authoritative publication state, exact source, and package hash.

## Checked scope

- Release build: zero warnings/errors; complete core regression passed.
- Isolated fixtures covered five-view navigation, My Computer search, capacity cards/system items, and existing Windows/Google theme regression. [The reference image](prototypes/computer.png) uses synthetic capacities and the Chinese UI.
- The default window showed five disks and seven Control Panel categories without scrolling: three-column disks, one-row system entries, two-column Control Panel.
- Read-only inspection found five drives and seven allowed Control Panel categories; enumeration was available. GitHub Releases responded successfully. At this milestone, development version 17 was newer than public version 16, so no update was returned.
- Isolated HTTP fixtures covered numeric versions, draft exclusion, rejecting external download origins, stable/preview channels, byte/SHA256 validation, failed-hash cleanup, and cancellation cleanup. Fixture files were not executed.
- QA first install, overwrite, uninstall, and a path with spaces/non-ASCII characters passed without opening the real menu.
- In-app installation uses the existing Inno wizard, passing the current directory/startup choice and requesting graceful exit at preparation time. Development/acceptance runs cannot launch the installer.

## Outstanding at this milestone

The complete download-and-overwrite path to a future public version had not been exercised because no public release exceeded development version 17. Disk/network/Control Panel settings were unchanged; individual system-item interactions needed manual acceptance. Windows 10, other Windows 11 builds, multiple monitors, cancellation, disk-full, permission-denied, and other failure cases remained pending.

Downloads require user initiation and the fixed GitHub source. The installer was unsigned. README, ARCHITECTURE, and VISION were synchronized for this milestone. Later release/update evidence does not retroactively change this record's scope.
