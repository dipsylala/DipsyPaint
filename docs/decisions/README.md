# Decision Records

| ADR | Title | Status |
| --- | --- | --- |
| [0001](0001-feature-baseline-modern-paint.md) | Feature baseline is modern Windows 11 Paint | Accepted |
| [0002](0002-no-ai-features.md) | AI features are a permanent non-goal | Accepted |
| [0003](0003-target-platforms-desktop.md) | v1 targets desktop Windows, macOS and Linux | Accepted |
| [0004](0004-stack-avalonia-skiasharp.md) | Build on Avalonia UI + SkiaSharp (.NET 10, C#) | Accepted |
| [0005](0005-document-model-flat-layers.md) | Document model — flat 8-bit RGBA layers, region-diff undo | Accepted |
| [0006](0006-project-format-openraster.md) | OpenRaster (.ora) is the native project format | Accepted |
| [0007](0007-licence-mit.md) | The project is open source under the MIT licence | Accepted |
| [0008](0008-codecs-imagesharp.md) | All file I/O through ImageSharp; Skia for rendering only | Accepted; format list superseded by 0019; amended by 0021 |
| [0009](0009-milestone-phasing.md) | Deliver parity in three milestones (M1/M2/M3) | Accepted |
| [0010](0010-no-printing.md) | Printing is not supported | Accepted |
| [0011](0011-additions-beyond-parity.md) | Features beyond reference Paint are allowed, case by case | Accepted |
| [0012](0012-ui-familiar-own-identity.md) | Paint-familiar UI with its own visual identity | Accepted |
| [0013](0013-crash-recovery-snapshots.md) | Periodic recovery snapshots and memory-budget checks | Accepted |
| [0014](0014-untrusted-file-handling.md) | Untrusted-file handling — input limits and malicious-sample tests | Accepted |
| [0015](0015-distribution-unsigned-first.md) | Distribute unsigned via GitHub Releases first; add signing later | Accepted |
| [0016](0016-testing-ui-free-engine.md) | UI-free engine; unit, golden-image and headless UI tests on 3-OS CI | Accepted |
| [0017](0017-no-telemetry.md) | No telemetry; local crash logs and user-submitted reports | Accepted |
| [0018](0018-performance-targets.md) | Performance targets with non-blocking CI benchmark tracking | Accepted; amended by 0023 |
| [0019](0019-file-formats-match-paint.md) | Flat file formats match reference Paint, minus HEIC | Accepted |
| [0020](0020-imagesharp-licence-key.md) | ImageSharp 4 licence key: obtain one; CI falls back to Debug without it | Accepted |
| [0021](0021-clipboard-image-formats.md) | Clipboard image formats per OS; Windows output through Avalonia's bitmap | Accepted |
| [0022](0022-command-bar-layout.md) | Command bar wraps; command groups dock to window edges; layout is saved per user | Accepted |
| [0023](0023-transform-drag-target.md) | Transform drags have the brush's 16 ms target; previews may be cheaper than the result | Accepted |
| [0024](0024-image-resolution.md) | Documents carry a resolution (DPI) that files keep; sizes stay in pixels | Accepted |
| [0025](0025-selection-tools.md) | Selections are made and moved with separate tools | Accepted |

## Open Questions

None.

## Deferred with defaults

Not discussed in depth. These defaults apply until revisited.

| Area | Default | Impact if changed later |
| --- | --- | --- |
| Localisation | English only at launch; all UI strings in `.resx` resources from M1 | Adding languages is cheap; retrofitting externalised strings would not be |
| Accessibility | Every tool/command keyboard-reachable; Avalonia automation names on controls; no formal WCAG commitment | Formal compliance later may require UI rework |
| Min OS / CPU | Whatever .NET 10 and Avalonia 12 support; x64 + arm64 on all three OSes | Dropping is easy; adding is CI work |
| Pen pressure | Supported where Avalonia exposes it; not required (ADR-0003) | None |
| Code signing / notarisation | Deferred to a future sprint; releases unsigned (ADR-0015) | Until done: SmartScreen/Gatekeeper friction; adding it needs CI secrets for certificates |

## Spikes needed (unverified assumptions)

- ~~NativeAOT compatibility of ImageSharp + SkiaSharp + Avalonia 12: ADR-0018~~
  Done (ticket 14): compatible on all six RIDs with no trim/AOT warnings; releases
  use NativeAOT (ADR-0018, amendment 2026-10-10)
- SignPath Foundation eligibility given ImageSharp's Split License: ADR-0015
