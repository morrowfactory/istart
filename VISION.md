[English](VISION.md) | [简体中文](docs/zh-CN/VISION.md)

# iStart vision

## Mission

Make Windows Start a personal work entry point: frequently used apps, files, project materials, and websites within easy reach, without relying on the desktop to store every shortcut.

## Audience

Windows 10/11 users who work across multiple apps, documents, project folders, and websites and want to keep everyday tools together. The first release focuses on personal desktop organization rather than enterprise desktop management.

## Core experience

Click the familiar Start button to open a compact, clear, reliable menu. Access My Apps, Recently Opened, Frequent Folders, My Websites, and My Computer. Prioritize frequently used items and switch folders and websites to recent ordering when needed. Windows Start remains easy to restore; authentication, account actions, and power behavior stay under Windows control.

Provide lightweight local screenshots: hide the menu, select a region, annotate it, and copy or save. Capture and output are user-initiated, with no automatic collection or upload of screen contents.

Respect the user's Windows display language by default while allowing an explicit preference. Start with Simplified Chinese and English and add languages when there is demonstrated demand. Changing the interface language must not rename apps, files, or websites or change Start integration. Documentation follows the same accessibility goal: English defaults and separate, clearly linked Chinese pages rather than two languages mixed into one document.

The first-use experience should be straightforward: a friendly Chinese installation wizard explains what is being installed, shows real progress, and makes completion clear. Cancellation or failure should have understandable outcomes and recovery paths. Settings shows the version and supports user-initiated update checks and downloads with visible progress. Upgrades retain personal items and settings. My Computer groups disks, Recycle Bin, Network, and Control Panel, while Windows continues to handle system management.

Do not introduce a separate installed-app recommendation panel. My Apps shows at most 30 apps with activity evidence or explicit user additions, ordered by usage. Missing history must be acknowledged instead of filling the menu with every installed app. Browser history stays local, with frequent and recent website views and no browsing-activity upload.

## Product principles

1. **User control:** allow adding, hiding, and choosing frequent/recent order. Retain existing data; automatic suggestions never move source files.
2. **Compact and clear:** reduce empty space and decoration, allow sensible density adjustments, and preserve legibility, keyboard access, and clear feedback.
3. **Recover native Start:** make temporary access and default-mode switching easy to find. A failure must not leave Start unusable.
4. **References first:** store access points. Removing an item does not delete its source; dropping a file does not move it.
5. **Local and restrained:** do not upload paths or activity by default or scan the entire disk for recommendations. Make history scope transparent and clearable.
6. **Evidence-based compatibility:** distinguish targets, compatibility trials, and tested systems. Untested builds require successful capability detection and explicit opt-in; failures retain native Start. Users control diagnostics. Do not promise compatibility with every future Windows update.
7. **Transparent installation:** explain options, show actual progress, and make results clear. Users decide whether to enable integration; a failure must not prevent access to Windows Start.

## Scope boundaries

The first release focuses on Start integration, apps and file access, drag-and-drop organization, search, and reliable recovery. It excludes replacement authentication, a full desktop shell, whole-disk content search, cloud synchronization, advertising recommendations, and remote-device management. Future extensions should follow real needs rather than speculative infrastructure.

## Delivery stages

Validate integration and recovery with the smallest viable implementation before expanding the menu. See the [execution plan](EXECUTION_PLAN.md); a standalone launcher does not substitute for Start-integration acceptance.

| Stage | Deliverable | Completion evidence |
| --- | --- | --- |
| Requirements and prototype | Product documentation and compact visual design | Drag-in, ordering, native fallback, and system-function retention recorded; static prototypes available |
| Technical validation, current | Runnable preview and recovery evidence | Windows 11 build 26200 basic integration and abnormal-exit recovery checked; complete dual-system acceptance pending |
| Local interaction | Menu, item organization, drag-and-drop, search, and persistence | Added items and chosen ordering survive closing and reopening |
| System integration | Start button and Win key on Windows 10/11, native switching, and recovery | Named system builds pass; abnormal exit retains a recovery path |
| Complete initial release | App discovery, bounded history, account/power actions, Chinese installation, and uninstall | Real progress, cancellation/failure recovery, compatibility matrix, core acceptance, and public limitations |
| Later refinements | Density, performance, and accessibility improvements based on feedback | Measurable improvements without weakening recovery or adding unnecessary collection |

## Success criteria

- Users can add apps and folders and find apps, recent files, folders, websites, and system resources through five recognizable views.
- Ordering is understandable; frequent/recent preferences persist; restarts and upgrades preserve existing items.
- Start-button and Win-key integration, native fallback, and account/power behavior pass on the declared tested builds.
- Exit, crashes, Explorer restarts, and uninstall do not leave Start unusable.
- Installation requires no command line. Users can understand the current step and progress; cancellation or failure preserves existing items and native Start.
- Activity reads are asynchronous and bounded. Missing history has a clear explanation. Hiding items, disabling features, and upgrading do not delete external history or source files.
- Frequently used content takes few actions to reach. The menu stays compact and adapts to real display scaling. Performance targets are measured after the runnable prototype exists.

A runnable preview, Chinese installation wizard, and constrained Windows 11 integration are available. Complete cross-system compatibility, complex recovery, and abnormal-installation acceptance remain prerequisites for a full initial release.
