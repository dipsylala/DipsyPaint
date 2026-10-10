using System.Runtime.CompilerServices;

namespace DipsyPaint.Engine;

/// <summary>
/// Wraps a layer's pixels and, before the first write to each 64×64 tile, copies that tile's
/// before-pixels. <see cref="TakeStep"/> turns the copies into a history step, so a step's
/// memory follows the area actually changed rather than its bounding box (ADR-0005).
/// </summary>
internal sealed class PixelRecorder(IPixelAccess target) : IPixelAccess
{
    private const int TileSize = 64;

    private readonly Dictionary<(int X, int Y), (IntRect Region, Rgba[] Before)> _tiles = [];

    public int Width => target.Width;
    public int Height => target.Height;

    public Rgba GetPixel(int x, int y) => target.GetPixel(x, y);

    public void SetPixel(int x, int y, Rgba value)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height)
        {
            Save(x / TileSize, y / TileSize);
        }

        target.SetPixel(x, y, value); // throws for out-of-bounds pixels, as the target does
    }

    public void ReadRegion(IntRect region, Span<Rgba> destination) => target.ReadRegion(region, destination);

    public void WriteRegion(IntRect region, ReadOnlySpan<Rgba> source)
    {
        if (!region.IsEmpty && region.Intersect(new IntRect(0, 0, Width, Height)) == region)
        {
            for (var ty = region.Y / TileSize; ty <= (region.Bottom - 1) / TileSize; ty++)
            {
                for (var tx = region.X / TileSize; tx <= (region.Right - 1) / TileSize; tx++)
                {
                    Save(tx, ty);
                }
            }
        }

        target.WriteRegion(region, source); // throws for invalid regions, as the target does
    }

    /// <summary>Returns a step that restores every tile written since the last call, or null if none was.</summary>
    public HistoryStep? TakeStep()
    {
        if (_tiles.Count == 0)
        {
            return null;
        }

        var step = new PixelRegionsStep(target, [.. _tiles.Values]);
        _tiles.Clear();
        return step;
    }

    private void Save(int tileX, int tileY)
    {
        if (_tiles.ContainsKey((tileX, tileY)))
        {
            return;
        }

        var region = new IntRect(tileX * TileSize, tileY * TileSize, TileSize, TileSize)
            .Intersect(new IntRect(0, 0, Width, Height));
        var before = GC.AllocateUninitializedArray<Rgba>(region.Width * region.Height);
        target.ReadRegion(region, before);
        _tiles.Add((tileX, tileY), (region, before));
    }

    /// <summary>
    /// Restores rectangles of one layer. Undo and redo both swap the stored pixels with the
    /// layer's, so the step holds before-pixels while done and after-pixels while undone.
    /// </summary>
    private sealed class PixelRegionsStep(IPixelAccess target, (IntRect Region, Rgba[] Pixels)[] regions) : HistoryStep
    {
        public override long SizeBytes { get; } =
            regions.Sum(r => (long)r.Pixels.Length * Unsafe.SizeOf<Rgba>());

        public override IntRect Undo() => Swap();

        public override IntRect Redo() => Swap();

        private IntRect Swap()
        {
            var dirty = IntRect.Empty;
            for (var i = 0; i < regions.Length; i++)
            {
                var (region, stored) = regions[i];
                var current = GC.AllocateUninitializedArray<Rgba>(stored.Length);
                target.ReadRegion(region, current);
                target.WriteRegion(region, stored);
                regions[i] = (region, current);
                dirty = dirty.Union(region);
            }

            return dirty;
        }
    }
}
