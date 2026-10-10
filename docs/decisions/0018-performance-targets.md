# ADR-0018: Performance targets with non-blocking CI benchmark tracking

- **Status:** Accepted (assumptions confirmed 2026-10-10)
- **Date:** 2026-10-10
- **Depends on:** [ADR-0005](0005-document-model-flat-layers.md), [ADR-0013](0013-crash-recovery-snapshots.md), [ADR-0016](0016-testing-ui-free-engine.md)

## Context

Scale target is 4K × ~20 layers (ADR-0005). Without explicit targets, layer
compositing and undo memory tend to degrade silently. CI runners are too noisy
for hard performance gates. Options: targets + CI trend tracking; targets with
manual checks only; stricter 120 Hz targets.

## Decision

**Reference hardware:** 2020-era 4-core laptop, integrated GPU.

| Metric | Target |
| --- | --- |
| Brush input → on screen | ≤ 16 ms at 4K × 20 layers |
| Flood fill, 4K canvas | ≤ 200 ms |
| Cold start to usable | ≤ 2 s |
| Undo history memory | Cap = min(1 GB, 25% of RAM); oldest steps dropped |

**Enforcement:** BenchmarkDotNet suites run in CI; regressions are flagged but
do not block merges. Manual verification on reference hardware before each
milestone release (ADR-0009).

## Consequences

- Compositor caches the composite of layers below and above the active layer
  and redraws only dirty rectangles.
- Flood fill uses a scanline algorithm (no recursion).
- Large operations run off the UI thread so it stays responsive (no
  file-opening time target; see Amendments).
- Publish with ReadyToRun or NativeAOT (Avalonia supports AOT; ImageSharp/
  SkiaSharp AOT compatibility to be confirmed by spike).
- Undo cap shares the memory budget of ADR-0013.

## Amendments

- **2026-10-10:** Removed the file-opening target (originally "Open 4K PNG /
  50 MP JPEG ≤ 1 s / ≤ 3 s"). There is no non-functional requirement on
  opening time.
