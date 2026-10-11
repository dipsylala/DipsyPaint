# 40: Image resolution (DPI): stored, kept by files, shown in the status bar

**What to build:** A document has a resolution: horizontal and vertical DPI, 96 × 96 by default. Opening a file reads its resolution, saving writes it back, and the status bar shows it. Width and height stay in pixels; resolution only tells other programs how big to print the image. An *addition* beyond parity (ADR-0011). Spec: ADR-0024.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The document stores horizontal and vertical DPI. New images, and files without resolution information, get 96 × 96
- [ ] PNG, JPEG, BMP, TIFF and OpenRaster (`xres`/`yres`) read the file's resolution on open and write the document's on save. GIF saves without it, silently
- [ ] Opening and re-saving writes the original stored values back exactly (e.g. 11811 px/m in a PNG stays 11811), for every format above
- [ ] A file's value counts only from 1 to 10 000 dpi per direction. Missing, zero, negative, unreadable, out-of-range and aspect-ratio-only values mean 96. Opening never fails because of resolution
- [ ] Resize, crop, rotate, skew and canvas-growing paste leave the resolution unchanged
- [ ] The status bar shows the resolution beside the canvas size ("2480 × 3508 px · 300 dpi", or "204 × 196 dpi" when the directions differ). After opening a file whose value was invalid it shows "96 dpi (file's value was invalid)" until the next save or open. Strings come from `.resx`
- [ ] Crash-recovery snapshots and `.ora` saves keep the resolution
- [ ] Tests: round trip per format; a malicious-sample set (zero, negative, huge, zero TIFF denominator, non-numeric `.ora` attributes, JPEG aspect-ratio-only) in the CI corpus; status bar text

## Notes

- ImageSharp exposes resolution through `ImageMetadata` (`HorizontalResolution`, `VerticalResolution`, `ResolutionUnits`). Keep the file's own units and values where needed, so nothing drifts.
- The clipboard ignores resolution: paste uses the document's.
- Nothing changes the resolution after a document is created, so there is no history step.
