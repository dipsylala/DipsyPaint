using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine.Tests;

/// <summary>
/// Fast transform drags (ticket 38): while the rotate handle is dragged the overlay shows a quick
/// preview, and releasing it gives exactly what a turn by the same angle gives; large composites,
/// stretches and shapes are computed in parallel with the same pixels as before.
/// </summary>
public class TransformDragTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    private static Rgba Marker(int x, int y) => new((byte)(x * 7), (byte)(y * 5), (byte)(x ^ y), 255);

    /// <summary>A document whose pixels are <see cref="Marker"/>s.</summary>
    private static DocumentEditor CreateEditor(int width, int height, ITool? tool = null)
    {
        var editor = new DocumentEditor(Document.CreateBlank(width, height)) { ActiveTool = tool, HandleTolerance = 1 };
        var layer = editor.Document.ActiveLayer.Pixels;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                layer.SetPixel(x, y, Marker(x, y));
            }
        }

        editor.Compositor.Update(new IntRect(0, 0, width, height));
        return editor;
    }

    private static Rgba[] Snapshot(DocumentEditor editor)
    {
        var pixels = editor.Document.ActiveLayer.Pixels;
        var copy = new Rgba[pixels.Width * pixels.Height];
        pixels.ReadRegion(new IntRect(0, 0, pixels.Width, pixels.Height), copy);
        return copy;
    }

    // A free-form shape (a triangle), so the mask is turned too.
    private static bool[] Triangle(int width, int height)
    {
        var mask = new bool[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x <= y * width / height; x++)
            {
                mask[y * width + x] = true;
            }
        }

        return mask;
    }

    // Presses the rotate handle; returns the pointer for a turn of `degrees` from the press (Shift snaps it exactly).
    private static Func<PointerAction, double, PointerInput> PressRotateHandle(DocumentEditor editor)
    {
        var frame = editor.Adornment!.Value.Frame;
        double centreX = frame.X + frame.Width / 2.0, centreY = frame.Y + frame.Height / 2.0;
        var (x, y) = editor.Adornment!.Value.RotateHandle(editor.HandleTolerance);
        editor.Apply(new PointerInput(PointerAction.Down, x, y));
        var radius = centreY - y;
        return (action, degrees) => new PointerInput(
            action, centreX + radius * Math.Sin(degrees * Math.PI / 180), centreY - radius * Math.Cos(degrees * Math.PI / 180), Shift: true);
    }

    [Fact]
    public void RotateDrag_OfASelection_GivesExactlyWhatATurnByTheSameAngleGives()
    {
        var expected = CreateEditor(80, 60, new SelectionTool());
        expected.Select(new IntRect(10, 12, 50, 30), Triangle(50, 30));
        expected.Rotate(30);
        var editor = CreateEditor(80, 60, new SelectionTool());
        editor.Select(new IntRect(10, 12, 50, 30), Triangle(50, 30));

        var pointer = PressRotateHandle(editor);
        editor.Apply(pointer(PointerAction.Move, 15));
        editor.Apply(pointer(PointerAction.Move, 45));
        editor.Apply(pointer(PointerAction.Move, 30));
        editor.Apply(pointer(PointerAction.Up, 30));

        var (selection, reference) = (editor.Selection!.Value, expected.Selection!.Value);
        Assert.Equal(reference.Bounds, selection.Bounds);
        Assert.Equal(reference.FloatingPixels, selection.FloatingPixels);
        Assert.Equal(reference.Mask, selection.Mask);
        Assert.Equal(expected.Compositor.Pixels.ToArray(), editor.Compositor.Pixels.ToArray());
        editor.CommitPending();
        expected.CommitPending();
        Assert.Equal(Snapshot(expected), Snapshot(editor));
    }

    [Fact]
    public void RotateDrag_WhileDragging_ShowsTheTurnedFrame_AndKeepsTheSelectionAsAtThePress()
    {
        var expected = CreateEditor(80, 60, new SelectionTool());
        expected.Select(new IntRect(10, 12, 50, 30));
        expected.Rotate(30);
        var editor = CreateEditor(80, 60, new SelectionTool());
        editor.Select(new IntRect(10, 12, 50, 30));
        var changes = 0;
        editor.SelectionChanged += (_, _) => changes++;

        var pointer = PressRotateHandle(editor);
        editor.Apply(pointer(PointerAction.Move, 30));

        Assert.Equal(expected.Adornment, editor.Adornment);
        Assert.Equal(new IntRect(10, 12, 50, 30), editor.Selection!.Value.Bounds);
        Assert.Equal(0, changes);
        editor.Apply(pointer(PointerAction.Up, 30));
        Assert.Equal(expected.Selection!.Value.Bounds, editor.Selection!.Value.Bounds);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void RotateDrag_PreviewAtAQuarterTurn_ShowsExactlyTheTurnedPixels()
    {
        // At a quarter turn every pixel centre lands on one, so the nearest-neighbour preview is exact.
        var expected = CreateEditor(80, 60, new SelectionTool());
        expected.Select(new IntRect(10, 12, 40, 30), Triangle(40, 30));
        expected.Rotate(90);
        var editor = CreateEditor(80, 60, new SelectionTool());
        editor.Select(new IntRect(10, 12, 40, 30), Triangle(40, 30));

        var pointer = PressRotateHandle(editor);
        editor.Apply(pointer(PointerAction.Move, 90));

        Assert.Equal(expected.Compositor.Pixels.ToArray(), editor.Compositor.Pixels.ToArray());
    }

    [Fact]
    public void RotateDrag_Preview_TakesTheSourcePixelNearestEachPreimage()
    {
        // On a gradient whose channels are the pixel's coordinates, bilinear sampling gives the preimage's
        // coordinates rounded, as the nearest pixel does: inside the turned area the preview matches the
        // exact turn, give or take rounding at halves.
        DocumentEditor Create()
        {
            var editor = new DocumentEditor(Document.CreateBlank(200, 160)) { ActiveTool = new SelectionTool(), HandleTolerance = 1 };
            for (var y = 0; y < 160; y++)
            {
                for (var x = 0; x < 200; x++)
                {
                    editor.Document.ActiveLayer.Pixels.SetPixel(x, y, new Rgba((byte)x, (byte)y, 0, 255));
                }
            }

            editor.SelectAll(); // lifted off the only layer, so the composite shows the turned pixels alone
            return editor;
        }

        var expected = Create();
        expected.Rotate(30);
        var editor = Create();
        var pointer = PressRotateHandle(editor);
        editor.Apply(pointer(PointerAction.Move, 30));

        var (exact, preview) = (expected.Compositor.Pixels.ToArray(), editor.Compositor.Pixels.ToArray());
        var (compared, differing) = (0, 0);
        for (var i = 0; i < exact.Length; i++)
        {
            if (exact[i].A == 255 && preview[i].A == 255)
            {
                compared++;
                Assert.InRange(Math.Abs(exact[i].R - preview[i].R), 0, 1);
                Assert.InRange(Math.Abs(exact[i].G - preview[i].G), 0, 1);
                differing += exact[i] == preview[i] ? 0 : 1;
            }
        }

        Assert.True(compared > 20000);
        Assert.True(differing < compared / 100, $"{differing} of {compared} pixels differ");
    }

    [Fact]
    public void RotateDrag_CommittedWhileDragging_CommitsTheExactTurn_AsOneStep()
    {
        var expected = CreateEditor(80, 60, new SelectionTool());
        expected.Select(new IntRect(10, 12, 50, 30));
        expected.Rotate(45);
        expected.CommitPending();
        var editor = CreateEditor(80, 60, new SelectionTool());
        var before = Snapshot(editor);
        editor.Select(new IntRect(10, 12, 50, 30));

        var pointer = PressRotateHandle(editor);
        editor.Apply(pointer(PointerAction.Move, 45));
        editor.CommitPending(); // e.g. another tool chosen mid-drag
        editor.Apply(pointer(PointerAction.Up, 45));

        Assert.Null(editor.Selection);
        Assert.Equal(Snapshot(expected), Snapshot(editor));
        editor.Undo();
        Assert.Equal(before, Snapshot(editor));
    }

    [Fact]
    public void RotateDrag_OfATransparentSelection_PreviewLeavesOutTheSecondaryColour()
    {
        var expected = CreateEditor(80, 60, new SelectionTool());
        expected.Select(new IntRect(10, 12, 40, 30));
        expected.Rotate(90);
        var shown = expected.Compositor.Pixels[27 * 80 + 30];
        var editor = CreateEditor(80, 60, new SelectionTool(new SelectionSettings { Transparent = true }));
        editor.Select(new IntRect(10, 12, 40, 30));

        var pointer = PressRotateHandle(editor);
        editor.Apply(pointer(PointerAction.Move, 90));
        Assert.Equal(shown, editor.Compositor.Pixels[27 * 80 + 30]);
        editor.SecondaryColor = shown; // re-shows the pending pixels without it

        // Lifted from the only layer, so nothing is under the hidden pixel.
        Assert.Equal(Rgba.Transparent, editor.Compositor.Pixels[27 * 80 + 30]);
    }

    [Fact]
    public void RotateDrag_OfATextBox_GivesExactlyWhatATurnByTheSameAngleGives()
    {
        DocumentEditor Create()
        {
            var tool = new TextTool(new TextSettings(), new StripesRenderer());
            var editor = new DocumentEditor(Document.CreateBlank(100, 100)) { ActiveTool = tool, PrimaryColor = Red, HandleTolerance = 1 };
            Script.Stroke(editor, PointerButton.Left, (20.5, 30.5), (69.5, 59.5));
            tool.Text = "x";
            editor.RefreshPending();
            return editor;
        }

        var expected = Create();
        expected.Rotate(30);
        var editor = Create();

        var pointer = PressRotateHandle(editor);
        editor.Apply(pointer(PointerAction.Move, 15));
        editor.Apply(pointer(PointerAction.Move, 30));
        editor.Apply(pointer(PointerAction.Up, 30));

        Assert.Equal(expected.Adornment, editor.Adornment);
        Assert.Equal(expected.Compositor.Pixels.ToArray(), editor.Compositor.Pixels.ToArray());
    }

    [Fact]
    public void StretchDrag_OfALargeSelection_TakesTheNearestPixels()
    {
        var editor = CreateEditor(400, 300, new SelectionTool());
        editor.Select(new IntRect(0, 0, 300, 250));

        Script.Stroke(editor, PointerButton.Left, (300, 250), (380, 290)); // the bottom-right handle

        var selection = editor.Selection!.Value;
        Assert.Equal(new IntRect(0, 0, 380, 290), selection.Bounds);
        for (var y = 0; y < 290; y++)
        {
            for (var x = 0; x < 380; x++)
            {
                Assert.Equal(Marker(x * 300 / 380, y * 250 / 290), selection.FloatingPixels![y * 380 + x]);
            }
        }
    }

    [Fact]
    public void Compositor_ALargeRegion_GivesTheSamePixelsAsSmallOnes()
    {
        var document = Document.CreateBlank(400, 300);
        var random = new Random(38);
        for (var i = 0; i < 3; i++)
        {
            var layer = i == 0 ? document.Layers[0] : document.AddLayer();
            for (var y = 0; y < 300; y++)
            {
                for (var x = 0; x < 400; x++)
                {
                    // Opaque, transparent and part-transparent pixels, premultiplied.
                    var alpha = (byte)(random.Next(3) switch { 0 => 255, 1 => 0, _ => random.Next(256) });
                    layer.Pixels.SetPixel(x, y, new Rgba((byte)random.Next(alpha + 1), (byte)random.Next(alpha + 1), (byte)random.Next(alpha + 1), alpha));
                }
            }
        }

        document.ActiveLayer = document.Layers[1];
        var whole = new Compositor(document); // composites the whole canvas at once
        var tiled = new Compositor(document);
        for (var y = 0; y < 300; y += 50)
        {
            for (var x = 0; x < 400; x += 50)
            {
                tiled.UpdateActive(new IntRect(x, y, 50, 50));
            }
        }

        Assert.Equal(tiled.Pixels.ToArray(), whole.Pixels.ToArray());
    }

    [Fact]
    public void Over_AnOpaquePixelReplaces_AndATransparentOneChangesNothing()
    {
        var random = new Random(38);
        for (var i = 0; i < 1000; i++)
        {
            var alpha = (byte)random.Next(256);
            var background = new Rgba((byte)random.Next(alpha + 1), (byte)random.Next(alpha + 1), (byte)random.Next(alpha + 1), alpha);
            Assert.Equal(Red, Red.Over(background));
            Assert.Equal(background, Rgba.Transparent.Over(background));
        }
    }

    [Fact]
    public void Rotate_ALargeRectangleShape_ByAQuarterTurn_KeepsItsOutlineAndFill()
    {
        var settings = new ShapeSettings { Kind = ShapeKind.Rectangle, Outline = ShapeStyle.Solid, Fill = ShapeStyle.Solid, Size = 3 };
        var blue = new Rgba(0, 0, 255, 255);
        var editor = new DocumentEditor(Document.CreateBlank(500, 500)) { ActiveTool = new ShapeTool(settings), PrimaryColor = Red, SecondaryColor = blue };
        Script.Stroke(editor, PointerButton.Left, (50.5, 100.5), (449.5, 399.5)); // 400×300

        editor.Rotate(90);

        var frame = editor.Adornment!.Value.Frame;
        Assert.Equal(new IntRect(100, 50, 300, 400), frame);
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var outline = x < 3 || y < 3 || x >= frame.Width - 3 || y >= frame.Height - 3;
                Assert.Equal(outline ? Red : blue, editor.Compositor.Pixels[(frame.Y + y) * 500 + frame.X + x]);
            }
        }
    }

    /// <summary>Renders any text as horizontal stripes of the foreground colour, so a turn shows.</summary>
    private sealed class StripesRenderer : ITextRenderer
    {
        public PixelImage Render(TextRequest request)
        {
            var pixels = new Rgba[request.Width * request.MinHeight];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = i / request.Width % 4 < 2 ? request.Foreground : Rgba.Transparent;
            }

            return new PixelImage(request.Width, request.MinHeight, pixels);
        }
    }
}
