# ADR-0015: Distribute unsigned via GitHub Releases first; add signing later

- **Status:** Accepted (Velopack confirmed 2026-10-10; signing deferred)
- **Date:** 2026-10-10
- **Depends on:** [ADR-0003](0003-target-platforms-desktop.md), [ADR-0007](0007-licence-mit.md)

## Context

Unsigned apps trigger Windows SmartScreen warnings and are blocked by macOS
Gatekeeper unless the user overrides it in System Settings. Signing costs:
Apple Developer Program $99/yr (required for notarisation). Windows options:

- ~~Azure Trusted Signing~~: maintainer is **not eligible** (confirmed 2026-10-10).
- **SignPath Foundation**: free for OSS. Requires an OSI-approved licence
  "without commercial dual-licensing for all components", fully automated
  builds from a public repo, and SignPath as named publisher. **Risk:**
  ImageSharp's Split License (ADR-0008) offers a commercial path and may
  disqualify the project. Needs confirming with SignPath.
- **Microsoft Store**: free individual accounts; Store signs the MSIX.
- **Purchased OV certificate**: annual cost; key must sit in an HSM or cloud KMS.

Options considered for this ADR: GitHub + Velopack, signed; store-first;
unsigned first, sign later.

## Decision

- **Initial releases are unsigned**, published on **GitHub Releases**.
- Packaging and auto-update via **Velopack** (MIT) on all three OSes from the
  start, so signing can be added without changing the update path.
- Linux: AppImage from GitHub Releases; Flathub optional later.
- **Signing/notarisation:** explicitly **deferred to a future sprint** (2026-10-10);
  no trigger set. Candidates: M1 public release, or
  first user-reported install friction. Until then, README documents the
  SmartScreen and Gatekeeper override steps.

## Consequences

- Zero signing cost early; materially worse first-run UX, especially macOS.
- Lower user trust; some corporate machines will refuse unsigned binaries.
- Update integrity relies on GitHub account security plus Velopack package
  hashes over HTTPS — protect the release account with 2FA and restricted
  release permissions.
- Adding signing later requires CI secrets management for certificates.

## Sources

- https://signpath.org/terms
- https://about.signpath.io/product/open-source
