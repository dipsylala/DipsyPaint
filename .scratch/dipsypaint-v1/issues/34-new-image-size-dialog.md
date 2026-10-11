# 34: New image size dialog

**What to build:** File → New (Ctrl/Cmd+N) asks for the new image's width and height, prefilled with 800 × 600 px, before replacing the document. Cancel leaves the current document untouched. This is an *addition* beyond parity (ADR-0011): reference Paint's New creates a canvas without asking. Requested by the maintainer on 2026-10-11, which also makes 800 × 600 the deliberate default rather than a placeholder.

**Blocked by:** 04 (New/Open/Save for PNG, JPEG, BMP)

**Status:** ready-for-agent

- [x] New shows a dialog with width and height in pixels, defaulting to 800 × 600
- [x] OK creates a blank document of the entered size (empty history, title and status bar updated); Cancel changes nothing
- [x] Sizes are limited to 1–16384 px per side, and a size over the memory budget is refused with a message
- [x] The dialog is keyboard-operable (Enter = OK, Esc = Cancel) with automation names, and its strings come from `.resx`

## Notes

- Size presets, a width/height swap and a read-only resolution are added in ticket 41 (with resolution itself in ticket 40, ADR-0024).
