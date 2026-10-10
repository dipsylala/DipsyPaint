# ADR-0002: AI features are a permanent non-goal

- **Status:** Accepted
- **Date:** 2026-10-10
- **Amends:** [ADR-0001](0001-feature-baseline-modern-paint.md) (resolves its pending AI scope)

## Context

Reference Paint includes AI tools: Generative Erase, Generative Fill, Image
Creator/Cocreator, and ML-based background removal. Options considered:

- No AI in v1, add later behind an extension seam
- Local light AI (background removal, erase via small ONNX models)
- Full AI via cloud or bring-your-own API key
- Never ship AI

## Decision

The project will **never** ship AI/ML features. This is an explicit, permanent
deviation from reference Paint parity.

ML-based background removal is excluded too. A non-ML magic wand substitutes
for it ([ADR-0011](0011-additions-beyond-parity.md)).

## Consequences

- No ML runtime, model downloads, model licensing, GPU requirements, API keys,
  network dependence, or per-use cost.
- No need to architect an AI provider seam.
- "Parity" in this project means *reference Paint minus AI features*.
- Users wanting AI features must use other tools; this is a positioning choice.
