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
using Avalonia.VisualTree;
using DipsyPaint.Engine;
using DipsyPaint.Layout;
using DipsyPaint.Resources;
using DipsyPaint.Settings;

namespace DipsyPaint.Tests;

public sealed class CommandBarLayoutTests : IDisposable
{
    private static readonly string[] DefaultOrder = ["file", "history", "clipboard", "image", "tools", "colours", "help", "update"];

    // Never the real user profile.
    private readonly string _settingsDirectory = Path.Combine(Path.GetTempPath(), "DipsyPaintTests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_settingsDirectory))
        {
            Directory.Delete(_settingsDirectory, recursive: true);
        }
    }

    private string SettingsPath => Path.Combine(_settingsDirectory, "settings.json");

    private SettingsStore Store() => new(SettingsPath);

    private static MainWindow ShowWindow(SettingsStore? store = null, int width = 640, int height = 480, int? windowWidth = null)
    {
        var window = new MainWindow(Document.CreateBlank(width, height), store);
        if (windowWidth is { } w)
        {
            window.Width = w;
        }

        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static CommandBarDock Layout(MainWindow window) => window.CommandBarLayout;

    private static void AssertDefault(MainWindow window)
    {
        Assert.Equal(DefaultOrder, Layout(window).Arrangement[DockEdge.Top]);
        Assert.Empty(Layout(window).Arrangement[DockEdge.Bottom]);
        Assert.Empty(Layout(window).Arrangement[DockEdge.Left]);
        Assert.Equal(["layers"], Layout(window).Arrangement[DockEdge.Right]);
        Assert.Equal(DefaultOrder, Layout(window).EdgePanel(DockEdge.Top).Children.OfType<CommandGroupHost>().Select(h => h.Id));
    }

    private static Rect RectOf(Visual visual, Visual relativeTo) =>
        new(visual.TranslatePoint(default, relativeTo)!.Value, visual.Bounds.Size);

    private static Point CentreOf(Visual visual, Visual relativeTo) => RectOf(visual, relativeTo).Center;

    private static void Drag(Window window, Control grip, Point to)
    {
        var from = CentreOf(grip, window);
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(from + new Point(10, 10));
        window.MouseMove(to);
        window.MouseUp(to, MouseButton.Left);
        window.UpdateLayout();
    }

    private static MenuItem Item(CommandGroupHost host, string header) =>
        host.MenuItems.Single(i => (string?)i.Header == header);

    private static void Choose(MainWindow window, string id, string header)
    {
        Item(Layout(window).Host(id), header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        window.UpdateLayout();
    }

    /// <summary>Every visible command control on the edges lies wholly inside the window and its edge.</summary>
    private static void AssertNothingClipped(MainWindow window)
    {
        var client = new Rect(window.ClientSize);
        foreach (var edge in CommandBarArrangement.Edges)
        {
            var panel = Layout(window).EdgePanel(edge);
            var panelRect = RectOf(panel, window);
            var controls = panel.GetVisualDescendants().OfType<Control>()
                .Where(c => c is Button or ComboBox or Slider or MenuItem or TextBlock && c.IsEffectivelyVisible)
                .ToList();
            Assert.All(controls, c =>
            {
                var rect = RectOf(c, window);
                Assert.True(client.Contains(rect), $"{c.GetType().Name} '{AutomationProperties.GetName(c)}' at {rect} is outside the window {client}");
                Assert.True(panelRect.Contains(rect), $"{c.GetType().Name} '{AutomationProperties.GetName(c)}' at {rect} is outside its edge {panelRect}");
            });

            var hosts = panel.Children.OfType<CommandGroupHost>().Where(h => h.IsVisible).Select(h => RectOf(h, window)).ToList();
            for (var i = 0; i < hosts.Count; i++)
            {
                for (var j = i + 1; j < hosts.Count; j++)
                {
                    Assert.False(hosts[i].Intersects(hosts[j]), $"groups overlap: {hosts[i]} and {hosts[j]}");
                }
            }
        }

        var canvasArea = window.FindControl<ScrollViewer>("CanvasScroll")!;
        Assert.True(canvasArea.Bounds.Width > 0 && canvasArea.Bounds.Height > 0);
    }

    private static void SelectTool(Window window, string name) =>
        window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == name)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public void DefaultLayoutIsPaintsSingleTopBarInGroupOrder()
    {
        var window = ShowWindow();

        AssertDefault(window);
        Assert.All(DefaultOrder, id => Assert.Equal(Orientation.Horizontal, CommandGroup.GetOrientation(Layout(window).Host(id).Group)));
    }

    [AvaloniaTheory]
    [InlineData(1100)]
    [InlineData(900)]
    [InlineData(700)]
    [InlineData(480)]
    public void GroupsWrapInsteadOfClippingAtDefaultAndNarrowerWidths(int width)
    {
        var window = ShowWindow(windowWidth: width);
        Assert.Equal(width, window.ClientSize.Width);

        foreach (var tool in new[] { ToolStrings.Pencil, BrushStrings.Brush, ShapeStrings.Shapes, TextStrings.Text })
        {
            SelectTool(window, tool);
            window.UpdateLayout();
            AssertNothingClipped(window);
        }

        // At the default width the groups no longer fit one row, so the top edge has wrapped.
        var tops = Layout(window).EdgePanel(DockEdge.Top).Children.OfType<CommandGroupHost>()
            .Where(h => h.IsVisible).Select(h => RectOf(h, window).Top).Distinct().Count();
        Assert.True(tops > 1);
    }

    [AvaloniaTheory]
    [InlineData(1100, DockEdge.Top)]
    [InlineData(900, DockEdge.Top)]
    [InlineData(700, DockEdge.Top)]
    [InlineData(480, DockEdge.Top)]
    [InlineData(1100, DockEdge.Left)]
    [InlineData(1100, DockEdge.Right)]
    public void SwitchingToolsNeverMovesTheCanvas(int width, DockEdge toolsEdge)
    {
        var window = ShowWindow(windowWidth: width);
        Layout(window).Move("tools", toolsEdge, 0);
        window.UpdateLayout();
        var canvasArea = window.FindControl<ScrollViewer>("CanvasScroll")!;
        var expected = RectOf(canvasArea, window);

        foreach (var tool in Tools.ToolRegistry.Tools)
        {
            SelectTool(window, tool.DisplayName);
            window.UpdateLayout();
            Assert.True(expected == RectOf(canvasArea, window), $"the canvas area moved from {expected} to {RectOf(canvasArea, window)} for {tool.DisplayName}");
        }
    }

    // The room kept for tool options must come from how the panels really lay out, or the bar grows for nothing.
    [AvaloniaTheory]
    [InlineData(DockEdge.Top)]
    [InlineData(DockEdge.Left)]
    public void RoomKeptForToolOptionsIsNoBiggerThanTheLargestPanelShown(DockEdge toolsEdge)
    {
        var window = ShowWindow(windowWidth: 1100);
        Layout(window).Move("tools", toolsEdge, 0);
        window.UpdateLayout();
        var picker = window.GetVisualDescendants().OfType<Tools.ToolPicker>().Single();
        var options = picker.FindControl<ContentControl>("ToolOptions")!;
        var largest = default(Size);
        foreach (var tool in Tools.ToolRegistry.Tools)
        {
            SelectTool(window, tool.DisplayName);
            window.UpdateLayout();
            largest = new Size(Math.Max(largest.Width, options.DesiredSize.Width), Math.Max(largest.Height, options.DesiredSize.Height));
        }

        var reserve = picker.FindControl<Tools.OptionsSlot>("OptionsSlot")!.Reserve;
        Assert.True(reserve.Width <= largest.Width + 0.5 && reserve.Height <= largest.Height + 0.5,
            $"kept {reserve} for options, but the largest panel shown is {largest}");
    }

    // At the default width the options share the tool buttons' row; narrower, they wrap below.
    [AvaloniaFact]
    public void ToolOptionSlidersLineUpWithTheToolButtons()
    {
        var window = ShowWindow(windowWidth: 1100);

        foreach (var tool in new[] { ToolStrings.Pencil, BrushStrings.Brush, BrushStrings.Eraser })
        {
            SelectTool(window, tool);
            window.UpdateLayout();
            var button = window.GetLogicalDescendants().OfType<ToggleButton>().Single(b => AutomationProperties.GetName(b) == tool);
            var slider = window.GetVisualDescendants().OfType<Slider>().First(s => s.IsEffectivelyVisible && s.FindAncestorOfType<Tools.ToolPicker>() is not null);
            Assert.True(Math.Abs(CentreOf(button, window).Y - CentreOf(slider, window).Y) < 1,
                $"{tool}: the button's centre is at {CentreOf(button, window).Y}, the slider's at {CentreOf(slider, window).Y}");
        }
    }

    [AvaloniaFact]
    public void WindowCannotBeNarrowerThanTheWidthTheCommandBarIsCheckedAt()
    {
        var window = ShowWindow();

        Assert.Equal(480, window.MinWidth);
    }

    [AvaloniaFact]
    public void MoveToCommandDocksAGroupWithoutAMouse()
    {
        var window = ShowWindow();
        var colours = Layout(window).Host("colours");

        // The handle is a named, focusable button; Enter opens its "Move to" menu.
        Assert.True(colours.Grip.Focusable && colours.Grip.IsTabStop);
        Assert.Equal(string.Format(LayoutStrings.MoveGroupFormat, ColourStrings.ColoursGroupName), AutomationProperties.GetName(colours.Grip));
        colours.Grip.Focus(NavigationMethod.Tab);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Assert.True(colours.Grip.Flyout!.IsOpen);
        Assert.All(colours.MenuItems, i => Assert.Equal(i.Header, AutomationProperties.GetName(i)));
        Assert.False(Item(colours, LayoutStrings.MoveToTop).IsEnabled); // already there
        Assert.True(Item(colours, LayoutStrings.MoveToLeft).IsEnabled);

        Choose(window, "colours", LayoutStrings.MoveToLeft);

        Assert.Equal(["colours"], Layout(window).Arrangement[DockEdge.Left]);
        Assert.Same(Layout(window).EdgePanel(DockEdge.Left), colours.Parent);
        Assert.DoesNotContain("colours", Layout(window).Arrangement[DockEdge.Top]);
        Assert.True(colours.Grip.IsFocused); // ready for the next move

        Choose(window, "tools", LayoutStrings.MoveToLeft);
        Assert.Equal(["colours", "tools"], Layout(window).Arrangement[DockEdge.Left]);
        Choose(window, "tools", LayoutStrings.MoveEarlier);
        Assert.Equal(["tools", "colours"], Layout(window).Arrangement[DockEdge.Left]);
        Choose(window, "tools", LayoutStrings.MoveLater);
        Assert.Equal(["colours", "tools"], Layout(window).Arrangement[DockEdge.Left]);

        Choose(window, "file", LayoutStrings.MoveToRight);
        Choose(window, "help", LayoutStrings.MoveToBottom);
        Assert.Equal(["layers", "file"], Layout(window).Arrangement[DockEdge.Right]);
        Assert.Equal(["help"], Layout(window).Arrangement[DockEdge.Bottom]);
        Assert.True(RectOf(Layout(window).Host("file"), window).Left > window.ClientSize.Width / 2);
        Assert.True(RectOf(Layout(window).Host("help"), window).Top > window.ClientSize.Height / 2);
    }

    [AvaloniaFact]
    public void MoveEarlierAndLaterSkipHiddenGroupsAndStopAtTheEnds()
    {
        var window = ShowWindow();
        var help = Layout(window).Host("help");
        help.Grip.Flyout!.ShowAt(help.Grip);

        // The update group is hidden until an update is ready, so Help is effectively last.
        Assert.False(Item(help, LayoutStrings.MoveLater).IsEnabled);
        Assert.True(Item(help, LayoutStrings.MoveEarlier).IsEnabled);
        Assert.False(Layout(window).CanMoveBy("file", -1));
    }

    [AvaloniaFact]
    public void DraggingAGroupDocksAndReordersIt()
    {
        var window = ShowWindow();
        var canvasArea = RectOf(window.FindControl<ScrollViewer>("CanvasScroll")!, window);

        // Onto the canvas near its right side: docks to the right edge.
        Drag(window, Layout(window).Host("colours").Grip, new Point(canvasArea.Right - 5, canvasArea.Center.Y));
        Assert.Contains("colours", Layout(window).Arrangement[DockEdge.Right]);

        // Onto the left edge of the canvas, then a second group above the first.
        Drag(window, Layout(window).Host("tools").Grip, new Point(canvasArea.Left + 5, canvasArea.Center.Y));
        Assert.Equal(["tools"], Layout(window).Arrangement[DockEdge.Left]);
        var tools = RectOf(Layout(window).Host("tools"), window);
        Drag(window, Layout(window).Host("image").Grip, new Point(tools.Center.X, tools.Top + 2));
        Assert.Equal(["image", "tools"], Layout(window).Arrangement[DockEdge.Left]);

        // Within the top edge: History dropped before File.
        var file = RectOf(Layout(window).Host("file"), window);
        Drag(window, Layout(window).Host("history").Grip, new Point(file.Left + 2, file.Center.Y));
        Assert.Equal(["history", "file", "clipboard", "help", "update"], Layout(window).Arrangement[DockEdge.Top]);

        // Below the canvas: the bottom edge.
        Drag(window, Layout(window).Host("help").Grip, new Point(canvasArea.Center.X, canvasArea.Bottom - 5));
        Assert.Equal(["help"], Layout(window).Arrangement[DockEdge.Bottom]);
    }

    [AvaloniaFact]
    public void GroupsOnTheSidesLayOutVerticallyAndStayUsable()
    {
        var window = ShowWindow();
        foreach (var id in new[] { "file", "history", "clipboard", "image" })
        {
            Layout(window).Move(id, DockEdge.Left, int.MaxValue);
        }

        foreach (var id in new[] { "tools", "colours", "help" })
        {
            Layout(window).Move(id, DockEdge.Right, int.MaxValue);
        }

        window.UpdateLayout();

        var colours = Layout(window).Host("colours");
        Assert.Equal(Orientation.Vertical, CommandGroup.GetOrientation(colours.Group));
        var palette = colours.Group.FindControl<UniformGrid>("PaletteGrid")!;
        Assert.Equal((10, 2), (palette.Rows, palette.Columns));
        // The palette's first column is its old top row.
        var swatches = palette.Children.Select(c => AutomationProperties.GetName(c)).ToList();
        Assert.Equal([ColourStrings.Black, ColourStrings.White, ColourStrings.Grey50, ColourStrings.Grey25], swatches.Take(4));

        // Tool buttons stack in a column, and the handle sits above each group.
        var toolButtons = Layout(window).Host("tools").Group.FindControl<WrapPanel>("ToolButtons")!;
        Assert.Equal(Orientation.Vertical, toolButtons.Orientation);
        Assert.Single(toolButtons.Children.Select(b => RectOf(b, window).Left).Distinct());
        Assert.Equal(Dock.Top, DockPanel.GetDock(colours.Grip));

        foreach (var tool in new[] { ToolStrings.Pencil, BrushStrings.Brush, ShapeStrings.Shapes, TextStrings.Text })
        {
            SelectTool(window, tool);
            window.UpdateLayout();
            AssertNothingClipped(window);
        }

        // Narrow columns: well under a third of the window each.
        Assert.InRange(Layout(window).EdgePanel(DockEdge.Right).Bounds.Width, 1, window.ClientSize.Width / 3);
        Assert.InRange(Layout(window).EdgePanel(DockEdge.Left).Bounds.Width, 1, window.ClientSize.Width / 3);

        // Back on top, the palette is two rows of ten again.
        Layout(window).Move("colours", DockEdge.Top, 0);
        Assert.Equal((2, 10), (palette.Rows, palette.Columns));
        Assert.Equal(ColourStrings.Grey50, AutomationProperties.GetName(palette.Children[1]));
    }

    [AvaloniaFact]
    public void CanvasAreaTakesTheSpaceLeftAndFitToWindowUsesIt()
    {
        var window = ShowWindow(width: 4000, height: 3000);
        Layout(window).Move("colours", DockEdge.Left, 0);
        Layout(window).Move("tools", DockEdge.Right, 0);
        Layout(window).Move("image", DockEdge.Bottom, 0);
        window.UpdateLayout();
        var scroller = window.FindControl<ScrollViewer>("CanvasScroll")!;
        var area = RectOf(scroller, window);

        foreach (var edge in CommandBarArrangement.Edges)
        {
            Assert.False(area.Intersects(RectOf(Layout(window).EdgePanel(edge), window)), $"canvas area overlaps the {edge} edge");
        }

        var zoom = window.FindControl<ZoomControl>("Zoom")!;
        zoom.FitToWindow();
        window.UpdateLayout();

        var canvas = window.FindControl<Border>("Canvas")!;
        var rendered = new Rect(canvas.TranslatePoint(default, window)!.Value,
            canvas.TranslatePoint(new Point(canvas.Bounds.Width, canvas.Bounds.Height), window)!.Value);
        var viewport = new Rect(area.Position, scroller.Viewport);
        Assert.True(viewport.Contains(rendered), $"{rendered} not within {viewport}");
    }

    [AvaloniaFact]
    public void LayoutIsSavedToTheSettingsFileAndRestoredAtStart()
    {
        var first = ShowWindow(Store());
        Assert.False(File.Exists(SettingsPath)); // nothing written until the layout changes

        Layout(first).Move("colours", DockEdge.Left, 0);
        Layout(first).Move("help", DockEdge.Bottom, 0);
        Layout(first).Move("history", DockEdge.Top, 0);
        Assert.True(File.Exists(SettingsPath));

        var second = ShowWindow(Store());

        foreach (var edge in CommandBarArrangement.Edges)
        {
            Assert.Equal(Layout(first).Arrangement[edge], Layout(second).Arrangement[edge]);
        }

        Assert.Same(Layout(second).EdgePanel(DockEdge.Left), Layout(second).Host("colours").Parent);
        Assert.Equal(DockEdge.Bottom, Layout(second).Arrangement.EdgeOf("help"));
    }

    [AvaloniaTheory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"layout\"")]
    [InlineData("{\"commandBar\": 5}")]
    [InlineData("{\"commandBar\": {\"top\": [1, {}], \"left\": \"colours\"}}")]
    [InlineData("{\"commandBar\": {\"top\": [], \"bottom\": null}}")]
    [InlineData("\u0000\u0001binary�")]
    public void CorruptOrEmptySettingsFallBackToTheDefaultLayout(string contents)
    {
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(SettingsPath, contents);

        var window = ShowWindow(Store());

        AssertDefault(window);
    }

