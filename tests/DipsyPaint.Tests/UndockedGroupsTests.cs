using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using DipsyPaint.Engine;
using DipsyPaint.Layout;
using DipsyPaint.Resources;
using DipsyPaint.Settings;

namespace DipsyPaint.Tests;

/// <summary>Undocked command groups and the drag outline (ADR-0026).</summary>
public sealed class UndockedGroupsTests : IDisposable
{
    private static readonly string[] DefaultTop = ["menu", "history", "clipboard", "image", "tools", "colours", "update"];

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

    private static MainWindow ShowWindow(SettingsStore? store = null, int windowWidth = 1100, int windowHeight = 750)
    {
        var window = new MainWindow(Document.CreateBlank(640, 480), store) { Width = windowWidth, Height = windowHeight };
        window.Show();
        window.UpdateLayout();
        return window;
    }

    private static CommandBarDock Layout(MainWindow window) => window.CommandBarLayout;

    private static Rect RectOf(Visual visual, Visual relativeTo) =>
        new(visual.TranslatePoint(default, relativeTo)!.Value, visual.Bounds.Size);

    private static Rect HostRect(MainWindow window, string id) => RectOf(Layout(window).Host(id), window);

    /// <summary>The canvas area: the space the edges leave, which undocked groups sit over.</summary>
    private static Rect Area(MainWindow window) => RectOf(Layout(window).UndockedPanel, window);

    private static Rect CanvasScroll(MainWindow window) => RectOf(window.FindControl<ScrollViewer>("CanvasScroll")!, window);

    private static Rect OutlineRect(MainWindow window) => RectOf(Layout(window).DragOutline, window);

    // Presses a group's handle and moves the pointer far enough to start a drag, ending at the point.
    private static void StartDrag(MainWindow window, string id, Point to)
    {
        var from = RectOf(Layout(window).Host(id).Grip, window).Center;
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(from + new Point(10, 10));
        window.MouseMove(to);
        window.UpdateLayout();
    }

    private static void Drag(MainWindow window, string id, Point to)
    {
        StartDrag(window, id, to);
        window.MouseUp(to, MouseButton.Left);
        window.UpdateLayout();
    }

    private static MenuItem Item(CommandGroupHost host, string header) =>
        host.MenuItems.Single(i => (string?)i.Header == header);

    private static void Choose(MainWindow window, string id, string header)
    {
        var host = Layout(window).Host(id);
        host.Grip.Flyout!.ShowAt(host.Grip); // updates which items are enabled
        Item(host, header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        host.Grip.Flyout.Hide();
        window.UpdateLayout();
    }

    private static void Press(MainWindow window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, null);
        window.KeyRelease(key, RawInputModifiers.None, physical, null);
        window.UpdateLayout();
    }

    private static void AssertClose(Point expected, Point actual) =>
        Assert.True(Math.Abs(expected.X - actual.X) < 1 && Math.Abs(expected.Y - actual.Y) < 1, $"expected {expected}, got {actual}");

    private static void AssertInside(Rect outer, Rect inner) =>
        Assert.True(outer.Inflate(0.5).Contains(inner), $"{inner} is not inside {outer}");

    private static void AssertUndocked(MainWindow window, string id)
    {
        var host = Layout(window).Host(id);
        Assert.NotNull(Layout(window).Arrangement.UndockedPosition(id));
        Assert.Null(Layout(window).Arrangement.EdgeOf(id));
        Assert.Same(Layout(window).UndockedPanel, host.Parent);
        Assert.Equal(Orientation.Horizontal, CommandGroup.GetOrientation(host.Group));
        Assert.True(host.Grip.IsEffectivelyVisible);
        Assert.Equal(Dock.Left, DockPanel.GetDock(host.Grip));
        AssertInside(Area(window), HostRect(window, id));
    }

