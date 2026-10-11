# 41: Size presets in the New image dialog

**What to build:** File → New (Ctrl/Cmd+N) offers a list of common sizes as well as typing a width and height, so sizes other than 800 × 600 take one choice. A swap button flips width and height, and the dialog shows the new image's resolution read-only. Extends ticket 34; an *addition* beyond parity (ADR-0011). Spec: ADR-0024.

**Blocked by:** 40 (Image resolution)

**Status:** ready-for-agent

- [ ] A Size dropdown lists these presets in groups, each with its size in pixels:
  - 800 × 600 (default)
  - Screen: 1280 × 720 (HD), 1920 × 1080 (Full HD), 2560 × 1440 (QHD), 3840 × 2160 (4K UHD)
  - Square: 1080 × 1080, 512 × 512, 256 × 256
  - Portrait: 1080 × 1920
  - Print: A4 2480 × 3508 and US Letter 2550 × 3300, both at 300 dpi
  - Custom
- [ ] Choosing a preset fills in Width, Height and Resolution (300 dpi for the print presets, 96 for the rest)
- [ ] Editing Width or Height switches the dropdown to Custom and keeps the resolution. Choosing Custom keeps the current values
- [ ] A ⇄ button swaps width and height and keeps the preset selected (A4 swapped is A4 landscape) and its resolution. Provisional: revisit after trying it
- [ ] The resolution is shown read-only (e.g. "Resolution: 300 dpi"), and OK creates the document with it (ticket 40)
- [ ] The dialog always opens on 800 × 600 at 96 dpi
- [ ] Ticket 34's rules still hold: 1–16384 px per side, the memory-budget check, Enter = OK, Esc = Cancel. Strings and automation names come from `.resx`
- [ ] Headless tests: every preset's values, Custom on edit, swap, resolution passed to the new document, keyboard use

## Notes

- The resolution label should later become an editable dropdown of common values (72, 96, 150, 300, 600, or typed). Lay the dialog out so the label can be swapped for one.
- Physical sizes (mm or inches) and other DPIs for print presets are out of scope.
