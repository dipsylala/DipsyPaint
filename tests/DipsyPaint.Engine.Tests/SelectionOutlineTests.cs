using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

/// <summary>
/// The actual outline of free-form and magic-wand selections (issue #111): the edge drawn from the mask, and
/// the Move tools starting a move only on a selected pixel.
/// </summary>
public class SelectionOutlineTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    private static readonly ScreenMap OnePixelEach = new(1, 0, 0, 1, 0, 0);

    private static bool[] Mask(params string[] rows) =>
        [.. rows.SelectMany(row => row.Select(c => c == '#'))];

    // The raster as text: '#' black, 'o' white, '.' transparent.
    private static string[] Draw(IntRect bounds, bool[]? mask, ScreenMap map, int width, int height, int dashX = 0, int dashY = 0)
    {
        var pixels = new Rgba[width * height];
        SelectionOutline.Draw(bounds, mask, map, width, height, y => pixels.AsSpan(y * width, width), dashX, dashY);
        return
        [
            .. Enumerable.Range(0, height).Select(y => new string([.. pixels.AsSpan(y * width, width).ToArray().Select(p =>
                p == Rgba.Transparent ? '.' : p == new Rgba(0, 0, 0, 255) ? '#' : p == Rgba.White ? 'o' : '?')])),
        ];
    }

    // Opaque pixels as 'e', whichever colour.
    private static string[] Edge(string[] raster) => [.. raster.Select(row => row.Replace('#', 'e').Replace('o', 'e'))];

    [Fact]
    public void AtOneScreenPixelEach_TheEdgeIsThePixelsAroundTheShape_DiagonalsIncluded()
    {
        var raster = Draw(new IntRect(2, 2, 2, 2), Mask("#.", "##"), OnePixelEach, 6, 6);

        Assert.Equal(["......", ".eee..", ".e.ee.", ".e..e.", ".eeee.", "......"], Edge(raster));
    }

    [Fact]
    public void AHole_IsEdgedFromInside()
    {
        var raster = Draw(new IntRect(1, 1, 3, 3), Mask("###", "#.#", "###"), OnePixelEach, 5, 5);

        Assert.Equal(["eeeee", "e...e", "e.e.e", "e...e", "eeeee"], Edge(raster));
    }

    [Fact]
    public void Zoomed_TheEdgeIsOneScreenPixel_JustOutsideTheSelectedPixels()
    {
        // Four screen pixels per document pixel: document pixel (1, 1) is screen pixels 4 to 7 each way.
        var raster = Edge(Draw(new IntRect(1, 1, 1, 1), null, new ScreenMap(0.25, 0, 0, 0.25, 0, 0), 12, 12));

        Assert.Equal("...eeeeee...", raster[3]);
        Assert.All(raster[4..8], row => Assert.Equal("...e....e...", row));
        Assert.Equal("...eeeeee...", raster[8]);
        Assert.Equal(new string('.', 12), raster[2]);
        Assert.Equal(new string('.', 12), raster[9]);
    }

    [Fact]
    public void TheEdge_IsDashedEveryFourScreenPixels_AlongItOrDownIt_FromTheGivenOrigin()
    {
        var across = new IntRect(0, 1, 20, 1);
        Assert.Equal("####oooo####oooo####", Draw(across, null, OnePixelEach, 20, 3)[0]);
        Assert.Equal("##oooo####oooo####oo", Draw(across, null, OnePixelEach, 20, 3, dashX: 2)[0]);

        var down = new IntRect(1, 0, 1, 20);
        Assert.Equal("####oooo####oooo####", string.Concat(Draw(down, null, OnePixelEach, 3, 20).Select(row => row[0])));
        Assert.Equal("##oooo####oooo####oo", string.Concat(Draw(down, null, OnePixelEach, 3, 20, dashY: 2).Select(row => row[0])));

        // A diagonal edge is dashed too.
        var staircase = Draw(new IntRect(0, 0, 16, 16), Mask([.. Enumerable.Range(0, 16).Select(y => new string('#', 16 - y).PadRight(16, '.'))]), OnePixelEach, 17, 17);
        var diagonal = Enumerable.Range(1, 15).Select(x => staircase[16 - x][x]).ToArray();
        Assert.Contains('#', diagonal);
        Assert.Contains('o', diagonal);
    }

    [Fact]
    public void ATurningMap_DrawsTheShapeTurned()
    {
        // A quarter turn clockwise about (3, 3), the centre of the shape: the screen shows document point
        // (v − 3 + 3, 3 − (u − 3)) at (u, v).
        var turned = Draw(new IntRect(2, 2, 2, 2), Mask("#.", "##"), new ScreenMap(0, -1, 1, 0, 0, 6), 6, 6);

        Assert.Equal(Draw(new IntRect(2, 2, 2, 2), Mask("##", "#."), OnePixelEach, 6, 6), turned);
    }

    [Fact]
    public void NothingSelected_DrawsNothing()
    {
        var raster = Draw(new IntRect(0, 0, 3, 2), Mask("...", "..."), OnePixelEach, 5, 4);

        Assert.All(raster, row => Assert.Equal(".....", row));
    }
    // The Move tools with a free-form triangle selected: pixels with x ≤ y within (4, 4)–(11, 11).

    private static DocumentEditor WithTriangleSelected(SelectionToolMode mode)
    {
        var editor = new DocumentEditor(Document.CreateBlank(32, 32)) { HandleTolerance = 1 };
        editor.ActiveTool = new SelectionTool(editor.SelectionSettings);
        editor.Document.ActiveLayer.Pixels.WriteRegion(new IntRect(4, 4, 8, 8), Enumerable.Repeat(Red, 64).ToArray());
        editor.Compositor.Update(new IntRect(0, 0, 32, 32));
        var mask = new bool[64];
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x <= y; x++)
            {
                mask[y * 8 + x] = true;
            }
        }

        editor.Select(new IntRect(4, 4, 8, 8), mask);
        Script.Use(editor, mode);
        return editor;
    }

    private static void Drag(DocumentEditor editor, (double X, double Y) from, (double X, double Y) to) =>
        Script.Stroke(editor, PointerButton.Left, from, to);

    [Theory]
    [InlineData(SelectionToolMode.MoveSelection)]
    [InlineData(SelectionToolMode.MoveSelectedPixels)]
    public void MoveTools_StartAMove_OnlyOnASelectedPixel(SelectionToolMode mode)
    {
        var editor = WithTriangleSelected(mode);
        var selection = editor.Selection;

        Drag(editor, (9.5, 6.5), (19.5, 6.5)); // in the bounding rectangle, outside the triangle

        Assert.Equal(selection, editor.Selection);
        Assert.Equal(Red, editor.Document.ActiveLayer.Pixels.GetPixel(9, 6));
        Assert.True(editor.Overlay.IsEmpty);
        Assert.Equal(FrameHandle.None, editor.Adornment!.Value.HitTest(9.5, 6.5, 1));

        Drag(editor, (5.5, 9.5), (15.5, 9.5)); // inside it

        Assert.Equal(new IntRect(14, 4, 8, 8), editor.Selection?.Bounds);
    }

    [Fact]
    public void MoveSelection_WithFloatingPixels_AndAPressOutsideTheShape_CommitsNothing()
    {
        var editor = WithTriangleSelected(SelectionToolMode.MoveSelection);
        editor.Transform(ImageTransform.FlipHorizontal); // floating; now pixels with x ≥ 7 − y

        Drag(editor, (4.5, 4.5), (14.5, 4.5));

        Assert.NotNull(editor.Selection?.FloatingPixels);
        Assert.Equal(new IntRect(4, 4, 8, 8), editor.Selection?.Bounds);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Handles_StillWork_WhereverTheyAre()
    {
        var editor = WithTriangleSelected(SelectionToolMode.MoveSelectedPixels);

        // The top-right handle sits outside the triangle.
        Assert.Equal(FrameHandle.TopRight, editor.Adornment!.Value.HitTest(12, 4, 1));
        Drag(editor, (12, 4), (16, 4));

        Assert.Equal(new IntRect(4, 4, 12, 8), editor.Selection?.Bounds);
        Assert.NotNull(editor.Selection?.Mask);
        Assert.Same(editor.Selection?.Mask, editor.Adornment?.Mask); // the stretched shape takes the presses
    }

    [Fact]
    public void ARectangleSelection_TakesAPressAnywhereInIt()
    {
        var editor = new DocumentEditor(Document.CreateBlank(32, 32)) { HandleTolerance = 1 };
        editor.ActiveTool = new SelectionTool(editor.SelectionSettings);
        editor.Select(new IntRect(4, 4, 8, 8));
        Script.Use(editor, SelectionToolMode.MoveSelection);

        Assert.Null(editor.Adornment?.Mask);
        Assert.Equal(FrameHandle.Move, editor.Adornment!.Value.HitTest(10.5, 5.5, 1));
    }

    [Fact]
    public void PreviewTurn_IsTheTurnTheRotateDragAdds_ToTheSelectionAsAtThePress()
    {
        var editor = WithTriangleSelected(SelectionToolMode.MoveSelectedPixels);
        editor.Rotate(10);
        var tool = (SelectionTool)editor.ActiveTool!;
        var selection = editor.Selection;
        Assert.Null(tool.PreviewTurn);

        var frame = editor.Adornment!.Value.Frame;
        double centreX = frame.X + frame.Width / 2.0, centreY = frame.Y + frame.Height / 2.0;
        var (x, y) = editor.Adornment!.Value.RotateHandle(editor.HandleTolerance);
        var radius = centreY - y;
        editor.Apply(new PointerInput(PointerAction.Down, x, y));
        editor.Apply(new PointerInput(PointerAction.Move, centreX + radius, centreY)); // about a quarter turn

        Assert.Equal(selection, editor.Selection);
        var (degrees, cx, cy) = tool.PreviewTurn!.Value;
        Assert.InRange(degrees, 80, 100); // on top of the 10° the selection already has
        Assert.Equal((8, 8), (cx, cy)); // the centre of the first turn

        editor.Apply(new PointerInput(PointerAction.Up, centreX + radius, centreY));
        Assert.Null(tool.PreviewTurn);
    }
}
