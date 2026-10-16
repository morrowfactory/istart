[English](SCREENSHOT_VALIDATION.md) | [简体中文](zh-CN/SCREENSHOT_VALIDATION.md)

# Screenshot and annotation validation

preview.20 development changes used Alt+Shift+A, as shown in the WeCom capture hint, as the default global shortcut, with alternate combinations and an Off option. The toolbar switched to icons and added double-click/Enter completion, right-click reselection, and redo. Fixtures checked shortcut mapping, rejection of unknown combinations, preference persistence, undo/redo, and clearing annotations on reselection. Actual shortcut conflicts, background activation, and simultaneous WeCom use still required interactive acceptance; fixture success did not cover them. Synthetic tests did not capture the real desktop.

The screenshot feature originated in preview.19. Public availability is determined by GitHub Releases.

Automated fixtures passed for dragging a selection, moving/resizing its edges and corners, rejecting annotations outside it, arrow/ellipse/rectangle color pixels, pen paths, text/mosaic export, undo/clear, PNG decoding, and retained dimensions. They used synthetic imagery without reading the desktop or writing the user's clipboard. Toolbar positioning beside the selection was rendered and checked.

Interactive acceptance remained pending for actually hiding the menu before capture, correct real-desktop contents, DPI/multiple-monitor placement, text IME, copy/paste, and save cancellation. Capture is limited to the menu's current monitor, without cross-monitor selections, scrolling capture, OCR, or translation. Mosaic is a visual annotation and does not guarantee irreversible removal of sensitive information.

Workflow: Capture → drag selection → adjust/annotate → finish and copy, or save PNG. Esc cancels; Ctrl+Z undoes; Ctrl+C copies. In text mode, Enter completes text and Shift+Enter adds a line break. Screenshots are not automatically saved or uploaded.
