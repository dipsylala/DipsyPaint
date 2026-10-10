# 08: Fill tool

**What to build:** The user can flood-fill a region with the active colour, as in reference Paint. The fill uses a scanline algorithm with no recursion. Spec: AC32, ADR-0018.

**Blocked by:** 03 (Undo/redo with region-diff history steps)

**Status:** ready-for-agent

- [ ] Fill matches reference Paint's behaviour (contiguous region, active colour)
- [ ] Scanline implementation; a full 4K canvas fill causes no stack overflow
- [ ] Golden images for fills of simple and complex regions
- [ ] Fill is undoable
