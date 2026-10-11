# ADR-0025: Selections are made and moved with separate tools

- **Status:** Accepted
- **Date:** 2026-10-11
- **Related:** [ADR-0001](0001-feature-baseline-modern-paint.md), [ADR-0005](0005-document-model-flat-layers.md), [ADR-0011](0011-additions-beyond-parity.md), [ADR-0023](0023-transform-drag-target.md)

## Context

Reference Paint has one Select tool. Dragging outside the selection makes a new one, dragging inside it
lifts and moves the selected pixels, and its handles stretch and rotate them. DipsyPaint matched this
(tickets 10, 21, 22, 23). The same drag therefore means different things depending on where it starts.
There is also no way to move the selection outline without moving the image. The maintainer wants each
drag to have one meaning, and to be able to reposition the outline on its own.

Options considered: keep Paint's tool; add move tools next to Paint's behaviour; split making and
moving into separate tools, with or without a setting that restores Paint's behaviour.

## Decision

A deliberate deviation from reference Paint, with no setting to restore Paint's behaviour.

- **Select** only makes selections: Rectangle (Shift gives a square), Free-form or Magic wand. Any drag
  starts a new selection, inside or outside the current one. It shows the outline with no handles.
- **Move selection** (a new tool) moves the outline without changing the image. It has no handles. An
  outline may sit partly off the canvas; the part outside selects nothing.
- **Move selected pixels** (a new tool) lifts and moves the selected pixels. Its handles stretch and
  rotate them as the Select tool's did.
- Both Move tools: Shift locks a drag to horizontal or vertical; the arrow keys nudge 1 px and
  Shift+arrow 10 px; with nothing selected, they do nothing. The three tools sit together in the Tools
  group, in the order Select, Move selection, Move selected pixels, with no keyboard shortcuts.
- **Committing.** Switching among the three selection tools, or pressing Enter, commits floating pixels
  and keeps the outline where they landed. Switching to any other tool commits and clears the selection.
  With a Move tool, a click outside the selection does nothing. Esc cancels a pending pixel move, or
  removes the selection when nothing is floating.
- **Commands.** Delete, Cut, Copy, Crop, Invert, Select all, Transparent selection and the Image menu's
  rotate, flip, Resize and skew and Custom rotate work whichever of the three tools is active. Select
  all and Invert switch to Select. Paste switches to Move selected pixels.
- **History.** Making or moving the outline is not a history step. Moving, nudging (a run of nudges),
  stretching or rotating pixels is one history step when committed, as before.

## Consequences

- Moving pixels takes one more click than in Paint: choose Move selected pixels first. Paste puts the user
  there directly.
- The parity audit lists this as an intended deviation, not a gap.
- The engine's selection keeps its lift, stretch and rotate behaviour, now driven by the Move tool. The
  results of moving pixels are unchanged, and so are the golden images.
- Headless tests that move pixels by dragging inside a selection with the Select tool must use Move
  selected pixels.
