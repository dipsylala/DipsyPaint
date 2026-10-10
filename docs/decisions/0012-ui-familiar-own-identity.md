# ADR-0012: Paint-familiar UI with its own visual identity

- **Status:** Accepted (product name: **DipsyPaint**, 2026-10-10)
- **Date:** 2026-10-10
- **Depends on:** [ADR-0004](0004-stack-avalonia-skiasharp.md)

## Context

Options: close look-alike of reference Paint; Paint-familiar layout with own
identity; fully original design. Copying Microsoft's bespoke tool icons, app
icon or "Paint" branding carries trade-dress/trademark risk. Microsoft's
Fluent UI System Icons are MIT-licensed and safe to use.

## Decision

- **Layout and mental model match reference Paint:** tool/command bar across
  the top, canvas centre, layers panel right, status bar bottom; same tool
  grouping and keyboard shortcuts where practical.
- **Visuals:** Avalonia Fluent theme, light and dark mode, Fluent UI System
  Icons (MIT).
- **Own identity:** product name **DipsyPaint**; own app icon and accent colour. Never use
  "Microsoft", "MS Paint" or Microsoft's Paint icon in name, UI or marketing.

## Consequences

- Reference Paint users can transfer muscle memory.
- Icon set is consistent and licence-clean; bespoke icons needed only for
  tools Fluent icons lack (e.g. specific brushes).
- Product name: **DipsyPaint**. Avoid styling that evokes the Teletubbies character "Dipsy" (third-party trademark).

## Sources

- https://github.com/microsoft/fluentui-system-icons (MIT)
