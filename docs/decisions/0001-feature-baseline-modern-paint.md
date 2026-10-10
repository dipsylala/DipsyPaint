# ADR-0001: Feature baseline is modern Windows 11 Paint

- **Status:** Accepted (AI scope resolved by [ADR-0002](0002-no-ai-features.md))
- **Date:** 2026-10-10

## Context

"Cross-platform equivalent to Microsoft Paint" has several readings: classic
Win7-era Paint (single opaque bitmap), retro 95/98 Paint, or current Windows 11
Paint. Current Win11 Paint (stable 11.2605.x, mid-2026) includes, per Microsoft
and press coverage:

- Layers and transparency; `.paint` project files preserving layers (announced Sep 2025)
- Opacity slider for pencil/brush strokes; adjustable eraser transparency (Jun 2026)
- Freeform rotate of shapes, text and selections (Feb 2026)
- Background removal
- AI tools: Generative Erase, Generative Fill, Image Creator / Cocreator
- Classic tools: pencil, brushes, fill, text, shapes, eraser, colour picker,
  magnifier, select/crop/resize/skew/rotate/flip

## Decision

v1 targets feature parity with **modern Windows 11 Paint**, not classic Paint.

Alternatives rejected:
- *Classic + transparency* — smaller, but not what the user wants.
- *Pure classic* / *retro clone* — too narrow; nostalgia-driven.

## Consequences

- The document model must support **layers** and **per-pixel alpha** from day one.
- A native project format (equivalent to `.paint`) is needed alongside flat exports.
- Scope is substantially larger than a classic clone; phasing will be required.
- AI features carry model/runtime/cost/privacy implications — decided separately.

## Sources

- https://learn.microsoft.com/en-us/windows-insider/release-notes/apps/paint
- https://www.windowscentral.com/microsoft/windows-11/microsoft-announces-major-update-to-windows-11-paint-that-adds-photoshop-like-project-files
- https://www.neowin.net/news/a-highly-requested-feature-is-finally-coming-to-paint-in-windows-11/
- https://www.microsoft.com/en-us/windows/paint
