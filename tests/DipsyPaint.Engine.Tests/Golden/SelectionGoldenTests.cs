using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

/// <summary>
/// Rectangle selection on a 48×32 pattern of coloured 8×8 tiles. Lifted or deleted pixels leave
/// the layer transparent, which shows as transparency in the golden images.
/// </summary>
public class SelectionGoldenTests
{
    private static DocumentEditor CreateEditor()
    {
        var editor = new DocumentEditor(Document.CreateBlank(48, 32)) { ActiveTool = new SelectionTool() };
        var layer = editor.Document.ActiveLayer.Pixels;
        for (var y = 0; y < layer.Height; y++)
        {
            for (var x = 0; x < layer.Width; x++)
            {
                var (tx, ty) = (x / 8, y / 8);
                layer.SetPixel(x, y, (tx + ty) % 2 == 0 ? Rgba.White : new Rgba((byte)(tx * 50), (byte)(ty * 80), 160, 255));
            }
        }

        editor.Compositor.Update(new IntRect(0, 0, layer.Width, layer.Height));
        return editor;
    }

    [Fact]
    public void Move()
    {
        var editor = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (19.5, 15.5));   // select 16×12 pixels
        Script.Stroke(editor, PointerButton.Left, (12.5, 10.5), (34.5, 20.5)); // move by (22, 10)
        editor.CommitPending();

        GoldenImage.AssertMatches("selection-move", editor.Compositor);
    }

    [Fact]
    public void Delete()
    {
        var editor = CreateEditor();

        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (19.5, 15.5));
        editor.DeleteSelection();
        Script.Stroke(editor, PointerButton.Left, (40.5, 30.5), (28.5, 20.5));  // drawn upwards
        editor.DeleteSelection();

        GoldenImage.AssertMatches("selection-delete", editor.Compositor);
    }
}
