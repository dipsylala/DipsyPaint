# 39: Command bar polish: steady canvas, compact tools row, one menu group

**What to build:** Fixes to the command bar after ticket 35 and the M2 tickets, from the maintainer's use of the app. Switching tools must not move the canvas. The Tools row must be no taller than its buttons, with tool options lined up on them. File, View and Help must sit together in one command group, like a menu bar. Written after the work was done, to track it. Spec: ADR-0022.

**Blocked by:** 35 (Command bar wrapping, edge docking and saved layout)

**Status:** ready-for-agent

- [x] Switching tools never moves the canvas: the Tools group keeps room for its largest options panel, measured in the space the group has (PR #40)
- [x] Tool options are centred on the 40 px tool buttons, not centred in the room kept for them (PR #47)
- [x] The room kept for options is no bigger than the largest panel actually shown: the text tool's font boxes have a fixed height, so the editable size box no longer measures several lines tall before its text is set (PR #48)
- [x] The Tools row is no taller than its buttons: the shape gallery uses 20 px buttons, so two rows fit beside the 40 px tool buttons (PR #48)
- [x] File, View and Help form one "menu" command group, first on the top edge; saved layouts naming the old `file`, `view` and `help` groups fall back to the menu group's default place (PR #57)
- [x] Headless tests cover each of these in `CommandBarLayoutTests`: `SwitchingToolsNeverMovesTheCanvas`, `ToolOptionSlidersLineUpWithTheToolButtons`, `RoomKeptForToolOptionsIsNoBiggerThanTheLargestPanelShown`, `ToolsGroupIsNoTallerThanItsButtonRow`, `FileViewAndHelpShareOneMenuGroupFirstOnTheTopEdge`

## Notes

- Trade-offs accepted: tools with small or no options leave empty space in the bar, and the shape gallery's buttons are smaller click targets (20 px instead of 26).
- Any new tool options panel must be no taller than the tool buttons, or `ToolsGroupIsNoTallerThanItsButtonRow` fails.
- The gaps between File, View and Help are wider than a typical menu bar's, because each menu keeps its own margin. Not changed.
