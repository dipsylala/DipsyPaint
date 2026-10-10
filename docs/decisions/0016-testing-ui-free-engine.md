# ADR-0016: UI-free engine; unit, golden-image and headless UI tests on 3-OS CI

- **Status:** Accepted (assumptions confirmed 2026-10-10)
- **Date:** 2026-10-10
- **Depends on:** [ADR-0003](0003-target-platforms-desktop.md), [ADR-0004](0004-stack-avalonia-skiasharp.md), [ADR-0005](0005-document-model-flat-layers.md)

## Context

Paint-tool correctness is largely "are the pixels right". Skia output can
differ slightly across OS/CPU (SIMD paths). Platform-specific behaviour
(clipboard, file dialogs, native Skia binaries) differs per OS. Options:
full (unit + golden + headless UI, 3-OS CI); lean (unit + golden, Linux CI);
unit only.

## Decision

- **Architecture:** the engine — document, layers, tools, history,
  codecs — is a **UI-free .NET library** with no Avalonia dependency. The
  Avalonia app is a thin shell over it.
- **Unit tests** on the engine.
- **Golden-image tests:** apply a scripted tool sequence, compare against a
  reference PNG with a **per-pixel tolerance** (not exact match); failures
  emit a diff image as a CI artefact.
- **UI smoke tests** with Avalonia.Headless.
- **Malicious-sample tests** per ADR-0014.
- **CI:** GitHub Actions matrix — Windows, macOS, Linux — on every PR.

## Consequences

- Tools must be driveable programmatically (input events as data), which also
  benefits recording/replay for bug reports.
- Golden images need a documented update workflow; tolerance thresholds need
  tuning to avoid flakiness.
- macOS runners are slower; CI time is the main cost (free for public repos).
