using BenchmarkDotNet.Attributes;
using DipsyPaint.Engine;

namespace DipsyPaint.Benchmarks;

/// <summary>
/// The edge of a free-form or magic-wand selection (issue #111). The shell has the engine draw it into a bitmap of
/// window pixels (<see cref="SelectionOutline.Draw"/>) whenever the selection or the view changes, e.g. on every
/// pointer event of a move. This draws the edge of a magic-wand selection of a noisy 3840×2160 image (each pixel
/// red or white at random, 70% red; millions of edge pixels) into a 3840×2160 raster at 100% zoom, the largest a 4K
/// window needs. Avalonia's drawing of the bitmap is not included.
/// </summary>
public class OutlineBenchmarks
{
    private const int Width = 3840;
    private const int Height = 2160;

    private static readonly ScreenMap OnePixelEach = new(1, 0, 0, 1, 0, 0);

    private Selection _selection;
    private Rgba[] _raster = [];

    [GlobalSetup]
    public void Setup()
    {
        var pixels = Document.CreateBlank(Width, Height).ActiveLayer.Pixels;
        var random = new Random(111);
        var row = new Rgba[Width];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                row[x] = random.NextDouble() < 0.7 ? new Rgba(255, 0, 0, 255) : Rgba.White;
            }

            pixels.WriteRegion(new IntRect(0, y, Width, 1), row);
        }

        pixels.SetPixel(Width / 2, Height / 2, new Rgba(255, 0, 0, 255));
        _selection = MagicWand.Select(pixels, Width / 2, Height / 2, 0)!.Value;
        _raster = new Rgba[Width * Height];
    }

    /// <summary>Draws the edge over a whole 4K window.</summary>
    [Benchmark]
    [Target(16)]
    public void DrawFullWindow() =>
        SelectionOutline.Draw(_selection.Bounds, _selection.Mask, OnePixelEach, Width, Height, y => _raster.AsSpan(y * Width, Width));
}
