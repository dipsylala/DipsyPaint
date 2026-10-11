using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;
using DipsyPaint.Tools;

namespace DipsyPaint.Tests;

public class ShapeTests
{
    /// <summary>Shows a window with the shape tool active, drawing outlined rectangles (the app-wide defaults).</summary>
    private static MainWindow ShowWindowWithRectangleTool()
    {
        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        window.UpdateLayout();
        Click(window, ShapeStrings.Shapes);
        window.UpdateLayout();
        var options = window.GetLogicalDescendants().OfType<ShapeOptions>().Single();
        options.FindControl<ComboBox>("OutlineBox")!.SelectedIndex = (int)ShapeStyle.Solid;
        options.FindControl<ComboBox>("FillBox")!.SelectedIndex = (int)ShapeStyle.None;
        options.FindControl<ComboBox>("SizeBox")!.SelectedIndex = 0;
        Click(window, ShapeStrings.Rectangle);
        window.UpdateLayout(); // the command bar may rewrap, moving the canvas
        return window;
    }

    private static void Click(Window window, string buttonName) =>
        window.GetLogicalDescendants().OfType<ToggleButton>()
            .Single(b => AutomationProperties.GetName(b) == buttonName)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static Point InWindow(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static void Drag(Window window, (double X, double Y) from, (double X, double Y) to)
    {
        window.MouseDown(InWindow(window, from.X, from.Y), MouseButton.Left);
        window.MouseMove(InWindow(window, to.X, to.Y));
        window.MouseUp(InWindow(window, to.X, to.Y), MouseButton.Left);
    }

    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
    }

