# ADR-0009: Deliver parity in three milestones (M1/M2/M3)

- **Status:** Accepted (individual feature placement may move between milestones without a new ADR)
- **Date:** 2026-10-10
- **Depends on:** [ADR-0001](0001-feature-baseline-modern-paint.md), [ADR-0002](0002-no-ai-features.md), [ADR-0005](0005-document-model-flat-layers.md)
- **Amended by:** [ADR-0010](0010-no-printing.md), [ADR-0011](0011-additions-beyond-parity.md), [ADR-0013](0013-crash-recovery-snapshots.md), [ADR-0019](0019-file-formats-match-paint.md)

## Context

Non-AI parity with reference Paint is large (≈9 brushes, ≈23 shapes, layers,
selections with freeform transforms, text, printing). Options: phased
milestones; phased with layers in M1; one full-parity release.

## Decision

Three milestones. The layered document model (ADR-0005) exists from M1 even
though the layers UI arrives in M2 — M1 documents simply have one layer.

**M1 — Usable Paint**

- Pencil, basic brush, eraser, fill, colour picker
- Shapes: line, rectangle, ellipse (outline/fill)
- Rectangle selection: move, delete, copy/cut/paste
- Image: crop, resize, rotate 90°/180°, flip
- Zoom/pan, undo/redo
- Formats: PNG, JPEG, BMP; OS clipboard image in/out
- Primary/secondary colour, palette

**M2 — Modern Paint**

- Layers panel: add, delete, duplicate, merge, hide, reorder; `.ora` (ADR-0006)
- Brush/pencil/eraser opacity
- Text tool
- Free-form selection, invert, transparent selection, freeform rotate, skew
- Magic wand selection (addition beyond parity, [ADR-0011](0011-additions-beyond-parity.md))
- All remaining shapes
- Formats ([ADR-0019](0019-file-formats-match-paint.md)): GIF and TIFF open/save; ICO and WebP open-only; low-colour BMP save variants
- Custom colour editor
- Crash-recovery snapshots ([ADR-0013](0013-crash-recovery-snapshots.md))

**M3 — Parity polish**

- Artistic brushes: calligraphy ×2, airbrush, oil, crayon, marker,
  natural pencil, watercolour
- Rulers, gridlines, magnifier, full screen
- ~~Printing~~ — dropped, see [ADR-0010](0010-no-printing.md)
- Remaining gaps found by a parity audit against reference Paint

## Consequences

- Early releasable builds and user feedback after M1.
- Tool/brush architecture must anticipate opacity and layers from M1 to avoid
  rework in M2.
- "v1.0" = M3 complete.
