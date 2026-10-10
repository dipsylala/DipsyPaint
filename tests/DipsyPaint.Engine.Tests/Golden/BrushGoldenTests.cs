using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

public class BrushGoldenTests
{
    [Fact]
    public void BrushStrokes()
    {
        var editor = new DocumentEditor(Document.CreateBlank(96, 64)) { SecondaryColor = new Rgba(0, 0, 255, 255) };

        DrawBrushStrokes(editor);

        GoldenImage.AssertMatches("brush-strokes", editor.Compositor);
    }

    [Fact]
    public void EraserStrokes()
    {
        var editor = new DocumentEditor(Document.CreateBlank(96, 64));

        DrawEraserStrokes(editor);

        GoldenImage.AssertMatches("eraser-strokes", editor.Compositor);
    }

    [Fact]
    public void UndoingBrushAndEraserStrokesRestoresTheBlankDocument_AndRedoingReproducesThem()
    {
        var editor = new DocumentEditor(Document.CreateBlank(96, 64));
        var blank = editor.Compositor.Pixels.ToArray();
        DrawEraserStrokes(editor);

        while (editor.History.CanUndo)
        {
            editor.Undo();
        }

        Assert.Equal(blank, editor.Compositor.Pixels.ToArray());

        while (editor.History.CanRedo)
        {
            editor.Redo();
        }

        GoldenImage.AssertMatches("eraser-strokes", editor.Compositor);
    }

    /// <summary>Brush strokes at several sizes, both buttons, diagonals and a self-crossing path.</summary>
    private static void DrawBrushStrokes(DocumentEditor editor)
    {
        Brush(editor, 1, PointerButton.Left, (4, 4), (40, 4));
        Brush(editor, 3, PointerButton.Left, (4, 10), (40, 12));
        Brush(editor, 5, PointerButton.Left, (4, 20), (40, 30));                     // shallow diagonal
        Brush(editor, 10, PointerButton.Left, (60, 8));                               // single dab
        Brush(editor, 25, PointerButton.Left, (78, 20));                              // single large dab
        Brush(editor, 8, PointerButton.Right, (10, 40), (40, 60));                    // secondary colour
        Brush(editor, 4, PointerButton.Left, (50, 40), (90, 60), (90, 40), (50, 60)); // crosses itself
        Brush(editor, 6, PointerButton.Left, (-10, 62), (30, 34));                    // enters from off the canvas
    }

    /// <summary>Black brush paint erased at several sizes, plus an eraser stroke over plain white.</summary>
    private static void DrawEraserStrokes(DocumentEditor editor)
    {
        Brush(editor, 30, PointerButton.Left, (10, 18), (86, 18));
        Brush(editor, 30, PointerButton.Left, (10, 46), (86, 46));
        Eraser(editor, 1, PointerButton.Left, (4, 10), (90, 10));
        Eraser(editor, 4, PointerButton.Left, (4, 22), (90, 26));
        Eraser(editor, 8, PointerButton.Right, (20, 2), (40, 60));           // both buttons erase
        Eraser(editor, 20, PointerButton.Left, (70, 46));                     // single click
        Eraser(editor, 6, PointerButton.Left, (50, 60), (90, 36), (94, 62)); // over paint and white
    }

    private static void Brush(DocumentEditor editor, int size, PointerButton button, params (double X, double Y)[] points)
    {
        editor.ActiveTool = new BrushTool(new StrokeSettings(size));
        Script.Stroke(editor, button, points);
    }

    private static void Eraser(DocumentEditor editor, int size, PointerButton button, params (double X, double Y)[] points)
    {
        editor.ActiveTool = new EraserTool(new StrokeSettings(size));
        Script.Stroke(editor, button, points);
    }
}
