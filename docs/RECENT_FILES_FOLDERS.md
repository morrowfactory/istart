[English](RECENT_FILES_FOLDERS.md) | [简体中文](zh-CN/RECENT_FILES_FOLDERS.md)

# Recent files and frequent folders

preview.15 limited Recently Opened to existing local documents, images, and similar files, excluding folders, programs, shortcuts, and missing targets. Filtering covered iStart records and other application history without deleting existing launches or favorites.

At that milestone, Frequent Folders first preserved valid folders manually pinned in that category and their order, then filled visible icon slots with suggestions. An existing category of that name was reused; otherwise a virtual view showed all favorited folders, with pins saved to Uncategorized. The virtual view did not create a stored category. Categories and the All Favorites page were later removed from navigation; see [activity views](ACTIVITY_VIEWS.md) for current behavior.

Suggestions considered iStart launch counts, folder records in Windows Recent/automatic Jump Lists, and activity counts for directories containing recent files. Higher activity ranked first, with recency breaking ties. Candidates were deduplicated; pinned entries exceeding a screen were not truncated. External timestamps could be approximate, and counts were neither complete system visit frequencies nor a guaranteed one-month statistic. No whole-drive scan, document-content reading, or automatic UNC-folder probing was performed.

Automatic suggestions were not persisted as favorites and did not enter All Favorites. The context menu could pin or temporarily hide a suggestion; hiding reset after restart. Original program/file/folder locations were unchanged. Search expanded the candidate scope. Normal view filled up to 48 items according to window size; search allowed up to 100.

The external recent-file/folder activity setting controlled these sources. Turning it off cleared external in-memory caches and used only iStart records. Reading had a five-second asynchronous budget; failure or timeout retained the existing view and manual entries. Windows and other applications' history was not changed.

Validation covered file-only recent results, independent folder-history reading, preserving manual order, target deduplication, frequency-first/recent fill, no fill when full, temporary hiding, folder-view exclusion of files/programs, and existing core/both-theme layout regressions.
