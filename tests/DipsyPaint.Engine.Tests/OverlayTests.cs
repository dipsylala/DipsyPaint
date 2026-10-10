namespace DipsyPaint.Engine.Tests;

public class OverlayTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);
    private static readonly Rgba HalfBlue = new(0, 0, 128, 128);

    [Fact]
    public void Compositor_ShowsTheOverlayAboveTheActiveLayerWithoutChangingIt()
    {
        var document = Document.CreateBlank(4, 1);
        var overlay = new Overlay();
        var compositor = new Compositor(document, overlay);

        var dirty = overlay.Show(new IntRect(1, 0, 2, 1), [Red, HalfBlue]);
        compositor.Update(dirty);

        Assert.Equal(new IntRect(1, 0, 2, 1), dirty);
        Assert.Equal([Rgba.White, Red, new Rgba(127, 127, 255, 255), Rgba.White], compositor.Pixels.ToArray());
        Assert.Equal(Rgba.White, document.ActiveLayer.Pixels.GetPixel(1, 0));
    }

    [Fact]
    public void Compositor_PutsTheOverlayBelowLayersAboveTheActiveOne()
    {
        var document = Document.CreateBlank(1, 1);
        var bottom = document.ActiveLayer;
        document.AddLayer().Pixels.SetPixel(0, 0, Rgba.Black);
        document.ActiveLayer = bottom;
        var overlay = new Overlay();
        var compositor = new Compositor(document, overlay);

        compositor.Update(overlay.Show(new IntRect(0, 0, 1, 1), [Red]));

        Assert.Equal(Rgba.Black, compositor.Pixels[0]);
    }

    [Fact]
    public void Show_ReportsOldAndNewBounds_AndClearRemovesTheContent()
    {
        var document = Document.CreateBlank(4, 1);
        var overlay = new Overlay();
        var compositor = new Compositor(document, overlay);
        Rgba[] pixel = [Red];
        overlay.Show(new IntRect(0, 0, 1, 1), pixel);

        var moved = overlay.Show(new IntRect(3, 0, 1, 1), pixel);
        compositor.Update(moved);
        Assert.Equal(new IntRect(0, 0, 4, 1), moved);
        Assert.Equal([Rgba.White, Rgba.White, Rgba.White, Red], compositor.Pixels.ToArray());

        compositor.Update(overlay.Clear());
        Assert.True(overlay.IsEmpty);
        Assert.Equal(Rgba.White, compositor.Pixels[3]);
    }

    [Fact]
    public void DrawOnto_CompositesTheOnCanvasPartOntoTheLayer()
    {
        var document = Document.CreateBlank(2, 1);
        var overlay = new Overlay();
        overlay.Show(new IntRect(-1, 0, 2, 1), [Red, HalfBlue]);

        var changed = overlay.DrawOnto(document.ActiveLayer.Pixels);

        Assert.Equal(new IntRect(0, 0, 1, 1), changed);
        Assert.Equal(new Rgba(127, 127, 255, 255), document.ActiveLayer.Pixels.GetPixel(0, 0));
        Assert.Equal(Rgba.White, document.ActiveLayer.Pixels.GetPixel(1, 0));
        Assert.False(overlay.IsEmpty); // committing is the tool's call; the overlay keeps its content
    }

    [Fact]
    public void Adornment_HitTestsHandlesThenTheInside()
    {
        var adornment = new Adornment(new IntRect(10, 10, 20, 10));

        Assert.Equal(FrameHandle.TopLeft, adornment.HitTest(9, 11, tolerance: 2));
        Assert.Equal(FrameHandle.Right, adornment.HitTest(30, 15, tolerance: 2));
        Assert.Equal(FrameHandle.Bottom, adornment.HitTest(20, 21.5, tolerance: 2));
        Assert.Equal(FrameHandle.Move, adornment.HitTest(15, 15, tolerance: 2));
        Assert.Equal(FrameHandle.None, adornment.HitTest(40, 15, tolerance: 2));
        Assert.Equal(8, adornment.Handles.Count());
    }

    [Fact]
    public void Adornment_DragMovesOnlyTheDraggedSides()
    {
        Assert.Equal(((2, 1), (10, 8)), Adornment.Drag((0, 0), (10, 8), FrameHandle.TopLeft, 2, 1));
        Assert.Equal(((0, 0), (13, 8)), Adornment.Drag((0, 0), (10, 8), FrameHandle.Right, 3, 5));
        Assert.Equal(((10, 0), (-3, 8)), Adornment.Drag((10, 0), (0, 8), FrameHandle.Left, -3, 5)); // corners in either order
        Assert.Equal(((0, 0), (-4, 8)), Adornment.Drag((0, 0), (10, 8), FrameHandle.Right, -14, 0)); // past the left: flips
        Assert.Equal(((5, 5), (15, 13)), Adornment.Drag((0, 0), (10, 8), FrameHandle.Move, 5, 5));
    }
}
