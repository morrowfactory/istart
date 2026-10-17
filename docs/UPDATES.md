[English](UPDATES.md) | [简体中文](zh-CN/UPDATES.md)

# Updates

Available from preview.24. Earlier installers use manual updates. GitHub publication and complete cross-version upgrade acceptance are separate gates.

## Notifications and release details

Installed copies check the fixed `morrowfactory/istart` public GitHub Releases at startup when a network is available. Successful checks repeat after six hours; failures retry after fifteen minutes. A network reconnection can trigger a check with a two-minute minimum interval. An available network interface does not guarantee GitHub is reachable; failed requests leave the current version usable.

A newer release adds red dots to the main Settings button and the upgrade button inside Settings. Text and accessible descriptions also identify the update. Opening the update page does not dismiss the dots. The upgrade section is first in Settings, and the page shows:

- Current installed version and target version.
- Changes and new features from the publisher's GitHub release notes, in their original language.
- Actual download progress, verification, and installation status.

Empty notes have an explicit placeholder. Notes are bounded to 12,000 characters and exclude the known technical provenance footer. Preview installations accept newer preview or stable versions; stable installations accept stable versions only.

## Automatic or manual

**Automatic updates is checked by default**, including when an older configuration has no preference. The checkbox saves immediately without the general Save settings button.

| Mode | Download | Installation |
| --- | --- | --- |
| Checked | Background download and verification | Wait for the menu to be hidden, no Settings/update/screenshot window, and at least one minute of inactivity in the current Windows session |
| Unchecked | Notify only; user clicks Download | User clicks Install to open the full wizard |

Changing to manual cancels an automatic download and retains the update notification. In manual mode closing the update page cancels its download; in automatic mode closing the page lets it continue. Exiting iStart cancels pending checks/downloads and discards late results.

Automatic installation still shows an installer progress window. It does not reboot Windows or force other apps closed. Immediately before launch, the installer file is verified again and automatic-install conditions are rechecked. The installer requests normal iStart exit; failure to exit stops installation. A successful automatic install relaunches iStart in the background and honors the saved native/custom Start preference. Startup selection, items, history, and valid settings are retained.

Only installed copies with their uninstaller present can launch installation. Development and UI-test runs do not start automatic scheduling or execute installers. No new Windows service, driver, scheduled task, or UI dependency is added.

## Source, privacy, and cache

- Only exact versioned x64 assets from the fixed repository are accepted. Drafts, external URLs, invalid versions, and missing SHA256 digests are rejected.
- Check budget: thirty seconds, 100 releases, 2 MiB response. Download budget: fifteen minutes, at most 256 MiB. Length and SHA256 must match before `.part` becomes EXE and again before installation.
- GitHub requests do not upload app lists, paths, browsing history, or screenshots. Idle detection reads elapsed session input time without recording keys.
- Failed/cancelled downloads remove only their current temporary data. Successful downloads clean only known updater-owned caches older than seven days, keeping at least two recent downloads. Personal configuration is untouched.
- The installer remains unsigned. TLS and checksums do not replace publisher signing; installation failures are not a guarantee of full old-version rollback.

## Validation

On 2026-10-17, the local Windows 11 build passed with zero warnings/errors, existing regression, and isolated update lifecycle/UI tests. Fixtures cover defaults and persistence, offline/reconnection behavior, shared checks, manual notification without download, deferred automatic installation, cancellation on mode change/exit, late results, install-time tampering and redownload, install arguments, both badges, version details, and English/Chinese Windows/Google rendering. Fixture executables are never run.

The separate QA installer checks first installation, overwrite using automatic-update arguments, and uninstall in a fresh test directory. It skips production exit/relaunch operations and does not overwrite the daily installation.

Full upgrades between future public releases, successful production relaunch, interactive cancellation, locked files, low disk space, interrupted installation, and Windows 10/other Windows 11 builds still require acceptance. Passing model/QA checks does not establish those results. See [architecture](../ARCHITECTURE.md) and [release procedure](GITHUB_RELEASE.md).
