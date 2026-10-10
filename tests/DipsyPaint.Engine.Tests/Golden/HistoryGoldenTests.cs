using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

public class HistoryGoldenTests
{
    [Fact]
    public void UndoingEveryStrokeRestoresTheBlankDocument_AndRedoingReproducesThem()
    {
        var editor = new DocumentEditor(Document.CreateBlank(48, 32)) { ActiveTool = new PencilTool() };
        var blank = editor.Compositor.Pixels.ToArray();
        PencilGoldenTests.DrawStrokes(editor);

        while (editor.History.CanUndo)
        {
            editor.Undo();
        }

        Assert.Equal(blank, editor.Compositor.Pixels.ToArray());

        while (editor.History.CanRedo)
        {
            editor.Redo();
        }

        GoldenImage.AssertMatches("pencil-strokes", editor.Compositor);
    }
}
