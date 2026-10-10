# ADR-0014: Untrusted-file handling — input limits and malicious-sample tests

- **Status:** Accepted
- **Date:** 2026-10-10
- **Depends on:** [ADR-0005](0005-document-model-flat-layers.md), [ADR-0006](0006-project-format-openraster.md), [ADR-0008](0008-codecs-imagesharp.md)

## Context

Users open images from untrusted sources. ImageSharp is fully managed, so the
main risks are denial of service (crash, hang, memory exhaustion) rather than
code execution. The `.ora` reader (ZIP + XML + PNG) is our own code. Options:
defaults only; defaults + fuzzing (SharpFuzz) in CI; plus sandboxed
out-of-process decoding.

## Decision

Apply the following **defaults**, verified by **regression tests using known-
malicious sample files**. No fuzzing, no process sandbox.

- **Decompression bombs:** identify dimensions from the header before decoding;
  reject > 16384 px per side or beyond the memory budget (ADR-0013).
- **`.ora` ZIP:** reject entry names with absolute paths, `..`, or drive
  letters (zip-slip); cap entry count and total uncompressed size; reject
  layer PNGs whose dimensions exceed the canvas.
- **`stack.xml`:** DTD processing disabled (XXE), size cap.
- **Decode failures** surface as a user-facing "could not open" error; never
  crash the app or leave a half-open document.

## Consequences

- Covers known attack classes cheaply; unknown parser bugs in our `.ora`
  reader are found only by tests and reports, not fuzzing.
- Revisit (add fuzzing) if a parser bug is reported in the wild.
