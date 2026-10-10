# ADR-0006: OpenRaster (.ora) is the native project format

- **Status:** Accepted
- **Date:** 2026-10-10
- **Depends on:** [ADR-0005](0005-document-model-flat-layers.md)

## Context

Reference Paint saves layered work as proprietary, undocumented `.paint`
files. We need a format that preserves layers (ADR-0005). Options: OpenRaster;
a custom ZIP+PNG+JSON format; custom native plus `.ora` interchange;
reverse-engineered `.paint`.

## Decision

- The native layered format is **OpenRaster** (`.ora`: ZIP containing
  `mimetype`, `stack.xml`, per-layer PNGs, `mergedimage.png`, thumbnail).
- App-specific data goes in **namespaced extension attributes/elements** in
  `stack.xml`; files must remain valid OpenRaster that other apps can open.
- **No `.paint` support** in v1 (undocumented; fragile to reverse-engineer).

## Consequences

- Layered work interoperates with Krita, MyPaint, Pinta, GIMP (plugin).
- Features the spec cannot express (e.g. future editable text layers) need
  extensions that other apps will ignore — they degrade to raster.
- Layer properties we expose (opacity, visibility, blend mode, name) should
  stay within what OpenRaster supports, or extensions become load-bearing.
- Users moving from reference Paint cannot open existing `.paint` files.
