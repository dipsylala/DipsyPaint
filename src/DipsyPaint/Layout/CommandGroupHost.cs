using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using DipsyPaint.Resources;

namespace DipsyPaint.Layout;

/// <summary>
/// Wraps a command group with its move handle: a button that can be dragged to another place on
/// any window edge, or clicked (or pressed with Enter or Space) for a "Move to" menu, so every move
/// can also be made without a mouse.
/// </summary>
public sealed class CommandGroupHost : DockPanel
{
    private const double DragThreshold = 4;

    // Two columns of three dots.
    private const string GripIcon = "M0,0 h2 v2 h-2 z M4,0 h2 v2 h-2 z M0,4 h2 v2 h-2 z M4,4 h2 v2 h-2 z M0,8 h2 v2 h-2 z M4,8 h2 v2 h-2 z";

    private readonly CommandBarDock _dock;
    private readonly PathIcon _gripIcon;
    private readonly Dictionary<DockEdge, MenuItem> _moveToItems = [];
    private readonly MenuItem _earlierItem;
    private readonly MenuItem _laterItem;
    private Point? _pressedAt;
    private bool _dragging;

    internal CommandGroupHost(CommandBarDock dock, string id, Control group)
    {
        _dock = dock;
        Id = id;
        Group = group;
        var title = CommandGroup.GetTitle(group) ?? id;

        _gripIcon = new PathIcon { Data = Geometry.Parse(GripIcon), Width = 6, Height = 10 };
        Grip = new Button
        {
            Content = _gripIcon,
            Padding = default,
            Margin = new Thickness(2),
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.SizeAll),
        };
        AutomationProperties.SetName(Grip, string.Format(LayoutStrings.MoveGroupFormat, title));
        ToolTip.SetTip(Grip, string.Format(LayoutStrings.MoveGroupToolTipFormat, title));

        var menu = new MenuFlyout();
        foreach (var (edge, text) in new[]
                 {
                     (DockEdge.Top, LayoutStrings.MoveToTop), (DockEdge.Bottom, LayoutStrings.MoveToBottom),
                     (DockEdge.Left, LayoutStrings.MoveToLeft), (DockEdge.Right, LayoutStrings.MoveToRight),
                 })
        {
            var item = MenuItemFor(text, () => _dock.Move(Id, edge, int.MaxValue));
            _moveToItems[edge] = item;
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        _earlierItem = MenuItemFor(LayoutStrings.MoveEarlier, () => _dock.MoveBy(Id, -1));
        _laterItem = MenuItemFor(LayoutStrings.MoveLater, () => _dock.MoveBy(Id, 1));
        menu.Items.Add(_earlierItem);
        menu.Items.Add(_laterItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItemFor(LayoutStrings.ResetLayout, _dock.Reset));
        menu.Opening += (_, _) => UpdateMenu();
        Grip.Flyout = menu;

        Grip.AddHandler(PointerPressedEvent, OnGripPressed, RoutingStrategies.Tunnel);
        Grip.AddHandler(PointerMovedEvent, OnGripMoved, RoutingStrategies.Tunnel);
        // After the button's own handling, so a drag that ends away from the handle does not also click it.
        Grip.AddHandler(PointerReleasedEvent, OnGripReleased, RoutingStrategies.Bubble, handledEventsToo: true);
        Grip.AddHandler(PointerCaptureLostEvent, (_, _) => EndDrag(), RoutingStrategies.Direct);

        Children.Add(Grip);
        Children.Add(group);
        // A hidden group (e.g. the update button before an update is ready) hides its handle too.
        Bind(IsVisibleProperty, group.GetObservable(IsVisibleProperty));
        ApplyOrientation(CommandGroup.GetOrientation(this));
    }

    /// <summary>The group's stable id.</summary>
    public string Id { get; }

    /// <summary>The command group control itself.</summary>
    public Control Group { get; }

    /// <summary>The move handle: drag it to move the group, or click it for the "Move to" menu.</summary>
    public Button Grip { get; }

    /// <summary>The "Move to" menu's items, for tests and automation.</summary>
    public IEnumerable<MenuItem> MenuItems => ((MenuFlyout)Grip.Flyout!).Items.OfType<MenuItem>();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CommandGroup.OrientationProperty)
        {
            ApplyOrientation(change.GetNewValue<Orientation>());
        }
    }

    private MenuItem MenuItemFor(string text, Action action)
    {
        var item = new MenuItem { Header = text };
        AutomationProperties.SetName(item, text);
        item.Click += (_, _) =>
        {
            action();
            Grip.Focus(NavigationMethod.Tab);
        };
        return item;
    }

    private void UpdateMenu()
    {
        var edge = _dock.Arrangement.EdgeOf(Id);
        foreach (var (target, item) in _moveToItems)
        {
            item.IsEnabled = target != edge;
        }

        _earlierItem.IsEnabled = _dock.CanMoveBy(Id, -1);
        _laterItem.IsEnabled = _dock.CanMoveBy(Id, 1);
    }

    // Horizontal edges put the handle before the group; vertical edges put it above, turned sideways.
    private void ApplyOrientation(Orientation orientation)
    {
        var vertical = orientation == Orientation.Vertical;
        SetDock(Grip, vertical ? Dock.Top : Dock.Left);
        Grip.Width = vertical ? double.NaN : 14;
        Grip.Height = vertical ? 14 : double.NaN;
        Grip.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grip.VerticalAlignment = VerticalAlignment.Stretch;
        _gripIcon.RenderTransform = vertical ? new RotateTransform(90) : null;
    }

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(Grip).Properties.IsLeftButtonPressed && TopLevel.GetTopLevel(this) is { } top)
        {
            _pressedAt = e.GetPosition(top);
            _dragging = false;
            e.Pointer.Capture(Grip);
        }
    }

    private void OnGripMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedAt is { } start && !_dragging && TopLevel.GetTopLevel(this) is { } top)
        {
            var delta = e.GetPosition(top) - start;
            _dragging = Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold;
            Opacity = _dragging ? 0.5 : 1; // shows which group is being moved
        }
    }

    private void OnGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        var dragged = _dragging;
        EndDrag();
        if (dragged && TopLevel.GetTopLevel(this) is { } top)
        {
            _dock.Drop(this, e.GetPosition(top));
        }
    }

    private void EndDrag()
    {
        _pressedAt = null;
        _dragging = false;
        Opacity = 1;
    }
}
