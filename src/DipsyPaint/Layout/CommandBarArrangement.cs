using Avalonia;
using DipsyPaint.Settings;

namespace DipsyPaint.Layout;

/// <summary>A window edge a command group can be docked to (ADR-0022). Groups can also be undocked over the canvas (ADR-0026).</summary>
public enum DockEdge
{
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>
/// Which edge each command group is docked to, and the groups' order along each edge (left to
/// right, or top to bottom); or, for an undocked group, its position over the canvas area. Plain
/// data, so it can be saved and restored without the UI.
/// </summary>
public sealed class CommandBarArrangement
{
    private readonly Dictionary<DockEdge, List<string>> _edges = Enum.GetValues<DockEdge>().ToDictionary(e => e, _ => new List<string>());
    private readonly List<(string Id, Point Position)> _undocked = [];

    public static IReadOnlyList<DockEdge> Edges { get; } = Enum.GetValues<DockEdge>();

    /// <summary>The ids of the groups docked to <paramref name="edge"/>, in order.</summary>
    public IReadOnlyList<string> this[DockEdge edge] => _edges[edge];

    /// <summary>
    /// The undocked groups, bottom to top, each with its top-left corner relative to the canvas
    /// area's, in device-independent pixels.
    /// </summary>
    public IReadOnlyList<(string Id, Point Position)> Undocked => _undocked;

    /// <summary>All group ids, edge by edge, then the undocked ones.</summary>
    public IEnumerable<string> Ids => Edges.SelectMany(e => _edges[e]).Concat(_undocked.Select(u => u.Id));

    public CommandBarArrangement Clone()
    {
        var copy = new CommandBarArrangement();
        foreach (var edge in Edges)
        {
            copy._edges[edge].AddRange(_edges[edge]);
        }

        copy._undocked.AddRange(_undocked);
        return copy;
    }

    /// <summary>The edge <paramref name="id"/> is docked to, or null if it is not arranged.</summary>
    public DockEdge? EdgeOf(string id) => Edges.Cast<DockEdge?>().FirstOrDefault(e => _edges[e!.Value].Contains(id));

    /// <summary>Where undocked group <paramref name="id"/> is, or null if it is docked or not arranged.</summary>
    public Point? UndockedPosition(string id) => _undocked.Where(u => u.Id == id).Select(u => (Point?)u.Position).FirstOrDefault();

    /// <summary>
    /// Docks <paramref name="id"/> to <paramref name="edge"/> at <paramref name="index"/>, counted
    /// among the edge's other groups; out-of-range indexes are clamped.
    /// </summary>
    public void Move(string id, DockEdge edge, int index)
    {
        Remove(id);
        var target = _edges[edge];
        target.Insert(Math.Clamp(index, 0, target.Count), id);
    }

    /// <summary>Undocks <paramref name="id"/> at <paramref name="position"/>, above the other undocked groups.</summary>
    public void Undock(string id, Point position)
    {
        Remove(id);
        _undocked.Add((id, position));
    }

    /// <summary>The arrangement in its saved form.</summary>
    public CommandBarSettings ToSettings() => new()
    {
        Top = [.. _edges[DockEdge.Top]],
        Bottom = [.. _edges[DockEdge.Bottom]],
        Left = [.. _edges[DockEdge.Left]],
        Right = [.. _edges[DockEdge.Right]],
        Undocked = [.. _undocked.Select(u => new UndockedGroupSettings { Id = u.Id, X = u.Position.X, Y = u.Position.Y })],
    };

    /// <summary>
    /// The arrangement <paramref name="saved"/> describes, made valid against
    /// <paramref name="defaults"/>, which lists every known group: unknown and repeated ids are
    /// dropped, as are undocked groups without a valid position (finite and not negative; one beyond
    /// the canvas area is pulled back in when shown). Groups the saved layout lacks (e.g. added in a
    /// later version) go to their default edge, after the group that precedes them there by default.
    /// No saved layout gives the defaults.
    /// </summary>
    public static CommandBarArrangement Restore(CommandBarSettings? saved, CommandBarArrangement defaults)
    {
        if (saved is null)
        {
            return defaults.Clone();
        }

        var known = defaults.Ids.ToHashSet(StringComparer.Ordinal);
        var result = new CommandBarArrangement();
        foreach (var edge in Edges)
        {
            foreach (var id in SavedList(saved, edge) ?? [])
            {
                if (id is not null && known.Contains(id) && !result.Contains(id))
                {
                    result._edges[edge].Add(id);
                }
            }
        }

        foreach (var group in saved.Undocked ?? [])
        {
            if (group is { Id: { } id, X: { } x, Y: { } y } && known.Contains(id) && !result.Contains(id)
                && double.IsFinite(x) && double.IsFinite(y) && x >= 0 && y >= 0)
            {
                result._undocked.Add((id, new Point(x, y)));
            }
        }

        foreach (var edge in Edges)
        {
            var order = defaults._edges[edge];
            for (var i = 0; i < order.Count; i++)
            {
                if (result.Contains(order[i]))
                {
                    continue;
                }

                var target = result._edges[edge];
                var after = order.Take(i).LastOrDefault(target.Contains);
                target.Insert(after is null ? 0 : target.IndexOf(after) + 1, order[i]);
            }
        }

        return result;
    }

    private bool Contains(string id) => EdgeOf(id) is not null || UndockedPosition(id) is not null;

    private void Remove(string id)
    {
        foreach (var list in _edges.Values)
        {
            list.Remove(id);
        }

        _undocked.RemoveAll(u => u.Id == id);
    }

    private static List<string?>? SavedList(CommandBarSettings saved, DockEdge edge) => edge switch
    {
        DockEdge.Top => saved.Top,
        DockEdge.Bottom => saved.Bottom,
        DockEdge.Left => saved.Left,
        _ => saved.Right,
    };
}
