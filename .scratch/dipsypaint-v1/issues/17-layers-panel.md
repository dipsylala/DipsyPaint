# 17: Layers panel

**What to build:** The user can add, delete, duplicate, merge, hide and reorder *layers* in a panel on the right, as in reference Paint. The compositor caches the composite of the layers above and below the active layer. Spec: user story 21, AC17, ADR-0006, ADR-0018. If ticket 35 has landed, the panel is a command group (or panel) in its docking system, docked right by default (ADR-0022).

**Blocked by:** 03 (Undo/redo with region-diff history steps)

**Status:** ready-for-agent

- [x] Add, delete, duplicate, merge, hide and reorder behave as in reference Paint
- [x] Each layer operation is undoable
- [x] Exports flatten to the composited image
- [x] New layer and duplicate are checked against the memory budget and refused with a message when over
- [x] Layer properties exposed stay within what OpenRaster can represent
- [x] Golden images for compositing multiple layers with visibility changes
- [x] With the composite of the layers above and below the active layer cached, the 4K × 20-layer fill benchmark meets ≤ 200 ms (currently ~730 ms without caching; see `docs/benchmarks.md`)
- [x] Unresolved (non-blocking): blend modes. Expose them only if reference Paint does, and only modes OpenRaster supports. Resolved: reference Paint has no layer blend modes, so none are exposed.
