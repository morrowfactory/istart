[English](SEARCH_VALIDATION.md) | [简体中文](zh-CN/SEARCH_VALIDATION.md)

# Search fix validation: preview.7

Date: 2026-10-13. The capture showed an unchanged recommendation area after entering `codex`. Previously, `TextChanged` filtered favorites only; the first 12 recommendations were outside search. Discovery also found only traditional EXE shortcuts and missed Store apps.

At this milestone, immediate search covered all discovered applications and favorites across categories, with scrollable app results and a no-results message. Names and entry identifiers matched case-insensitively. Disabling recommendations retained local application search. It did not scan whole drives or upload data. Store apps could be opened through Windows but could not yet be pinned or dragged in.

A read-only local sample showed AppsFolder display name `ChatGPT` with identifier `OpenAI.Codex_2p2nqsd0c76g0!App`. Discovery found 166 candidates and one Codex identifier. Regressions covered apps beyond item 12, case handling, no results, search with recommendations disabled, and a Store identifier differing from its display name. Build: 0 warnings/errors. Tests did not automatically send system keys or launch Codex.

README, architecture, and execution plan were synchronized; vision was reviewed without a direction change. Actual UI search and click-to-launch still required user feedback; read-only discovery was not launch acceptance.

## Store icons in preview.8

The user confirmed that a Store result had text but no icon. The fix parsed the Shell PIDL and read its native icon, falling back to a generic application icon. HICON/PIDL resources were released, and frozen bitmaps used the bounded cache. The actual local Codex 32×32 icon was saved as test output `codex-icon.png`; a white-background preview showed the original app mark. Build and core regressions passed. Vision was unchanged.

API reference: [Microsoft SHGetFileInfo](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow).

## Later historical UI changes

preview.11 disabled recommendations by default, collapsing the entire container without blank space. Settings became the only way to enable it; in-menu enable/disable controls were removed. Normal recommendations hid their heading/description; only search showed a results heading. Default-value, disabled-search visibility, and clear-search collapse regressions passed. Documentation was synchronized and vision reviewed.

preview.12 visually grouped All Favorites: with no search and both entry types present, programs appeared above files with a thin divider before the second group, without a new heading. Icon/list previews were visually checked. Stored `Entries` order did not change; search and recent views were not grouped. Documentation was synchronized and vision reviewed.

These are historical implementation records. The recommendation panel and category navigation were later removed. Current My Apps behavior and its 30-item limit are documented in [activity views](ACTIVITY_VIEWS.md).
