# Golden images

A golden test runs a scripted tool sequence through the engine (`DocumentEditor.Apply`) and
compares the composited document with a committed PNG in `Images/` (ADR-0016).

- **Tolerance:** each channel of each pixel may differ by at most `GoldenImage.DefaultTolerance`
  (currently 2 of 255, premultiplied). No pixel may exceed it. A test can pass its own tolerance;
  loosen it only for a real cross-platform rendering difference, and say why in the test.
- **On failure** the harness writes `<name>.actual.png`, `<name>.expected.png` and
  `<name>.diff.png` (mismatches red, matching pixels faded) to `TestResults/golden-diffs/` at
  the repository root. CI uploads that folder as the `golden-diffs-<os>` artefact.

## Adding or updating a golden image

1. Write or change the test, then run it with `DIPSYPAINT_UPDATE_GOLDENS=1`. This writes the
   current output to `Images/<name>.png` instead of comparing:

   ```sh
   DIPSYPAINT_UPDATE_GOLDENS=1 dotnet test tests/DipsyPaint.Engine.Tests --filter "FullyQualifiedName~Golden"
   ```

   PowerShell: `$env:DIPSYPAINT_UPDATE_GOLDENS='1'; dotnet test ...; Remove-Item Env:DIPSYPAINT_UPDATE_GOLDENS`

2. Inspect every new or changed PNG by eye (zoom in; they are small). Check it against
   reference Paint's behaviour.
3. Run the tests again without the variable; they must pass.
4. Commit the PNGs with the change that caused them, and say in the PR why they changed.

## Text golden images

Text is rasterised by the shell (`DipsyPaint.Text.TextRenderer`), so its golden tests live in
`tests/DipsyPaint.Tests/Golden/` with their own `Images/`. They use Inter, a font bundled with the
tests, never a system font. Each OS's font scaler anti-aliases differently, so `TextGoldenImage`
accepts a pixel when a pixel within one pixel of it in the other image is within 64 per channel,
and allows 0.2% outliers. Update them with the same variable, on the `DipsyPaint.Tests` project.
