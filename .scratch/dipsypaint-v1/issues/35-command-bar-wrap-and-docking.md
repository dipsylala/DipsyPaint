# 35: Command bar wrapping, edge docking and saved layout

**What to build:** The command bar never clips its controls: *command groups* (File, History, Clipboard, Image, Tools with the active tool's options, Colours, Help, Update) wrap onto extra rows or columns when space runs out. The user can move any group to the top, bottom, left or right edge of the window, and reorder groups within an edge, by dragging or with a keyboard-accessible "Move to" command. Groups docked left or right lay out vertically. The layout is saved per user and restored at start, and a Reset layout command restores the default single top bar. An *addition* beyond parity (ADR-0011), requested by the maintainer. Spec: ADR-0022.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [x] At the default window size and at narrower widths, no command-bar control is clipped or off-screen: groups wrap to extra rows (top/bottom) or columns (left/right)
- [x] Any command group can be docked to the top, bottom, left or right edge and reordered within an edge, by drag and drop
- [x] The same moves can be made without a mouse (e.g. a "Move to" command on each group), with automation names and strings from `.resx`
- [x] Groups docked left or right lay out vertically and stay usable (palette, tool options, buttons)
- [x] The canvas area resizes to the space left; fit-to-window and zoom use it
- [x] The layout is saved to a per-user settings file in local app data and restored at start; nothing is sent anywhere
- [x] A corrupt or unknown saved layout falls back to the default without crashing; groups added in later versions appear in their default place
- [x] Reset layout restores the default arrangement (reference Paint's single top bar, current group order)
- [x] Headless tests cover wrapping at narrow widths, docking via the keyboard command, the save/restore round trip, corrupt-file fallback and Reset layout

## Notes

- Floating tool windows are out of scope (ADR-0022).
- Any docking library must be MIT-compatible and produce no trimming/AOT warnings; a small custom layout is fine if simpler.
- Ticket 17's layers panel should fit into this system (as a group or panel docked right by default) rather than adding a second mechanism.
