# CLAUDE.md

DipsyPaint: an MIT-licensed, cross-platform (Windows, macOS, Linux) equivalent of Windows 11 Paint, built on Avalonia + SkiaSharp + ImageSharp (.NET 10).

## Project knowledge

- **Decisions:** `docs/decisions/README.md` indexes the ADRs. Read the ADRs touching the area before changing behaviour. To change a decision, write a new ADR that supersedes or amends the old one and link the two; the old ADR's history stays intact.
- **Vocabulary:** `docs/GLOSSARY.md`. Use its accepted terms (*document*, *layer*, *history step*, *engine*, *shell*, …) in code, tickets and PRs.
- **Spec:** `.scratch/dipsypaint-v1/spec.md`.
- **Tickets:** GitHub issues (`gh issue list`), one per tracer-bullet slice, grouped by milestone (M1–M3) and linked to the issues that block them. Older docs and code say "ticket NN": `.scratch/dipsypaint-v1/issues/README.md` maps those numbers to issues.
- **Parity rule:** when a ticket leaves a tool's behaviour unspecified, match reference Paint.

## Building and testing

`dotnet test` (Debug) works without setup. Release builds need a Six Labors licence key for ImageSharp, via the `SixLaborsLicenseKey` environment variable or a git-ignored `sixlabors.lic` at the repo root (ADR-0020). Test Release only when that key is available. CI picks the configuration automatically.

## Ticket workflow

Each ticket is delivered on its own branch and merged into `main` through a pull request. `main` receives changes only through merged PRs.

1. **Pick** an open issue whose blocking issues are all closed.
2. **Branch** from an up-to-date `main` as `issue/<number>-<slug>`, from the issue's number and title (e.g. `issue/100-image-resolution`).
3. **Build** until every acceptance criterion in the issue holds, then tick its checkboxes in the issue body (`gh issue edit <number> --body-file …`). A ticket is done when its PR merges with all boxes ticked.
4. **Open a PR** to `main` titled `<number>: <issue title>`. The body starts with `Closes #<number>` and lists each acceptance criterion with how it was verified.
5. **Auto-merge:** run `gh pr merge --auto --merge`. The PR merges once the required CI checks pass on Windows, macOS and Linux.

Work outside a ticket (tooling, docs) follows the same flow on a `chore/<slug>` branch.

## Parallel-friendly code layout

Several tickets are often built at once on separate branches. Keep each feature's changes in files it owns, so merges stay clean:

- **UI:** put each feature's controls in its own `UserControl` and mount it in `MainWindow` with a single line.
- **Strings:** add a feature-named `.resx` under `src/DipsyPaint/Resources/` (e.g. `ZoomStrings.resx`). It generates a `DipsyPaint.Resources.<FileName>` class automatically.
- **Tools:** register each tool through the tool registry (`ToolRegistry`) instead of editing the command bar directly. A tool's settings (size, outline, fill…) go in its own options panel via `ToolDescriptor.CreateOptions`; the command bar shows it while the tool is active.