    [AvaloniaFact]
    public void OversizedSettingsFileFallsBackToTheDefaultLayout()
    {
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(SettingsPath, "{\"commandBar\": {\"left\": [\"colours\"]}," + new string(' ', (int)SettingsStore.MaxFileBytes) + "}");

        AssertDefault(ShowWindow(Store()));
    }

    [AvaloniaFact]
    public void UnknownGroupsAreIgnoredAndNewGroupsAppearInTheirDefaultPlace()
    {
        Directory.CreateDirectory(_settingsDirectory);
        // A layout from a version with a "ruler" group and without most of today's groups.
        File.WriteAllText(SettingsPath, """
            {"commandBar": {"top": ["file", "tools", "file", "ruler"], "left": ["colours", "bogus", null]}}
            """);

        var window = ShowWindow(Store());

        Assert.Equal(["file", "history", "clipboard", "image", "tools", "help", "update"], Layout(window).Arrangement[DockEdge.Top]);
        Assert.Equal(["colours"], Layout(window).Arrangement[DockEdge.Left]);
        Assert.Equal(["layers"], Layout(window).Arrangement[DockEdge.Right]);
    }

    [AvaloniaFact]
    public void SavingKeepsSettingsThisVersionDoesNotKnow()
    {
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(SettingsPath, """{"futureSetting": {"enabled": true}, "commandBar": null}""");
        var window = ShowWindow(Store());

        Layout(window).Move("colours", DockEdge.Right, 0);

        var saved = File.ReadAllText(SettingsPath);
        Assert.Contains("\"futureSetting\"", saved, StringComparison.Ordinal);
        Assert.Contains("\"commandBar\"", saved, StringComparison.Ordinal);
        Assert.Equal(["colours", "layers"], Store().Load().CommandBar!.Right);
    }

    [AvaloniaFact]
    public void ResetLayoutRestoresTheDefaultAndSavesIt()
    {
        var window = ShowWindow(Store());
        Choose(window, "colours", LayoutStrings.MoveToLeft);
        Choose(window, "file", LayoutStrings.MoveToBottom);
        Choose(window, "history", LayoutStrings.MoveLater);

        Choose(window, "tools", LayoutStrings.ResetLayout);

        AssertDefault(window);
        Assert.Equal(Orientation.Horizontal, CommandGroup.GetOrientation(Layout(window).Host("colours").Group));
        Assert.Equal(DefaultOrder, Store().Load().CommandBar!.Top);
        AssertDefault(ShowWindow(Store()));
    }

    [AvaloniaFact]
    public void MovingAGroupKeepsItsStateAndDocumentBinding()
    {
        var window = ShowWindow();
        SelectTool(window, ShapeStrings.Shapes);
        var tool = window.Editor.ActiveTool;

        Choose(window, "tools", LayoutStrings.MoveToRight);
        Choose(window, "colours", LayoutStrings.MoveToBottom);

        Assert.Same(tool, window.Editor.ActiveTool);
        Assert.Same(window.Editor, Layout(window).Host("tools").Group.DataContext);
    }
}
