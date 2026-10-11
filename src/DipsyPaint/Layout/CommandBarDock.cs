using Avalonia;
using Avalonia.Controls;
using DipsyPaint.Settings;

namespace DipsyPaint.Layout;

/// <summary>
/// Arranges the command groups on the four window edges (ADR-0022). The groups are the controls
/// with a <see cref="CommandGroup.IdProperty"/> found in the edge panels at construction; where they
/// are found is the default layout. Each is wrapped in a <see cref="CommandGroupHost"/> and can then
/// be moved to any edge. The edge panels wrap, so groups never clip.
/// </summary>
public sealed class CommandBarDock
{
    private readonly Control _owner;
    private readonly Control _content;
    private readonly Dictionary<DockEdge, Panel> _edges;
    private readonly Dictionary<string, CommandGroupHost> _hosts = new(StringComparer.Ordinal);
    private readonly CommandBarArrangement _default = new();

    /// <param name="owner">The window; groups keep its DataContext wherever they are docked.</param>
    /// <param name="content">The area the edges surround (the canvas); a drop over it goes to its nearest edge.</param>
    public CommandBarDock(Control owner, Control content, Panel top, Panel bottom, Panel left, Panel right)
    {
        _owner = owner;
        _content = content;
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

    /// <summary>Restores the default layout: every group on the top edge, as reference Paint's single command bar.</summary>
    public void Reset()
    {
        Arrangement = _default.Clone();
        Arrange();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Docks <paramref name="host"/> where it was dropped; <paramref name="point"/> is in window coordinates.</summary>
    internal void Drop(CommandGroupHost host, Point point)
    {
        var edge = EdgeAt(point);
        var vertical = edge is DockEdge.Left or DockEdge.Right;
        var others = Arrangement[edge].Where(id => id != host.Id).ToList();
        var index = others.Count;
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
                index = i;
                break;
            }
        }

        Move(host.Id, edge, index);
    }

    // The edge under the point, or the content area's edge nearest to it.
    private DockEdge EdgeAt(Point point)
    {
        foreach (var (edge, panel) in _edges)
        {
            if (RectOf(panel) is { } rect && rect.Contains(point))
            {
                return edge;
            }
        }

        var c = RectOf(_content) ?? default;
        return new[]
        {
            (Edge: DockEdge.Top, Distance: point.Y - c.Top),
            (Edge: DockEdge.Bottom, Distance: c.Bottom - point.Y),
            (Edge: DockEdge.Left, Distance: point.X - c.Left),
            (Edge: DockEdge.Right, Distance: c.Right - point.X),
        }.MinBy(d => d.Distance).Edge;
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
        foreach (var panel in _edges.Values)
        {
            panel.Children.RemoveAll(panel.Children.Where(hosts.Contains).ToList());
        }

        foreach (var (edge, panel) in _edges)
        {
            panel.Children.AddRange(Arrangement[edge].Select(id => _hosts[id]));
        }
    }
}
