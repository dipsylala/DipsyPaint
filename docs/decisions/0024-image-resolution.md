# ADR-0024: Documents carry a resolution (DPI) that files keep; sizes stay in pixels

- **Status:** Accepted
- **Date:** 2026-10-11
- **Related:** [ADR-0005](0005-document-model-flat-layers.md), [ADR-0006](0006-project-format-openraster.md), [ADR-0010](0010-no-printing.md), [ADR-0011](0011-additions-beyond-parity.md), [ADR-0014](0014-untrusted-file-handling.md), [ADR-0019](0019-file-formats-match-paint.md)

## Context

The New image dialog is getting size presets (ticket 41), including A4 and US Letter at 300 dpi.
DipsyPaint had no resolution concept: it never set resolution metadata, so saved files carried
ImageSharp's default whatever the image was for, and a 300 dpi scan opened and re-saved lost its
resolution. Showing "300 dpi" in the dialog would be wrong unless the saved file says the same.
DipsyPaint can't print (ADR-0010), so resolution only matters to the programs that open its files.

Options considered:

- **Label only:** "300 dpi" appears in the print presets' names, with no stored value.
- **Stored resolution:** the document keeps a resolution, and saving writes it into the file.
- **Physical size model:** width and height are entered in mm or inches, plus a DPI, as in
  Photoshop and GIMP.

## Decision

- **Pixels stay primary.** Width and height are always entered and shown in pixels. Resolution
  is separate metadata that only affects how other programs size the image for print. Reference
  Paint's New asks for nothing, and its Resize works in pixels and percent.
- **The document stores a resolution:** horizontal and vertical DPI, separately. New images and
  files without resolution information get 96 × 96.
- **Opening reads the file's resolution; saving writes it back.** This applies to PNG, JPEG,
  BMP, TIFF and OpenRaster (`xres`/`yres`). Re-saving writes the stored values back exactly. For
  example, PNG's pixels-per-metre is not round-tripped through rounded DPI, so values don't drift.
- **Formats without a resolution field drop it silently.** GIF is saved without it, and ICO and
  WebP are open-only. There is no lossy-save warning for resolution.
- **Validation (ADR-0014).** A file's value is accepted only from 1 to 10 000 dpi per direction.
  Missing, zero, negative, unreadable, out-of-range and aspect-ratio-only values all mean 96.
  Opening never fails because of resolution. When a file's value was invalid, the status bar
  says so until the next save or open; there is no dialog.
- **Resolution never changes with the pixels.** Resize, crop, rotate, skew and canvas-growing
  paste keep it, so the print size follows the pixel size.
- **Display.** The status bar shows the resolution beside the canvas size, as one number when
  both directions match and "X × Y dpi" otherwise. The New dialog shows it read-only (ticket 41).
  Choosing a value is left for later, probably an editable dropdown of common values.
- Showing and keeping resolution is an addition beyond reference Paint (ADR-0011).

## Consequences

- Each codec reads and writes resolution metadata, with round-trip and malicious-value tests per
  format. OpenRaster files gain `xres`/`yres`.
- Nothing changes resolution after a document is created, so there is no history step for it. A
  later "set resolution" command would need one.
- A future resolution dropdown, Image properties dialog or physical-size entry builds on the stored
  value without changing files.