    private static void AssertDefault(MainWindow window)
    {
        Assert.Equal(DefaultTop, Layout(window).Arrangement[DockEdge.Top]);
        Assert.Empty(Layout(window).Arrangement[DockEdge.Bottom]);
        Assert.Empty(Layout(window).Arrangement[DockEdge.Left]);
        Assert.Equal(["layers"], Layout(window).Arrangement[DockEdge.Right]);
        Assert.Empty(Layout(window).Arrangement.Undocked);
        Assert.Empty(Layout(window).UndockedPanel.Children);
    }

    [AvaloniaFact]
    public void DroppingAGroupOverTheCanvasUndocksItWhereDropped()
    {
        var window = ShowWindow();
        var area = Area(window);
        var to = area.Center;

        Drag(window, "colours", to);

        AssertUndocked(window, "colours");
        Assert.DoesNotContain("colours", Layout(window).Arrangement[DockEdge.Top]);
        // The handle stays under the pointer.
        AssertClose(to, RectOf(Layout(window).Host("colours").Grip, window).Center);
        // The palette lays out as on the top edge: two rows of ten.
        var palette = Layout(window).Host("colours").Group.FindControl<Avalonia.Controls.Primitives.UniformGrid>("PaletteGrid")!;
        Assert.Equal((2, 10), (palette.Rows, palette.Columns));
    }

    [AvaloniaTheory]
    [InlineData(DockEdge.Left)]
    [InlineData(DockEdge.Right)]
    [InlineData(DockEdge.Top)]
    [InlineData(DockEdge.Bottom)]
    public void DroppingNearTheCanvasEdgeDocksTheGroup(DockEdge edge)
    {
        var window = ShowWindow();
        var area = Area(window);
        var inset = CommandBarDock.DockDistance - 4;
        var to = edge switch
        {
            DockEdge.Left => new Point(area.Left + inset, area.Center.Y),
            DockEdge.Right => new Point(area.Right - inset, area.Center.Y),
            DockEdge.Top => new Point(area.Center.X, area.Top + inset),
            _ => new Point(area.Center.X, area.Bottom - inset),
        };

        Drag(window, "colours", to);

        Assert.Equal(edge, Layout(window).Arrangement.EdgeOf("colours"));
        Assert.Empty(Layout(window).Arrangement.Undocked);
    }

    [AvaloniaFact]
    public void DraggingAnUndockedGroupMovesItOrDocksItAgain()
    {
        var window = ShowWindow();
        var area = Area(window);
        Drag(window, "colours", area.Center);
        var before = HostRect(window, "colours");

        Drag(window, "colours", RectOf(Layout(window).Host("colours").Grip, window).Center + new Point(-100, 60));

        AssertUndocked(window, "colours");
        AssertClose(before.Position + new Point(-100, 60), HostRect(window, "colours").Position);

        // Near the canvas's bottom edge: docked there.
        area = Area(window);
        Drag(window, "colours", new Point(area.Center.X, area.Bottom - 5));
        Assert.Equal(["colours"], Layout(window).Arrangement[DockEdge.Bottom]);
        Assert.Empty(Layout(window).Arrangement.Undocked);

        // Undocked again, then onto the top edge panel between the menu and History.
        Drag(window, "colours", area.Center);
        var history = HostRect(window, "history");
        Drag(window, "colours", new Point(history.Left + 2, history.Center.Y));
        Assert.Equal(["menu", "colours", "history", "clipboard", "image", "tools", "update"], Layout(window).Arrangement[DockEdge.Top]);
        Assert.Same(Layout(window).EdgePanel(DockEdge.Top), Layout(window).Host("colours").Parent);
    }

