# ADR-0023: Transform drags have the brush's 16 ms target; previews may be cheaper than the result

- **Status:** Accepted
- **Date:** 2026-10-11
- **Amends:** [ADR-0018](0018-performance-targets.md) (adds a target)
- **Related:** [ADR-0005](0005-document-model-flat-layers.md), [ADR-0016](0016-testing-ui-free-engine.md)

## Context

ADR-0018 sets targets for brush input, flood fill and cold start, but none for dragging a
handle of pending content. Ticket 23 added freeform rotation, which re-warped the whole
floating bitmap bilinearly on every pointer move. Ticket 38 measured a rotate-handle drag
on a 3840×2160 selection over a 20-layer document at 126 ms per pointer event, and a
stretch drag at 96 ms (Release, developer machine). The drag lagged well behind the
pointer.

Options: no target (drags stay best effort); a looser target for transforms; the brush's
16 ms.

## Decision

**Target:** a transform drag (rotating or stretching pending content by its handles) costs
≤ 16 ms per pointer event at 4K × 20 layers, on the same terms as brush input: measured
through `DocumentEditor.Apply`, copying to the screen not included.

| Metric | Target |
| --- | --- |
| Transform drag, per pointer event | ≤ 16 ms at 4K × 20 layers |

**Previews:** while a handle is dragged, the engine may show a cheaper preview than the
result, as long as it looks no worse than reference Paint's drag. The result on release
must be the same pixels as without the preview, so golden images do not change. A rotate
drag shows a nearest-neighbour preview that the overlay samples only where it is
composited; releasing the handle computes the bilinear turn, from the original pixels as
before.

**Enforcement:** as ADR-0018. The `Transform.RotateEvent` and `Transform.StretchEvent`
benchmarks run in the benchmarks workflow (`docs/benchmarks.md`).

## Consequences

- During a rotate drag, `SelectionTool.Selection` stays as it was at the press, and only the
  frame (`Adornment`) and the overlay follow the pointer. Releasing the handle, committing,
  copying or stretching first turns the selection exactly.
- Recompositing a large dirty rectangle runs in parallel row bands. Every pixel is computed
  on its own, so the result does not depend on thread count or order, and engine output stays
  the same on every OS.
- A preview's edges are hard during the drag and soften on release.
