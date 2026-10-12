using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using DipsyPaint.Settings;

namespace DipsyPaint.Layout;

/// <summary>
/// Arranges the command groups on the four window edges (ADR-0022) and undocked over the canvas
/// area (ADR-0026). The groups are the controls with a <see cref="CommandGroup.IdProperty"/> found in
/// the edge panels at construction; where they are found is the default layout. Each is wrapped in a
/// <see cref="CommandGroupHost"/> and can then be moved to any edge or undocked. The edge panels
/// wrap, so docked groups never clip.
/// </summary>
public sealed class CommandBarDock
{
    /// <summary>
    /// How far inside the canvas area's edge a drop still docks to that edge, in device-independent
    /// pixels: a little more than a ruler (20), so a drop on a ruler docks, while the rest of the
    /// canvas area undocks.
    /// </summary>
    public const double DockDistance = 32;

    private readonly Control _owner;
    private readonly UndockedGroupsPanel _undocked;
    private readonly Rectangle _dragOutline;
    private readonly Dictionary<DockEdge, Panel> _edges;
    private readonly Dictionary<string, CommandGroupHost> _hosts = new(StringComparer.Ordinal);
    private readonly CommandBarArrangement _default = new();

    /// <param name="owner">The window; groups keep its DataContext wherever they are docked.</param>
    /// <param name="undocked">
    /// The panel over the area the edges surround (the canvas area), which holds the undocked groups;
    /// a drop over it undocks the group, or docks it to the nearest edge if within <see cref="DockDistance"/> of it.
    /// </param>
    /// <param name="dragOutline">Shows where a dragged group will land; in a canvas over the whole window, so it never moves the layout.</param>
    public CommandBarDock(Control owner, UndockedGroupsPanel undocked, Rectangle dragOutline, Panel top, Panel bottom, Panel left, Panel right)
    {
        _owner = owner;
        _undocked = undocked;
        _dragOutline = dragOutline;
        _edges = new() { [DockEdge.Top] = top, [DockEdge.Bottom] = bottom, [DockEdge.Left] = left, [DockEdge.Right] = right };
        foreach (var (edge, panel) in _edges)
        {
            foreach (var group in panel.Children.Where(c => CommandGroup.GetId(c) is not null).ToList())
            {
                var id = CommandGroup.GetId(group)!;
                if (_hosts.ContainsKey(id))
                {
                    throw new InvalidOperationException($"Command group id '{id}' is used twice.");
                }

                panel.Children.Remove(group);
                var host = new CommandGroupHost(this, id, group);
                // Bound rather than inherited, so moving a group does not briefly clear its DataContext.
                host.Bind(StyledElement.DataContextProperty, owner.GetObservable(StyledElement.DataContextProperty));
                _hosts[id] = host;
                _default.Move(id, edge, int.MaxValue);
            }
        }

        Arrangement = _default.Clone();
        Arrange();
    }

    /// <summary>Raised when the user changes the layout (a move or Reset layout), not on <see cref="Restore"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>The current layout.</summary>
    public CommandBarArrangement Arrangement { get; private set; }

    /// <summary>The default layout, as declared in XAML.</summary>
    public CommandBarArrangement DefaultArrangement => _default.Clone();

    /// <summary>The host of the group with <paramref name="id"/>.</summary>
    public CommandGroupHost Host(string id) => _hosts[id];

    /// <summary>The edge panel of <paramref name="edge"/>.</summary>
    public Panel EdgePanel(DockEdge edge) => _edges[edge];

    /// <summary>The panel over the canvas area that holds the undocked groups.</summary>
    public UndockedGroupsPanel UndockedPanel => _undocked;

    /// <summary>The dashed outline shown while a group is dragged, where it will land.</summary>
    public Rectangle DragOutline => _dragOutline;

    /// <summary>Shows a saved layout; see <see cref="CommandBarArrangement.Restore"/> for how bad data is handled.</summary>
    public void Restore(CommandBarSettings? saved)
    {
        Arrangement = CommandBarArrangement.Restore(saved, _default);
        Arrange();
    }

