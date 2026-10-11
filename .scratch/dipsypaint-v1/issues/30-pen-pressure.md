# 30: Pen pressure

**What to build:** On devices where Avalonia reports pen pressure, strokes respond to it. Mouse behaviour is unchanged. Spec: user story 16, ADR-0003.

**Blocked by:** 07 (Basic brush and eraser)

**Status:** ready-for-agent

- [x] Pressure affects strokes for pressure-capable tools when the OS reports it
- [x] Mouse input and pressure-less devices behave exactly as before
- [x] An input-event replay test with recorded pressure values produces a golden image
