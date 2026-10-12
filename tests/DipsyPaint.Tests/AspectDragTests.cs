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
using DipsyPaint.Resources;

namespace DipsyPaint.Tests;

/// <summary>Shift + dragging a corner handle keeps the aspect ratio (issue #113), through the shell.</summary>
public class AspectDragTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    private static MainWindow ShowWindow(Document document)
    {
        var window = new MainWindow(document);
        window.Show();
        window.UpdateLayout();
        return window;
    }

    // Then lays out, as the command bar may rewrap for a new tool's options, moving the canvas.
    private static void Click(Window window, string name)
    {
        window.GetLogicalDescendants().OfType<ToggleButton>()
            .Single(b => AutomationProperties.GetName(b) == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();
    }

    private static Point InWindow(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static void Drag(Window window, (double X, double Y) from, (double X, double Y) to)
    {
        window.MouseDown(InWindow(window, from.X, from.Y), MouseButton.Left);
        window.MouseMove(InWindow(window, to.X, to.Y));
        window.MouseUp(InWindow(window, to.X, to.Y), MouseButton.Left);
    }

    [AvaloniaFact]
    public void MoveSelectedPixels_ShiftCornerDrag_ScalesInProportion_AndShiftTakesEffectAtOnce()
    {
        var document = Document.CreateBlank(64, 48);
        document.ActiveLayer.Pixels.WriteRegion(new IntRect(10, 10, 20, 20), Enumerable.Repeat(Red, 400).ToArray());
        var window = ShowWindow(document);
        Click(window, SelectionStrings.Select);
        Drag(window, (10.5, 10.5), (29.5, 29.5));
        Click(window, SelectionStrings.MoveSelectedPixels);

        window.MouseDown(InWindow(window, 30, 30), MouseButton.Left); // the bottom-right handle
        window.MouseMove(InWindow(window, 40, 32), RawInputModifiers.Shift);
        Assert.Equal(new IntRect(10, 10, 30, 30), window.Editor.Selection?.Bounds);

        // Releasing and pressing Shift with the pointer held still switches at once.
        window.KeyRelease(Key.LeftShift, RawInputModifiers.None, PhysicalKey.ShiftLeft, null);
        Assert.Equal(new IntRect(10, 10, 30, 22), window.Editor.Selection?.Bounds);
        window.KeyPress(Key.LeftShift, RawInputModifiers.Shift, PhysicalKey.ShiftLeft, null);
        Assert.Equal(new IntRect(10, 10, 30, 30), window.Editor.Selection?.Bounds);

        window.MouseUp(InWindow(window, 40, 32), MouseButton.Left, RawInputModifiers.Shift);
        window.KeyRelease(Key.LeftShift, RawInputModifiers.None, PhysicalKey.ShiftLeft, null);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);

        var layer = window.Document.ActiveLayer.Pixels;
        Assert.Equal(Red, layer.GetPixel(39, 39));
        Assert.Equal(Rgba.White, layer.GetPixel(40, 39));
        Assert.Equal(Rgba.White, layer.GetPixel(39, 40));
    }

    [AvaloniaFact]
    public void PendingShape_ShiftCornerDrag_ScalesInProportion_AndShiftTakesEffectAtOnce()
    {
        var window = ShowWindow(Document.CreateBlank(64, 48));
        Click(window, ShapeStrings.Shapes);
        Click(window, ShapeStrings.Rectangle); // the default kind
        Drag(window, (5.5, 5.5), (24.5, 14.5)); // 20×10

        window.MouseDown(InWindow(window, 25, 15), MouseButton.Left); // the bottom-right handle
        window.MouseMove(InWindow(window, 45, 16), RawInputModifiers.Shift);
        Assert.Equal(new IntRect(5, 5, 40, 20), window.Editor.Adornment?.Frame);

        window.KeyRelease(Key.LeftShift, RawInputModifiers.None, PhysicalKey.ShiftLeft, null);
        Assert.Equal(new IntRect(5, 5, 40, 11), window.Editor.Adornment?.Frame);
        window.KeyPress(Key.LeftShift, RawInputModifiers.Shift, PhysicalKey.ShiftLeft, null);
        Assert.Equal(new IntRect(5, 5, 40, 20), window.Editor.Adornment?.Frame);

        window.MouseUp(InWindow(window, 45, 16), MouseButton.Left, RawInputModifiers.Shift);
        window.KeyRelease(Key.LeftShift, RawInputModifiers.None, PhysicalKey.ShiftLeft, null);
        Assert.Equal(new IntRect(5, 5, 40, 20), window.Editor.Adornment?.Frame);
    }
}
