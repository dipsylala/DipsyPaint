# ADR-0018: Performance targets with non-blocking CI benchmark tracking

- **Status:** Accepted (assumptions confirmed 2026-10-10)
- **Amended by:** [ADR-0023](0023-transform-drag-target.md) (transform-drag target)
- **Date:** 2026-10-10
- **Depends on:** [ADR-0005](0005-document-model-flat-layers.md), [ADR-0013](0013-crash-recovery-snapshots.md), [ADR-0016](0016-testing-ui-free-engine.md)

## Context

Scale target is 4K × ~20 layers (ADR-0005). Without explicit targets, layer
compositing and undo memory tend to degrade silently. CI runners are too noisy
for hard performance gates. Options: targets + CI trend tracking; targets with
manual checks only; stricter 120 Hz targets.

## Decision

**Reference hardware:** 2020-era 4-core laptop, integrated GPU.

| Metric | Target |
| --- | --- |
| Brush input → on screen | ≤ 16 ms at 4K × 20 layers |
| Flood fill, 4K canvas | ≤ 200 ms |
| Cold start to usable | ≤ 2 s |
| Undo history memory | Cap = min(1 GB, 25% of RAM); oldest steps dropped |

**Enforcement:** BenchmarkDotNet suites run in CI; regressions are flagged but
do not block merges. Manual verification on reference hardware before each
milestone release (ADR-0009).

## Consequences

- Compositor caches the composite of layers below and above the active layer
  and redraws only dirty rectangles.
- Flood fill uses a scanline algorithm (no recursion).
- Large operations run off the UI thread so it stays responsive (no
  file-opening time target; see Amendments).
- Publish with ReadyToRun or NativeAOT (Avalonia supports AOT; ImageSharp/
  SkiaSharp AOT compatibility to be confirmed by spike).
- Undo cap shares the memory budget of ADR-0013.

## Amendments

- **2026-10-10:** Removed the file-opening target (originally "Open 4K PNG /
  50 MP JPEG ≤ 1 s / ≤ 3 s"). There is no non-functional requirement on
  opening time.
- **2026-10-10 (ticket 14 spike):** Releases are published with **NativeAOT**,
  self-contained, for win/osx/linux × x64/arm64. `PublishAot` is set in
  `DipsyPaint.csproj` for the Release configuration only (Debug and `dotnet test`
  unaffected), so ticket 16 runs
  `dotnet publish src/DipsyPaint/DipsyPaint.csproj -c Release -r <rid>` on a
  native runner per RID (NativeAOT can't cross-compile between OSes). Fallback, if
  a future dependency is AOT-incompatible: self-contained ReadyToRun + trimmed
  (`-p:PublishAot=false -p:PublishReadyToRun=true -p:PublishTrimmed=true --self-contained`).

  Evidence (Avalonia 12.1.4, SkiaSharp 3.119.4, ImageSharp 4.1.3, .NET 10.0.12):
  - NativeAOT publish succeeds on all six RIDs (GitHub runners `windows-latest`,
    `windows-11-arm`, `ubuntu-latest`, `ubuntu-24.04-arm`, `macos-latest`,
    `macos-15-intel`) with **zero** IL2xxx/IL3xxx warnings; trimmed publishes
    (ILLink) also warn nothing. The published app opens its window, renders,
    and round-trips PNG and JPEG through ImageSharp on every RID (compiled XAML
    bindings and `.resx` resources are on that path). CI has no Six Labors key,
    so these builds used `-c Debug -p:Optimize=true` (ILC still runs with `-O`);
    a Release-configuration AOT build was not run anywhere, because this machine
    has no MSVC linker. Release build, Release tests and trimmed Release publish
    pass locally.
  - Cold start, from process start to the main window's first rendered frame
    (`DIPSYPAINT_STARTUP_PROBE`, below), median of warm runs in ms; first launch
    after publish in brackets:

    | RID (where run) | NativeAOT | R2R self-contained | R2R + trimmed | Framework-dependent |
    | --- | --- | --- | --- | --- |
    | win-x64 (CI) | 139 (351) | 405 (599) | 414 (566) | 814 (909) |
    | win-arm64 (CI) | 173 (485) | 615 (977) | 595 (1030) | 1255 (1468) |
    | linux-x64 (CI, Xvfb) | 128 (597) | 423 (434) | 423 (437) | 832 (859) |
    | linux-arm64 (CI, Xvfb) | 107 (2980) | 501 (486) | 529 (530) | 1037 (1075) |
    | osx-arm64 (CI) | 320 (5240) | 779 (2282) | 572 (3441) | 1224 (1317) |
    | osx-x64 (CI) | 381 (1521) | 779 (1256) | 1049 (1394) | 1572 (2017) |
    | win-x64, CI binaries on a Ryzen 7 3700X desktop, 11 runs | 474 | 876 | 797 | 1381 |
    | win-x64, local Release builds, same desktop, 10 runs | n/a | 771 | 789 | 1419 |

    All modes meet ≤ 2 s on these machines; NativeAOT starts 2–4× faster than
    ReadyToRun. None is the reference laptop; check it before M1 (ticket 15).
  - Published size, MB, excluding symbol files: NativeAOT 41–45;
    R2R self-contained 203–251; R2R + trimmed 72–79; framework-dependent 24–30
    (plus a separately installed .NET runtime).
  - Costs: NativeAOT needs the platform toolchain on each build runner (MSVC
    C++ tools on Windows, clang + zlib on Linux, Xcode on macOS), and new code
    and dependencies must stay AOT-compatible (Release builds now run the
    trim/AOT analyzers; `TreatWarningsAsErrors` turns findings into errors). A
    Linux AOT binary needs at least the glibc of the machine that built it, so
    ticket 16 should build Linux on the oldest distro it supports (e.g. an older
    runner or the .NET cross-build container), not `ubuntu-latest`.

  **Startup probe:** with `DIPSYPAINT_STARTUP_PROBE=<file>` set, the app appends
  `startup_ms=<N> codecs=ok|failed:<reason>` to the file after its first frame,
  then exits. Ticket 15 can use it for the cold-start benchmark, and ticket 16
  as a post-publish smoke test.
