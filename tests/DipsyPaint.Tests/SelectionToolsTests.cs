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

/// <summary>Select, Move selection and Move selected pixels in the Tools group (ADR-0025).</summary>
public class SelectionToolsTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    /// <summary>Shows a 64×48 white document with a red 20×20 square at (10, 10), selected with the Select tool.</summary>
    private static MainWindow ShowWindowWithSquareSelected()
    {
        var document = Document.CreateBlank(64, 48);
        document.ActiveLayer.Pixels.WriteRegion(new IntRect(10, 10, 20, 20), Enumerable.Repeat(Red, 400).ToArray());
        var window = new MainWindow(document);
        window.Show();
        window.UpdateLayout();
        UseTool(window, SelectionStrings.Select);
        Drag(window, (10.5, 10.5), (29.5, 29.5));
        return window;
    }

    private static ToggleButton ToolButton(Window window, string name) =>
        window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == name);

    // Then lays out, as the command bar may rewrap for a new tool's options, moving the canvas.
    private static void UseTool(Window window, string name)
    {
        ToolButton(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();
    }

    private static Point InWindow(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static void Drag(Window window, (double X, double Y) from, (double X, double Y) to, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.MouseDown(InWindow(window, from.X, from.Y), MouseButton.Left, modifiers);
        window.MouseMove(InWindow(window, to.X, to.Y), modifiers);
        window.MouseUp(InWindow(window, to.X, to.Y), MouseButton.Left, modifiers);
    }

    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
    }

    private static Rgba LayerPixel(MainWindow window, int x, int y) => window.Document.ActiveLayer.Pixels.GetPixel(x, y);

    private static Rgba[] Snapshot(MainWindow window)
    {
        var pixels = new Rgba[64 * 48];
        window.Document.ActiveLayer.Pixels.ReadRegion(new IntRect(0, 0, 64, 48), pixels);
        return pixels;
    }

    [AvaloniaFact]
    public void ToolsGroup_HasSelectThenTheTwoMoveTools_WithoutShortcuts_NamedFromResources()
    {
        var names = new[] { SelectionStrings.Select, SelectionStrings.MoveSelection, SelectionStrings.MoveSelectedPixels };
        Assert.Equal(["Select", "Move selection", "Move selected pixels"], names);
        var ids = ToolRegistry.Tools.Select(t => t.Id).ToList();
        var start = ids.IndexOf("selection");
        Assert.Equal(["selection", "move-selection", "move-selected-pixels"], ids.Skip(start).Take(3));

        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        var buttons = window.FindControl<ToolPicker>("Tools")?.FindControl<WrapPanel>("ToolButtons")
            ?? window.GetLogicalDescendants().OfType<ToolPicker>().Single().FindControl<WrapPanel>("ToolButtons")!;
        var shown = buttons.Children.Select(AutomationProperties.GetName).ToList();
        Assert.Equal(names, shown.Skip(shown.IndexOf(SelectionStrings.Select)).Take(3));
        foreach (var (tool, name) in ToolRegistry.Tools.Skip(start).Take(3).Zip(names))
        {
            Assert.Null(tool.Shortcut);
            Assert.Equal(name, tool.DisplayName);
            Assert.Equal(name, ToolTip.GetTip(ToolButton(window, name)));
        }

        // Fluent "Arrow Move" for the pixels; a bespoke dashed square with arrows for the outline.
        Assert.StartsWith("M15.2803 6.03033", ToolRegistry.Tools[start + 2].IconData);
        Assert.StartsWith(ToolRegistry.Tools[start].IconData, ToolRegistry.Tools[start + 1].IconData);
    }

    [AvaloniaFact]
    public void EachToolShowsTheOutline_OnlyMoveSelectedPixelsHasHandles()
    {
        var window = ShowWindowWithSquareSelected();

        Assert.False(window.Editor.Adornment?.HasHandles);
        UseTool(window, SelectionStrings.MoveSelection);
        Assert.False(window.Editor.Adornment?.HasHandles);
        UseTool(window, SelectionStrings.MoveSelectedPixels);
        Assert.True(window.Editor.Adornment?.HasHandles);
        Assert.Equal(new IntRect(10, 10, 20, 20), window.Editor.Adornment?.Frame);
    }

    [AvaloniaFact]
    public void SwitchingTools_RedrawsTheOutline_WithOrWithoutHandles()
    {
        var window = ShowWindowWithSquareSelected();
        var outline = RenderedCanvas(window);

        UseTool(window, SelectionStrings.MoveSelectedPixels);
        var withHandles = RenderedCanvas(window);
        UseTool(window, SelectionStrings.MoveSelection);

        Assert.NotEqual(outline, withHandles);
        Assert.Equal(outline, RenderedCanvas(window));
    }

    // The rendered pixels of the canvas and the margin around it, where the handles are drawn.
    private static byte[] RenderedCanvas(Window window)
    {
        window.UpdateLayout();
        var canvas = window.FindControl<DocumentCanvas>("DocumentView")!;
        var area = new Rect(canvas.TranslatePoint(new Point(-20, -20), window)!.Value, canvas.Bounds.Size + new Size(40, 40));
        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();
        var bytes = new List<byte>();
        for (var y = (int)area.Y; y < (int)area.Bottom; y++)
        {
            var row = new byte[(int)area.Width * 4];
            System.Runtime.InteropServices.Marshal.Copy(buffer.Address + y * buffer.RowBytes + (int)area.X * 4, row, 0, row.Length);
            bytes.AddRange(row);
        }

        return [.. bytes];
    }

    [AvaloniaFact]
    public void Select_WithShift_DragsASquare_AndADragInsideStartsANewSelection()
    {
        var window = ShowWindowWithSquareSelected();

        Drag(window, (15.5, 15.5), (40.5, 25.5), RawInputModifiers.Shift);

        Assert.Equal(new Selection(new IntRect(15, 15, 26, 26), null), window.Editor.Selection);
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void MoveSelection_MovesTheOutlineAlone_ShiftLockingTheAxis()
    {
        var window = ShowWindowWithSquareSelected();
        var before = Snapshot(window);
        UseTool(window, SelectionStrings.MoveSelection);

        Drag(window, (20.5, 20.5), (35.5, 23.5), RawInputModifiers.Shift);

        Assert.Equal(new Selection(new IntRect(25, 10, 20, 20), null), window.Editor.Selection);
        Assert.Equal(before, Snapshot(window));
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void MoveSelectedPixels_MovesThePixels_AndAClickOutsideDoesNothing()
    {
        var window = ShowWindowWithSquareSelected();
        UseTool(window, SelectionStrings.MoveSelectedPixels);

        Drag(window, (20.5, 20.5), (30.5, 25.5));
        Drag(window, (60.5, 45.5), (60.5, 45.5));

        Assert.Equal(new IntRect(20, 15, 20, 20), window.Editor.Selection?.Bounds);
        Assert.NotNull(window.Editor.Selection?.FloatingPixels);
        Assert.Equal(Rgba.Transparent, LayerPixel(window, 10, 10));
        Assert.False(window.Editor.History.CanUndo); // pending
    }

    [AvaloniaFact]
    public void ArrowKeys_NudgeTheOutline_OrThePixels_ByOneOrTenWithShift()
    {
        var window = ShowWindowWithSquareSelected();
        var before = Snapshot(window);

        UseTool(window, SelectionStrings.MoveSelection);
        Press(window, Key.Right);
        Press(window, Key.Down, RawInputModifiers.Shift);
        Assert.Equal(new Selection(new IntRect(11, 20, 20, 20), null), window.Editor.Selection);
        Assert.Equal(before, Snapshot(window));

        Press(window, Key.Up, RawInputModifiers.Shift);
        Press(window, Key.Left);
        UseTool(window, SelectionStrings.MoveSelectedPixels);
        Press(window, Key.Right, RawInputModifiers.Shift);
        Press(window, Key.Right);
        Assert.Equal(new IntRect(21, 10, 20, 20), window.Editor.Selection?.Bounds);
        Assert.False(window.Editor.History.CanUndo);

        Press(window, Key.Enter); // the run of nudges is one step; the outline stays where the pixels landed

        Assert.Equal(new Selection(new IntRect(21, 10, 20, 20), null), window.Editor.Selection);
        Assert.Equal(Red, LayerPixel(window, 40, 29));
        window.Editor.Undo();
        Assert.Equal(before, Snapshot(window));
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void ArrowKeys_WithSelect_LeaveTheSelection()
    {
        var window = ShowWindowWithSquareSelected();

        Press(window, Key.Right);

        Assert.Equal(new Selection(new IntRect(10, 10, 20, 20), null), window.Editor.Selection);
    }

    [AvaloniaFact]
    public void SwitchingAmongTheSelectionTools_KeepsTheSelection_AnyOtherToolClearsIt()
    {
        var window = ShowWindowWithSquareSelected();
        UseTool(window, SelectionStrings.MoveSelectedPixels);
        Drag(window, (20.5, 20.5), (30.5, 20.5));

        UseTool(window, SelectionStrings.MoveSelection);

        Assert.Equal(new Selection(new IntRect(20, 10, 20, 20), null), window.Editor.Selection);
        Assert.Equal(Red, LayerPixel(window, 39, 10));
        Assert.True(window.Editor.History.CanUndo);

        UseTool(window, SelectionStrings.Select);
        Assert.Equal(new IntRect(20, 10, 20, 20), window.Editor.Selection?.Bounds);

        UseTool(window, ToolStrings.Pencil);
        Assert.Null(window.Editor.Selection);
        Assert.Null(window.Editor.Adornment);
    }

    [AvaloniaFact]
    public void DeleteAndSelectAll_WorkWithAMoveTool_AndSelectAllSwitchesToSelect()
    {
        var window = ShowWindowWithSquareSelected();
        UseTool(window, SelectionStrings.MoveSelection);

        Press(window, Key.Delete);

        Assert.Null(window.Editor.Selection);
        Assert.Equal(Rgba.Transparent, LayerPixel(window, 20, 20));

        UseTool(window, SelectionStrings.MoveSelectedPixels);
        window.GetLogicalDescendants().OfType<SelectionMenu>().Single().SelectAll();

        Assert.Equal(new IntRect(0, 0, 64, 48), window.Editor.Selection?.Bounds);
        Assert.True(ToolButton(window, SelectionStrings.Select).IsChecked);
        Assert.False(ToolButton(window, SelectionStrings.MoveSelectedPixels).IsChecked);
    }

    [AvaloniaFact]
    public void PickingASelectionShape_SwitchesAMoveToolToSelect()
    {
        var window = ShowWindowWithSquareSelected();
        UseTool(window, SelectionStrings.MoveSelection);

        window.GetLogicalDescendants().OfType<SelectionMenu>().Single().UseShape(SelectionShape.Rectangle);
        window.UpdateLayout();

        Assert.True(ToolButton(window, SelectionStrings.Select).IsChecked);
        Assert.Equal(new IntRect(10, 10, 20, 20), window.Editor.Selection?.Bounds); // kept
    }
}
