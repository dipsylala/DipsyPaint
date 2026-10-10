using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace DipsyPaint.Engine.Tests.Golden;

/// <summary>
/// Compares rendered pixels with committed golden images in <c>Images/</c>.
/// See README.md beside this file for the update workflow.
/// </summary>
public static class GoldenImage
{
    /// <summary>Set to <c>1</c> to overwrite golden images with the current output instead of comparing.</summary>
    public const string UpdateVariable = "DIPSYPAINT_UPDATE_GOLDENS";

    /// <summary>
    /// Largest allowed difference per channel (0–255, premultiplied). Deliberately tight;
    /// loosen only if a platform shows legitimate rendering differences (ADR-0016).
    /// </summary>
    public const int DefaultTolerance = 2;

    public static string ImagesDirectory => Path.Combine(SourceDirectory(), "Images");

    /// <summary>Where failing comparisons write images; CI uploads this folder as an artefact.</summary>
    public static string DiffDirectory =>
        Path.GetFullPath(Path.Combine(SourceDirectory(), "..", "..", "..", "TestResults", "golden-diffs"));

    public static void AssertMatches(string name, Compositor compositor, int tolerance = DefaultTolerance)
    {
        var goldenPath = Path.Combine(ImagesDirectory, name + ".png");
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Save(goldenPath, compositor.Pixels, compositor.Width, compositor.Height);
            return;
        }

        var failure = Compare(name, goldenPath, compositor.Pixels, compositor.Width, compositor.Height, tolerance, DiffDirectory);
        if (failure is not null)
        {
            Assert.Fail(failure);
        }
    }

    /// <summary>
    /// Returns null if every channel of every pixel is within <paramref name="tolerance"/> of the golden image.
    /// Otherwise writes <c>name.actual.png</c>, and when a golden exists <c>name.expected.png</c> and
    /// <c>name.diff.png</c> (mismatches red, matches faded), to <paramref name="diffDirectory"/> and
    /// returns a failure message.
    /// </summary>
    public static string? Compare(
        string name, string goldenPath, ReadOnlySpan<Rgba> actual, int width, int height, int tolerance, string diffDirectory)
    {
        string Fail(string reason)
        {
            return $"Golden image '{name}': {reason}. Images written to {diffDirectory}. "
                   + $"If the new output is correct, re-run with {UpdateVariable}=1 (see tests/DipsyPaint.Engine.Tests/Golden/README.md).";
        }

        if (!File.Exists(goldenPath))
        {
            Save(Path.Combine(diffDirectory, name + ".actual.png"), actual, width, height);
            return Fail($"no golden image at {goldenPath}");
        }

        var expected = Load(goldenPath, out var expectedWidth, out var expectedHeight);
        if (expectedWidth != width || expectedHeight != height)
        {
            Save(Path.Combine(diffDirectory, name + ".actual.png"), actual, width, height);
            return Fail($"size {width}×{height} differs from golden {expectedWidth}×{expectedHeight}");
        }

        var diff = new Rgba[actual.Length];
        int mismatches = 0, largest = 0;
        for (var i = 0; i < actual.Length; i++)
        {
            var difference = MaxChannelDifference(actual[i], expected[i]);
            largest = Math.Max(largest, difference);
            if (difference > tolerance)
            {
                mismatches++;
                diff[i] = new Rgba(255, 0, 0, 255);
            }
            else
            {
                diff[i] = expected[i].WithOpacity(0.25f).Over(Rgba.White);
            }
        }

        if (mismatches == 0)
        {
            return null;
        }

        Save(Path.Combine(diffDirectory, name + ".actual.png"), actual, width, height);
        Save(Path.Combine(diffDirectory, name + ".expected.png"), expected, width, height);
        Save(Path.Combine(diffDirectory, name + ".diff.png"), diff, width, height);
        return Fail($"{mismatches} pixel(s) differ by more than {tolerance} (largest channel difference {largest})");
    }

    public static void Save(string path, ReadOnlySpan<Rgba> pixels, int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        MemoryMarshal.AsBytes(pixels).CopyTo(bitmap.GetPixelSpan());
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, data.ToArray());
    }

    public static Rgba[] Load(string path, out int width, out int height)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidDataException($"Cannot decode {path}.");
        width = codec.Info.Width;
        height = codec.Info.Height;
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result != SKCodecResult.Success)
        {
            throw new InvalidDataException($"Cannot decode {path}: {result}.");
        }

        var pixels = new Rgba[width * height];
        bitmap.GetPixelSpan().CopyTo(MemoryMarshal.AsBytes(pixels.AsSpan()));
        return pixels;
    }

    private static int MaxChannelDifference(Rgba a, Rgba b) =>
        Math.Max(Math.Max(Math.Abs(a.R - b.R), Math.Abs(a.G - b.G)), Math.Max(Math.Abs(a.B - b.B), Math.Abs(a.A - b.A)));

    private static string SourceDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
