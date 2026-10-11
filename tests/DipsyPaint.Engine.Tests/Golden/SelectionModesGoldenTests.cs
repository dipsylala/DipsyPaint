using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests.Golden;

/// <summary>
/// Free-form selection, select all, invert selection and transparent selection on the 48×32 pattern
/// of coloured and white 8×8 tiles that <see cref="SelectionGoldenTests"/> uses. Lifted or deleted
/// pixels leave the layer transparent, which shows as transparency in the golden images.
/// </summary>
public class SelectionModesGoldenTests
{
    private static DocumentEditor CreateEditor(SelectionSettings settings)
    {
        var editor = new DocumentEditor(Document.CreateBlank(48, 32)) { SelectionSettings = settings, HandleTolerance = 0.25 };
        editor.ActiveTool = new SelectionTool(settings);
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
    public void FreeFormMove()
    {
        var editor = CreateEditor(new SelectionSettings { Shape = SelectionShape.FreeForm });

        // A lasso through (10, 2), (20, 10), (14, 22), (4, 18), closed back to the start.
        Script.Stroke(editor, PointerButton.Left, (10.5, 2.5), (20.5, 10.5), (14.5, 22.5), (4.5, 18.5));
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);
        Script.Stroke(editor, PointerButton.Left, (12.5, 12.5), (36.5, 20.5)); // move by (24, 8)
        editor.CommitPending();

        GoldenImage.AssertMatches("selection-free-form-move", editor.Compositor);
    }

    [Fact]
    public void SelectAllMove()
    {
        var editor = CreateEditor(new SelectionSettings());

        editor.SelectAll();
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);
        Script.Stroke(editor, PointerButton.Left, (20.5, 20.5), (26.5, 24.5)); // move by (6, 4)
        editor.CommitPending();

        GoldenImage.AssertMatches("selection-select-all-move", editor.Compositor);
    }

    [Fact]
    public void InvertDelete()
    {
        var editor = CreateEditor(new SelectionSettings { Shape = SelectionShape.FreeForm });

        Script.Stroke(editor, PointerButton.Left, (24.5, 2.5), (44.5, 16.5), (24.5, 29.5), (6.5, 16.5)); // a diamond
        editor.InvertSelection();
        editor.DeleteSelection();

        GoldenImage.AssertMatches("selection-invert-delete", editor.Compositor);
    }

    [Fact]
    public void TransparentMove()
    {
        var editor = CreateEditor(new SelectionSettings { Transparent = true }); // colour 2 is white

        Script.Stroke(editor, PointerButton.Left, (0.5, 0.5), (15.5, 15.5));   // two coloured and two white tiles
        Script.Use(editor, SelectionToolMode.MoveSelectedPixels);
        Script.Stroke(editor, PointerButton.Left, (8.5, 8.5), (28.5, 12.5));   // move by (20, 4)
        editor.CommitPending();

        GoldenImage.AssertMatches("selection-transparent-move", editor.Compositor);
    }
}
