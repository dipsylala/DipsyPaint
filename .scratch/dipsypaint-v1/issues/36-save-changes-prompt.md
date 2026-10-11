# 36: "Save changes?" prompt and unsaved-changes tracking

**What to build:** DipsyPaint knows when the document has *unsaved changes*: any history step since the last save, open or new, including steps undone back past the saved point or redone forward from it. Before New, Open, closing the window, or "Restart to update" discards a document with unsaved changes, it asks whether to save, as reference Paint does: Save, Don't save, or Cancel. The title bar marks unsaved changes as reference Paint does. Ticket 27 (crash recovery) reuses the same tracking to decide when to write snapshots.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The editor exposes whether the document has unsaved changes, with a change notification; undoing back to the saved point clears it and redoing past it sets it again
- [ ] New, Open, window close and "Restart to update" prompt only when there are unsaved changes
- [ ] Save saves first (falling back to Save as for an untitled document) and continues only if the save succeeded; Don't save continues; Cancel leaves everything as it was
- [ ] The prompt's wording, buttons and title-bar marker match reference Paint, with strings from `.resx` and automation names
- [ ] The prompt is keyboard-operable (Enter = Save, Esc = Cancel)
- [ ] Headless tests cover each trigger with each choice, and the undo/redo cases of the tracking

## Notes

- Closing on macOS includes Cmd+Q and the app menu's Quit, not only the window's close button.
- A failed or cancelled save keeps the document open.
