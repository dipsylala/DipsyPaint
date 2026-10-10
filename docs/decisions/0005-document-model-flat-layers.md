# ADR-0005: Document model — flat 8-bit RGBA layers, region-diff undo

- **Status:** Accepted (assumptions confirmed 2026-10-10)
- **Date:** 2026-10-10
- **Depends on:** [ADR-0001](0001-feature-baseline-modern-paint.md), [ADR-0004](0004-stack-avalonia-skiasharp.md)

## Context

ADR-0001 requires layers and per-pixel alpha. Memory per layer at 4 bytes/px:

| Canvas | Per layer | 20 layers |
| --- | --- | --- |
| 1920×1080 | 8 MB | 166 MB |
| 3840×2160 | 33 MB | 663 MB |
| 8000×8000 | 256 MB | 5.1 GB |

Phone photos (12–50 MP) are 48–200 MB per layer.

Alternatives: tiled sparse layers with copy-on-write undo (bounded memory at
8K+, much more engine work); small-scale full-snapshot undo (simplest, fails
on phone photos).

## Decision

- **Design target:** 3840×2160 with ~20 layers. **Hard limit:** 16384 px per side.
- **Layer storage:** one flat bitmap per layer, accessed only through a
  pixel-access abstraction (no tool touches raw bitmaps directly), so tiled
  storage can replace it later without rewriting tools.
- **Undo:** each history step stores only the changed rectangle(s) (before
  pixels), not full-layer snapshots.
- **Pixel format:** 8-bit-per-channel RGBA, sRGB, premultiplied in
  memory (Skia native); straight alpha on export. No colour management in v1.
  Matches reference Paint.

## Consequences

- Large images (e.g. 50 MP photos) open, but with reduced layer headroom;
  memory pressure must be surfaced to the user rather than crashing.
- Undo memory is proportional to edited area, not document size × steps.
- Moving to tiles later is contained to the storage layer if the abstraction
  is respected — this must be enforced in code review.
- 16-bit / wide-gamut / ICC-aware editing would require revisiting this ADR.
- Undo memory cap and compositing performance targets: [ADR-0018](0018-performance-targets.md).
