# AGENTS.md

DipsyPaint: an MIT-licensed, cross-platform (Windows, macOS, Linux) equivalent of Windows 11 Paint, built on Avalonia + SkiaSharp + ImageSharp (.NET 10).

## Project knowledge

- **Decisions:** `docs/decisions/README.md` indexes the ADRs. Read the ADRs touching the area before changing behaviour. To change a decision, write a new ADR that supersedes or amends the old one and link the two; the old ADR's history stays intact.
- **Vocabulary:** `docs/GLOSSARY.md`. Use its accepted terms (*document*, *layer*, *history step*, *engine*, *shell*, …) in code, tickets and PRs.
- **Spec:** `.scratch/dipsypaint-v1/spec.md`.
- **Tickets:** `.scratch/dipsypaint-v1/issues/NN-<slug>.md`, one per tracer-bullet slice, each listing the tickets that block it.
- **Parity rule:** when a ticket leaves a tool's behaviour unspecified, match reference Paint.

## Ticket workflow

Each ticket is delivered on its own branch and merged into `main` through a pull request. `main` receives changes only through merged PRs.

1. **Pick** a ticket whose blocking tickets are all merged into `main`.
2. **Branch** from an up-to-date `main` as `issue/NN-<slug>`, matching the ticket filename (e.g. `issue/01-walking-skeleton`).
3. **Build** until every acceptance criterion in the ticket holds, then tick its checkboxes in the ticket file on the same branch. A ticket is done when its PR merges with all boxes ticked.
4. **Open a PR** to `main` titled `NN: <ticket title>`. The body links the ticket file and lists each acceptance criterion with how it was verified.
5. **Merge** once CI is green on Windows, macOS and Linux.

Work outside a ticket (tooling, docs) follows the same flow on a `chore/<slug>` branch.
