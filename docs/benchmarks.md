# Benchmarks

The benchmarks track the performance targets of [ADR-0018](decisions/0018-performance-targets.md)
(spec AC31 to AC33) and [ADR-0023](decisions/0023-transform-drag-target.md) (transform drags).
CI flags results that miss a target or regress, but never fails because of them. Before each
milestone release, someone runs them on reference hardware (see the checklist below).

## What is measured

| Benchmark | Measures | Target |
| --- | --- | --- |
| `Brush.StrokeEvent [Size=5\|100]` | One brush stroke on a 3840×2160 document with 20 layers, drawing on the top layer: pointer down, 20 moves 10 px apart, pointer up, all through `DocumentEditor.Apply`. Covers painting, the history step and recompositing the dirty rectangle. Reported as the mean cost per pointer event, at the shell's default brush size (5) and the largest (100). | ≤ 16 ms (AC31) |
| `ArtisticBrush.StrokeEvent [Brush=calligraphy-brush\|calligraphy-pen\|crayon\|marker\|natural-pencil]` | The `Brush.StrokeEvent` stroke at size 100 with each artistic brush of ticket 28. | ≤ 16 ms (AC31) |
| `Transform.RotateEvent` | A drag of the rotate handle of a 3840×2160 selection (the whole canvas of a 20-layer document, lifted off the top layer): pointer down, 20 moves turning it 2° each, pointer up, all through `DocumentEditor.Apply`. Covers the preview shown while dragging, the exact turn on release and recompositing. Drags alternate between 10°–50° and back, so each does the same work. Reported as the mean cost per pointer event. | ≤ 16 ms (ADR-0023) |
| `Transform.StretchEvent` | The same selection stretched by its bottom-right handle: pointer down, 20 moves 10 px apart, pointer up, alternately in and back out. Mean cost per pointer event. | ≤ 16 ms (ADR-0023) |
| `Fill.FloodFill [Layers=1\|20]` | A flood fill of a whole 3840×2160 layer through the editor: region search, write, history step and recompositing. Measured on a one-layer document and with 20 layers. | ≤ 200 ms (AC32) |
| `ColdStart.FirstFrame` | Time from process start to the main window's first rendered frame. Median of 10 launches. | ≤ 2 s (AC33) |

Not measured: copying the composite to the screen (the shell's `DocumentCanvas`) and the GPU.
The cold-start runs follow one another, so the operating system's file cache is warm after the
first. The log lists every run, so the first, coldest one is visible.

The engine benchmarks use [BenchmarkDotNet](https://benchmarkdotnet.org/) (`benchmarks/DipsyPaint.Benchmarks`).
Cold start does not fit BenchmarkDotNet, so the same project has a `coldstart` command. It launches
the app with `DIPSYPAINT_STARTUP_PROBE` set to a temporary file. With that variable set, the app's
startup probe (`src/DipsyPaint/StartupProbe.cs`, shared with the publish-mode spike, ticket 14)
writes the time since process start to the file after the main window's first frame, then exits.
Without it, the probe does nothing.

## Running locally

Benchmarks need a Release build, and a Release build needs the Six Labors licence key (ADR-0020):
set `SixLaborsLicenseKey`, put `sixlabors.lic` at the repository root, or set the
`SixLaborsLicenseFile` environment variable to the path of a `sixlabors.lic`. Close other
programs while you measure.

```sh
# Engine benchmarks (BenchmarkDotNet options work too, e.g. --filter '*Fill*' or --job short)
dotnet run -c Release --project benchmarks/DipsyPaint.Benchmarks -- --filter '*'

# Cold start: build the app, then launch it 10 times
dotnet build src/DipsyPaint -c Release
dotnet run -c Release --project benchmarks/DipsyPaint.Benchmarks -- coldstart src/DipsyPaint/bin/Release/net10.0/DipsyPaint --runs 10
```

On Windows the app is `DipsyPaint.exe`. On Linux without a display, wrap the cold-start command
in `xvfb-run -a`.

The engine run writes BenchmarkDotNet's reports and a `measurements.json` to
`BenchmarkDotNet.Artifacts/results/`; `coldstart --out <file>` writes the same format. The
`report` command compares such files with the targets and with an earlier run:

```sh
dotnet run -c Release --project benchmarks/DipsyPaint.Benchmarks -- report --previous old.json --out results.json \
  BenchmarkDotNet.Artifacts/results/measurements.json coldstart.json
```

## CI

`.github/workflows/benchmarks.yml` runs on every push to `main` and on demand (*Run workflow*),
on Ubuntu only. It runs all the benchmarks, writes a results table to the job summary and uploads
everything as the `benchmarks` artifact. It prints a warning annotation when a result:

- misses its target, or
- is more than 25% slower than the previous `main` run (cached between runs).

The workflow never fails because of a result. It needs the `SIXLABORS_LICENSE_KEY` secret; when the
secret is unavailable, it skips the benchmarks with a notice.

Shared CI runners are not reference hardware and their timings vary from run to run, so use
CI results to spot trends. Use the checklist below to decide whether a target is met.

## Reference-hardware checklist (each milestone release)

Reference hardware: a 2020-era laptop with 4 cores and an integrated GPU (ADR-0018), for example
an Intel Core i5-10210U or an AMD Ryzen 5 4500U.

- [ ] Plug in the laptop, choose the balanced or performance power plan, and close other programs.
- [ ] Record the CPU, RAM, GPU, OS version and the commit being measured.
- [ ] Engine benchmarks (default job, no `--job short`): `Brush.StrokeEvent` ≤ 16 ms per event at both sizes, and `ArtisticBrush.StrokeEvent` for every brush; `Transform.RotateEvent` and `Transform.StretchEvent` ≤ 16 ms per event; `Fill.FloodFill` ≤ 200 ms.
- [ ] Cold start with the release package (the publish mode chosen in ticket 14), `--runs 10`: median ≤ 2 s. Note the first run after a reboot as well.
- [ ] By hand on a 3840×2160 document with 20 layers: brush strokes keep up with the pointer, with no visible lag; so do turning and stretching a selection of the whole canvas by its handles.
- [ ] By hand: a flood fill of the whole canvas feels instant, and the window stays responsive during large operations (AC34).
- [ ] Record the results in the release notes or the milestone issue, and open a ticket for each missed target.
