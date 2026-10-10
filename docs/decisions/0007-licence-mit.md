# ADR-0007: The project is open source under the MIT licence

- **Status:** Accepted
- **Date:** 2026-10-10
- **Amended by:** [ADR-0020](0020-imagesharp-licence-key.md) (ImageSharp 4 requires a build-time licence key)

## Context

Licence choice constrains which dependencies are usable and how patent-encumbered
codecs (e.g. HEVC/HEIC) can be distributed. Options: MIT, Apache-2.0, GPL-3.0,
proprietary. Core dependencies Avalonia and SkiaSharp are MIT.

## Decision

The project is **open source under MIT**. Not sold commercially.

## Consequences

- Dependencies must be MIT-compatible for redistribution (MIT, BSD, Apache-2.0,
  zlib, etc.). GPL/LGPL dependencies need explicit review.
- Six Labors ImageSharp (Split License) is usable under Apache-2.0 because the
  consuming project is open source. Downstream closed-source forks by large
  companies would need a Six Labors commercial licence — to be noted in
  third-party notices if ImageSharp is adopted.
- Bundling patent-encumbered codecs (HEVC for HEIC) is avoided; see the codec ADR.

## Sources

- https://packages.nuget.org/packages/SixLabors.ImageSharp/3.1.3/License
