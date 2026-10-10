# ADR-0010: Printing is not supported

- **Status:** Accepted
- **Date:** 2026-10-10
- **Amends:** [ADR-0009](0009-milestone-phasing.md) (removes printing from M3)

## Context

Reference Paint prints. Avalonia (12.x) has no first-party printing API;
third-party options are alpha (PrintingTools 0.1.0-alpha.1) or unproven on
Avalonia 12 (Avae.Printables). Options considered: in-app page setup → Skia
PDF → OS print handoff; native per-OS print shims; third-party library; drop.

## Decision

The app **does not print**. Users export (e.g. PNG) and print from another app.

## Consequences

- Documented parity gap; no print menu item.
- No per-OS print code, no print-related dependencies.
- If revisited, the cheapest path is Skia PDF generation + OS print handoff.

## Sources

- https://www.nuget.org/packages/PrintingTools
- https://packages.nuget.org/packages/Avae.Printables/
- https://www.nuget.org/profiles/avaloniaui
