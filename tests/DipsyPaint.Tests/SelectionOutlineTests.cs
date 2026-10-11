using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;
using DipsyPaint.Tools;

namespace DipsyPaint.Tests;

/// <summary>
/// Free-form and magic-wand selections show their actual outline, a dashed edge along the mask, instead of their
/// bounding rectangle (issue #111).
/// </summary>
public sealed class SelectionOutlineTests : IDisposable
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    // The selection options are app-wide, so each test starts and ends with the defaults.
    public SelectionOutlineTests() => ResetSettings();

    public void Dispose() => ResetSettings();

    private static void ResetSettings()
    {
        ToolRegistry.SelectionSettings.Shape = SelectionShape.Rectangle;
        ToolRegistry.SelectionSettings.Transparent = false;
        ToolRegistry.SelectionSettings.Tolerance = SelectionSettings.DefaultTolerance;
    }

    /// <summary>
    /// Shows a white document (64×48 unless given) with a red 20×20 square at (10, 10); with <paramref name="hole"/>, a
    /// white 6×6 hole at (17, 17) makes it a ring.
    /// </summary>
    private static MainWindow ShowWindow(bool hole = false, int width = 64, int height = 48)
    {
        var document = Document.CreateBlank(width, height);
        document.ActiveLayer.Pixels.WriteRegion(new IntRect(10, 10, 20, 20), Enumerable.Repeat(Red, 400).ToArray());
        if (hole)
        {
            document.ActiveLayer.Pixels.WriteRegion(new IntRect(17, 17, 6, 6), Enumerable.Repeat(Rgba.White, 36).ToArray());
        }

        var window = new MainWindow(document);
        window.Show();
        window.UpdateLayout();
        return window;
    }

    // A free-form triangle over the red square's top-left half: pixels with x + y ≤ 39 within it.
    private static MainWindow ShowWindowWithTriangleSelected()
    {
        var window = ShowWindow();
        ToolRegistry.SelectionSettings.Shape = SelectionShape.FreeForm;
        UseTool(window, SelectionStrings.Select);
        Drag(window, (10.5, 10.5), (29.5, 10.5), (10.5, 29.5));
        Assert.NotNull(window.Editor.Selection?.Mask);
        return window;
    }

    // The red ring, selected with the magic wand.
    private static MainWindow ShowWindowWithRingSelected(int width = 64, int height = 48)
    {
        var window = ShowWindow(hole: true, width, height);
        ToolRegistry.SelectionSettings.Shape = SelectionShape.MagicWand;
        UseTool(window, SelectionStrings.Select);
        Drag(window, (11.5, 11.5));
        Assert.Equal(new IntRect(10, 10, 20, 20), window.Editor.Selection?.Bounds);
        Assert.False(window.Editor.Selection!.Value.Contains(20, 20));
        return window;
    }

    private static void UseTool(Window window, string name)
    {
        window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();
    }

    private static DocumentCanvas Canvas(Window window) => window.FindControl<DocumentCanvas>("DocumentView")!;

    private static Point InWindow(Window window, double x, double y) => Canvas(window).TranslatePoint(new Point(x, y), window)!.Value;

    private static void Drag(Window window, params (double X, double Y)[] points)
    {
        window.MouseDown(InWindow(window, points[0].X, points[0].Y), MouseButton.Left);
        foreach (var (x, y) in points.Skip(1))
        {
            window.MouseMove(InWindow(window, x, y));
        }

        window.MouseUp(InWindow(window, points[^1].X, points[^1].Y), MouseButton.Left);
    }

    private static Rgba[] Snapshot(MainWindow window)
    {
        var pixels = new Rgba[64 * 48];
        window.Document.ActiveLayer.Pixels.ReadRegion(new IntRect(0, 0, 64, 48), pixels);
        return pixels;
    }

    /// <summary>A rendered frame of the window, read at document or window positions.</summary>
    private sealed class Rendered
    {
        private readonly Window _window;
        private readonly byte[] _bytes;
        private readonly int _rowBytes;

        public Rendered(Window window)
        {
            window.UpdateLayout();
            _window = window;
            using var frame = window.CaptureRenderedFrame()!;
            using var buffer = frame.Lock();
            _rowBytes = buffer.RowBytes;
            _bytes = new byte[_rowBytes * buffer.Size.Height];
            Marshal.Copy(buffer.Address, _bytes, 0, _bytes.Length);
        }

        /// <summary>The colour shown at window pixel (<paramref name="x"/>, <paramref name="y"/>), as (red, green, blue).</summary>
        public (int R, int G, int B) AtScreen(int x, int y)
        {
            var i = y * _rowBytes + x * 4; // RGBA
            return (_bytes[i], _bytes[i + 1], _bytes[i + 2]);
        }

        /// <summary>The colour shown at the centre of document pixel (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public (int R, int G, int B) At(int x, int y)
        {
            var point = InWindow(_window, x + 0.5, y + 0.5);
            return AtScreen((int)point.X, (int)point.Y);
        }

        /// <summary>The document pixels of a horizontal or vertical run, inclusive.</summary>
        public (int R, int G, int B)[] Run((int X, int Y) from, (int X, int Y) to)
        {
            var (dx, dy) = (Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));
            var colours = new List<(int, int, int)>();
            for (var p = from; ; p = (p.X + dx, p.Y + dy))
            {
                colours.Add(At(p.X, p.Y));
                if (p == to)
                {
                    return [.. colours];
                }
            }
        }

        /// <summary>All the colours shown over the canvas.</summary>
        public IEnumerable<(int R, int G, int B)> OverCanvas()
        {
            var canvas = Canvas(_window);
            var topLeft = canvas.TranslatePoint(default, _window)!.Value;
            var bottomRight = canvas.TranslatePoint(new Point(canvas.Bounds.Width, canvas.Bounds.Height), _window)!.Value;
            for (var y = (int)topLeft.Y; y < (int)bottomRight.Y; y++)
            {
                for (var x = (int)topLeft.X; x < (int)bottomRight.X; x++)
                {
                    yield return AtScreen(x, y);
                }
            }
        }
    }

    // The edge is black dashes on white: every pixel it covers is grey, from white to black.
    private static bool IsGrey((int R, int G, int B) c) => c.R == c.G && c.G == c.B;

    private static bool IsBlack((int R, int G, int B) c) => c is { R: < 32, G: < 32, B: < 32 };

    // The frame's blue, faint or not; the document is only red and white.
    private static bool IsBlue((int R, int G, int B) c) => c.B > c.R + 16 && c.B > c.G + 16;

    private static readonly (int R, int G, int B) RedShown = (255, 0, 0);
    private static readonly (int R, int G, int B) WhiteShown = (255, 255, 255);

    // A dashed edge runs along every pixel of a run: all grey, some black.
    private static void AssertEdge((int R, int G, int B)[] run)
    {
        Assert.All(run, c => Assert.True(IsGrey(c), $"{c} is not on the edge"));
        Assert.Contains(run, IsBlack);
    }

    [AvaloniaFact]
    public void FreeForm_ShowsItsEdge_NotItsBoundingRectangle()
    {
        var window = ShowWindowWithTriangleSelected();

        var frame = new Rendered(window);

        // Just outside the diagonal: red underneath, covered by the edge.
        AssertEdge([.. Enumerable.Range(11, 19).Select(x => frame.At(x, 40 - x))]);
        AssertEdge(frame.Run((10, 9), (29, 9)));  // along the top, just outside
        AssertEdge(frame.Run((9, 10), (9, 29)));  // along the left
        Assert.Equal(RedShown, frame.At(15, 15)); // inside, away from the edge
        Assert.Equal(RedShown, frame.At(27, 27)); // outside the triangle, inside its bounding rectangle
        Assert.Equal(WhiteShown, frame.At(30, 25)); // where the bounding rectangle's right side was drawn
        Assert.DoesNotContain(frame.OverCanvas(), IsBlue);
        Assert.False(window.Editor.History.CanUndo); // display only
    }

    [AvaloniaFact]
    public void MagicWand_ShowsItsEdge_WithTheHoleEdgedToo()
    {
        var window = ShowWindowWithRingSelected();

        var frame = new Rendered(window);

        AssertEdge(frame.Run((10, 9), (29, 9)));    // outer, top
        AssertEdge(frame.Run((30, 10), (30, 29)));  // outer, right
        AssertEdge(frame.Run((17, 17), (22, 17)));  // the hole's top, just inside it
        AssertEdge(frame.Run((17, 17), (17, 22)));  // the hole's left
        Assert.Equal(WhiteShown, frame.At(19, 19)); // the middle of the hole
        Assert.Equal(RedShown, frame.At(16, 19));   // the ring beside it
        Assert.DoesNotContain(frame.OverCanvas(), IsBlue);
    }

    [AvaloniaFact]
    public void ARectangleSelection_StillShowsItsBlueFrame()
    {
        var window = ShowWindow();
        UseTool(window, SelectionStrings.Select);
        Drag(window, (10.5, 10.5), (29.5, 29.5));

        Assert.Contains(new Rendered(window).Run((10, 9), (29, 9)), IsBlue);
    }

    [AvaloniaFact]
    public void OnlyMoveSelectedPixels_AddsAFaintBoundingRectangle_WithTheHandles()
    {
        var window = ShowWindowWithTriangleSelected();
        UseTool(window, SelectionStrings.MoveSelection);

        var moveSelection = new Rendered(window);
        AssertDiagonalEdge(moveSelection);
        Assert.DoesNotContain(moveSelection.OverCanvas(), IsBlue);

        UseTool(window, SelectionStrings.MoveSelectedPixels);
        var movePixels = new Rendered(window);

        AssertDiagonalEdge(movePixels);
        Assert.Contains(movePixels.OverCanvas(), c => c == (0, 103, 192)); // the handles' borders
        // The bounding rectangle's right side and bottom, over white, between the handles: faint dashes.
        (int R, int G, int B)[] side =
        [
            .. movePixels.Run((30, 14), (30, 15)), .. movePixels.Run((30, 24), (30, 25)),
            .. movePixels.Run((14, 30), (15, 30)), .. movePixels.Run((24, 30), (25, 30)),
        ];
        Assert.Contains(side, IsBlue);
        Assert.All(side, c => Assert.True(c == WhiteShown || (IsBlue(c) && c.R > 100), $"{c} is not faint"));

        // Clear of the handles at the bounding rectangle's corners, which are drawn over the edge.
        static void AssertDiagonalEdge(Rendered frame) => AssertEdge([.. Enumerable.Range(14, 13).Select(x => frame.At(x, 40 - x))]);
    }

    [AvaloniaFact]
    public void MoveTools_GrabInsideTheEdge_NotElsewhereInTheBoundingRectangle()
    {
        var window = ShowWindowWithTriangleSelected();
        UseTool(window, SelectionStrings.MoveSelection);
        var selection = window.Editor.Selection;

        window.MouseMove(InWindow(window, 25.5, 20.5)); // outside the triangle, inside its bounding rectangle
        Assert.Null(Canvas(window).Cursor);
        Drag(window, (25.5, 20.5), (35.5, 20.5));
        Assert.Equal(selection, window.Editor.Selection);

        window.MouseMove(InWindow(window, 12.5, 12.5));
        Assert.NotNull(Canvas(window).Cursor);
        Drag(window, (12.5, 12.5), (22.5, 12.5));
        Assert.Equal(new IntRect(20, 10, 20, 20), window.Editor.Selection?.Bounds);

        UseTool(window, SelectionStrings.MoveSelectedPixels);
        var before = Snapshot(window);
        Drag(window, (30.5, 24.5), (40.5, 24.5)); // outside the moved triangle, clear of the handles
        Assert.Equal(before, Snapshot(window));
        Assert.Null(window.Editor.Selection?.FloatingPixels);
    }

    [AvaloniaFact]
    public void TheEdge_FollowsMovesAndNudges()
    {
        var window = ShowWindowWithRingSelected();
        UseTool(window, SelectionStrings.MoveSelection);

        Drag(window, (11.5, 11.5), (21.5, 11.5)); // by (10, 0)
        var moved = new Rendered(window);
        AssertEdge(moved.Run((20, 9), (39, 9)));
        Assert.DoesNotContain(moved.Run((10, 9), (18, 9)), IsBlack);
        AssertEdge(moved.Run((27, 17), (32, 17))); // the hole's top

        window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(Key.Down, RawInputModifiers.None, PhysicalKey.None, null);
        var nudged = new Rendered(window);
        AssertEdge(nudged.Run((20, 10), (39, 10)));
        Assert.DoesNotContain(nudged.Run((22, 9), (37, 9)), IsBlack);

        UseTool(window, SelectionStrings.MoveSelectedPixels);
        Drag(window, (25.5, 16.5), (25.5, 26.5)); // the pixels, by (0, 10), clear of the handles
        Assert.Equal(new IntRect(20, 21, 20, 20), window.Editor.Selection?.Bounds);
        AssertEdge(new Rendered(window).Run((27, 28), (32, 28))); // the hole's top, clear of the handles
    }

    [AvaloniaFact]
    public void TheEdge_FollowsStretchingAndRotating()
    {
        var window = ShowWindowWithTriangleSelected();
        UseTool(window, SelectionStrings.MoveSelectedPixels);

        Drag(window, (30, 10), (40, 10)); // the top-right handle, 10 px to the right
        Assert.Equal(new IntRect(10, 10, 30, 20), window.Editor.Selection?.Bounds);
        var stretched = new Rendered(window); // the stretched top, between the handles
        AssertEdge(stretched.Run((14, 9), (20, 9)));
        AssertEdge(stretched.Run((29, 9), (35, 9)));

        var editor = window.Editor;
        var (hx, hy) = editor.Adornment!.Value.RotateHandle(editor.HandleTolerance);
        var centre = (X: 25.0, Y: 20.0);
        var radius = centre.Y - hy;
        window.MouseDown(InWindow(window, hx, hy), MouseButton.Left);
        window.MouseMove(InWindow(window, centre.X + radius * Math.Sin(Math.PI / 4), centre.Y - radius * Math.Cos(Math.PI / 4)));

        var turning = new Rendered(window);
        Assert.NotNull(((SelectionTool)editor.ActiveTool!).PreviewTurn);
        Assert.DoesNotContain(turning.Run((17, 9), (25, 9)), IsBlack); // the top edge has turned away
        Assert.Contains(turning.OverCanvas(), IsBlack);

        window.MouseUp(InWindow(window, centre.X + radius * Math.Sin(Math.PI / 4), centre.Y - radius * Math.Cos(Math.PI / 4)), MouseButton.Left);
        Assert.Contains(new Rendered(window).OverCanvas(), IsBlack);
    }

    [AvaloniaTheory]
    [InlineData(4.0)]
    [InlineData(8.0)]
    public void TheEdge_IsOneScreenPixel_JustOutsideTheSelection_AtAnyZoom(double zoom)
    {
        var window = ShowWindowWithRingSelected();
        window.FindControl<ZoomControl>("Zoom")!.ZoomFactor = zoom;
        window.UpdateLayout();

        var frame = new Rendered(window);
        var z = (int)zoom;

        // Down the ring's outer left side (x = 10): the last screen column of pixel 9 is the edge.
        var left = (int)Math.Round(InWindow(window, 10, 0).X);
        var top = (int)Math.Round(InWindow(window, 0, 12).Y);
        var column = Enumerable.Range(top, 6 * z).Select(y => frame.AtScreen(left - 1, y)).ToArray();
        AssertEdge(column);
        Assert.All(Enumerable.Range(top, 6 * z), y => Assert.Equal(WhiteShown, frame.AtScreen(left - 2, y)));
        Assert.All(Enumerable.Range(top, 6 * z), y => Assert.Equal(RedShown, frame.AtScreen(left, y)));

        // Down the hole's left side (x = 17): the first screen column of pixel 17, inside the hole.
        var hole = (int)Math.Round(InWindow(window, 17, 0).X);
        var holeTop = (int)Math.Round(InWindow(window, 0, 18).Y);
        AssertEdge([.. Enumerable.Range(holeTop, 4 * z).Select(y => frame.AtScreen(hole, y))]);
        Assert.All(Enumerable.Range(holeTop, 4 * z), y => Assert.Equal(RedShown, frame.AtScreen(hole - 1, y)));
        Assert.All(Enumerable.Range(holeTop, 4 * z), y => Assert.Equal(WhiteShown, frame.AtScreen(hole + 1, y)));
    }

    [AvaloniaFact]
    public void TheEdge_FollowsScrolling()
    {
        var window = ShowWindowWithRingSelected(160, 120);
        window.FindControl<ZoomControl>("Zoom")!.ZoomFactor = 8;
        window.UpdateLayout();
        var scroller = window.FindControl<ScrollViewer>("CanvasScroll")!;
        scroller.Offset = new Vector(0, 1000); // to the bottom: the ring is above the window
        window.UpdateLayout();
        Assert.True(InWindow(window, 0, 31).Y < 0);
        new Rendered(window);

        scroller.Offset = new Vector(0, 100);
        var frame = new Rendered(window);

        // Down the hole's left side (x = 17), as above.
        var hole = (int)Math.Round(InWindow(window, 17, 0).X);
        var holeTop = (int)Math.Round(InWindow(window, 0, 18).Y);
        AssertEdge([.. Enumerable.Range(holeTop, 32).Select(y => frame.AtScreen(hole, y))]);
        Assert.All(Enumerable.Range(holeTop, 32), y => Assert.Equal(RedShown, frame.AtScreen(hole - 1, y)));
    }

    [AvaloniaFact]
    public void TheEdge_FollowsInvert_AndSelectAllShowsTheFrame()
    {
        var window = ShowWindowWithTriangleSelected();
        var menu = window.GetLogicalDescendants().OfType<SelectionMenu>().Single();

        menu.FindControl<MenuItem>("InvertItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        window.UpdateLayout();

        Assert.False(window.Editor.Selection!.Value.Contains(15, 15));
        var inverted = new Rendered(window);
        AssertEdge([.. Enumerable.Range(11, 19).Select(x => inverted.At(x, 39 - x))]); // the triangle's diagonal, now outside
        Assert.Equal(RedShown, inverted.At(15, 15));
        Assert.DoesNotContain(inverted.OverCanvas(), IsBlue);

        menu.SelectAll();
        window.UpdateLayout();

        var all = new Rendered(window);
        Assert.Null(window.Editor.Selection?.Mask);
        Assert.DoesNotContain(all.Run((11, 28), (28, 11)), IsBlack);
    }
}