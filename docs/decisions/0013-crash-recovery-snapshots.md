# ADR-0013: Periodic recovery snapshots and memory-budget checks

- **Status:** Accepted (assumptions confirmed 2026-10-10)
- **Date:** 2026-10-10
- **Depends on:** [ADR-0005](0005-document-model-flat-layers.md), [ADR-0006](0006-project-format-openraster.md), [ADR-0011](0011-additions-beyond-parity.md)

## Context

Reference Paint has no autosave; a crash loses unsaved work. Options: none;
periodic recovery snapshot; write-ahead history journal (recover to last
stroke, much more complex). At the ADR-0005 scale, documents can reach
hundreds of MB, so memory exhaustion is a realistic crash cause.

## Decision

**Recovery snapshots** (an addition per ADR-0011), from **M2**:
- When the document is dirty, every N minutes (configurable; default 2 minutes,
  to be tuned) copy layer pixels and write an `.ora` to a per-user app-data
  recovery folder on a background thread.
- Never write to the user's own file.
- On launch after an unclean exit, offer to restore or discard each snapshot.
- Delete a document's snapshot on clean save or explicit discard.

**Memory policy:**
- Before large allocations (open, new layer, canvas resize, duplicate), check
  estimated cost against a memory budget; refuse with a clear message rather
  than crash.
- Catch out-of-memory as a last resort and attempt an emergency snapshot.

## Consequences

- Snapshot cost: one pixel copy (~33 MB per 4K layer) plus background
  compression; must not block the UI thread.
- Recovery folder needs size management (purge stale snapshots).
- Snapshots contain user image data on disk in app-data — note in privacy docs.
