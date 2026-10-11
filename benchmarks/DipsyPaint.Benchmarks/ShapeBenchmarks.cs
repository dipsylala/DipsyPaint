using BenchmarkDotNet.Attributes;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Benchmarks;

/// <summary>
/// Shape drags (ADR-0023, ticket 37, issue #102): dragging a handle or the body of a pending
/// 3780×2080 oval, outlined at 8 px and filled in one style, on a 4K document of 20 layers, through
/// the editor. Each invocation
/// is one drag (down, 20 moves, up); the result is the mean cost per pointer event. Successive drags
/// go back and forth, so every invocation does the same work. The pending shape is redrawn on every
/// move, so this measures the textured styles' rendering against solid's.
/// </summary>
public class ShapeBenchmarks
{
    private const int Moves = 20;
    private const int Events = Moves + 2;
    private const double DegreesPerMove = 2;
    private const int PixelsPerMove = 10;

    private DocumentEditor _editor = null!;
    private int _direction;

    [Params("solid", "crayon", "marker", "oil", "natural-pencil", "watercolour")]
    public string Style { get; set; } = "";

    [GlobalSetup(Target = nameof(RotateEvent))]
    public void SetupRotate()
    {
        Setup();
        _editor.Rotate(10); // the drags then sweep 10°–50° and back
    }

    [GlobalSetup(Targets = [nameof(StretchEvent), nameof(MoveEvent)])]
    public void SetupStretch() => Setup();

    /// <summary>Drags the rotate handle 40° about the shape's centre, 2° per move.</summary>
    [Benchmark(OperationsPerInvoke = Events)]
    [Target(16)]
    public void RotateEvent()
    {
        var frame = _editor.Adornment!.Value.Frame;
        double centreX = frame.X + frame.Width / 2.0, centreY = frame.Y + frame.Height / 2.0;
        var (x, y) = _editor.Adornment!.Value.RotateHandle(_editor.HandleTolerance);
        var radius = Math.Sqrt((x - centreX) * (x - centreX) + (y - centreY) * (y - centreY));
        var pressAngle = Math.Atan2(y - centreY, x - centreX);
        _direction = _direction == 1 ? -1 : 1;
        PointerInput At(PointerAction action, int move)
        {
            var angle = pressAngle + _direction * move * DegreesPerMove * Math.PI / 180;
            return new PointerInput(action, centreX + radius * Math.Cos(angle), centreY + radius * Math.Sin(angle));
        }

        _editor.Apply(new PointerInput(PointerAction.Down, x, y));
        for (var i = 1; i <= Moves; i++)
        {
            _editor.Apply(At(PointerAction.Move, i));
        }

        _editor.Apply(At(PointerAction.Up, Moves));
    }

    /// <summary>Drags the bottom-right handle 200 px in (or back out), 10 px per move.</summary>
    [Benchmark(OperationsPerInvoke = Events)]
    [Target(16)]
    public void StretchEvent()
    {
        var frame = _editor.Adornment!.Value.Frame;
        double x = frame.Right, y = frame.Bottom;
        _direction = _direction == -1 ? 1 : -1;
        _editor.Apply(new PointerInput(PointerAction.Down, x, y));
        for (var i = 1; i <= Moves; i++)
        {
            _editor.Apply(new PointerInput(PointerAction.Move, x + _direction * i * PixelsPerMove, y + _direction * i * PixelsPerMove));
        }

        _editor.Apply(new PointerInput(PointerAction.Up, x + _direction * Moves * PixelsPerMove, y + _direction * Moves * PixelsPerMove));
    }

    /// <summary>Drags the shape by its body 200 px left and up (or back), 10 px per move.</summary>
    [Benchmark(OperationsPerInvoke = Events)]
    [Target(16)]
    public void MoveEvent()
    {
        var frame = _editor.Adornment!.Value.Frame;
        double x = frame.X + frame.Width / 2.0, y = frame.Y + frame.Height / 2.0;
        _direction = _direction == -1 ? 1 : -1;
        _editor.Apply(new PointerInput(PointerAction.Down, x, y));
        for (var i = 1; i <= Moves; i++)
        {
            _editor.Apply(new PointerInput(PointerAction.Move, x + _direction * i * PixelsPerMove, y + _direction * i * PixelsPerMove));
        }

        _editor.Apply(new PointerInput(PointerAction.Up, x + _direction * Moves * PixelsPerMove, y + _direction * Moves * PixelsPerMove));
    }

    private void Setup()
    {
        var document = Document.CreateBlank(3840, 2160);
        while (document.Layers.Count < 20)
        {
            document.AddLayer();
        }

        var style = Style switch
        {
            "solid" => ShapeStyle.Solid,
            "crayon" => ShapeStyle.Crayon,
            "marker" => ShapeStyle.Marker,
            "oil" => ShapeStyle.Oil,
            "natural-pencil" => ShapeStyle.NaturalPencil,
            _ => ShapeStyle.Watercolour,
        };
        var settings = new ShapeSettings { Kind = ShapeKind.Ellipse, Outline = style, Fill = style, Size = 8 };
        _editor = new DocumentEditor(document) { ActiveTool = new ShapeTool(settings) };
        _editor.Apply(new PointerInput(PointerAction.Down, 30, 40));
        _editor.Apply(new PointerInput(PointerAction.Move, 3809, 2119));
        _editor.Apply(new PointerInput(PointerAction.Up, 3809, 2119));
        _direction = 0;
    }
}