    [AvaloniaFact]
    public void UndockedGroupsStayInsideTheCanvasAreaWhenTheWindowShrinks()
    {
        var window = ShowWindow();
        var area = Area(window);

        // Dropped against the bottom-right corner, as far as it can go.
        Drag(window, "colours", new Point(area.Right - CommandBarDock.DockDistance - 1, area.Bottom - CommandBarDock.DockDistance - 1));
        Layout(window).Undock("colours", new Point(10_000, 10_000));
        window.UpdateLayout();
        AssertUndocked(window, "colours");
        var corner = HostRect(window, "colours");
        AssertClose(area.BottomRight, corner.BottomRight);

        window.Width = 600;
        window.Height = 450;
        window.UpdateLayout();
        AssertUndocked(window, "colours");
        AssertClose(Area(window).BottomRight, HostRect(window, "colours").BottomRight);

        // Kept relative to the canvas area, so it goes back when the window grows again.
        window.Width = 1100;
        window.Height = 750;
        window.UpdateLayout();
        AssertClose(corner.Position, HostRect(window, "colours").Position);
    }

    [AvaloniaFact]
    public void UndockingAndDockingMoveTheCanvasOnlyByTheEdgeLeftOrJoined()
    {
        var window = ShowWindow();
        Layout(window).Move("clipboard", DockEdge.Bottom, 0);
        window.UpdateLayout();
        var before = CanvasScroll(window);

        // While dragging, the outline does not move the canvas either.
        StartDrag(window, "clipboard", Area(window).Center);
        Assert.True(Layout(window).DragOutline.IsVisible);
        Assert.Equal(before, CanvasScroll(window));
        window.MouseUp(Area(window).Center, MouseButton.Left);
        window.UpdateLayout();

        // Leaving the bottom edge: only the canvas's bottom moves.
        AssertUndocked(window, "clipboard");
        var after = CanvasScroll(window);
        Assert.Equal((before.Left, before.Top, before.Right), (after.Left, after.Top, after.Right));
        Assert.True(after.Bottom > before.Bottom);

        // Docking to the left: only the canvas's left moves.
        before = after;
        Choose(window, "clipboard", LayoutStrings.MoveToLeft);
        after = CanvasScroll(window);
        Assert.Equal((before.Top, before.Right, before.Bottom), (after.Top, after.Right, after.Bottom));
        Assert.True(after.Left > before.Left);
    }

    [AvaloniaFact]
    public void AnUndockedGroupTakesPointerInputOverItsOwnAreaOnly()
    {
        var window = ShowWindow();
        Layout(window).Undock("colours", new Point(40, 40));
        window.UpdateLayout();
        var group = HostRect(window, "colours");
        var host = Layout(window).Host("colours");
        using var frame = window.CaptureRenderedFrame(); // hit testing uses the rendered scene

        // Over the group, between its controls: the group, never the canvas below it.
        var overGroup = (Visual)window.InputHitTest(new Point(group.Right - 2, group.Bottom - 2))!;
        Assert.True(overGroup == host || host.IsVisualAncestorOf(overGroup), $"{overGroup} is not part of the group");

        // Beside it: the canvas area, not the undocked groups' panel.
        var beside = (Visual)window.InputHitTest(new Point(group.Right + 20, group.Bottom + 20))!;
        Assert.False(beside == Layout(window).UndockedPanel || Layout(window).UndockedPanel.IsVisualAncestorOf(beside));
        Assert.True(window.FindControl<ScrollViewer>("CanvasScroll")!.IsVisualAncestorOf(beside));
    }

