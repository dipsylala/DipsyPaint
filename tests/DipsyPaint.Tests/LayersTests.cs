using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using DipsyPaint.Drawing;
using DipsyPaint.Engine;
using DipsyPaint.Layers;
using DipsyPaint.Layout;
using DipsyPaint.Resources;

namespace DipsyPaint.Tests;

public class LayersTests
{
    private sealed class FakeDialogs : ILayerDialogs
    {
        public List<string> Errors { get; } = [];

        public Task ShowErrorAsync(string message)
        {
            Errors.Add(message);
            return Task.CompletedTask;
        }
    }

    private static (MainWindow Window, LayersPanel Panel, FakeDialogs Dialogs) ShowWindow()
    {
        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        window.UpdateLayout();
        var panel = window.FindControl<LayersPanel>("Layers")!;
        var dialogs = new FakeDialogs();
        panel.Dialogs = dialogs;
        return (window, panel, dialogs);
    }

    private static Point InWindow(Window window, double x, double y) =>
        window.FindControl<DocumentCanvas>("DocumentView")!.TranslatePoint(new Point(x, y), window)!.Value;

    private static Point CentreOf(Visual visual, Window window) =>
        visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), window)!.Value;

    private static void Click(Window window, Control control)
    {
        window.UpdateLayout();
        var centre = CentreOf(control, window);
        window.MouseDown(centre, MouseButton.Left);
        window.MouseUp(centre, MouseButton.Left);
        window.UpdateLayout();
    }

    private static void ClickAdd(Window window, LayersPanel panel) => Click(window, panel.FindControl<Button>("AddButton")!);

    private static void DrawLine(Window window, double x0, double x1, double y)
    {
        window.MouseDown(InWindow(window, x0, y), MouseButton.Left);
        window.MouseMove(InWindow(window, x1, y));
        window.MouseUp(InWindow(window, x1, y), MouseButton.Left);
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

    private static void PressCommand(Window window, Key key, PhysicalKey physicalKey)
    {
        var command = (RawInputModifiers)window.GetPlatformSettings()!.HotkeyConfiguration.CommandModifiers;
        window.KeyPress(key, command, physicalKey, null);
        window.KeyRelease(key, command, physicalKey, null);
    }

    /// <summary>Opens <paramref name="row"/>'s menu from the keyboard (Shift+F10) and clicks the item named <paramref name="name"/>.</summary>
    private static void ChooseFromMenu(Window window, LayerRow row, string name)
    {
        row.Select.Focus(NavigationMethod.Tab);
        window.KeyPress(Key.F10, RawInputModifiers.Shift, PhysicalKey.F10, null);
        window.KeyRelease(Key.F10, RawInputModifiers.Shift, PhysicalKey.F10, null);
        var menu = (MenuFlyout)row.Select.ContextFlyout!;
        Assert.True(menu.IsOpen, "Shift+F10 should open the layer's menu");
        var item = menu.Items.OfType<MenuItem>().Single(i => AutomationProperties.GetName(i) == name);
        Assert.True(item.IsEnabled, $"'{name}' should be enabled");
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.Hide();
        window.UpdateLayout();
    }

    private static MenuItem MenuItemOf(LayerRow row, string name)
    {
        var menu = (MenuFlyout)row.Select.ContextFlyout!;
        menu.ShowAt(row.Select);
        var item = menu.Items.OfType<MenuItem>().Single(i => AutomationProperties.GetName(i) == name);
        menu.Hide();
        return item;
    }

    private static Rgba ThumbnailPixel(LayerRow row, double fx, double fy)
    {
        var image = row.Select.GetVisualDescendants().OfType<Image>().Single();
        var bitmap = (WriteableBitmap)image.Source!;
        using var buffer = bitmap.Lock();
        var x = (int)(fx * bitmap.PixelSize.Width);
        var y = (int)(fy * bitmap.PixelSize.Height);
        var bytes = new byte[4];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address + y * buffer.RowBytes + x * 4, bytes, 0, 4);
        return new Rgba(bytes[0], bytes[1], bytes[2], bytes[3]);
    }

    [AvaloniaFact]
    public void ThePanelIsACommandGroupDockedRightShowingOneSelectedLayer()
    {
        var (window, panel, _) = ShowWindow();

        Assert.Equal(["layers"], window.CommandBarLayout.Arrangement[DockEdge.Right]);
        Assert.Equal(Orientation.Vertical, CommandGroup.GetOrientation(panel));
        var row = Assert.Single(panel.LayerRows);
        Assert.Same(window.Document.ActiveLayer, row.Layer);
        Assert.True(row.Select.IsChecked);
        Assert.True(row.Visibility.IsChecked);
        Assert.Equal(string.Format(LayerStrings.LayerFormat, 1), AutomationProperties.GetName(row.Select));
        Assert.Equal(string.Format(LayerStrings.LayerVisibleFormat, 1), AutomationProperties.GetName(row.Visibility));
        Assert.Equal(LayerStrings.AddLayer, AutomationProperties.GetName(panel.FindControl<Button>("AddButton")!));
        Assert.True(panel.FindControl<Button>("AddButton")!.IsTabStop);
    }

    [AvaloniaFact]
    public void AddingALayerPutsItOnTopSelected_AndDrawingGoesToIt()
    {
        var (window, panel, _) = ShowWindow();
        var bottom = window.Document.ActiveLayer;

        ClickAdd(window, panel);

        Assert.Equal(2, window.Document.Layers.Count);
        var rows = panel.LayerRows.ToList();
        Assert.Equal([window.Document.Layers[1], bottom], rows.Select(r => r.Layer));
        Assert.Equal([true, false], rows.Select(r => r.Select.IsChecked == true));
        Assert.Equal(string.Format(LayerStrings.LayerFormat, 2), AutomationProperties.GetName(rows[0].Select));

        DrawLine(window, 10.5, 30.5, 20.5);

        Assert.Equal(Rgba.Black, window.Document.Layers[1].Pixels.GetPixel(20, 20));
        Assert.Equal(Rgba.White, bottom.Pixels.GetPixel(20, 20));
        Assert.Equal((0, 0, 0), RenderedPixel(window, InWindow(window, 20.5, 20.5)));
    }

    [AvaloniaFact]
    public void ClickingAThumbnailSelectsItsLayer()
    {
        var (window, panel, _) = ShowWindow();
        ClickAdd(window, panel);
        var bottomRow = panel.LayerRows.Last();

        Click(window, bottomRow.Select);

        Assert.Same(bottomRow.Layer, window.Document.ActiveLayer);
        Assert.Equal([false, true], panel.LayerRows.Select(r => r.Select.IsChecked == true));

        Click(window, bottomRow.Select); // clicking the selected layer keeps it selected
        Assert.True(bottomRow.Select.IsChecked);
    }

    [AvaloniaFact]
    public void TheEyeHidesAndShowsALayer_AndHidingIsUndoable()
    {
        var (window, panel, _) = ShowWindow();
        ClickAdd(window, panel);
        DrawLine(window, 10.5, 30.5, 20.5);
        var top = panel.LayerRows.First();

        Click(window, top.Visibility);

        Assert.False(top.Layer.IsVisible);
        Assert.False(top.Visibility.IsChecked);
        Assert.Equal((255, 255, 255), RenderedPixel(window, InWindow(window, 20.5, 20.5)));

        PressCommand(window, Key.Z, PhysicalKey.Z);

        Assert.True(top.Layer.IsVisible);
        Assert.True(top.Visibility.IsChecked);
        Assert.Equal((0, 0, 0), RenderedPixel(window, InWindow(window, 20.5, 20.5)));
    }

    [AvaloniaFact]
    public void TheMenuDuplicatesMergesAndDeletesLayers_FromTheKeyboard()
    {
        var (window, panel, _) = ShowWindow();
        ClickAdd(window, panel);
        DrawLine(window, 10.5, 30.5, 20.5);
        var top = panel.LayerRows.First();

        ChooseFromMenu(window, top, LayerStrings.DuplicateLayer);
        Assert.Equal(3, window.Document.Layers.Count);
        var copy = window.Document.ActiveLayer;
        Assert.Equal(Rgba.Black, copy.Pixels.GetPixel(20, 20));
        Assert.Same(copy, panel.LayerRows.First().Layer);
        Assert.True(panel.RowOf(copy).Select.IsFocused, "focus stays on the selected layer");

        ChooseFromMenu(window, panel.RowOf(copy), LayerStrings.MergeDown);
        Assert.Equal(2, window.Document.Layers.Count);
        Assert.Same(top.Layer, window.Document.ActiveLayer);

        ChooseFromMenu(window, panel.RowOf(top.Layer), LayerStrings.DeleteLayer);
        var only = Assert.Single(window.Document.Layers);
        Assert.Single(panel.LayerRows);
        Assert.Equal(Rgba.White, only.Pixels.GetPixel(20, 20));
        Assert.Equal((255, 255, 255), RenderedPixel(window, InWindow(window, 20.5, 20.5)));

        // The last layer can't be deleted, nor the bottom layer merged down.
        Assert.False(MenuItemOf(panel.RowOf(only), LayerStrings.DeleteLayer).IsEnabled);
        Assert.False(MenuItemOf(panel.RowOf(only), LayerStrings.MergeDown).IsEnabled);

        // Each was one history step.
        PressCommand(window, Key.Z, PhysicalKey.Z);
        PressCommand(window, Key.Z, PhysicalKey.Z);
        PressCommand(window, Key.Z, PhysicalKey.Z);
        Assert.Equal(2, window.Document.Layers.Count);
        Assert.Equal(2, panel.LayerRows.Count());
    }

    [AvaloniaFact]
    public void TheMenuHidesAndMovesLayers()
    {
        var (window, panel, _) = ShowWindow();
        ClickAdd(window, panel);
        var (top, bottom) = (window.Document.Layers[1], window.Document.Layers[0]);

        ChooseFromMenu(window, panel.RowOf(top), LayerStrings.HideLayer);
        Assert.False(top.IsVisible);
        Assert.Equal(LayerStrings.ShowLayer, MenuItemOf(panel.RowOf(top), LayerStrings.ShowLayer).Header);

        Assert.False(MenuItemOf(panel.RowOf(top), LayerStrings.MoveLayerUp).IsEnabled);
        ChooseFromMenu(window, panel.RowOf(top), LayerStrings.MoveLayerDown);
        Assert.Equal([bottom, top], panel.LayerRows.Select(r => r.Layer));
        Assert.Equal([top, bottom], window.Document.Layers);

        ChooseFromMenu(window, panel.RowOf(top), LayerStrings.MoveLayerUp);
        Assert.Equal([bottom, top], window.Document.Layers);
    }

    [AvaloniaFact]
    public void DraggingAThumbnailOntoAnotherMovesTheLayerThere()
    {
        var (window, panel, _) = ShowWindow();
        ClickAdd(window, panel);
        ClickAdd(window, panel);
        var layers = window.Document.Layers.ToArray();
        var (topRow, bottomRow) = (panel.RowOf(layers[2]), panel.RowOf(layers[0]));

        var from = CentreOf(topRow.Select, window);
        var to = CentreOf(bottomRow.Select, window);
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(new Point(from.X, from.Y + 10));
        window.MouseMove(to);
        window.MouseUp(to, MouseButton.Left);
        window.UpdateLayout();

        Assert.Equal([layers[2], layers[0], layers[1]], window.Document.Layers);
        Assert.Equal([layers[1], layers[0], layers[2]], panel.LayerRows.Select(r => r.Layer));
        Assert.Equal(1, topRow.Opacity);
    }

    [AvaloniaFact]
    public void ANewLayerOverTheMemoryBudgetIsRefusedWithAMessage()
    {
        var (window, panel, dialogs) = ShowWindow();
        panel.Budget = new MemoryBudget(64 * 48 * 4 * 5); // room for the bitmaps there are now, not one more layer

        ClickAdd(window, panel);
        ChooseFromMenu(window, panel.LayerRows.Single(), LayerStrings.DuplicateLayer);

        Assert.Single(window.Document.Layers);
        Assert.Equal([string.Format(LayerStrings.NotEnoughMemory, 64, 48), string.Format(LayerStrings.NotEnoughMemory, 64, 48)], dialogs.Errors);
        Assert.False(window.Editor.History.CanUndo);
    }

    [AvaloniaFact]
    public void ThumbnailsShowEachLayersPixels()
    {
        var (window, panel, _) = ShowWindow();
        ClickAdd(window, panel);
        var row = panel.LayerRows.First();
        Assert.Equal(Rgba.Transparent, ThumbnailPixel(row, 0.5, 0.5));
        Assert.Equal(Rgba.White, ThumbnailPixel(panel.LayerRows.Last(), 0.5, 0.5));

        window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == ToolStrings.Fill)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout(); // the tool's options may rewrap the command bar and move the canvas
        window.MouseDown(InWindow(window, 5, 5), MouseButton.Left);
        window.MouseUp(InWindow(window, 5, 5), MouseButton.Left);

        Assert.Equal(Rgba.Black, ThumbnailPixel(row, 0.5, 0.5));
        Assert.Equal(Rgba.White, ThumbnailPixel(panel.LayerRows.Last(), 0.5, 0.5));
        // 64×48 fits the 64×48 thumbnail box exactly.
        Assert.Equal(new PixelSize(64, 48), ((WriteableBitmap)row.Select.GetVisualDescendants().OfType<Image>().Single().Source!).PixelSize);

        PressCommand(window, Key.Z, PhysicalKey.Z);
        Assert.Equal(Rgba.Transparent, ThumbnailPixel(row, 0.5, 0.5));
    }

    [AvaloniaTheory]
    [InlineData(DockEdge.Top)]
    [InlineData(DockEdge.Bottom)]
    public void ThePanelWorksDockedHorizontally(DockEdge edge)
    {
        var (window, panel, _) = ShowWindow();
        window.CommandBarLayout.Move("layers", edge, int.MaxValue);
        window.UpdateLayout();
        ClickAdd(window, panel);
        ClickAdd(window, panel);

        Assert.Equal(Orientation.Horizontal, CommandGroup.GetOrientation(panel));
        var rows = panel.LayerRows.ToList();
        Assert.Equal(3, rows.Count);
        var xs = rows.Select(r => CentreOf(r, window).X).ToList();
        Assert.True(xs[0] < xs[1] && xs[1] < xs[2], $"rows should run left to right: {string.Join(", ", xs)}");
        Assert.Single(rows.Select(r => CentreOf(r, window).Y).Distinct());

        var edgeRect = new Rect(window.CommandBarLayout.EdgePanel(edge).TranslatePoint(default, window)!.Value, window.CommandBarLayout.EdgePanel(edge).Bounds.Size);
        foreach (var control in new Control[] { panel.FindControl<Button>("AddButton")! }.Concat(rows.SelectMany(r => new Control[] { r.Select, r.Visibility })))
        {
            var rect = new Rect(control.TranslatePoint(default, window)!.Value, control.Bounds.Size);
            Assert.True(edgeRect.Contains(rect), $"{AutomationProperties.GetName(control)} at {rect} is outside the edge {edgeRect}");
        }

        Click(window, rows[2].Select);
        Assert.Same(rows[2].Layer, window.Document.ActiveLayer);
    }

    [AvaloniaFact]
    public void ManyLayersScrollInsteadOfGrowingTheEdge()
    {
        var (window, panel, _) = ShowWindow();
        for (var i = 0; i < 19; i++)
        {
            window.Editor.AddLayer();
        }

        window.UpdateLayout();

        Assert.Equal(20, panel.LayerRows.Count());
        Assert.True(panel.Bounds.Height <= window.CommandBarLayout.EdgePanel(DockEdge.Right).Bounds.Height);
        var scroller = panel.FindControl<ScrollViewer>("Scroller")!;
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height);
    }

    [AvaloniaFact]
    public void ANewDocumentShowsItsOwnLayers()
    {
        var (window, panel, _) = ShowWindow();
        ClickAdd(window, panel);

        window.ReplaceDocument(Document.CreateBlank(32, 32));
        window.UpdateLayout();

        Assert.Same(window.Document.ActiveLayer, Assert.Single(panel.LayerRows).Layer);
    }
}
