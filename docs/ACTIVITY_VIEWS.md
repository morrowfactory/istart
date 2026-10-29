[English](ACTIVITY_VIEWS.md) | [简体中文](zh-CN/ACTIVITY_VIEWS.md)

# App, folder, and website activity views

preview.16 introduced four fixed views: My Apps, Recently Opened, Frequent Folders, and My Websites. My Computer was added later as the fifth. The recommendation panel/switch, custom-category editing, and mixed All Favorites view were removed. Legacy Entries/Categories remain compatible for rollback; old recommendation preferences are no longer used. Other preferences and launch records remain intact.

## My Apps

At most 30 entries. Only installed apps with activity evidence or explicit additions appear. Ordering uses descending local activity score, most recent iStart launch, then name. Windows evidence is the current user's positive cumulative `Explorer/FeatureUsage/AppSwitched` values, matched by exact EXE path or `System.AppUserModel.ID`; known Program Files GUIDs expand to system directories. The larger of Windows and iStart counts is used, not a sum presented as precise launch statistics. This is not an exact monthly report. At most 2,048 registry values are read; failure falls back to existing items and iStart records.

Search can find installed apps without prior activity, still capped at 30. Added apps can appear here. Hide only adds the target to local HiddenSoftware; it does not uninstall software. Settings can reset hidden entries. Paths, app lists, and activity are not uploaded.

The preview.25 activity fix checks at most 2,048 positive records from the same Windows snapshot and adds at most 128 valid local EXE candidates. Missing old paths, already-discovered apps, and filtered entries do not consume these supplement slots. Existing local portable apps can appear without a Start-menu shortcut. Missing/relative/network paths, installers, and common helper processes are excluded; existing shortcut targets keep their original launch arguments. Returning to My Apps refreshes discovery/activity; replaced refreshes are cancelled without overwriting the newer scan's status. This does not scan drives or change the 30-app cap.

## Recently Opened: FreeCAD source (preview.25)

FreeCAD does not always publish its file opens to Windows Recent. iStart reads the current user's saved `FreeCAD/user.cfg` and up to eight immediate `FreeCAD/v*/user.cfg` configurations under `%APPDATA%`, at most 4 MiB each. Only `Root/BaseApp/Preferences/RecentFiles/FCText` entries named `MRU` plus a number are considered, in numeric order, up to 100 per config. STP, STEP, FCStd, and other valid local document targets are supported; folders, missing files, executables, relative paths, and network locations are excluded. Other `Recent` groups (such as material identifiers) are ignored.

MRU0 is the newest saved entry. `RecentFiles` is a display-capacity preference, not a visit counter. FreeCAD supplies no per-file opening timestamp here; profile modification time is only an approximate merge key. Unsaved session changes or a custom `--user-cfg` location are not monitored. The existing external-file-history switch controls these reads; no file contents are read, configurations are not changed, and results are not persisted or uploaded. Missing Windows Recent folders do not suppress these application records.

The 2026-10-29 local check found four valid FreeCAD records, including one STP, and a used FreeCAD EXE without a current Start-menu shortcut. Fixtures verify numeric order, legacy/version-profile merging, source preservation, filtering, cancellation, malformed/oversized XML, and DTD rejection. Existing regressions passed. This is a source fix, not a claim that every application's history is available.

