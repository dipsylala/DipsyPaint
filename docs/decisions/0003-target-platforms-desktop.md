# ADR-0003: v1 targets desktop Windows, macOS and Linux

- **Status:** Accepted
- **Date:** 2026-10-10

## Context

Options: desktop only; desktop + web; desktop + mobile/tablet; web only.
Web adds constraints (File System Access API is Chromium-only, limited image
clipboard, memory caps). Mobile/tablet requires a touch/pen-first UI and
app-store distribution — effectively a second product shape.

## Decision

v1 supports **Windows, macOS and Linux desktop**. Input priority: mouse and
keyboard first; pen/tablet pressure is a bonus, not a requirement.

A web build is a **stretch goal**, pursued only if the chosen stack makes it
near-free. Mobile/tablet is out of scope for v1.

## Consequences

- Stack choice must produce native-feeling desktop builds for all three OSes.
- Stacks offering a cheap web target get a tiebreaker advantage, not a requirement.
- Platform integration needed: native file dialogs, OS clipboard (image data),
  file associations, HiDPI/mixed-DPI displays.
