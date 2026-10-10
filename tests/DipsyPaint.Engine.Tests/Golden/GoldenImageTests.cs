namespace DipsyPaint.Engine.Tests.Golden;

public sealed class GoldenImageTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("dipsypaint-golden-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string GoldenPath => Path.Combine(_directory, "golden.png");
    private string DiffDirectory => Path.Combine(_directory, "diffs");

    private static Rgba[] Image() => [Rgba.White, Rgba.Black, new(10, 20, 30, 255), Rgba.Transparent, new(64, 0, 0, 128), Rgba.White];

    [Fact]
    public void SaveThenLoad_RoundTripsPixels()
    {
        GoldenImage.Save(GoldenPath, Image(), 3, 2);

        var loaded = GoldenImage.Load(GoldenPath, out var width, out var height);

        Assert.Equal((3, 2), (width, height));
        Assert.Equal(Image(), loaded);
    }

    [Fact]
    public void Compare_PassesWithinTolerance()
    {
        GoldenImage.Save(GoldenPath, Image(), 3, 2);
        var actual = Image();
        actual[2] = new Rgba(12, 18, 30, 255);

        Assert.Null(GoldenImage.Compare("test", GoldenPath, actual, 3, 2, tolerance: 2, DiffDirectory));
        Assert.False(Directory.Exists(DiffDirectory));
    }

    [Fact]
    public void Compare_FailureWritesDiffImages()
    {
        GoldenImage.Save(GoldenPath, Image(), 3, 2);
        var actual = Image();
        actual[0] = Rgba.Black;

        var failure = GoldenImage.Compare("test", GoldenPath, actual, 3, 2, tolerance: 2, DiffDirectory);

        Assert.Contains("1 pixel(s) differ", failure);
        var diff = GoldenImage.Load(Path.Combine(DiffDirectory, "test.diff.png"), out _, out _);
        Assert.Equal(new Rgba(255, 0, 0, 255), diff[0]);
        Assert.NotEqual(new Rgba(255, 0, 0, 255), diff[1]);
        Assert.True(File.Exists(Path.Combine(DiffDirectory, "test.actual.png")));
        Assert.True(File.Exists(Path.Combine(DiffDirectory, "test.expected.png")));
    }

    [Fact]
    public void Compare_MissingGoldenFailsAndWritesActual()
    {
        var failure = GoldenImage.Compare("test", GoldenPath, Image(), 3, 2, tolerance: 2, DiffDirectory);

        Assert.Contains("no golden image", failure);
        Assert.True(File.Exists(Path.Combine(DiffDirectory, "test.actual.png")));
    }

    [Fact]
    public void Compare_SizeMismatchFails()
    {
        GoldenImage.Save(GoldenPath, Image(), 3, 2);

        var failure = GoldenImage.Compare("test", GoldenPath, Image(), 2, 3, tolerance: 2, DiffDirectory);

        Assert.Contains("size", failure);
    }
}
