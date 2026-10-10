# 29: Artistic brushes II

**What to build:** The user can paint with airbrush, oil brush and watercolour brush, each with size and opacity controls, matching reference Paint's look. Spec: user story 13.

**Blocked by:** 19 (Stroke opacity)

**Status:** ready-for-agent

- [ ] The three brushes are present and their strokes resemble reference Paint's
- [ ] Size and opacity controls apply
- [ ] Golden images per brush; randomised brushes (e.g. airbrush) are seeded so golden tests are deterministic
- [ ] Brush latency stays within the ≤ 16 ms target (benchmark from ticket 15 extended)
