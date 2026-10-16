[English](PACKAGE_SIZE.md) | [简体中文](zh-CN/PACKAGE_SIZE.md)

# Installer size optimization

preview.23 optimizes installation size. The public installer is published by GitHub Actions from a verified `main` build artifact; `release-manifest.json` records its actual byte count and SHA256. Menu, tray, screenshot, activity-reading, update, and recovery behavior retain their existing implementations.

## Local comparison

On 2026-10-16, the original packaging at main `62ca6c2` and the optimized working tree were each built on the same Windows 11 PC with .NET SDK 10.0.401 and Inno Setup 7.1.0. These are that round's samples; later commit/version metadata can change a small number of bytes.

| Item | Before | After | Reduction |
| --- | ---: | ---: | ---: |
| Installer | 53,610,564 bytes (53.6 MB) | 50,929,534 bytes (50.9 MB) | 2.7 MB, about 5.0% |
| Self-contained publish directory | 179,900,614 bytes (179.9 MB) | 163,224,290 bytes (163.2 MB) | 16.7 MB, about 9.3% |
| Published files | 473 | 268 | 205 |
| Framework language directories | 13 | 1 (`zh-Hans`) | 12 |
| PDB debug-symbol files | 1 | 0 | 1 |

MB uses decimal units. `IStart.App.dll` is about 0.37 MB; most of the installer contains the .NET/WPF/WinForms framework needed for offline execution. Removing comments or `using` directives would not materially reduce it. Diagnostics and regression entry points were retained.

## Packaging changes

- `SatelliteResourceLanguages=en;zh-Hans`: the product currently provides English and Simplified Chinese. English framework strings use neutral resources; product English strings remain embedded. Only Simplified Chinese framework satellites are distributed. Other system languages use the existing English fallback; native Windows dialogs follow the OS language.
- `CopyOutputSymbolsToPublishDirectory=false`: PDB files remain in development build outputs and are excluded from the installer. Runtime diagnostic logs and recovery remain available.
- `Compression=lzma2/ultra64` and `SolidCompression=yes`: compression takes more build work, with an approximately 64 MiB decompression dictionary during installation. No extra runtime component is needed.
- Before publishing, cleanup is limited to the fixed, generated `artifacts/preview-win-x64` directory after path-scope validation; reparse points are rejected. This prevents stale languages or symbols from entering a new publish. Other packages, personal configuration, and test results are outside that cleanup scope.

The full self-contained runtime and license files remain bundled. WPF, WinForms, dynamic COM, and Shell integration are retained. Unsupported WPF assembly trimming is not enabled, framework DLLs potentially loaded through reflection/interoperability are not manually removed, and regional/date formatting remains available. Adding a product language requires expanding the publish-language list and validating again.

An upgrade does not actively delete language resources installed by older versions. Directory sizes above describe newly published content and a fresh-install payload, not automatic cleanup of an existing installation.

## Validation and delivery

`scripts/Test-Prototype.ps1` continues to check core behavior, Chinese/English UI, layout, screenshots, update verification, and recovery models. CI also runs the same regression against the self-contained published EXE, preventing a development PC's shared framework from masking missing publish resources. Both results are retained in technical-validation artifacts. The QA installer still checks first install, overwrite, and uninstall in a Chinese path containing spaces.

Local Windows 11 builds had 0 warnings/errors. Existing regressions passed in both development and self-contained modes; optimized QA first/overwrite/uninstall checks passed. The actual payload contained only the `zh-Hans` framework language directory and no PDB. Vision was reviewed: this changed packaging efficiency without changing product direction or feature scope.

Use the existing build commands in the README. Public publication still requires successful GitHub Actions build and release workflows from the latest main. A locally measured installer cannot replace the official artifact. Windows 10 and other Windows 11 acceptance boundaries remain unchanged.

References: [.NET satellite-resource publishing](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#satelliteresourcelanguages), [WPF/WinForms trimming limitations](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities), [Inno compression levels and memory](https://jrsoftware.org/ishelp/topic_setup_compression.htm).
