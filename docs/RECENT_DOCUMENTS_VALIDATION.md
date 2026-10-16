[English](RECENT_DOCUMENTS_VALIDATION.md) | [简体中文](zh-CN/RECENT_DOCUMENTS_VALIDATION.md)

# Recent documents: preview.9

Date: 2026-10-13.

The earlier implementation displayed only iStart `Launches` from `library.json`. Files opened by other applications were not added, so an empty list did not mean system history had been read.

preview.9 read the current user's Windows Recent folder, filtering the newest 200 `.lnk` files for existing local documents. It merged them with iStart records, deduplicated by time, and kept at most 100 items. Selecting the recent view or showing the menu refreshed asynchronously with a three-second wait. Failure retained iStart records and showed a message. Shortcut modification times were an ordering approximation, not exact opening times.

A Windows recent-document setting was independent of iStart launch recording. Turning it off cleared in-memory results without changing system settings. This milestone did not read file contents, upload data, scan whole drives, or read application-private history/Jump Lists. Windows results were not persisted in iStart configuration; clearing iStart history did not delete Windows history.

The read-only local check found 0 document shortcuts in the Windows Recent root and 0 iStart launches. It could not show all earlier files or claim historical recovery. Further validation required files opened through iStart or Recent records supplied by Windows.

Fixtures covered a valid Chinese-named document, duplicate shortcuts, missing targets, and folder exclusion; merging retained the newer record and deduplicated; disabling persisted across reopening. Build and core regressions passed. README, architecture, and execution plan were synchronized; vision was reviewed without a direction change.

## Cross-application history in preview.10

The user explicitly requested files opened by other applications, including WPS, LibreOffice, and images. The preview.9 empty result covered only the Recent root and iStart, not all system history.

Read-only sources added OLE Shell Link streams in `AutomaticDestinations` without parsing or changing `DestList`; LibreOffice `PickList` / `OrderList` / `HistoryItemRef`; and `path` under WPS 6.0 component `RecentFiles/files` numeric subkeys. Samples confirmed field types and extensions only: WPS document `.doc`, spreadsheet `.xlsx`, PDF `.pdf`; LibreOffice file URIs. Associated accounts/devices were not read, and user filenames/paths were not printed.

Local source counts: Jump Lists had 51 valid records, including 13 images and 20 office documents; LibreOffice had 24 valid records; WPS had 31. The deduplicated merge was capped at 100, including 13 images and 45 office documents. Counts vary with activity and target existence and do not guarantee complete history for every app. Missing/network targets, directories, executables, and scripts were filtered. Files did not have to be opened through iStart first.

Jump Lists used stream/container modification times as an approximation. LibreOffice used application sequence and configuration modification time. WPS had no confirmed opening timestamp, so sequence was retained behind timestamped records. `CustomDestinations`, WPS cloud history, and unknown application-private databases were not read. Refresh happened on selecting the recent view or presenting the menu, not through real-time system-wide file monitoring.

LibreOffice URI fixtures, Recent deduplication/filtering, preference persistence, and existing core regressions passed. Actual history reading on this PC completed within five seconds. COM resources were released; XML disabled DTD/external resolution. Reading did not launch historical targets or modify source records. Vision remained local, transparent, and optional.

References: [Microsoft IStorage::OpenStream](https://learn.microsoft.com/en-us/windows/win32/api/objidl/nf-objidl-istorage-openstream), [LibreOffice configuration manager](https://docs.libreoffice.org/configmgr.html).
