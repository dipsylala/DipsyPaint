# 03: Undo/redo with region-diff history steps

**What to build:** The user can undo and redo pencil strokes using reference Paint's shortcuts. Each *history step* stores only the before-pixels of the changed rectangle(s). History memory is capped, and the oldest steps are dropped when the cap is reached. Every tool from here on records history steps. Spec: AC10, AC11, ADR-0005, ADR-0018.

**Blocked by:** 02 (Pencil tool + golden-image harness)

**Status:** ready-for-agent

- [x] Undo restores the pixels exactly as before the step; redo reapplies it exactly
- [x] A history step's memory is proportional to the changed area, not the document size
- [x] History memory never exceeds min(1 GB, 25% of RAM); oldest steps are dropped first
- [x] Undo/redo shortcuts match reference Paint
- [x] Engine unit tests cover exact restore, redo after undo, redo cleared by a new step, and cap eviction
