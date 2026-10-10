# 14: Spike: NativeAOT vs ReadyToRun

**What to build:** Find out whether DipsyPaint can publish with NativeAOT given ImageSharp, SkiaSharp and Avalonia 12, and which publish mode meets the cold-start target. Record the outcome. Spec: Unresolved Question 1, AC33, ADR-0018.

**Blocked by:** 01 (Walking skeleton)

**Status:** ready-for-agent

- [x] A NativeAOT build of the skeleton is attempted on all three OSes; trimming/AOT warnings and failures are documented
- [x] Cold start to usable is measured for NativeAOT and ReadyToRun
- [x] The chosen publish mode and evidence are recorded as an amendment to ADR-0018
- [x] The "Spikes needed" entry in the decision index is updated
