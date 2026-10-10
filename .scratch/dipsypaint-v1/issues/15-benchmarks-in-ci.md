# 15: Benchmarks in CI

**What to build:** BenchmarkDotNet suites track the performance targets in CI. Regressions are flagged but don't block merges. A manual checklist covers verification on reference hardware before milestone releases. Spec: AC31 to AC33, ADR-0018.

**Blocked by:** 07 (Basic brush and eraser), 08 (Fill tool)

**Status:** ready-for-agent

- [ ] A benchmark measures brush input → composited output at 4K × 20 layers (target ≤ 16 ms)
- [ ] A benchmark measures a 4K flood fill (target ≤ 200 ms)
- [ ] A benchmark measures cold start (target ≤ 2 s)
- [ ] CI runs the suites and flags regressions without failing the build
- [ ] A reference-hardware checklist (2020-era 4-core laptop, integrated GPU) is documented for milestone releases
