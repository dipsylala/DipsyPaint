# 02: Pencil tool + golden-image harness

**What to build:** The user selects the pencil and draws on the canvas. This slice establishes the engine's core:

- the document as an ordered stack of *layers* (8-bit RGBA sRGB, premultiplied in memory), with exactly one layer in M1
- the pixel-access abstraction that every tool goes through
- tools driven by input events as data
- a dirty-rectangle compositor
- brush/tool architecture that already supports opacity and multiple layers, so M2 needs no rework

It also delivers the *golden image* test harness. Spec: ADR-0005, ADR-0009, ADR-0016, ADR-0018.

**Blocked by:** 01 (Walking skeleton)

**Status:** ready-for-agent

- [x] Drawing with the pencil shows on the canvas
- [x] A scripted pencil stroke, run through the engine API, matches a golden image within a per-pixel tolerance on all three OSes
- [x] A failing golden test emits a diff image as a CI artefact
- [x] The golden-image update workflow is documented
- [x] M1 documents have exactly one layer; the engine supports multiple layers
- [x] No tool accesses layer bitmaps except through the pixel-access abstraction
- [x] The pencil's keyboard shortcut matches reference Paint where practical
- [x] Unresolved (non-blocking): golden tolerance thresholds start conservative and are tuned to avoid flakiness
