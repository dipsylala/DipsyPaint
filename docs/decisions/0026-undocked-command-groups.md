# ADR-0026: Command groups can be undocked over the canvas; drags show where a group will land

- **Status:** Accepted
- **Date:** 2026-10-12
- **Amends:** [ADR-0022](0022-command-bar-layout.md) (its "no floating windows" rule)
- **Related:** [ADR-0011](0011-additions-beyond-parity.md), [ADR-0017](0017-no-telemetry.md)

## Context

ADR-0022 lets each command group dock to any window edge, and rules out floating groups because
separate OS windows behave differently per OS (notably Linux/Wayland, where an app cannot place its
own windows) and are harder to test. The maintainer wants groups to be undockable too. While
dragging, the group's only feedback is that it fades, so the user can't see where it will land
until they let go.

Options considered for an undocked group: a separate OS tool window, or a panel inside the main
window over the canvas. For undocking: a drop over the canvas, a menu command only, or a modifier
key while dropping. For drag feedback: an outline where the group will land, an outline that follows
the pointer, or both.

## Decision

An addition beyond reference Paint (ADR-0011), whose command bar is fixed.

- **Undocked groups** are panels inside the main window, over the canvas. They can't leave the
  window, so they behave the same on every OS and can be tested headless. There are still no
  separate OS windows. An undocked group lays out horizontally, as on the top edge, and keeps its
  move handle.
- **Undocking:** dropping a group over the canvas, away from its edges, undocks it there. A drop on
  an edge panel, or over the canvas within a short distance of its edge, docks it as before.
  Dragging an undocked group's handle moves it, or docks it by the same rules. The handle's menu
  gains **Undock**; its **Move to** items dock an undocked group.
- **Without a mouse:** Undock places the group at the canvas's top-left corner. The arrow keys on a
  focused undocked group's handle move it 10 px.
- **Drag outline:** while a group is dragged, a dashed outline shows where it will land: the docked
  slot it would take on an edge, or its undocked position over the canvas. It follows the pointer
  and the group fades as before. Esc cancels the drag.
- **Undocked groups stay inside the canvas area.** Their position is kept relative to it, and
  pulled back in when the window shrinks. The canvas does not move or resize when a group is
  undocked or docked, other than by the edge it leaves or joins.
- **Saved layout:** undocked groups and their positions are saved with the layout (ADR-0022) and
  restored at start. **Reset layout** docks every group in its default place.

## Consequences

- An undocked group covers part of the canvas. The user can move it aside or dock it again; painting
  under it needs it moved.
- The saved layout gains undocked groups and positions. Layouts saved by earlier versions restore
  unchanged; unknown or bad positions fall back to docking the group in its default place.
- The docked-slot outline is the group's size at the insertion point, so after the drop the
  edge's wrapping can place it slightly differently.
- Separate OS windows remain open as a later option; this ADR would then need superseding.
