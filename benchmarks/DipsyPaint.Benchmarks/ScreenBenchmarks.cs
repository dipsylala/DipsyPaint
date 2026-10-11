using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using DipsyPaint.Engine;

namespace DipsyPaint.Benchmarks;

/// <summary>
/// Copying the composite to the screen (issue #102): after a change to the whole of a 3840×2160
/// canvas, the shell's <c>DocumentCanvas.CopyToBitmap</c> copies every row of the composite into its
/// <c>WriteableBitmap</c>. This runs the same row-by-row copy into a buffer of the same layout, so it
/// measures the CPU side of that copy. Avalonia's upload of the bitmap to the GPU and its drawing are
/// not included.
/// </summary>
public class ScreenBenchmarks
{
    private const int Width = 3840;
    private const int Height = 2160;
    private const int RowBytes = Width * 4;

    private Compositor _compositor = null!;
    private byte[] _bitmap = [];

    [GlobalSetup]
    public void Setup()
    {
        var document = Document.CreateBlank(Width, Height);
        while (document.Layers.Count < 20)
        {
            document.AddLayer();
        }

        _compositor = new Compositor(document);
        _bitmap = new byte[RowBytes * Height];
    }

    /// <summary>Copies the whole composite, as the shell does after a full-canvas change.</summary>
    [Benchmark]
    [Target(16)]
    public void CopyFullCanvas()
    {
        var region = new IntRect(0, 0, Width, Height);
        var composite = _compositor.Pixels;
        for (var y = region.Y; y < region.Bottom; y++)
        {
            var row = MemoryMarshal.AsBytes(composite.Slice(y * Width + region.X, region.Width));
            row.CopyTo(_bitmap.AsSpan(y * RowBytes + region.X * 4, row.Length));
        }
    }
}
