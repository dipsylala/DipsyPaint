namespace DipsyPaint.Engine;

/// <summary>
/// The current selection (see <see cref="DocumentEditor.Selection"/>): where it is, in document
/// pixels, and its pixels once lifted off the layer (row-major, premultiplied,
/// <paramref name="Bounds"/>-sized), else null, in which case its pixels are the active layer's
/// within <paramref name="Bounds"/>. A lifted selection may extend past the canvas. The floating
/// pixels are the array shown in the overlay; callers must not change them. Lifted pixels outside
/// the selection's shape are transparent.
/// </summary>
/// <param name="Mask">
/// Which pixels within <paramref name="Bounds"/> are selected (row-major, <paramref name="Bounds"/>-sized),
/// for a free-form or inverted selection; null when the whole rectangle is selected. Callers must not
/// change it.
/// </param>
public readonly record struct Selection(IntRect Bounds, Rgba[]? FloatingPixels, bool[]? Mask = null)
{
    /// <summary>Whether the document pixel (<paramref name="x"/>, <paramref name="y"/>) is selected.</summary>
    public bool Contains(int x, int y) =>
        x >= Bounds.X && x < Bounds.Right && y >= Bounds.Y && y < Bounds.Bottom
        && (Mask is null || Mask[(y - Bounds.Y) * Bounds.Width + x - Bounds.X]);
}

/// <summary>
/// Builds selection shapes as a bounding rectangle plus a mask (see <see cref="Selection.Mask"/>),
/// for <see cref="DocumentEditor.Select"/> and <see cref="Tools.SelectionTool.Select"/>. Each returns
/// a selection with no floating pixels, trimmed to the selected pixels, or null when none are selected.
/// </summary>
public static class SelectionMasks
{
    /// <summary>
    /// The pixels enclosed by a path through the given pixels, closed with a straight line back to the
    /// first, plus the pixels the path runs through (even-odd rule, as for a free-form selection). Null
    /// for a click (fewer than two distinct pixels).
    /// </summary>
    public static Selection? FromPath(IReadOnlyList<(int X, int Y)> path)
    {
        if (path.Count == 0 || path.All(p => p == path[0]))
        {
            return null;
        }

        int left = path.Min(p => p.X), top = path.Min(p => p.Y);
        var bounds = new IntRect(left, top, path.Max(p => p.X) - left + 1, path.Max(p => p.Y) - top + 1);
        var mask = new bool[bounds.Width * bounds.Height];
        var crossings = new List<double>();
        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            crossings.Clear();
            for (var i = 0; i < path.Count; i++)
            {
                var (a, b) = (path[i], path[(i + 1) % path.Count]);
                if ((a.Y <= y && y < b.Y) || (b.Y <= y && y < a.Y))
                {
                    crossings.Add(a.X + (double)(y - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                }
            }

            crossings.Sort();
            for (var i = 0; i + 1 < crossings.Count; i += 2)
            {
                for (var x = (int)Math.Ceiling(crossings[i]); x <= (int)Math.Floor(crossings[i + 1]); x++)
                {
                    mask[(y - bounds.Y) * bounds.Width + x - bounds.X] = true;
                }
            }
        }

        for (var i = 0; i < path.Count; i++)
        {
            MarkLine(mask, bounds, path[i], path[(i + 1) % path.Count]);
        }

        return Trim(bounds, mask);
    }

    /// <summary>
    /// Every pixel of <paramref name="canvas"/> outside <paramref name="selection"/> (its shape at its
    /// current place); the whole canvas when there is no selection.
    /// </summary>
    public static Selection? Invert(Selection? selection, IntRect canvas)
    {
        if (selection is not { } current)
        {
            return new Selection(canvas, null);
        }

        var mask = new bool[canvas.Width * canvas.Height];
        for (var y = 0; y < canvas.Height; y++)
        {
            for (var x = 0; x < canvas.Width; x++)
            {
                mask[y * canvas.Width + x] = !current.Contains(canvas.X + x, canvas.Y + y);
            }
        }

        return Trim(canvas, mask);
    }

    /// <summary>
    /// The selection of the pixels set in <paramref name="mask"/> (row-major, <paramref name="bounds"/>-sized),
    /// shrunk to their bounding rectangle; its mask is null when they fill it. Null when none is set.
    /// The result may share <paramref name="mask"/>, so the caller must not change it afterwards.
    /// </summary>
    public static Selection? Trim(IntRect bounds, bool[] mask)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(mask.Length, Math.Max(bounds.Width * bounds.Height, 0), nameof(mask));
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue, count = 0;
        for (var y = 0; y < bounds.Height; y++)
        {
            for (var x = 0; x < bounds.Width; x++)
            {
                if (mask[y * bounds.Width + x])
                {
                    (left, right) = (Math.Min(left, x), Math.Max(right, x));
                    (top, bottom) = (Math.Min(top, y), Math.Max(bottom, y));
                    count++;
                }
            }
        }

        if (count == 0)
        {
            return null;
        }

        var trimmed = new IntRect(bounds.X + left, bounds.Y + top, right - left + 1, bottom - top + 1);
        if (count == trimmed.Width * trimmed.Height)
        {
            return new Selection(trimmed, null);
        }

        if (trimmed == bounds)
        {
            return new Selection(bounds, null, mask);
        }

        var result = new bool[trimmed.Width * trimmed.Height];
        for (var y = 0; y < trimmed.Height; y++)
        {
            mask.AsSpan((top + y) * bounds.Width + left, trimmed.Width).CopyTo(result.AsSpan(y * trimmed.Width));
        }

        return new Selection(trimmed, null, result);
    }

    // Bresenham, both ends included.
    private static void MarkLine(bool[] mask, IntRect bounds, (int X, int Y) from, (int X, int Y) to)
    {
        int dx = Math.Abs(to.X - from.X), dy = -Math.Abs(to.Y - from.Y);
        int sx = from.X < to.X ? 1 : -1, sy = from.Y < to.Y ? 1 : -1;
        var (x, y) = from;
        var error = dx + dy;
        while (true)
        {
            mask[(y - bounds.Y) * bounds.Width + x - bounds.X] = true;
            if ((x, y) == to)
            {
                return;
            }

            var doubled = 2 * error;
            if (doubled >= dy)
            {
                error += dy;
                x += sx;
            }

            if (doubled <= dx)
            {
                error += dx;
                y += sy;
            }
        }
    }
}