    [AvaloniaFact]
    public void ADashedOutlineShowsWhereTheDraggedGroupWillLand()
    {
        var window = ShowWindow();
        var outline = Layout(window).DragOutline;
        var colours = Layout(window).Host("colours");
        var size = colours.Bounds.Size;
        Assert.False(outline.IsVisible);
        Assert.NotNull(outline.StrokeDashArray);
        Assert.NotEmpty(outline.StrokeDashArray!);

        // Over the canvas: the undocked position, the handle under the pointer.
        var area = Area(window);
        StartDrag(window, "colours", area.Center);
        Assert.True(outline.IsVisible);
        Assert.Equal(0.5, colours.Opacity);
        var grip = RectOf(colours.Grip, colours).Center;
        AssertClose(area.Center - grip, OutlineRect(window).Position);
        Assert.Equal(size, OutlineRect(window).Size);

        // It follows the pointer.
        window.MouseMove(area.Center + new Point(30, 20));
        window.UpdateLayout();
        AssertClose(area.Center + new Point(30, 20) - grip, OutlineRect(window).Position);

        // Before History on the top edge: History's slot, at the group's size.
        var history = HostRect(window, "history");
        window.MouseMove(new Point(history.Left + 2, history.Center.Y));
        window.UpdateLayout();
        AssertClose(history.Position, OutlineRect(window).Position);
        Assert.Equal(size, OutlineRect(window).Size);

        // Near the canvas's left edge: the empty left edge's slot, turned sideways.
        window.MouseMove(new Point(area.Left + 5, area.Center.Y));
        window.UpdateLayout();
        AssertClose(RectOf(Layout(window).EdgePanel(DockEdge.Left), window).Position, OutlineRect(window).Position);
        Assert.Equal(new Size(size.Height, size.Width), OutlineRect(window).Size);

        // Dropped over the canvas, it lands in the outline shown; then the outline goes.
        window.MouseMove(area.Center);
        window.UpdateLayout();
        var shown = OutlineRect(window);
        window.MouseUp(area.Center, MouseButton.Left);
        window.UpdateLayout();
        Assert.False(outline.IsVisible);
        Assert.Equal(1, colours.Opacity);
        AssertClose(shown.Position, HostRect(window, "colours").Position);
    }

    [AvaloniaFact]
    public void EscCancelsADragAndLeavesTheLayoutUnchanged()
    {
        var window = ShowWindow(Store());
        var changes = 0;
        Layout(window).Changed += (_, _) => changes++;
        var colours = Layout(window).Host("colours");
        var area = Area(window);

        StartDrag(window, "colours", area.Center);
        Assert.True(Layout(window).DragOutline.IsVisible);
        Press(window, Key.Escape, PhysicalKey.Escape);

        Assert.False(Layout(window).DragOutline.IsVisible);
        Assert.Equal(1, colours.Opacity);
        window.MouseMove(area.Center + new Point(20, 0));
        window.MouseUp(area.Center + new Point(20, 0), MouseButton.Left);
        window.UpdateLayout();

        AssertDefault(window);
        Assert.Equal(0, changes);
        Assert.False(File.Exists(SettingsPath));
        Assert.False(Layout(window).DragOutline.IsVisible);
        Assert.False(colours.Grip.Flyout!.IsOpen);
    }

    [AvaloniaFact]
    public void UndockCommandPlacesTheGroupAtTheCanvasTopLeftAndMoveToDocksIt()
    {
        var window = ShowWindow();
        var colours = Layout(window).Host("colours");
        colours.Grip.Flyout!.ShowAt(colours.Grip);
        Assert.True(Item(colours, LayoutStrings.Undock).IsEnabled);
        Assert.Equal(LayoutStrings.Undock, AutomationProperties.GetName(Item(colours, LayoutStrings.Undock)));
        colours.Grip.Flyout.Hide();

        Choose(window, "colours", LayoutStrings.Undock);

        AssertUndocked(window, "colours");
        AssertClose(Area(window).Position, HostRect(window, "colours").Position);
        Assert.True(colours.Grip.IsFocused); // ready for the arrow keys
        colours.Grip.Flyout.ShowAt(colours.Grip);
        Assert.False(Item(colours, LayoutStrings.Undock).IsEnabled);
        Assert.False(Item(colours, LayoutStrings.MoveEarlier).IsEnabled);
        Assert.False(Item(colours, LayoutStrings.MoveLater).IsEnabled);
        Assert.All(CommandBarArrangement.Edges, edge => Assert.True(Item(colours, edge switch
        {
            DockEdge.Top => LayoutStrings.MoveToTop,
            DockEdge.Bottom => LayoutStrings.MoveToBottom,
            DockEdge.Left => LayoutStrings.MoveToLeft,
            _ => LayoutStrings.MoveToRight,
        }).IsEnabled));
        colours.Grip.Flyout.Hide();

        Choose(window, "colours", LayoutStrings.MoveToRight);

        Assert.Equal(["layers", "colours"], Layout(window).Arrangement[DockEdge.Right]);
        Assert.Empty(Layout(window).Arrangement.Undocked);
        Assert.Same(Layout(window).EdgePanel(DockEdge.Right), colours.Parent);
        Assert.Equal(Orientation.Vertical, CommandGroup.GetOrientation(colours.Group));
    }

