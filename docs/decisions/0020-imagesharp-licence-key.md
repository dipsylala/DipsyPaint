# ADR-0020: ImageSharp 4 licence key: obtain one; CI falls back to Debug without it

- **Status:** Accepted
- **Date:** 2026-10-10
- **Amends:** [ADR-0007](0007-licence-mit.md), [ADR-0008](0008-codecs-imagesharp.md)

## Context

ADR-0008 chose ImageSharp for all file I/O. ImageSharp 4.0+ enforces a Six Labors
licence key at build time for direct package dependencies: Release builds fail
without one; Debug builds only warn. The licence terms are unchanged (Six Labors
Split License; Apache-2.0 applies to open-source consumers, ADR-0007), but the key
is now required to build.

- Six Labors' docs say an open-source project must not commit the key or
  `sixlabors.lic`; use repository secrets, and contributors apply for their own keys
  at https://licensing.sixlabors.com/. Whether free community keys exist for open
  source is unconfirmed.
- Staying on ImageSharp 3.1.12 (no key) is not viable: seven advisories published
  2026-10-07 (four high, three moderate) are fixed only in 4.1.2+.

Options considered: obtain a key; switch to Magick.NET; SkiaSharp plus small
dedicated codecs; stay on 3.1.12 with mitigations.

## Decision

- Keep ImageSharp 4.x (ADR-0008 stands). The maintainer obtains a Six Labors key.
- CI reads the key from the repository secret `SIXLABORS_LICENSE_KEY` (full
  `sixlabors.lic` contents) into the `SixLaborsLicenseKey` environment variable.
- CI tests the **Release** configuration when the secret is available, and
  **Debug** otherwise (pull requests from forks, which never receive secrets, or
  before the secret exists). The run logs which configuration it tested.
- `sixlabors.lic` is git-ignored; the key is never committed.
- Local builds: Debug works without a key. Release needs `SixLaborsLicenseKey`
  set, or `sixlabors.lic` at the repo root (`Directory.Build.props` points
  ImageSharp at it, since ImageSharp only searches below each project).

## Consequences

- Contributors without a key can build, run and test in Debug; only Release
  builds and packaging (ticket 16) need the key.
- A CI run without the key tests Debug, not Release. Release-only problems (e.g.
  trimming/AOT, ticket 14) surface only on runs with the key.
- If Six Labors offers no suitable key, revisit: the fallback is SkiaSharp plus
  small dedicated codecs (superseding ADR-0008).

## Sources

- https://github.com/SixLabors/ImageSharp/discussions/3129
- https://licensing.sixlabors.com/
- GitHub advisories for SixLabors.ImageSharp (e.g. GHSA-j3p4-wp97-rph4, GHSA-jjfr-hcj7-qf5w)
