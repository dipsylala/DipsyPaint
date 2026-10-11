# 42: Fast handle drags on large shapes

**What to build:** Dragging a handle of a pending shape keeps up with the pointer, whatever the outline and fill style. Today every pointer move redraws the whole shape. The `Shape.RotateEvent` and `Shape.StretchEvent` benchmarks miss ADR-0023's ≤ 16 ms per event target on a 3780×2080 oval at 4K × 20 layers. Numbers from PR #56 (local Release, `--job short`):

| Style | Rotate | Stretch |
| --- | ---: | ---: |
| solid | 12.1 ms | 43.8 ms |
| marker | 18.0 ms | 19.1 ms |
| natural pencil | 28.6 ms | 30.2 ms |
| crayon | 34.9 ms | 35.6 ms |
| watercolour | 49.7 ms | 51.7 ms |
| oil | 50.9 ms | 52.6 ms |

Spec: ADR-0018, ADR-0023.

**Blocked by:** 37 (Textured shape outline and fill styles), 38 (Fast rotate drags on large selections)

**Status:** ready-for-agent

- [ ] `Shape.RotateEvent` and `Shape.StretchEvent` meet ≤ 16 ms per event for every style on the developer machine (default job, not `--job short`), with before and after numbers in the PR
- [ ] The shape drawn when the drag ends, and when it is committed, is unchanged: all existing shape goldens, solid and textured, pass byte-for-byte
- [ ] Moving a pending shape by its body is measured too. If it misses the target, it gets the same treatment
- [ ] Copying the composite to the screen (`DocumentCanvas`) is measured for a full-canvas change at 4K, as a benchmark or a documented probe. If it alone takes a large share of 16 ms, record that and open a follow-up ticket
- [ ] The preview's geometry is tested: the preview covers the same area as the final shape, so an error there is caught

## Notes

- ADR-0023 allows a cheaper preview while dragging if it looks no worse than reference Paint's drag. Options:
  - an engine-side preview, as ticket 38 did for selections: e.g. a solid or lower-detail rendering of the textured style, or reusing the previous rendering transformed
  - drawing the pending shape in the shell with a GPU transform (Skia) while dragging, and rendering exact pixels on release

  State which, and why. The engine must stay UI-free (ADR-0016), and layers above the active layer must still draw over the preview.
- Solid stretch is slow on its own (43.8 ms), separately from textures: the oval is rebuilt in full on every move.
- Rotating textured shapes keeps their texture seeded from the shape (ticket 37), so the texture must not flicker during or after a drag.