    [AvaloniaFact]
    public void ArrowKeysOnAnUndockedGroupsHandleMoveIt10Pixels()
    {
        var window = ShowWindow();
        Layout(window).Undock("colours", new Point(100, 100));
        window.UpdateLayout();
        var colours = Layout(window).Host("colours");
        colours.Grip.Focus(NavigationMethod.Tab);
        var start = HostRect(window, "colours").Position;

        Press(window, Key.Right, PhysicalKey.ArrowRight);
        AssertClose(start + new Point(10, 0), HostRect(window, "colours").Position);
        Press(window, Key.Down, PhysicalKey.ArrowDown);
        Press(window, Key.Down, PhysicalKey.ArrowDown);
        AssertClose(start + new Point(10, 20), HostRect(window, "colours").Position);
        Press(window, Key.Left, PhysicalKey.ArrowLeft);
        Press(window, Key.Up, PhysicalKey.ArrowUp);
        AssertClose(start + new Point(0, 10), HostRect(window, "colours").Position);
        Assert.True(colours.Grip.IsFocused);
        Assert.Equal(new Point(100, 110), Layout(window).Arrangement.UndockedPosition("colours"));

        // Not past the canvas area's edge.
        for (var i = 0; i < 20; i++)
        {
            Press(window, Key.Left, PhysicalKey.ArrowLeft);
        }

        Assert.Equal(new Point(0, 110), Layout(window).Arrangement.UndockedPosition("colours"));

        // A docked group's handle leaves the arrow keys alone.
        var tools = Layout(window).Host("tools");
        tools.Grip.Focus(NavigationMethod.Tab);
        Press(window, Key.Right, PhysicalKey.ArrowRight);
        Assert.Equal(DockEdge.Top, Layout(window).Arrangement.EdgeOf("tools"));
    }

    [AvaloniaFact]
    public void UndockedGroupsAreSavedAndRestoredAtStart()
    {
        var first = ShowWindow(Store());
        Layout(first).Undock("colours", new Point(120, 80));
        Layout(first).Undock("history", new Point(10, 200));
        Layout(first).Move("tools", DockEdge.Left, 0);

        var saved = Store().Load().CommandBar!;
        Assert.Equal(["colours", "history"], saved.Undocked!.Select(u => u!.Id));

        var second = ShowWindow(Store());
        Assert.Equal(Layout(first).Arrangement.Undocked, Layout(second).Arrangement.Undocked);
        foreach (var edge in CommandBarArrangement.Edges)
        {
            Assert.Equal(Layout(first).Arrangement[edge], Layout(second).Arrangement[edge]);
        }

        AssertUndocked(second, "colours");
        AssertUndocked(second, "history");
        AssertClose(Area(second).Position + new Point(120, 80), HostRect(second, "colours").Position);
        // Bottom to top, as saved.
        Assert.Equal([Layout(second).Host("colours"), Layout(second).Host("history")], Layout(second).UndockedPanel.Children);
    }

    [AvaloniaFact]
    public void LayoutSavedByVersion020RestoresUnchanged()
    {
        Directory.CreateDirectory(_settingsDirectory);
        // As v0.2.0 writes it: edges only.
        File.WriteAllText(SettingsPath, """
            {
              "commandBar": {
                "top": ["menu", "history", "clipboard", "image", "tools", "update"],
                "bottom": [],
                "left": ["colours"],
                "right": ["layers"]
              }
            }
            """);

        var window = ShowWindow(Store());

        Assert.Equal(["menu", "history", "clipboard", "image", "tools", "update"], Layout(window).Arrangement[DockEdge.Top]);
        Assert.Equal(["colours"], Layout(window).Arrangement[DockEdge.Left]);
        Assert.Equal(["layers"], Layout(window).Arrangement[DockEdge.Right]);
        Assert.Empty(Layout(window).Arrangement[DockEdge.Bottom]);
        Assert.Empty(Layout(window).Arrangement.Undocked);
    }

