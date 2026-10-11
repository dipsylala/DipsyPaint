using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Resources;
using DipsyPaint.Tools;
using DipsyPaint.View;

namespace DipsyPaint.Tests;

/// <summary>The magnifier tool and full screen (ticket 32).</summary>
public class MagnifierAndFullScreenTests
{
    private static MainWindow ShowWindow(int width = 2000, int height = 1500)
    {
        var window = new MainWindow(Document.CreateBlank(width, height));
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static ViewMenu Menu(Window window) => window.FindControl<ViewMenu>("ViewMenu")!;

    private static ZoomControl Zoom(Window window) => window.FindControl<ZoomControl>("Zoom")!;

    private static Border Canvas(Window window) => window.FindControl<Border>("Canvas")!;

    private static ToggleButton ToolButton(Window window, string name) =>
        window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == name);

    private static Point CentreOf(Window window, Control control) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static Point OnScreen(Window window, double x, double y) => Canvas(window).TranslatePoint(new Point(x, y), window)!.Value;

    private static void Click(Window window, Point point, MouseButton button = MouseButton.Left)
    {
        window.MouseDown(point, button);
        window.MouseUp(point, button);
        window.UpdateLayout();
    }

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        window.UpdateLayout();
    }

    private static void SelectMagnifier(Window window) =>
        Click(window, CentreOf(window, ToolButton(window, ViewStrings.Magnifier)));

    [AvaloniaFact]
    public void MagnifierIsAToolInTheToolsGroup()
    {
        var window = ShowWindow();

        Assert.Contains(ToolRegistry.Tools, t => t.Id == "magnifier");
        SelectMagnifier(window);

        Assert.IsType<MagnifierTool>(((MainWindow)window).Editor.ActiveTool);
        Assert.True(ToolButton(window, ViewStrings.Magnifier).IsChecked);
    }

    [AvaloniaFact]
    public void ClickZoomsInOnTheClickedPointAndRightClickZoomsOut()
    {
        var window = ShowWindow();
        SelectMagnifier(window);
        var point = OnScreen(window, 150, 100);

        Click(window, point);
        Assert.Equal(2, Zoom(window).ZoomFactor);
        // The clicked document point stays under the pointer.
        Assert.True(Point.Distance(point, OnScreen(window, 150, 100)) < 1, $"moved to {OnScreen(window, 150, 100)}");

        Click(window, point);
        Assert.Equal(3, Zoom(window).ZoomFactor);
        Assert.True(Point.Distance(point, OnScreen(window, 150, 100)) < 1);

        Click(window, point, MouseButton.Right);
        Click(window, point, MouseButton.Right);
        Click(window, point, MouseButton.Right);
        Assert.Equal(0.5, Zoom(window).ZoomFactor);

        // The magnifier stays selected and changes nothing in the picture.
        Assert.IsType<MagnifierTool>(window.Editor.ActiveTool);
        Assert.False(window.Editor.History.CanUndo);
        Assert.False(window.Editor.HasUnsavedChanges);
        Assert.All(window.Editor.Compositor.Pixels.ToArray(), p => Assert.Equal(Rgba.White, p));
    }

    [AvaloniaFact]
    public void MagnifierStopsAtTheZoomLimits()
    {
        var window = ShowWindow(64, 48);
        SelectMagnifier(window);
        Zoom(window).ZoomTo(8);

        Click(window, OnScreen(window, 2, 2));
        Assert.Equal(8, Zoom(window).ZoomFactor);

        Zoom(window).ZoomTo(0.125);
        Click(window, OnScreen(window, 2, 2), MouseButton.Right);
        Assert.Equal(0.125, Zoom(window).ZoomFactor);
    }

    [AvaloniaFact]
    public void F11ShowsThePictureAloneFullScreenAndF11Returns()
    {
        var window = ShowWindow(64, 48);
        Menu(window).SetRulers(true);
        Menu(window).SetGridlines(true);
        Assert.Equal("F11", Menu(window).FindControl<MenuItem>("FullScreenItem")!.InputGesture?.ToString());

        Press(window, Key.F11);

        Assert.True(Menu(window).IsFullScreen);
        Assert.Equal(WindowState.FullScreen, window.WindowState);
        Assert.False(window.FindControl<Border>("CommandBar")!.IsVisible);
        Assert.False(window.FindControl<Border>("StatusBar")!.IsVisible);
        Assert.False(((Control)window.FindControl<WrapPanel>("BottomEdge")!.Parent!).IsVisible);
        Assert.False(((Control)window.FindControl<WrapPanel>("LeftEdge")!.Parent!).IsVisible);
        Assert.False(((Control)window.FindControl<WrapPanel>("RightEdge")!.Parent!).IsVisible);
        Assert.False(window.FindControl<CanvasRulers>("Rulers")!.IsShown);
        Assert.False(window.FindControl<Gridlines>("Gridlines")!.IsVisible);
        // The canvas area fills the window.
        Assert.Equal(new Rect(window.ClientSize), window.FindControl<ScrollViewer>("CanvasScroll")!.Bounds);

        Press(window, Key.F11);

        Assert.False(Menu(window).IsFullScreen);
        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.True(window.FindControl<Border>("CommandBar")!.IsVisible);
        Assert.True(window.FindControl<Border>("StatusBar")!.IsVisible);
        Assert.True(((Control)window.FindControl<WrapPanel>("RightEdge")!.Parent!).IsVisible);
        // The display options come back as they were.
        Assert.True(window.FindControl<CanvasRulers>("Rulers")!.IsShown);
        Assert.True(window.FindControl<Gridlines>("Gridlines")!.IsVisible);
        Assert.True(Menu(window).ShowRulers);
    }

    [AvaloniaFact]
    public void EscOrAClickLeavesFullScreenWithoutDrawing()
    {
        var window = ShowWindow(64, 48);
        Assert.IsType<PencilTool>(window.Editor.ActiveTool);

        Menu(window).FindControl<MenuItem>("FullScreenItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        window.UpdateLayout();
        Assert.Equal(WindowState.FullScreen, window.WindowState);
        Press(window, Key.Escape);
        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.False(Menu(window).IsFullScreen);

        Press(window, Key.F11);
        Assert.True(Menu(window).IsFullScreen);
        Click(window, OnScreen(window, 10.5, 10.5));

        Assert.False(Menu(window).IsFullScreen);
        Assert.Equal(WindowState.Normal, window.WindowState);
        Assert.False(window.Editor.History.CanUndo); // the click did not draw
        Assert.Equal(Rgba.White, window.Editor.Compositor.Pixels[10 * 64 + 10]);
    }

    [AvaloniaFact]
    public void LeavingFullScreenRestoresAMaximisedWindow()
    {
        var window = ShowWindow(64, 48);
        window.WindowState = WindowState.Maximized;

        Menu(window).SetFullScreen(true);
        Assert.Equal(WindowState.FullScreen, window.WindowState);
        Menu(window).SetFullScreen(false);

        Assert.Equal(WindowState.Maximized, window.WindowState);
    }

    [AvaloniaFact]
    public void TheWindowLeavingFullScreenByItselfBringsTheCommandBarBack()
    {
        var window = ShowWindow(64, 48);
        Menu(window).SetFullScreen(true);

        window.WindowState = WindowState.Normal; // e.g. macOS's own full-screen control
        window.UpdateLayout();

        Assert.False(Menu(window).IsFullScreen);
        Assert.True(window.FindControl<Border>("CommandBar")!.IsVisible);
        Assert.True(window.FindControl<Border>("StatusBar")!.IsVisible);
    }
}