    /// <summary>Reads one pixel of the rendered frame as (R, G, B).</summary>
    private static (byte R, byte G, byte B) RenderedPixel(Window window, Point point)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();
        var bytes = new byte[4];
        var offset = (int)point.Y * buffer.RowBytes + (int)point.X * 4;
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address + offset, bytes, 0, 4);
        return buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888 ? (bytes[2], bytes[1], bytes[0]) : (bytes[0], bytes[1], bytes[2]);
    }

    private static List<(int X, int Y)> BlackLayerPixels(MainWindow window)
    {
        var layer = window.Document.ActiveLayer.Pixels;
        var found = new List<(int, int)>();
        for (var y = 0; y < layer.Height; y++)
        {
            for (var x = 0; x < layer.Width; x++)
            {
                if (layer.GetPixel(x, y) == Rgba.Black)
                {
                    found.Add((x, y));
                }
            }
        }

        return found;
    }

    [AvaloniaFact]
    public void ShapesToolComesFromTheRegistryWithItsOptions()
    {
        var window = ShowWindowWithRectangleTool();

        Assert.Contains(ToolRegistry.Tools, t => t.Id == "shapes");
        Assert.IsType<ShapeTool>(window.Editor.ActiveTool);
        var options = window.GetLogicalDescendants().OfType<ShapeOptions>().Single();
        Assert.True(options.IsEffectivelyVisible);
        Assert.Equal(23, options.GetLogicalDescendants().OfType<ToggleButton>().Count());
    }

    [AvaloniaFact]
    public void GalleryOffersEveryShapeOfReferencePaintInItsOrder()
    {
        var window = ShowWindowWithRectangleTool();
        var options = window.GetLogicalDescendants().OfType<ShapeOptions>().Single();

        var names = options.FindControl<UniformGrid>("ShapeButtons")!.Children.Select(b => AutomationProperties.GetName(b)).ToList();

        Assert.Equal(
            [
                ShapeStrings.Line, ShapeStrings.Curve, ShapeStrings.Ellipse, ShapeStrings.Rectangle, ShapeStrings.RoundedRectangle,
                ShapeStrings.Polygon, ShapeStrings.Triangle, ShapeStrings.RightTriangle, ShapeStrings.Diamond, ShapeStrings.Pentagon,
                ShapeStrings.Hexagon, ShapeStrings.RightArrow, ShapeStrings.LeftArrow, ShapeStrings.UpArrow, ShapeStrings.DownArrow,
                ShapeStrings.FourPointStar, ShapeStrings.FivePointStar, ShapeStrings.SixPointStar, ShapeStrings.RoundedRectangularCallout,
                ShapeStrings.OvalCallout, ShapeStrings.CloudCallout, ShapeStrings.Heart, ShapeStrings.Lightning,
            ],
            names);
        Assert.All(options.FindControl<UniformGrid>("ShapeButtons")!.Children.OfType<ToggleButton>(), b =>
        {
            Assert.True(b.IsEffectivelyVisible);
            Assert.Equal(b.Foreground, ((Avalonia.Controls.Shapes.Path)b.Content!).Stroke); // the icon follows the button's text colour
        });
    }

    [AvaloniaTheory]
    [InlineData(nameof(ShapeStrings.Line), false)]
    [InlineData(nameof(ShapeStrings.Curve), false)]
    [InlineData(nameof(ShapeStrings.Polygon), true)]
    [InlineData(nameof(ShapeStrings.Heart), true)]
    public void PickingAShapeSelectsItAndFillIsOnlyOfferedForShapesWithAnInside(string name, bool canFill)
    {
        var window = ShowWindowWithRectangleTool();
        var options = window.GetLogicalDescendants().OfType<ShapeOptions>().Single();
        var label = ShapeStrings.ResourceManager.GetString(name)!;

        Click(window, label);

        var buttons = options.FindControl<UniformGrid>("ShapeButtons")!.Children.OfType<ToggleButton>().ToList();
        Assert.Equal([label], buttons.Where(b => b.IsChecked == true).Select(b => AutomationProperties.GetName(b)));
        Assert.Equal(canFill, options.FindControl<ComboBox>("FillBox")!.IsEnabled);
    }

    [AvaloniaFact]
    public void PolygonIsBuiltByClicksAndClosedByADoubleClick()
    {
        var window = ShowWindowWithRectangleTool();
        Click(window, ShapeStrings.Polygon);
        window.UpdateLayout();

        Drag(window, (10.5, 10.5), (40.5, 10.5));
        Drag(window, (40.5, 40.5), (40.5, 40.5));
        Assert.Null(window.Editor.Adornment);    // still open: presses add corners
        Assert.True(window.Editor.HasPending);
        Drag(window, (10.5, 40.5), (10.5, 40.5));
        Drag(window, (10.5, 40.5), (10.5, 40.5)); // the double-click closes it

        Assert.NotNull(window.Editor.Adornment);
        Press(window, Key.Enter);
        Assert.Equal(4 * 30, BlackLayerPixels(window).Count); // a 31×31 square's outline
    }

    [AvaloniaFact]
    public void EscDiscardsACurveWhileItIsBeingBent_AndEnterCommitsOne()
    {
        var window = ShowWindowWithRectangleTool();
        Click(window, ShapeStrings.Curve);
        window.UpdateLayout();

        Drag(window, (10.5, 30.5), (50.5, 30.5));
        Assert.Equal((0, 0, 0), RenderedPixel(window, InWindow(window, 30.5, 30.5)));
        Press(window, Key.Escape);

        Assert.False(window.Editor.HasPending);
        Assert.Equal((255, 255, 255), RenderedPixel(window, InWindow(window, 30.5, 30.5)));
        Assert.False(window.Editor.History.CanUndo);

        Drag(window, (10.5, 30.5), (50.5, 30.5));
        Drag(window, (30.5, 10.5), (30.5, 10.5)); // one bend
        Press(window, Key.Enter);

        Assert.False(window.Editor.HasPending);
        Assert.Contains((30, 15), BlackLayerPixels(window)); // pulled three quarters of the way up
        Assert.True(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void GalleryIsTwoRowsOnTopAndANarrowColumnOnASide()
    {
        var window = ShowWindowWithRectangleTool();
        var gallery = window.GetLogicalDescendants().OfType<ShapeOptions>().Single().FindControl<UniformGrid>("ShapeButtons")!;
        Assert.Equal(12, gallery.Columns);

        window.CommandBarLayout.Move("tools", DipsyPaint.Layout.DockEdge.Left, 0);
        window.UpdateLayout();

        Assert.Equal(4, gallery.Columns);
        Assert.InRange(gallery.Bounds.Width, 1, 4 * 28);
    }

    [AvaloniaFact]
    public void DrawingARectangleAndPressingEnterCommitsItAsOneUndoableStep()
    {
        var window = ShowWindowWithRectangleTool();

        Drag(window, (10.5, 10.5), (40.5, 30.5));

        Assert.Empty(BlackLayerPixels(window));                                   // pending: not on the layer yet
        Assert.Equal((0, 0, 0), RenderedPixel(window, InWindow(window, 20.5, 10.5))); // but shown
        Assert.Equal((255, 255, 255), RenderedPixel(window, InWindow(window, 10.5, 10.5))); // under the white handle

        Press(window, Key.Enter);

        Assert.Equal(2 * 31 + 2 * 19, BlackLayerPixels(window).Count);
        Assert.Null(window.Editor.Adornment);
        Assert.Equal((0, 0, 0), RenderedPixel(window, InWindow(window, 10.5, 10.5))); // handle gone

        window.KeyPress(Key.Z, Command(window), PhysicalKey.Z, null);
        window.KeyRelease(Key.Z, Command(window), PhysicalKey.Z, null);

        Assert.Empty(BlackLayerPixels(window));
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void DraggingAHandleResizesThePendingRectangle_AndClickingOutsideCommitsIt()
    {
        var window = ShowWindowWithRectangleTool();
        Drag(window, (10.5, 10.5), (30.5, 30.5));

        Drag(window, (31, 31), (41, 36)); // bottom-right handle
        Drag(window, (55.5, 5.5), (55.5, 5.5)); // a click outside

        var pixels = BlackLayerPixels(window);
        Assert.Contains((40, 35), pixels);
        Assert.DoesNotContain((30, 30), pixels);
        Assert.True(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void EscDiscardsThePendingRectangle()
    {
        var window = ShowWindowWithRectangleTool();
        Drag(window, (10.5, 10.5), (40.5, 30.5));

        Press(window, Key.Escape);

        Assert.Null(window.Editor.Adornment);
        Assert.Equal((255, 255, 255), RenderedPixel(window, InWindow(window, 20.5, 10.5)));
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void SwitchingToolsCommitsThePendingRectangle()
    {
        var window = ShowWindowWithRectangleTool();
        Drag(window, (10.5, 10.5), (40.5, 30.5));

        Press(window, Key.P);

        Assert.IsType<PencilTool>(window.Editor.ActiveTool);
        Assert.NotEmpty(BlackLayerPixels(window));
    }

    // Ctrl on Windows/Linux, Cmd on macOS.
    private static RawInputModifiers Command(Visual visual) =>
        (RawInputModifiers)visual.GetPlatformSettings()!.HotkeyConfiguration.CommandModifiers;
}
