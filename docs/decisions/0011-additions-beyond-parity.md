# ADR-0011: Features beyond reference Paint are allowed, case by case

- **Status:** Accepted (assumptions confirmed 2026-10-10)
- **Date:** 2026-10-10
- **Related:** [ADR-0001](0001-feature-baseline-modern-paint.md), [ADR-0002](0002-no-ai-features.md), [ADR-0009](0009-milestone-phasing.md)

## Context

ADR-0002 excludes ML background removal, leaving a gap. Options: strict parity;
parity plus an explicit list of non-ML gap-fillers; parity plus any useful
feature (Pinta-style).

## Decision

Features reference Paint lacks **may be added, judged case by case**.
AI/ML features remain excluded (ADR-0002).

First addition: **magic wand** (colour-tolerance flood selection), in **M2**,
as the non-ML substitute for background removal.

Guardrails:
- Additions are tracked in a backlog, separate from milestone parity scope.
- Additions **never block a milestone's exit**; parity items take priority.

## Consequences

- Scope is open-ended; the backlog and guardrails are the only creep control.
- Additions must still respect the document model (ADR-0005) and project
  format (ADR-0006) — anything not representable in OpenRaster needs an
  extension and degrades in other apps.
- Resolves the open question on background removal: excluded (ML); magic
  wand substitutes.
