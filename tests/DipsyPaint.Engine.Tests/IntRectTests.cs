namespace DipsyPaint.Engine.Tests;

public class IntRectTests
{
    [Fact]
    public void Union_CoversBothAndIgnoresEmpty()
    {
        var a = new IntRect(1, 2, 3, 4);

        Assert.Equal(new IntRect(0, 2, 4, 8), a.Union(new IntRect(0, 9, 1, 1)));
        Assert.Equal(a, a.Union(IntRect.Empty));
        Assert.Equal(a, IntRect.Empty.Union(a));
    }

    [Fact]
    public void Intersect_ReturnsOverlapOrEmpty()
    {
        var a = new IntRect(0, 0, 4, 4);

        Assert.Equal(new IntRect(2, 3, 2, 1), a.Intersect(new IntRect(2, 3, 5, 5)));
        Assert.True(a.Intersect(new IntRect(4, 0, 1, 1)).IsEmpty);
    }
}