Source: [FreeCAD recent-file read/write implementation](https://github.com/FreeCAD/FreeCAD/blob/main/src/Gui/Action.cpp).

## Read resilience and folder searches (preview.25)

Windows Recent links, automatic Jump Lists, LibreOffice, WPS, and FreeCAD are collected as separate readers. A recoverable read failure does not discard successful sources. Folder-history failure also leaves completed file reads available. The view reports a partial-read warning in either interface language; cancellation still propagates and the existing five-second UI budget remains in place. This does not guarantee partial results when the shared operation times out.

Folder searches apply the name/path filter before the result limit in both frequent and recent modes. Manually named folders are searchable. Case and trailing-separator duplicates in pinned folders are combined without rewriting stored entries or source directories.

The 2026-10-29 audit found 145 software candidates, compared with 114 before the stale-path quota fix; My Apps still displays at most 30. Source counts were 17 LibreOffice, 33 WPS, 50 automatic Jump List, and four FreeCAD records, with no reader failures in that check. These source counts overlap and exceed the final 100-file cap. Regressions cover invalid-path crowding, failed/locked sources, cancellation, folder search beyond the visible limit, duplicate spellings, and repeated software refreshes. Counts do not claim complete activity coverage for unsupported applications or unsaved histories.

## Frequent Folders

The default orders by iStart directory opens and recent-file directory activity, breaking ties by recent time and filling available space. The same-page switch selects recent ordering using records directly targeting folders from iStart/Windows, rather than treating a file's parent as a direct folder open. External times may be approximate; these are not complete Explorer visit counts. Pinned directories remain candidates without rewriting stored order. Pinned items with no direct opening record do not enter recent mode.

## My Websites

The user confirmed combining Chrome and Edge by domain. The same-page switch saves frequent/recent ordering; both modes show at most 30 sites. Frequent ordering aggregates retained cumulative URL visit counts; recent ordering uses last-visit time. Hostnames are case-insensitive. Nondefault ports remain separate; HTTP/HTTPS default ports combine. The latest visited page supplies the title; opening uses the site root without query parameters. Browser synchronization and iStart opens can overlap, so scores are not an exact visit audit.

Only HTTP/HTTPS addresses are supported. Hidden, unvisited, local-file/internal-protocol, and credential-bearing URLs are filtered. A local site icon is used without fetching favicons. Cookies, passwords, page contents, and private sessions are not read. Disabling browser history clears the external in-memory cache and leaves only iStart-opened sites. Clearing iStart records does not delete browser history.

Readers inspect Default/Profile databases under `%LOCALAPPDATA%/Google/Chrome/User Data` and `Microsoft/Edge/User Data`, at most 10 existing history databases per browser. Because browsers may lock them, History and any WAL/SHM are copied into a random private temporary directory, opened read-only using system winsqlite3, and closed/cleaned in finally after checking directory bounds. Each file is limited to 128 MiB; each profile reads at most the latest 10,000 URLs; the entire UI read waits up to five seconds. Browsers are not closed and source databases are not written. These are bounded snapshots, not guaranteed complete synchronization-time history. Failed, partially failed, and timed-out sources are reported rather than represented as empty history.

### Confirmed field catalog

The integrated object is the flat SQLite `urls` table in Chrome/Edge, without business enums or custom fields. Read-only local PRAGMA samples had the same seven columns; other History tables are not claimed as integrated. Code uses only url, title, visit_count, last_visit_time, and hidden. Examples are redacted.

| Field/API path | Observed type | Meaning/use | Example | Nullability and evidence |
| --- | --- | --- | --- | --- |
| urls.id | INTEGER / PK | URL row identity; unused | Redacted integer | Primary key; local PRAGMA and Chromium definition |
| urls.url | LONGVARCHAR | Address; HTTP/HTTPS only | https://[redacted-domain]/... | PRAGMA does not specify NOT NULL; empty values filtered |
| urls.title | LONGVARCHAR | Latest page title | [redacted-title] | Nullable; empty titles fall back to domain |
| urls.visit_count | INTEGER | Cumulative URL visits, aggregated for ordering | 323 | NOT NULL; observed sample and Chromium definition |
| urls.typed_count | INTEGER | Typed-navigation count; unused | Integer, not mapped | NOT NULL; local PRAGMA |
| urls.last_visit_time | INTEGER | Chromium timestamp converted to UTC | 2026-10-12 08:59:56 UTC after conversion | NOT NULL; invalid times filtered; sample conversion checked |
| urls.hidden | INTEGER | Include only 0 | 0 | NOT NULL; local samples and Chromium queries |

The user confirmed these mappings and domain aggregation in the working conversation. Queries check field readability; browser-internal schemas may change, and unsupported structures report failure. Real local PRAGMA and bounded read-only samples established the observed configuration; real browsing history was not committed.

Sources: [Chromium URL table](https://chromium.googlesource.com/chromium/src/+/refs/heads/main/components/history/core/browser/url_database.cc), [Microsoft system SQLite API availability](https://learn.microsoft.com/en-us/uwp/win32-and-com/win32-extension-apis), and [SQLite read-only open flags](https://sqlite.org/c3ref/c_open_autoproxy.html).

## Validation

Fixtures cover app ordering, the 30-item cap, hiding, excluding inactive recommendations, browser-profile filtering, domain aggregation, frequent/recent order, URL search, session hiding, and damaged-history reporting. UI checks cover the original four-view switch, removed controls, ordering preferences, and both themes. `--probe-activity <output-directory>` outputs source counts without URLs, titles, or app paths for read-only diagnostics.
