# ADR-0022: Command bar wraps; command groups dock to window edges; layout is saved per user

- **Status:** Accepted
- **Date:** 2026-10-11
- **Amended by:** [ADR-0026](0026-undocked-command-groups.md) (groups can be undocked over the canvas)
- **Related:** [ADR-0011](0011-additions-beyond-parity.md), [ADR-0012](0012-ui-familiar-own-identity.md), [ADR-0017](0017-no-telemetry.md)

## Context

The command bar is one horizontal row of feature controls (File, History, Clipboard, Image,
Tools with the active tool's options, Colours, Help, Update). At the default 1100 px window
width it overflows: the palette is clipped and Help and the update button are off-screen.
Reference Paint's command bar is fixed (a collapsible toolbar is in Insider testing), so
anything beyond fitting the window is an addition (ADR-0011).

Options considered for movement: reorder within the top bar only; dock to the window edges;
edges plus floating tool windows. Floating windows behave differently per OS (notably
Linux/Wayland) and are harder to test. For persistence: per session only, or saved per user.

## Decision

- **Wrapping:** command groups wrap onto extra rows (top or bottom edge) or columns (left or
  right edge) instead of being clipped, at any window size the app allows.
- **Edge docking:** each command group can be moved to the top, bottom, left or right window
  edge and reordered within an edge. There are no floating windows. Groups on the left or right
  lay out vertically.
- **Accessible moving:** every move done by dragging can also be done without a mouse, e.g. a
  "Move to" command on the group.
- **Saved layout:** the layout is saved to a per-user settings file in local app data and
  restored at start. It is never sent anywhere (ADR-0017). A **Reset layout** command restores
  the default arrangement, which matches reference Paint's single top bar.
- This is DipsyPaint's first persisted setting; the settings file should be general enough for
  later settings, but only the layout is stored for now.

## Consequences

- Every command-bar control becomes a *command group* with a stable id, so saved layouts
  survive releases. Groups added in later versions appear in their default place; unknown or
  corrupt saved layouts fall back to the default without crashing.
- Group controls need horizontal and vertical arrangements (e.g. the palette's 2 × 10 grid
  becomes 10 × 2; the active tool's options panel must fit a narrow column).
- The canvas area takes whatever space the docked groups leave; fit-to-window uses that area.
- Any docking library must be MIT-compatible (ADR-0007) and produce no trimming/AOT warnings
  (NativeAOT, ADR-0018 amendment); a small custom layout is acceptable if simpler.
- Ticket 17's layers panel, which reference Paint shows on the right, should become a command
  group or panel in the same system rather than a separate mechanism.