    /// <summary>Docks group <paramref name="id"/> to <paramref name="edge"/> at <paramref name="index"/> (clamped).</summary>
    public void Move(string id, DockEdge edge, int index)
    {
        Arrangement.Move(id, edge, index);
        Arrange();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves group <paramref name="id"/> past its next visible neighbour on its edge, earlier (-1) or later (+1).</summary>
    public void MoveBy(string id, int direction)
    {
        if (NeighbourIndex(id, direction) is { } index)
        {
            Move(id, Arrangement.EdgeOf(id)!.Value, index);
        }
    }

    public bool CanMoveBy(string id, int direction) => NeighbourIndex(id, direction) is not null;

    /// <summary>
    /// Undocks group <paramref name="id"/> over the canvas area, its top-left corner at
    /// <paramref name="position"/> from the area's (pulled inside the area), above the other undocked groups.
    /// </summary>
    public void Undock(string id, Point position)
    {
        var size = SizeIn(_hosts[id], Orientation.Horizontal);
        Arrangement.Undock(id, UndockedGroupsPanel.Clamp(position, size, _undocked.Bounds.Size));
        Arrange();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves undocked group <paramref name="id"/> by <paramref name="offset"/> from where it is shown.</summary>
    internal void MoveUndocked(string id, Vector offset) => Undock(id, _hosts[id].Bounds.Position + offset);

    /// <summary>Restores the default layout: every group on the top edge, as reference Paint's single command bar.</summary>
    public void Reset()
    {
        Arrangement = _default.Clone();
        Arrange();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Where <paramref name="host"/> would land if dropped at <paramref name="point"/>, in window
    /// coordinates, having been grabbed at <paramref name="grabbedAt"/> within it.
    /// </summary>
    internal DropTarget TargetAt(CommandGroupHost host, Point point, Point grabbedAt)
    {
        var area = RectOf(_undocked) ?? default;
        if (area.Deflate(DockDistance).Contains(point))
        {
            // Undocked where dropped, the handle staying under the pointer.
            var size = SizeIn(host, Orientation.Horizontal);
            var position = UndockedGroupsPanel.Clamp(new Point(point.X - grabbedAt.X - area.X, point.Y - grabbedAt.Y - area.Y), size, area.Size);
            return new DropTarget(null, 0, position, new Rect(area.Position + position, size));
        }

        var edge = EdgeAt(point, area);
        var vertical = edge is DockEdge.Left or DockEdge.Right;
        var slot = SizeIn(host, vertical ? Orientation.Vertical : Orientation.Horizontal);
        var others = Arrangement[edge].Where(id => id != host.Id).ToList();
        Rect? last = null;
        for (var i = 0; i < others.Count; i++)
        {
            var other = _hosts[others[i]];
            if (!other.IsVisible || RectOf(other) is not { } r)
            {
                continue;
            }

            // Reading order: before a group on an earlier line, or on the same line nearer the start.
            var before = vertical
                ? point.X < r.Left || (point.X < r.Right && point.Y < r.Center.Y)
                : point.Y < r.Top || (point.Y < r.Bottom && point.X < r.Center.X);
            if (before)
            {
                return new DropTarget(edge, i, default, new Rect(r.Position, slot));
            }

            last = r;
        }

        // After the edge's last group, or alone on an empty edge against the window's side.
        var panel = RectOf(_edges[edge]) ?? default;
        var at = last is { } l
            ? (vertical ? l.BottomLeft : l.TopRight)
            : edge switch
            {
                DockEdge.Right => new Point(panel.Right - slot.Width, panel.Top),
                DockEdge.Bottom => new Point(panel.Left, panel.Bottom - slot.Height),
                _ => panel.Position,
            };
        return new DropTarget(edge, others.Count, default, new Rect(at, slot));
    }

    /// <summary>Moves <paramref name="host"/> to <paramref name="target"/>, from <see cref="TargetAt"/>.</summary>
    internal void Drop(CommandGroupHost host, DropTarget target)
    {
        if (target.Edge is { } edge)
        {
            Move(host.Id, edge, target.Index);
        }
        else
        {
            // Leaving an edge can move the canvas area (e.g. the top edge needs fewer rows); keep the
            // group where it was dropped, as the outline showed.
            var area = RectOf(_undocked)?.Position ?? default;
            Arrangement.Undock(host.Id, target.Position);
            Arrange();
            _owner.UpdateLayout();
            if (RectOf(_undocked) is { } moved && moved.Position != area)
            {
                var position = UndockedGroupsPanel.Clamp(target.Position + (area - moved.Position), host.DesiredSize, moved.Size);
                Arrangement.Undock(host.Id, position);
                UndockedGroupsPanel.SetPosition(host, position);
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Shows the drag outline at <paramref name="outline"/>, in window coordinates, or hides it if null.</summary>
    internal void ShowOutline(Rect? outline)
    {
        _dragOutline.IsVisible = outline is not null;
        if (outline is { } r && _dragOutline.Parent is Visual overlay && _owner.TranslatePoint(r.Position, overlay) is { } at)
        {
            Canvas.SetLeft(_dragOutline, at.X);
            Canvas.SetTop(_dragOutline, at.Y);
            _dragOutline.Width = r.Width;
            _dragOutline.Height = r.Height;
        }
    }

    // The edge under the point, or the canvas area's edge nearest to it.
    private DockEdge EdgeAt(Point point, Rect area)
    {
        foreach (var (edge, panel) in _edges)
        {
            if (RectOf(panel) is { } rect && rect.Contains(point))
            {
                return edge;
            }
        }

        return new[]
        {
            (Edge: DockEdge.Top, Distance: point.Y - area.Top),
            (Edge: DockEdge.Bottom, Distance: area.Bottom - point.Y),
            (Edge: DockEdge.Left, Distance: point.X - area.Left),
            (Edge: DockEdge.Right, Distance: area.Right - point.X),
        }.MinBy(d => d.Distance).Edge;
    }

    // The host's size laid out in the orientation: turned sideways if it is laid out the other way now.
    // The real size can differ a little (ADR-0026), but e.g. the palette's 2 x 10 grid becomes 10 x 2.
    private static Size SizeIn(CommandGroupHost host, Orientation orientation)
    {
        var size = host.Bounds.Size;
        return CommandGroup.GetOrientation(host) == orientation ? size : new Size(size.Height, size.Width);
    }

    private Rect? RectOf(Control control) =>
        control.TranslatePoint(default, _owner) is { } topLeft ? new Rect(topLeft, control.Bounds.Size) : null;

    private int? NeighbourIndex(string id, int direction)
    {
        if (Arrangement.EdgeOf(id) is not { } edge)
        {
            return null;
        }

        var ids = Arrangement[edge].ToList();
        for (var i = ids.IndexOf(id) + direction; i >= 0 && i < ids.Count; i += direction)
        {
            if (_hosts[ids[i]].IsVisible)
            {
                return i;
            }
        }

        return null;
    }

    private void Arrange()
    {
        var hosts = _hosts.Values.ToHashSet();
        foreach (var panel in _edges.Values.Append(_undocked))
        {
            panel.Children.RemoveAll(panel.Children.Where(hosts.Contains).ToList());
        }

        foreach (var (edge, panel) in _edges)
        {
            panel.Children.AddRange(Arrangement[edge].Select(id => _hosts[id]));
        }

        foreach (var (id, position) in Arrangement.Undocked)
        {
            UndockedGroupsPanel.SetPosition(_hosts[id], position);
            _undocked.Children.Add(_hosts[id]);
        }
    }
}

/// <summary>
/// Where a dragged command group would land: on <see cref="Edge"/> at <see cref="Index"/>, or, if
/// <see cref="Edge"/> is null, undocked at <see cref="Position"/> from the canvas area's top-left
/// corner. <see cref="Outline"/> is the space it would take, in window coordinates.
/// </summary>
internal readonly record struct DropTarget(DockEdge? Edge, int Index, Point Position, Rect Outline);
