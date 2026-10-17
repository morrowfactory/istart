[English](LOCALIZATION.md) | [简体中文](zh-CN/LOCALIZATION.md)

# Interface localization

`0.2.0-preview.22` introduced Settings → Interface language. GitHub Actions builds and publishes installers/checksums; downloads are linked in the [README](../README.md).

## Behavior

- System is the default and uses the current Windows user's display language. Chinese maps to Simplified Chinese; English and unsupported languages use English. Date formatting and keyboard/IME settings do not determine the language.
- Selecting Simplified Chinese or English applies and saves immediately without restarting. Selecting System restores automatic selection.
- Older configurations need no schema migration. Missing, empty, or unknown Language values become System without changing items, history, theme, screenshot shortcut, startup, or integration preferences.
- An open Settings window updates immediately while retaining unsaved checkboxes. The current page and search text remain. Language, appearance, screenshot shortcut, and the development automatic-update preference save immediately; menu/privacy checkboxes use the existing Save action.
- The menu, five navigation views, Settings, tray, context menus, account/power confirmation, screenshot tools, and updater are localized. Seven Control Panel titles use stable category IDs.
- App/file/folder names, volume labels, URLs, and screenshot text are not translated. Shell-supplied names, native Windows dialogs, the existing Chinese installation wizard, and GitHub release notes retain their own language.
- System language is read at startup. After changing the Windows display language while iStart runs, reselect System or restart iStart. Windows language is not modified, and no online translation is used.

## Maintenance

Localization.cs uses `L.T` to read the centralized English `Strings.en.json` mapping, with Chinese source text as fallback. `L.F` formats product messages using the selected UI language. JSON is explicitly embedded as a neutral resource, without external files or product satellite assemblies. XAML uses TrExtension; fixed Settings text uses language bindings; dynamic views redraw after a switch.

New product strings require English mappings with the same format placeholders and types. Do not use already-formatted user data as a translation key. Navigation, shortcut, and persisted-value logic must use stable identifiers rather than displayed labels.

Documentation localization is separate from the application's resource mapping. Default Markdown is English, Chinese pages live under `docs/zh-CN/`, and each page links to its counterpart. Update both versions when changing documented behavior; keep filenames in English.

## Validation

On 2026-10-16, the current Windows 11 25H2 x64 machine passed build checks with zero warnings/errors, core regression, 248 English mappings/format checks, compact layouts of five pages in both English themes, Settings/screenshot previews, and separate QA first install/overwrite/uninstall. Rendered results were visually inspected. This is not Windows 10 or an actual English Windows session acceptance claim.

SmokeTest.Localization.cs runs through scripts/Test-Prototype.ps1. It covers system-language parsing, unsupported-language fallback, manual precedence, old/unknown configuration, English persistence, immediate Settings/XAML round trips, retaining page/search/unsaved options, unchanged user data/privacy/theme/integration, the Off shortcut label versus its stable identifier, Control Panel labels, and default/compact English layouts. Format placeholders are checked for every English entry.

Artifacts include language-result.txt, default/compact Windows/Google English menus, Settings, and screenshot-tool previews. Fixtures use isolated configuration and synthetic images without changing host display language, startup, or Start preference. Explicit culture parameters test parsing; actual English Windows and Windows 10 language sessions remain pending.
