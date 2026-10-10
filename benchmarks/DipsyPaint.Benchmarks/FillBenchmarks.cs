using BenchmarkDotNet.Attributes;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Benchmarks;

/// <summary>
/// AC32: a flood fill of a whole 4K layer through the editor: region search, write, history step
/// and recomposite. Each invocation fills with the other of two colours, so every fill changes
/// every pixel. Measured on a single-layer document (the target's "4K canvas") and with 20 layers,
/// where recompositing the whole canvas dominates.
/// </summary>
public class FillBenchmarks
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    private DocumentEditor _editor = null!;

    [Params(1, 20)]
    public int Layers { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var document = Document.CreateBlank(3840, 2160);
        while (document.Layers.Count < Layers)
        {
            document.AddLayer();
        }

        _editor = new DocumentEditor(document) { ActiveTool = new FillTool() };
    }

    [Benchmark]
    [Target(200)]
    public void FloodFill()
    {
        _editor.PrimaryColor = _editor.PrimaryColor == Red ? Blue : Red;
        _editor.Apply(new PointerInput(PointerAction.Down, 1920, 1080));
        _editor.Apply(new PointerInput(PointerAction.Up, 1920, 1080));
    }
}
