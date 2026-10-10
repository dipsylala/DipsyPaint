# ADR-0019: Flat file formats match reference Paint, minus HEIC

- **Status:** Accepted
- **Date:** 2026-10-10
- **Supersedes:** the "v1 formats" bullet of [ADR-0008](0008-codecs-imagesharp.md)
  (ImageSharp-for-all-I/O still stands)
- **Related:** [ADR-0006](0006-project-format-openraster.md), [ADR-0009](0009-milestone-phasing.md)

## Context

ADR-0008 chose PNG, JPEG, BMP, GIF, TIFF, WebP, ICO for both import and export.
The maintainer then asked for the same formats as reference Paint. Per a
third-party guide (save list from a single source, so treat as likely, not
certain):

- **Paint opens:** BMP/DIB, JPEG, GIF, TIFF, PNG, ICO, HEIC, WebP
- **Paint saves:** BMP (monochrome, 16-colour, 256-colour, 24-bit), JPEG, GIF,
  TIFF, PNG, HEIC, `.paint`

Two items conflict with earlier decisions: HEIC (ADR-0008 deferred it over HEVC
patent exposure; ImageSharp lacks it; the only clean route is OS codecs, which
Linux lacks) and `.paint` (ADR-0006: proprietary, undocumented).

Options: match Paint minus HEIC and `.paint`; add HEIC via OS codecs; add HEIC
and reverse-engineered `.paint`.

## Decision

| Format | Open | Save |
| --- | --- | --- |
| PNG | ✓ | ✓ |
| JPEG (`.jpg` `.jpeg` `.jpe` `.jfif`) | ✓ | ✓ |
| BMP / DIB | ✓ | ✓ 24-bit, 256-colour, 16-colour, monochrome |
| GIF | ✓ (first frame) | ✓ |
| TIFF | ✓ | ✓ |
| ICO | ✓ | — |
| WebP | ✓ | — |
| OpenRaster `.ora` | ✓ | ✓ (project format, ADR-0006) |
| HEIC | — | — (deviation) |
| `.paint` | — | — (deviation, ADR-0006) |

All via ImageSharp (ADR-0008). Low-colour BMP and GIF saves use palette
quantisation.

## Consequences

- Two documented parity gaps: HEIC (iPhone photos) and `.paint`.
- ICO and WebP saving is dropped vs ADR-0008 (Paint doesn't save them).
- Saving to palette formats (≤ 256-colour BMP, GIF) loses colour and alpha;
  warn the user before a lossy save, as Paint does.
- Single codec path preserved; no OS-specific codec code.

## Sources

- https://www.digitalcitizen.life/paint-windows-11/
- https://en.wikipedia.org/wiki/Microsoft_Paint
