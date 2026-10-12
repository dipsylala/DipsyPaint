namespace DipsyPaint.Engine.Tools;

/// <summary>
/// A pixel's coordinates as a dictionary key for the pixels a stroke has covered. Its hash is cheaper to
/// compute than a tuple's, which matters when a stroke covers hundreds of thousands of pixels (issue #115).
/// </summary>
internal readonly record struct PixelKey(int X, int Y)
{
    public override int GetHashCode() => unchecked(X * -1640531535 + Y);
}
