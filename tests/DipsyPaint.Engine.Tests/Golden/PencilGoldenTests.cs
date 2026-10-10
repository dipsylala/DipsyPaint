using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

public class PencilGoldenTests
{
    [Fact]
    public void PencilStrokes()
    {
        var editor = new DocumentEditor(Document.CreateBlank(48, 32)) { ActiveTool = new PencilTool() };

        Script.Stroke(editor, PointerButton.Left, (2.5, 2.5));                           // single click
        Script.Stroke(editor, PointerButton.Left, (5, 2), (20, 2));                      // horizontal
        Script.Stroke(editor, PointerButton.Left, (2, 5), (2, 28));                      // vertical
        Script.Stroke(editor, PointerButton.Left, (5, 5), (20, 20));                     // diagonal
        Script.Stroke(editor, PointerButton.Left, (24, 4), (46, 10), (30, 28), (24.9, 4.9)); // shallow and steep
        Script.Stroke(editor, PointerButton.Left, (-6, 31.5), (12, 24));                // enters from off the canvas
        Script.Stroke(editor, PointerButton.Right, (12, -1), (12, 32));                 // secondary colour over black

        GoldenImage.AssertMatches("pencil-strokes", editor.Compositor);
    }
}
