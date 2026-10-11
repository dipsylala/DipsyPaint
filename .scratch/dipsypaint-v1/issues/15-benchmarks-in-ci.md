# 15: Benchmarks in CI

**What to build:** BenchmarkDotNet suites track the performance targets in CI. Regressions are flagged but don't block merges. A manual checklist covers verification on reference hardware before milestone releases. Spec: AC31 to AC33, ADR-0018.

**Blocked by:** 07 (Basic brush and eraser), 08 (Fill tool)

**Status:** ready-for-agent

- [x] A benchmark measures brush input → composited output at 4K × 20 layers (target ≤ 16 ms)
- [x] A benchmark measures a 4K flood fill (target ≤ 200 ms)
- [x] A benchmark measures cold start (target ≤ 2 s)
- [x] CI runs the suites and flags regressions without failing the build
- [x] A reference-hardware checklist (2020-era 4-core laptop, integrated GPU) is documented for milestone releases

Verified 2026-10-11 by a `main` run of `.github/workflows/benchmarks.yml` with the licence key: suites ran and the 20-layer fill was flagged over target (tracked by ticket 17). See `docs/benchmarks.md`.
