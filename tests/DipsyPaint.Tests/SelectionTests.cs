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

public class SelectionTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    /// <summary>Shows a 64×48 white document with a red 20×20 square at (10, 10) and the rectangle selection tool active.</summary>
    private static MainWindow ShowWindowWithSelectionTool()
    {
        var document = Document.CreateBlank(64, 48);
        document.ActiveLayer.Pixels.WriteRegion(new IntRect(10, 10, 20, 20), Enumerable.Repeat(Red, 400).ToArray());
        var window = new MainWindow(document);
        window.Show();
        window.UpdateLayout();
        window.GetLogicalDescendants().OfType<ToggleButton>()
            .Single(b => AutomationProperties.GetName(b) == SelectionStrings.RectangleSelection)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        return window;
    }

    private static Point InWindow(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static void Drag(Window window, (double X, double Y) from, (double X, double Y) to)
    {
        window.MouseDown(InWindow(window, from.X, from.Y), MouseButton.Left);
        window.MouseMove(InWindow(window, to.X, to.Y));
        window.MouseUp(InWindow(window, to.X, to.Y), MouseButton.Left);
    }

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
    }

    private static Rgba LayerPixel(MainWindow window, int x, int y) => window.Document.ActiveLayer.Pixels.GetPixel(x, y);

    [AvaloniaFact]
    public void SelectionToolComesFromTheRegistry()
    {
        var window = ShowWindowWithSelectionTool();

        Assert.Contains(ToolRegistry.Tools, t => t.Id == "rectangle-selection");
        Assert.IsType<SelectionTool>(window.Editor.ActiveTool);
    }

    [AvaloniaFact]
    public void DrawingAMarquee_MovingIt_AndDeletingIt_AreEachUndoable()
    {
        var window = ShowWindowWithSelectionTool();

        Drag(window, (10.5, 10.5), (29.5, 29.5));   // select the red square

        Assert.Equal(new IntRect(10, 10, 20, 20), window.Editor.Selection?.Bounds);
        Assert.False(window.Editor.History.CanUndo);

        Drag(window, (20.5, 20.5), (30.5, 30.5));   // move it by (10, 10)
        Drag(window, (55.5, 5.5), (55.5, 5.5));     // a click outside puts it down

        Assert.Null(window.Editor.Selection);
        Assert.Equal(Red, LayerPixel(window, 39, 39));
        Assert.Equal(Rgba.Transparent, LayerPixel(window, 10, 10));
        Assert.True(window.Editor.History.CanUndo);

        Drag(window, (20.5, 20.5), (39.5, 39.5));   // select it again
        Press(window, Key.Delete);

        Assert.Null(window.Editor.Selection);
        Assert.Equal(Rgba.Transparent, LayerPixel(window, 30, 30));

        window.Editor.Undo();                       // the delete
        Assert.Equal(Red, LayerPixel(window, 30, 30));

        window.Editor.Undo();                       // the move
        Assert.Equal(Red, LayerPixel(window, 10, 10));
        Assert.Equal(Rgba.White, LayerPixel(window, 39, 39));
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void EscPutsAMovedSelectionBack()
    {
        var window = ShowWindowWithSelectionTool();
        Drag(window, (10.5, 10.5), (29.5, 29.5));
        Drag(window, (20.5, 20.5), (30.5, 30.5));

        Press(window, Key.Escape);

        Assert.Null(window.Editor.Selection);
        Assert.Equal(Red, LayerPixel(window, 10, 10));
        Assert.Equal(Rgba.White, LayerPixel(window, 39, 39));
        Assert.False(window.Editor.History.CanUndo);
    }
}
