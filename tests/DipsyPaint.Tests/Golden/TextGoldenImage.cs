using System.Runtime.CompilerServices;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tests.Golden;

namespace DipsyPaint.Tests.Golden;

/// <summary>
/// Golden-image comparison for rendered text. Text is rasterised by each OS's font scaler
/// (DirectWrite, CoreText, FreeType), whose anti-aliased edges differ even with the same bundled
/// font, so this is looser than <see cref="GoldenImage"/>'s per-pixel tolerance (ADR-0016): a pixel
/// matches when a pixel within one pixel of it in the other image is within <see cref="Tolerance"/>
/// per channel, both ways round, and up to <see cref="MaxOutlierFraction"/> of pixels may still differ.
/// A wrong font, weight, style, size, decoration, colour or a shift of two pixels or more still fails.
/// Images live in <c>Images/</c> beside this file; update them as for the engine's golden images
/// (tests/DipsyPaint.Engine.Tests/Golden/README.md), with the same environment variable.
/// </summary>
public static class TextGoldenImage
{
    public const int Tolerance = 64;
    public const double MaxOutlierFraction = 0.002;

    public static string ImagesDirectory => Path.Combine(SourceDirectory(), "Images");

    public static void AssertMatches(string name, Compositor compositor)
    {
        var goldenPath = Path.Combine(ImagesDirectory, name + ".png");
        var (actual, width, height) = (compositor.Pixels.ToArray(), compositor.Width, compositor.Height);
        if (Environment.GetEnvironmentVariable(GoldenImage.UpdateVariable) == "1")
        {
            GoldenImage.Save(goldenPath, actual, width, height);
            return;
        }

        var diffDirectory = GoldenImage.DiffDirectory;
        if (!File.Exists(goldenPath))
        {
            GoldenImage.Save(Path.Combine(diffDirectory, name + ".actual.png"), actual, width, height);
            Assert.Fail($"Golden image '{name}': no golden image at {goldenPath}. Output written to {diffDirectory}.");
        }

        var expected = GoldenImage.Load(goldenPath, out var expectedWidth, out var expectedHeight);
        Assert.True((expectedWidth, expectedHeight) == (width, height),
            $"Golden image '{name}': size {width}×{height} differs from golden {expectedWidth}×{expectedHeight}.");

        var diff = new Rgba[actual.Length];
        var outliers = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = y * width + x;
                var matches = HasNear(expected, width, height, x, y, actual[i]) && HasNear(actual, width, height, x, y, expected[i]);
                diff[i] = matches ? expected[i].WithOpacity(0.25f).Over(Rgba.White) : new Rgba(255, 0, 0, 255);
                outliers += matches ? 0 : 1;
            }
        }

        if (outliers > MaxOutlierFraction * actual.Length)
        {
            GoldenImage.Save(Path.Combine(diffDirectory, name + ".actual.png"), actual, width, height);
            GoldenImage.Save(Path.Combine(diffDirectory, name + ".expected.png"), expected, width, height);
            GoldenImage.Save(Path.Combine(diffDirectory, name + ".diff.png"), diff, width, height);
            Assert.Fail($"Golden image '{name}': {outliers} pixel(s) have no match within one pixel (tolerance {Tolerance}). "
                        + $"Images written to {diffDirectory}. If the new output is correct, re-run with {GoldenImage.UpdateVariable}=1.");
        }
    }

    // Whether a pixel of `image` within one pixel of (x, y) is within the tolerance of `pixel`.
    private static bool HasNear(Rgba[] image, int width, int height, int x, int y, Rgba pixel)
    {
        for (var ny = Math.Max(0, y - 1); ny <= Math.Min(height - 1, y + 1); ny++)
        {
            for (var nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
            {
                var other = image[ny * width + nx];
                if (Math.Abs(other.R - pixel.R) <= Tolerance && Math.Abs(other.G - pixel.G) <= Tolerance
                    && Math.Abs(other.B - pixel.B) <= Tolerance && Math.Abs(other.A - pixel.A) <= Tolerance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
