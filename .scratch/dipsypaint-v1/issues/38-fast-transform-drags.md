# 38: Fast rotate drags on large selections

**What to build:** Dragging the rotate handle keeps up with the pointer on large selections, shapes and text. Today every pointer move re-warps the whole floating bitmap with bilinear sampling. That takes about 78 ms per move for a full-HD selection on the developer machine (Release, PR #43), and a 4K selection is slower still, so the drag lags. ADR-0018 has no target for transform drags, so this ticket sets one: the same ≤ 16 ms per pointer event as brush input. It records that target as an amendment to ADR-0018.

**Blocked by:** 23 (Freeform rotate and skew)

**Status:** ready-for-agent

- [x] ADR-0018 is amended with a transform-drag target of ≤ 16 ms per pointer event at 4K, and `docs/benchmarks.md` lists it
- [x] A benchmark drags the rotate handle on a 3840×2160 selection over a 20-layer document (pointer down, 20 moves, pointer up, through `DocumentEditor.Apply`) and reports the mean cost per pointer event; it runs in the benchmarks workflow
- [x] That benchmark meets ≤ 16 ms per event on the developer machine, with the numbers in the PR
- [x] The committed result is unchanged: the existing rotate and skew golden images pass byte-for-byte
- [x] Stretching (resizing) a large pending selection by its handles is measured too; if it misses the target, it gets the same treatment or a follow-up ticket

## Notes

- A cheaper preview during the drag is acceptable, with full-quality bilinear output on release, if reference Paint's drag looks no better. Options: nearest-neighbour preview, drawing the overlay with a transform in the shell (Skia) instead of re-warping pixels, warping only the dirty or visible area, or parallel rows. State which and why.
- Each rotation must still start from the original pixels (no cumulative blur), as ticket 23 does.
- Mention any other interaction found over 16 ms on large documents while measuring; don't fix it here.
