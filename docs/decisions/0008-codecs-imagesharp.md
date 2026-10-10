# ADR-0008: All file I/O through ImageSharp; Skia for rendering only

- **Status:** Accepted; format list superseded by [ADR-0019](0019-file-formats-match-paint.md)
- **Date:** 2026-10-10
- **Depends on:** [ADR-0004](0004-stack-avalonia-skiasharp.md), [ADR-0006](0006-project-format-openraster.md), [ADR-0007](0007-licence-mit.md)

## Context

Reference Paint opens/saves PNG, JPEG, BMP, GIF, TIFF, ICO, and opens HEIC with
Microsoft's HEVC extension. SkiaSharp's encoder set is believed (unverified)
to be PNG/JPEG/WebP only — no GIF or TIFF encode. Options: ImageSharp for all
I/O; Skia decode + ImageSharp for encode gaps; Skia only + hand-written BMP/ICO;
Magick.NET (full coverage incl. HEIC, large native binaries, patent exposure).

## Decision

- **SixLabors.ImageSharp** handles all encode/decode, including the per-layer
  PNGs inside `.ora` files and image data from the clipboard.
- **SkiaSharp** is used only for rendering, compositing and pixel operations;
  pixels cross the boundary via a single conversion point.
- ~~**v1 formats** (import and export): PNG, JPEG, BMP, GIF (first frame on
  import), TIFF, WebP, ICO.~~ Superseded by [ADR-0019](0019-file-formats-match-paint.md).
- **HEIC/AVIF deferred** (HEVC patent exposure; not v1-critical).

## Consequences

- One codec path → consistent handling of EXIF orientation, colour profiles,
  alpha, and malformed input; one place to harden (ADR-0014).
- JPEG decode somewhat slower than Skia's libjpeg-turbo; acceptable, as there
  is no opening-time requirement (ADR-0018 amendment).
- ImageSharp's Split License must be recorded in third-party notices (ADR-0007).
- Users opening iPhone HEIC photos are unsupported in v1.
- Animated GIF is flattened to its first frame; no animation editing.

## Sources

- https://packages.nuget.org/packages/SixLabors.ImageSharp/3.1.3/License
- https://learn.microsoft.com/dotnet/api/skiasharp.skencodedimageformat
