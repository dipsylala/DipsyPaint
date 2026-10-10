namespace DipsyPaint.Engine;

/// <summary>An axis-aligned rectangle of whole pixels. Empty when either side is zero or negative.</summary>
public readonly record struct IntRect(int X, int Y, int Width, int Height)
{
    public static IntRect Empty => default;

    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>The smallest rectangle containing both; an empty rectangle is ignored.</summary>
    public IntRect Union(IntRect other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        var x = Math.Min(X, other.X);
        var y = Math.Min(Y, other.Y);
        return new IntRect(x, y, Math.Max(Right, other.Right) - x, Math.Max(Bottom, other.Bottom) - y);
    }

    /// <summary>The overlap of both rectangles, or <see cref="Empty"/> if they do not overlap.</summary>
    public IntRect Intersect(IntRect other)
    {
        var x = Math.Max(X, other.X);
        var y = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right > x && bottom > y ? new IntRect(x, y, right - x, bottom - y) : Empty;
    }
}
