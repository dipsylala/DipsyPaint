# 27: Crash recovery + memory policy

**What to build:** While a document has unsaved changes, DipsyPaint periodically writes a recovery snapshot. After a crash, the next launch offers to restore it. Large operations are checked against a memory budget beforehand. Out-of-memory triggers an emergency snapshot. Spec: AC22 to AC27, ADR-0013.

**Blocked by:** 18 (OpenRaster open/save), 13 (Local crash log + "Report a problem"), 36 ("Save changes?" prompt and unsaved-changes tracking)

**Status:** ready-for-agent

- [ ] While dirty, an `.ora` snapshot is written to a per-user app-data recovery folder every 2 minutes by default (configurable)
- [ ] The pixel copy is synchronous; compression and writing run on a background thread, and the UI thread is not blocked
- [ ] Snapshots never write to the user's own file
- [ ] After an unclean exit, the next launch offers restore or discard for each snapshot
- [ ] A snapshot is deleted on clean save or explicit discard; stale snapshots are purged
- [ ] Open, new layer, canvas resize and duplicate are checked against the memory budget and refused with a clear message when over
- [ ] Out-of-memory is caught and an emergency snapshot attempted, alongside the crash log
- [ ] A headless UI test covers the recovery prompt after a simulated unclean exit
- [ ] Unresolved (non-blocking): tune the memory-budget value
