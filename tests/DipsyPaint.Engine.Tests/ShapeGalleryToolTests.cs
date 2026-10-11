using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

/// <summary>The shapes beyond line, rectangle and ellipse (ticket 24): box shapes, curve and polygon.</summary>
public class ShapeGalleryToolTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba Blue = new(0, 0, 255, 255);

    public static TheoryData<ShapeKind> BoxShapes { get; } =
        [.. Enum.GetValues<ShapeKind>().Where(k => k is not (ShapeKind.Line or ShapeKind.Curve or ShapeKind.Polygon))];

    private static DocumentEditor CreateEditor(ShapeSettings settings, int width = 48, int height = 48) =>
        new(Document.CreateBlank(width, height))
        {
            ActiveTool = new ShapeTool(settings),
            PrimaryColor = Red,
            SecondaryColor = Blue,
        };

    private static IPixelAccess Layer(DocumentEditor editor) => editor.Document.ActiveLayer.Pixels;

    private static Rgba Shown(DocumentEditor editor, int x, int y) => editor.Compositor.Pixels[y * editor.Compositor.Width + x];

    private static HashSet<(int X, int Y)> LayerPixelsOf(DocumentEditor editor, params Rgba[] colors)
    {
        var pixels = Layer(editor);
        var found = new HashSet<(int, int)>();
        for (var y = 0; y < pixels.Height; y++)
        {
            for (var x = 0; x < pixels.Width; x++)
            {
                if (colors.Contains(pixels.GetPixel(x, y)))
                {
                    found.Add((x, y));
                }
            }
        }

        return found;
    }

    private static IntRect BoundsOf(IEnumerable<(int X, int Y)> pixels)
    {
        var list = pixels.ToList();
        return Adornment.FrameOf((list.Min(p => p.X), list.Min(p => p.Y)), (list.Max(p => p.X), list.Max(p => p.Y)));
    }

    private static void Click(DocumentEditor editor, double x, double y) => Script.Stroke(editor, PointerButton.Left, (x, y));

    private static HashSet<(int X, int Y)> Draw(ShapeKind kind, ShapeStyle outline, ShapeStyle fill, int size)
    {
        var editor = CreateEditor(new ShapeSettings { Kind = kind, Outline = outline, Fill = fill, Size = size });
        Script.Stroke(editor, PointerButton.Left, (4.5, 6.5), (40.5, 33.5));
        editor.CommitPending();
        return LayerPixelsOf(editor, Red, Blue);
    }

    [Fact]
    public void Gallery_HasEveryShapeOfReferencePaint()
    {
        Assert.Equal(23, Enum.GetValues<ShapeKind>().Length);
        Assert.False(ShapeSettings.CanFill(ShapeKind.Line));
        Assert.False(ShapeSettings.CanFill(ShapeKind.Curve));
        Assert.True(ShapeSettings.CanFill(ShapeKind.Polygon));
    }

    [Theory]
    [MemberData(nameof(BoxShapes))]
    public void BoxShape_FillsItsFrameAndReachesEverySide(ShapeKind kind)
    {
        var frame = new IntRect(4, 6, 37, 28);
        var bounds = BoundsOf(Draw(kind, ShapeStyle.None, ShapeStyle.Solid, 1));

        // A sharp point (a star's) may stop short of the side by up to two pixels: a pixel is in the
        // shape only when its centre is.
        Assert.Equal(frame, bounds.Union(frame));
        Assert.InRange(bounds.X - frame.X, 0, 2);
        Assert.InRange(bounds.Y - frame.Y, 0, 2);
        Assert.InRange(frame.Right - bounds.Right, 0, 2);
        Assert.InRange(frame.Bottom - bounds.Bottom, 0, 2);
    }

    [Theory]
    [MemberData(nameof(BoxShapes))]
    public void BoxShape_OutlineGrowsInwardsAndTheFillTakesTheRest(ShapeKind kind)
    {
        var filled = Draw(kind, ShapeStyle.None, ShapeStyle.Solid, 1);

        foreach (var size in ShapeSettings.Sizes)
        {
            var settings = new ShapeSettings { Kind = kind, Fill = ShapeStyle.Solid, Size = size };
            var editor = CreateEditor(settings);
            Script.Stroke(editor, PointerButton.Left, (4.5, 6.5), (40.5, 33.5));
            editor.CommitPending();

            var outline = LayerPixelsOf(editor, Red);
            var inside = LayerPixelsOf(editor, Blue);
            Assert.NotEmpty(outline);
            Assert.Empty(outline.Intersect(inside));
            Assert.Equal(filled.Order(), outline.Union(inside).Order()); // same shape, outline inside it
        }
    }

    [Theory]
    [MemberData(nameof(BoxShapes))]
    public void BoxShape_ThickerOutlinesCoverThinnerOnes(ShapeKind kind)
    {
        var thin = Draw(kind, ShapeStyle.Solid, ShapeStyle.None, 1);
        var thick = Draw(kind, ShapeStyle.Solid, ShapeStyle.None, 5);

        Assert.Subset(thick, thin);
        Assert.True(thick.Count > thin.Count);
    }

    [Theory]
    [InlineData(ShapeKind.FivePointStar)]
    [InlineData(ShapeKind.Heart)]
    [InlineData(ShapeKind.Hexagon)]
    public void Shift_KeepsABoxShapeSquare(ShapeKind kind)
    {
        var editor = CreateEditor(new ShapeSettings { Kind = kind });

        editor.Apply(new PointerInput(PointerAction.Down, 0.5, 0.5));
        editor.Apply(new PointerInput(PointerAction.Up, 20.5, 9.5, Shift: true));

        Assert.Equal(new IntRect(0, 0, 21, 21), editor.Adornment!.Value.Frame);
    }

    [Fact]
    public void BoxShape_AdjustedMatchesTheShapeDrawnInPlace()
    {
        var settings = new ShapeSettings { Kind = ShapeKind.Heart, Fill = ShapeStyle.Solid, Size = 3 };
        var adjusted = CreateEditor(settings);
        Script.Stroke(adjusted, PointerButton.Left, (4.5, 4.5), (20.5, 20.5));
        Script.Stroke(adjusted, PointerButton.Left, (21, 21), (31, 41));         // bottom-right handle
        Script.Stroke(adjusted, PointerButton.Left, (15.5, 15.5), (20.5, 15.5)); // move
        adjusted.CommitPending();

        var direct = CreateEditor(settings);
        Script.Stroke(direct, PointerButton.Left, (9.5, 4.5), (35.5, 40.5));
        direct.CommitPending();

        Assert.Equal(direct.Compositor.Pixels.ToArray(), adjusted.Compositor.Pixels.ToArray());
    }

    [Fact]
    public void Curve_IsALineThenTwoBends_ThenAnAdjustableShape()
    {
        var editor = CreateEditor(new ShapeSettings { Kind = ShapeKind.Curve });

        Script.Stroke(editor, PointerButton.Left, (4.5, 30.5), (40.5, 30.5));
        Assert.Null(editor.Adornment); // still being built: the next press bends it
        Assert.True(editor.HasPending);
        Assert.Equal(Red, Shown(editor, 20, 30));

        Script.Stroke(editor, PointerButton.Left, (10.5, 40.5), (22.5, 6.5)); // drag the first bend into place
        Assert.Null(editor.Adornment);
        Assert.NotEqual(Red, Shown(editor, 22, 30));                          // pulled up off the straight line
        Assert.Empty(LayerPixelsOf(editor, Red));                             // nothing committed by that press

        Click(editor, 34.5, 44.5);                                            // the second bend
        var frame = editor.Adornment!.Value.Frame;
        Assert.Equal((4, 40), (frame.X, frame.Right - 1));                    // from start to end
        Assert.InRange(frame.Y, 7, 25);                                       // bent towards the first bend
        Assert.InRange(frame.Bottom - 1, 31, 44);                             // and the second
        Assert.False(editor.History.CanUndo);

        editor.CommitPending();
        Assert.Contains((4, 30), LayerPixelsOf(editor, Red));
        Assert.Contains((40, 30), LayerPixelsOf(editor, Red));
        editor.Undo();
        Assert.Empty(LayerPixelsOf(editor, Red));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Curve_EnterAfterOneBend_CommitsItAsItIs()
    {
        var editor = CreateEditor(new ShapeSettings { Kind = ShapeKind.Curve });
        Script.Stroke(editor, PointerButton.Left, (4.5, 30.5), (40.5, 30.5));
        Click(editor, 22.5, 6.5);

        editor.CommitPending();

        Assert.False(editor.HasPending);
        Assert.Equal(12, BoundsOf(LayerPixelsOf(editor, Red)).Y); // a single bend pulls the middle three quarters of the way
        Assert.True(editor.History.CanUndo);
    }

    [Fact]
    public void Curve_EscWhileBending_DiscardsIt()
    {
        var editor = CreateEditor(new ShapeSettings { Kind = ShapeKind.Curve });
        Script.Stroke(editor, PointerButton.Left, (4.5, 30.5), (40.5, 30.5));

        editor.CancelPending();
        editor.CommitPending();

        Assert.False(editor.HasPending);
        Assert.Empty(LayerPixelsOf(editor, Red));
        Assert.Equal(Rgba.White, Shown(editor, 20, 30));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Curve_ResizedByItsFrame_ScalesWithIt()
    {
        var editor = CreateEditor(new ShapeSettings { Kind = ShapeKind.Curve });
        Script.Stroke(editor, PointerButton.Left, (4.5, 20.5), (20.5, 20.5));
        Click(editor, 12.5, 4.5);
        Click(editor, 12.5, 4.5);
        var frame = editor.Adornment!.Value.Frame;

        Script.Stroke(editor, PointerButton.Left, (frame.Right, frame.Bottom), (frame.Right + 16, frame.Bottom + 10));
        editor.CommitPending();

        var drawn = BoundsOf(LayerPixelsOf(editor, Red));
        Assert.Equal(new IntRect(frame.X, frame.Y, frame.Width + 16, frame.Height + 10), drawn);
    }

    [Fact]
    public void Polygon_ClosesByAClickNearItsFirstCorner_AndFills()
    {
        var editor = CreateEditor(new ShapeSettings { Kind = ShapeKind.Polygon, Fill = ShapeStyle.Solid });

        Script.Stroke(editor, PointerButton.Left, (4.5, 40.5), (40.5, 40.5)); // first side
        Click(editor, 22.5, 4.5);                                             // second corner
        Assert.Null(editor.Adornment);
        Assert.Empty(LayerPixelsOf(editor, Blue)); // open until closed
        Click(editor, 6.5, 38.5);                                             // near the first corner

        Assert.Equal(new IntRect(4, 4, 37, 37), editor.Adornment!.Value.Frame);
        editor.CommitPending();
        Assert.Equal(Blue, Layer(editor).GetPixel(22, 30));
        Assert.Equal(Red, Layer(editor).GetPixel(4, 40));
        Assert.Equal(Rgba.White, Layer(editor).GetPixel(8, 8));
        Assert.Equal(new IntRect(4, 4, 37, 37), BoundsOf(LayerPixelsOf(editor, Red, Blue)));
    }

    [Fact]
    public void Polygon_ClosesByADoubleClickOnItsLastCorner()
    {
        var editor = CreateEditor(new ShapeSettings { Kind = ShapeKind.Polygon, Outline = ShapeStyle.None, Fill = ShapeStyle.Solid });

        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (40.5, 4.5));
        Click(editor, 40.5, 40.5);
        Click(editor, 4.5, 40.5);
        Assert.Null(editor.Adornment);
        Click(editor, 4.5, 40.5); // second click of the double-click

        Assert.NotNull(editor.Adornment);
        editor.CommitPending();
        Assert.Equal(37 * 37, LayerPixelsOf(editor, Blue).Count); // a square, closed back to its start
    }

    [Fact]
    public void Polygon_PressesWhileBuildingDoNotCommit_AndCommitClosesIt()
    {
        var editor = CreateEditor(new ShapeSettings { Kind = ShapeKind.Polygon, Outline = ShapeStyle.None, Fill = ShapeStyle.Solid });
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (40.5, 4.5));
        Script.Stroke(editor, PointerButton.Left, (30.5, 30.5), (40.5, 40.5)); // drag the corner into place

        Assert.Empty(LayerPixelsOf(editor, Blue));
        editor.CommitPending(); // Enter

        Assert.Contains((38, 10), LayerPixelsOf(editor, Blue)); // the triangle (4,4)-(40,4)-(40,40)
        Assert.DoesNotContain((10, 38), LayerPixelsOf(editor, Blue));
        Assert.True(editor.History.CanUndo);
        editor.Undo();
        Assert.Empty(LayerPixelsOf(editor, Blue));
    }

    [Fact]
    public void Polygon_ShiftKeepsASideAtAMultipleOf45Degrees()
    {
        var editor = CreateEditor(new ShapeSettings { Kind = ShapeKind.Polygon });
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (20.5, 4.5));

        editor.Apply(new PointerInput(PointerAction.Down, 30.5, 13.5, Shift: true));
        editor.Apply(new PointerInput(PointerAction.Up, 30.5, 13.5, Shift: true));
        Click(editor, 4.5, 4.5);

        Assert.Equal(new IntRect(4, 4, 27, 11), editor.Adornment!.Value.Frame); // the corner went to (30, 14)
    }

    [Fact]
    public void Polygon_ResizedByItsFrame_ScalesWithIt()
    {
        var editor = CreateEditor(new ShapeSettings { Kind = ShapeKind.Polygon, Outline = ShapeStyle.None, Fill = ShapeStyle.Solid });
        Script.Stroke(editor, PointerButton.Left, (4.5, 4.5), (20.5, 4.5));
        Click(editor, 20.5, 20.5);
        Click(editor, 4.5, 4.5);

        Script.Stroke(editor, PointerButton.Left, (21, 21), (41, 31)); // bottom-right handle
        editor.CommitPending();

        var filled = LayerPixelsOf(editor, Blue);
        Assert.Equal(new IntRect(4, 4, 37, 27), BoundsOf(filled).Union(new IntRect(4, 4, 37, 27)));
        Assert.Contains((40, 4), filled);
        Assert.Contains((40, 29), filled);
        Assert.Contains((39, 6), filled);
        Assert.DoesNotContain((6, 28), filled);
    }

    [Fact]
    public void ToolSwitchWhileBuilding_CommitsTheShape()
    {
        var editor = CreateEditor(new ShapeSettings { Kind = ShapeKind.Curve });
        Script.Stroke(editor, PointerButton.Left, (4.5, 30.5), (40.5, 30.5));

        editor.ActiveTool = new PencilTool();

        Assert.Contains((20, 30), LayerPixelsOf(editor, Red));
        Assert.True(editor.Overlay.IsEmpty);
    }

    [Fact]
    public void ShapeOutlines_FitTheirFrame()
    {
        foreach (var kind in BoxShapes.Select(row => row.Data))
        {
            var points = ShapeOutlines.Of(kind, 40, 25).SelectMany(c => c).ToList();
            Assert.Equal(0, points.Min(p => p.X), 6);
            Assert.Equal(40, points.Max(p => p.X), 6);
            Assert.Equal(0, points.Min(p => p.Y), 6);
            Assert.Equal(25, points.Max(p => p.Y), 6);
        }
    }
}