    [AvaloniaTheory]
    [InlineData("""{"id": "colours", "x": -5, "y": 10}""")]
    [InlineData("""{"id": "colours", "x": 10, "y": -0.5}""")]
    [InlineData("""{"id": "colours", "x": 10}""")]
    [InlineData("""{"id": "colours", "x": null, "y": 10}""")]
    [InlineData("""{"x": 10, "y": 10}""")]
    [InlineData("""{"id": "colours", "x": "NaN", "y": 10}""")]
    [InlineData("""{"id": "colours", "x": 1e400, "y": 10}""")]
    [InlineData("""null""")]
    [InlineData("""{"id": "bogus", "x": 10, "y": 10}""")]
    public void BadUndockedPositionsDockTheGroupInItsDefaultPlace(string entry)
    {
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(SettingsPath, $$$"""{"commandBar": {"undocked": [{{{entry}}}]}}""");

        AssertDefault(ShowWindow(Store()));
    }

    [AvaloniaTheory]
    [InlineData(double.NaN, 10)]
    [InlineData(10, double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity, 10)]
    public void NonFinitePositionsDockTheGroupInItsDefaultPlace(double x, double y)
    {
        var window = ShowWindow();
        var restored = CommandBarArrangement.Restore(
            new CommandBarSettings { Undocked = [new UndockedGroupSettings { Id = "colours", X = x, Y = y }] },
            Layout(window).DefaultArrangement);

        Assert.Empty(restored.Undocked);
        Assert.Equal(DefaultTop, restored[DockEdge.Top]);
    }

    [AvaloniaFact]
    public void AGroupSavedOutsideTheWindowIsPulledBackIn()
    {
        Directory.CreateDirectory(_settingsDirectory);
        File.WriteAllText(SettingsPath, """{"commandBar": {"undocked": [{"id": "colours", "x": 99999, "y": 1e300}]}}""");

        var window = ShowWindow(Store(), windowWidth: 700, windowHeight: 500);

        AssertUndocked(window, "colours");
        AssertClose(Area(window).BottomRight, HostRect(window, "colours").BottomRight);
        Assert.DoesNotContain("colours", Layout(window).Arrangement[DockEdge.Top]);
    }

    [AvaloniaFact]
    public void ResetLayoutDocksEveryUndockedGroupInItsDefaultPlace()
    {
        var window = ShowWindow(Store());
        Layout(window).Undock("colours", new Point(50, 50));
        Layout(window).Undock("layers", new Point(200, 50));
        window.UpdateLayout();

        Choose(window, "colours", LayoutStrings.ResetLayout);

        AssertDefault(window);
        Assert.Same(Layout(window).EdgePanel(DockEdge.Top), Layout(window).Host("colours").Parent);
        Assert.Same(Layout(window).EdgePanel(DockEdge.Right), Layout(window).Host("layers").Parent);
        Assert.Empty(Store().Load().CommandBar!.Undocked!);
        AssertDefault(ShowWindow(Store()));
    }

    [AvaloniaFact]
    public void FullScreenHidesUndockedGroupsWithTheRestOfTheCommandBar()
    {
        var window = ShowWindow();
        Layout(window).Undock("colours", new Point(50, 50));
        var viewMenu = window.FindControl<View.ViewMenu>("ViewMenu")!;

        viewMenu.SetFullScreen(!viewMenu.IsFullScreen);
        window.UpdateLayout();
        Assert.False(Layout(window).Host("colours").IsEffectivelyVisible);

        viewMenu.SetFullScreen(!viewMenu.IsFullScreen);
        window.UpdateLayout();
        Assert.True(Layout(window).Host("colours").IsEffectivelyVisible);
    }
}
