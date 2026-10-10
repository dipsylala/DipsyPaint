# ADR-0004: Build on Avalonia UI + SkiaSharp (.NET 10, C#)

- **Status:** Accepted
- **Date:** 2026-10-10
- **Depends on:** [ADR-0003](0003-target-platforms-desktop.md)

## Context

Need native desktop builds for Windows, macOS and Linux (ADR-0003), fast raster
operations on large layered bitmaps, and ideally a Win11-like look (ADR-0001).
Installed toolchains on the dev machine: .NET 10, Node 24, Java 26, Python 3.13
(no Rust, Flutter, Qt).

Alternatives considered:

| Stack | Rejected because |
| --- | --- |
| Tauri + TypeScript | Rust core not set up; Linux WebKitGTK canvas performance varies; heavy pixel ops need WASM |
| Electron + TypeScript | 100 MB+ bundles, high memory with large canvases |
| Qt (C++ / PySide) | C++ complexity, or Python too slow for per-pixel tools; LGPL obligations |
| Rust + egui/iced + wgpu | Toolchain absent; less mature UI toolkits |

## Decision

Use **Avalonia UI** 12.x (MIT; 12.0.1 current as of 2026-04) for the UI and **SkiaSharp** (MIT) for raster
rendering and pixel operations, in **C# on .NET 10**.

## Consequences

- Avalonia's Fluent theme provides a Win11-like baseline appearance.
- Controls are self-drawn, not OS-native widgets; platform integration
  (file dialogs, clipboard, file associations) goes through Avalonia's
  storage/clipboard APIs plus per-OS shims where those fall short.
- Web stretch goal (ADR-0003) is possible via Avalonia's WebAssembly target;
  not guaranteed and not a v1 commitment.
- Skia's native pixel format (premultiplied RGBA/BGRA 8-bit) is the natural
  default for the document model — decided separately.
- Native SkiaSharp binaries must ship per OS/architecture (win-x64/arm64,
  osx-x64/arm64, linux-x64/arm64).
