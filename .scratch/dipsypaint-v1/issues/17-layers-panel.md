# 17: Layers panel

**What to build:** The user can add, delete, duplicate, merge, hide and reorder *layers* in a panel on the right, as in reference Paint. The compositor caches the composite of the layers above and below the active layer. Spec: user story 21, AC17, ADR-0006, ADR-0018.

**Blocked by:** 03 (Undo/redo with region-diff history steps)

**Status:** ready-for-agent

- [ ] Add, delete, duplicate, merge, hide and reorder behave as in reference Paint
- [ ] Each layer operation is undoable
- [ ] Exports flatten to the composited image
- [ ] New layer and duplicate are checked against the memory budget and refused with a message when over
- [ ] Layer properties exposed stay within what OpenRaster can represent
- [ ] Golden images for compositing multiple layers with visibility changes
- [ ] Unresolved (non-blocking): blend modes. Expose them only if reference Paint does, and only modes OpenRaster supports
