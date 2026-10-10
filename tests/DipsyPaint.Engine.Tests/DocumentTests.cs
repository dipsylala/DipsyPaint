using DipsyPaint.Engine;

namespace DipsyPaint.Engine.Tests;

public class DocumentTests
{
    [Fact]
    public void CreateBlank_UsesDefaultSize()
    {
        var document = Document.CreateBlank();

        Assert.Equal(Document.DefaultWidth, document.Width);
        Assert.Equal(Document.DefaultHeight, document.Height);
    }

    [Fact]
    public void CreateBlank_AcceptsMaximumSide()
    {
        var document = Document.CreateBlank(Document.MaxSide, Document.MaxSide);

        Assert.Equal(Document.MaxSide, document.Width);
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(800, 0)]
    [InlineData(Document.MaxSide + 1, 600)]
    [InlineData(800, Document.MaxSide + 1)]
    public void CreateBlank_RejectsOutOfRangeSize(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Document.CreateBlank(width, height));
    }
}
