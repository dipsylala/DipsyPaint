using BenchmarkDotNet.Attributes;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Benchmarks;

/// <summary>
/// AC31: brush input to composited output on a 4K document with 20 layers, drawing on the top
/// layer. Each invocation is one stroke (down, moves 10 px apart, up) through the editor, which
/// paints, records the history step and recomposites the dirty rectangle; the result is the mean
/// cost per pointer event. Copying the composite to the screen (shell) is not included.
/// </summary>
public class BrushBenchmarks
{
    private const int Moves = 20;
    private const int Events = Moves + 2;

    private DocumentEditor _editor = null!;

    /// <summary>The shell's default brush size, and the largest.</summary>
    [Params(5, 100)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var document = Document.CreateBlank(3840, 2160);
        while (document.Layers.Count < 20)
        {
            document.AddLayer();
        }

        _editor = new DocumentEditor(document) { ActiveTool = new BrushTool(new StrokeSettings(Size)) };
    }

    [Benchmark(OperationsPerInvoke = Events)]
    [Target(16)]
    public void StrokeEvent()
    {
        const double x = 1500, y = 1000;
        _editor.Apply(new PointerInput(PointerAction.Down, x, y));
        for (var i = 1; i <= Moves; i++)
        {
            _editor.Apply(new PointerInput(PointerAction.Move, x + i * 10, y + i * 3));
        }

        _editor.Apply(new PointerInput(PointerAction.Up, x + Moves * 10, y + Moves * 3));
    }
}
