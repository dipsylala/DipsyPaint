using DipsyPaint.Settings;

namespace DipsyPaint.Layout;

/// <summary>A window edge a command group can be docked to (ADR-0022). There are no floating groups.</summary>
public enum DockEdge
{
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>
/// Which edge each command group is docked to, and the groups' order along each edge (left to
/// right, or top to bottom). Plain data, so it can be saved and restored without the UI.
/// </summary>
public sealed class CommandBarArrangement
{
    private readonly Dictionary<DockEdge, List<string>> _edges = Enum.GetValues<DockEdge>().ToDictionary(e => e, _ => new List<string>());

    public static IReadOnlyList<DockEdge> Edges { get; } = Enum.GetValues<DockEdge>();

    /// <summary>The ids of the groups docked to <paramref name="edge"/>, in order.</summary>
    public IReadOnlyList<string> this[DockEdge edge] => _edges[edge];

    /// <summary>All group ids, edge by edge.</summary>
    public IEnumerable<string> Ids => Edges.SelectMany(e => _edges[e]);

    public CommandBarArrangement Clone()
    {
        var copy = new CommandBarArrangement();
        foreach (var edge in Edges)
        {
            copy._edges[edge].AddRange(_edges[edge]);
        }

        return copy;
    }

    /// <summary>The edge <paramref name="id"/> is docked to, or null if it is not arranged.</summary>
    public DockEdge? EdgeOf(string id) => Edges.Cast<DockEdge?>().FirstOrDefault(e => _edges[e!.Value].Contains(id));

    /// <summary>
    /// Docks <paramref name="id"/> to <paramref name="edge"/> at <paramref name="index"/>, counted
    /// among the edge's other groups; out-of-range indexes are clamped.
    /// </summary>
    public void Move(string id, DockEdge edge, int index)
    {
        foreach (var list in _edges.Values)
        {
            list.Remove(id);
        }

        var target = _edges[edge];
        target.Insert(Math.Clamp(index, 0, target.Count), id);
    }

    /// <summary>The arrangement in its saved form.</summary>
    public CommandBarSettings ToSettings() => new()
    {
        Top = [.. _edges[DockEdge.Top]],
        Bottom = [.. _edges[DockEdge.Bottom]],
        Left = [.. _edges[DockEdge.Left]],
        Right = [.. _edges[DockEdge.Right]],
    };

    /// <summary>
    /// The arrangement <paramref name="saved"/> describes, made valid against
    /// <paramref name="defaults"/>, which lists every known group: unknown and repeated ids are
    /// dropped, and groups the saved layout lacks (e.g. added in a later version) go to their
    /// default edge, after the group that precedes them there by default. No saved layout gives
    /// the defaults.
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
                if (id is not null && known.Contains(id) && result.EdgeOf(id) is null)
                {
                    result._edges[edge].Add(id);
                }
            }
        }

        foreach (var edge in Edges)
        {
            var order = defaults._edges[edge];
            for (var i = 0; i < order.Count; i++)
            {
                if (result.EdgeOf(order[i]) is not null)
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

    private static List<string?>? SavedList(CommandBarSettings saved, DockEdge edge) => edge switch
    {
        DockEdge.Top => saved.Top,
        DockEdge.Bottom => saved.Bottom,
        DockEdge.Left => saved.Left,
        _ => saved.Right,
    };
}
