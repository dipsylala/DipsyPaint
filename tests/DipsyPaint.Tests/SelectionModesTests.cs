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
using DipsyPaint.Layout;
using DipsyPaint.Resources;
using DipsyPaint.Tools;

namespace DipsyPaint.Tests;

/// <summary>The Selection menu, free-form selection, select all, invert and transparent selection (ticket 21).</summary>
public sealed class SelectionModesTests : IDisposable
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    // The selection options are app-wide, so each test starts and ends with the defaults.
    public SelectionModesTests() => ResetSettings();

    public void Dispose() => ResetSettings();

    private static void ResetSettings()
    {
        ToolRegistry.SelectionSettings.Shape = SelectionShape.Rectangle;
        ToolRegistry.SelectionSettings.Transparent = false;
    }

    private sealed class RecordingClipboard : IImageClipboard
    {
        public List<PixelImage> Copied { get; } = [];

        public Task SetImageAsync(PixelImage image)
        {
            Copied.Add(image);
            return Task.CompletedTask;
        }

        public Task<PixelImage?> GetImageAsync() => Task.FromResult<PixelImage?>(null);

        public Task ShowErrorAsync(string message) => Task.CompletedTask;
    }

    /// <summary>Shows a 64×48 white document with a red 20×20 square at (10, 10); the pencil is active.</summary>
    private static MainWindow ShowWindow()
    {
        var document = Document.CreateBlank(64, 48);
        document.ActiveLayer.Pixels.WriteRegion(new IntRect(10, 10, 20, 20), Enumerable.Repeat(Red, 400).ToArray());
        var window = new MainWindow(document);
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static SelectionMenu Menu(Window window) => window.GetLogicalDescendants().OfType<SelectionMenu>().Single();

    private static MenuItem Item(Window window, string name) => Menu(window).FindControl<MenuItem>(name)!;

    private static void Click(Window window, string item) => Item(window, item).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static ToggleButton SelectToolButton(Window window) =>
        window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == SelectionStrings.Select);

    private static Point InWindow(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static void Drag(Window window, params (double X, double Y)[] points)
    {
        window.MouseDown(InWindow(window, points[0].X, points[0].Y), MouseButton.Left);
        foreach (var (x, y) in points.Skip(1))
        {
            window.MouseMove(InWindow(window, x, y));
        }

        window.MouseUp(InWindow(window, points[^1].X, points[^1].Y), MouseButton.Left);
    }

    // A free-form triangle over the red square's top-left half: pixels with x + y ≤ 39 from (10, 10).
    private static void DragTriangle(Window window) => Drag(window, (10.5, 10.5), (29.5, 10.5), (10.5, 29.5));

    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
    }

    // Ctrl on Windows/Linux, Cmd on macOS.
    private static RawInputModifiers Command(Window window) =>
        (RawInputModifiers)window.GetPlatformSettings()!.HotkeyConfiguration.CommandModifiers;

    private static Rgba LayerPixel(MainWindow window, int x, int y) => window.Document.ActiveLayer.Pixels.GetPixel(x, y);

    [AvaloniaFact]
    public void SelectionMenu_IsInTheImageGroup_WithNamedItems_AndShowsTheOptions()
    {
        var window = ShowWindow();

        var menu = Menu(window);
        Assert.Contains(menu, window.FindControl<Control>("ImageGroup")!.GetLogicalDescendants());
        var button = menu.FindControl<Button>("SelectionButton")!;
        Assert.Equal(SelectionStrings.Selection, AutomationProperties.GetName(button));
        Assert.True(button.Focusable && button.IsTabStop);
        string[] items = ["RectangleItem", "FreeFormItem", "SelectAllItem", "InvertItem", "DeleteItem", "TransparentItem"];
        Assert.All(items, name => Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(Item(window, name)))));
        Assert.True(Item(window, "RectangleItem").IsChecked);
        Assert.False(Item(window, "FreeFormItem").IsChecked);
        Assert.False(Item(window, "TransparentItem").IsChecked);
        Assert.False(Item(window, "DeleteItem").IsEnabled);
        Assert.Equal(new KeyGesture(Key.A, (KeyModifiers)Command(window)), Item(window, "SelectAllItem").InputGesture);
        Assert.Equal(new KeyGesture(Key.I, (KeyModifiers)Command(window) | KeyModifiers.Shift), Item(window, "InvertItem").InputGesture);
    }

    [AvaloniaFact]
    public void FreeForm_SwitchesToTheSelectionTool_AndSelectsTheDraggedShape_WhichMovesAndDeletes()
    {
        var window = ShowWindow();

        Click(window, "FreeFormItem");

        Assert.IsType<SelectionTool>(window.Editor.ActiveTool);
        Assert.True(SelectToolButton(window).IsChecked);
        Assert.True(Item(window, "FreeFormItem").IsChecked);
        Assert.False(Item(window, "RectangleItem").IsChecked);

        DragTriangle(window);

        var selection = Assert.IsType<Selection>(window.Editor.Selection);
        Assert.Equal(new IntRect(10, 10, 20, 20), selection.Bounds);
        Assert.True(selection.Contains(10, 29));
        Assert.False(selection.Contains(29, 29));
        Assert.True(Item(window, "DeleteItem").IsEnabled);

        Drag(window, (12.5, 12.5), (42.5, 22.5));  // move it by (30, 10)
        Drag(window, (60.5, 2.5), (60.5, 2.5));    // a click outside puts it down

        Assert.Equal(Rgba.Transparent, LayerPixel(window, 10, 10));
        Assert.Equal(Red, LayerPixel(window, 29, 29));   // outside the triangle: stays
        Assert.Equal(Red, LayerPixel(window, 40, 20));
        Assert.Equal(Rgba.White, LayerPixel(window, 59, 39));

        DragTriangle(window);
        Press(window, Key.Delete);

        Assert.Null(window.Editor.Selection);
        Assert.Equal(Red, LayerPixel(window, 29, 29));
        Assert.Equal(Rgba.Transparent, LayerPixel(window, 29, 10));

        window.Editor.Undo();
        window.Editor.Undo();

        Assert.Equal(Red, LayerPixel(window, 10, 10));
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void FreeFormPath_IsShownWhileDragging()
    {
        var window = ShowWindow();
        Click(window, "FreeFormItem");
        var tool = (SelectionTool)window.Editor.ActiveTool!;

        window.MouseDown(InWindow(window, 10.5, 10.5), MouseButton.Left);
        window.MouseMove(InWindow(window, 40.5, 10.5));
        window.MouseMove(InWindow(window, 40.5, 30.5));

        Assert.Equal(3, tool.FreeFormPath?.Count);
        using (var frame = window.CaptureRenderedFrame())
        {
            Assert.NotNull(frame); // renders the path without throwing
        }

        window.MouseUp(InWindow(window, 40.5, 30.5), MouseButton.Left);

        Assert.Null(tool.FreeFormPath);
    }

    [AvaloniaFact]
    public void CtrlA_SelectsAll_FromAnyTool()
    {
        var window = ShowWindow();
        Assert.IsNotType<SelectionTool>(window.Editor.ActiveTool);

        Press(window, Key.A, Command(window));

        Assert.Equal(new Selection(new IntRect(0, 0, 64, 48), null), window.Editor.Selection);
        Assert.True(SelectToolButton(window).IsChecked);
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void CtrlShiftI_InvertsTheSelection()
    {
        var window = ShowWindow();
        SelectToolButton(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Drag(window, (10.5, 10.5), (29.5, 29.5)); // the red square

        Press(window, Key.I, Command(window) | RawInputModifiers.Shift);

        var selection = Assert.IsType<Selection>(window.Editor.Selection);
        Assert.True(selection.Contains(0, 0));
        Assert.False(selection.Contains(10, 10));

        Press(window, Key.Delete);

        Assert.Equal(Rgba.Transparent, LayerPixel(window, 0, 0));
        Assert.Equal(Red, LayerPixel(window, 10, 10));
    }

    [AvaloniaFact]
    public void SelectAllAndInvertItems_Work()
    {
        var window = ShowWindow();

        Click(window, "SelectAllItem");
        Assert.Equal(new IntRect(0, 0, 64, 48), window.Editor.Selection?.Bounds);

        Click(window, "InvertItem");
        Assert.Null(window.Editor.Selection);

        Click(window, "InvertItem");
        Assert.Equal(new IntRect(0, 0, 64, 48), window.Editor.Selection?.Bounds);

        Click(window, "DeleteItem");
        Assert.Equal(Rgba.Transparent, LayerPixel(window, 10, 10));
    }

    [AvaloniaFact]
    public void CtrlA_InAFocusedTextBox_IsLeftToIt()
    {
        var window = ShowWindow();
        var textBox = new TextBox { Text = "text" };
        ((Panel)window.FindControl<Border>("CommandBar")!.Child!).Children.Add(textBox);
        window.UpdateLayout();
        textBox.Focus();

        Press(window, Key.A, Command(window));

        Assert.Null(window.Editor.Selection);
        Assert.Equal(4, textBox.SelectionEnd - textBox.SelectionStart);
    }

    [AvaloniaFact]
    public void TransparentSelection_LeavesColour2Behind_AndIsShared()
    {
        var window = ShowWindow();

        Click(window, "TransparentItem");

        Assert.True(Item(window, "TransparentItem").IsChecked);
        Assert.True(ToolRegistry.SelectionSettings.Transparent);
        Assert.Same(ToolRegistry.SelectionSettings, window.Editor.SelectionSettings);

        SelectToolButton(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Drag(window, (5.5, 5.5), (34.5, 34.5));   // the red square in a white border
        Drag(window, (20.5, 20.5), (40.5, 20.5)); // move by (20, 0)
        window.Editor.CommitPending();

        Assert.Equal(Rgba.White, LayerPixel(window, 5, 5)); // white (colour 2) was not lifted
        Assert.Equal(Rgba.Transparent, LayerPixel(window, 10, 10));
        Assert.Equal(Red, LayerPixel(window, 49, 29));

        Click(window, "TransparentItem");
        Assert.False(ToolRegistry.SelectionSettings.Transparent);
    }

    [AvaloniaFact]
    public async Task CopyingAFreeFormSelection_IsTransparentOutsideIt()
    {
        var window = ShowWindow();
        var clipboard = new RecordingClipboard();
        window.GetLogicalDescendants().OfType<ClipboardControl>().Single().ImageClipboard = clipboard;
        Click(window, "FreeFormItem");
        DragTriangle(window);

        await window.GetLogicalDescendants().OfType<ClipboardControl>().Single().CopyAsync();

        var image = Assert.Single(clipboard.Copied);
        Assert.Equal((20, 20), (image.Width, image.Height));
        Assert.Equal(Red, image.Pixels[0]);
        Assert.Equal(Rgba.Transparent, image.Pixels[19 * 20 + 19]);
    }

    [AvaloniaFact]
    public void SelectionMenu_StaysUsable_OnASideEdge()
    {
        var window = ShowWindow();

        window.CommandBarLayout.Move("image", DockEdge.Left, 0);
        window.UpdateLayout();

        var button = Menu(window).FindControl<Button>("SelectionButton")!;
        var crop = window.FindControl<Control>("ImageGroup")!.GetLogicalDescendants().OfType<Button>()
            .Single(b => AutomationProperties.GetName(b) == ImageStrings.Crop);
        var top = button.TranslatePoint(default, window)!.Value;
        var cropTop = crop.TranslatePoint(default, window)!.Value;
        Assert.True(button.IsEffectivelyVisible);
        Assert.Equal(top.X, cropTop.X);          // stacked in a column
        Assert.True(cropTop.Y > top.Y);
        Assert.True(top.X >= 0 && top.X + button.Bounds.Width <= window.Bounds.Width);

        Click(window, "SelectAllItem");
        Assert.Equal(new IntRect(0, 0, 64, 48), window.Editor.Selection?.Bounds);
    }
}
