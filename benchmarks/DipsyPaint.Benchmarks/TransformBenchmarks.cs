using BenchmarkDotNet.Attributes;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Benchmarks;

/// <summary>
/// Transform drags (ADR-0023): dragging a handle of a 3840×2160 selection (the whole canvas of a
/// 20-layer document, lifted off the top layer) through the editor. Each invocation is one drag
/// (down, 20 moves, up); the result is the mean cost per pointer event. Successive drags go back
/// and forth, so every invocation does the same work. Copying the composite to the screen (shell)
/// is not included.
/// </summary>
public class TransformBenchmarks
{
    private const int Moves = 20;
    private const int Events = Moves + 2;

    /// <summary>Degrees the pointer turns the selection per move.</summary>
    private const double DegreesPerMove = 2;

    /// <summary>Pixels the pointer moves the stretched corner per move, each way.</summary>
    private const int PixelsPerMove = 10;

    private DocumentEditor _editor = null!;
    private int _direction;

    [GlobalSetup(Target = nameof(RotateEvent))]
    public void SetupRotate()
    {
        Setup();
        _editor.Rotate(10); // lifts the selection; the drags then sweep 10°–50° and back
    }

    [GlobalSetup(Target = nameof(StretchEvent))]
    public void SetupStretch() => Setup();

    /// <summary>Drags the rotate handle 40° about the selection's centre, 2° per move.</summary>
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

    // A 4K document of 20 layers, the top one opaque and patterned, all of it selected.
    private void Setup()
    {
        var document = Document.CreateBlank(3840, 2160);
        while (document.Layers.Count < 20)
        {
            document.AddLayer();
        }

        var top = document.ActiveLayer.Pixels;
        var row = new Rgba[top.Width];
        for (var y = 0; y < top.Height; y++)
        {
            for (var x = 0; x < row.Length; x++)
            {
                row[x] = new Rgba((byte)x, (byte)y, (byte)(x ^ y), 255);
            }

            top.WriteRegion(new IntRect(0, 0, top.Width, 1) with { Y = y }, row);
        }

        _editor = new DocumentEditor(document) { ActiveTool = new SelectionTool() };
        _editor.SelectAll();
        _editor.ActiveTool = new SelectionTool { Mode = SelectionToolMode.MoveSelectedPixels }; // its handles drag (ADR-0025)
        _direction = 0;
    }
}
