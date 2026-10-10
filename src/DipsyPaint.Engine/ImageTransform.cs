namespace DipsyPaint.Engine;

/// <summary>Reference Paint's Rotate menu: exact quarter-turn rotations and mirror flips.</summary>
public enum ImageTransform
{
    /// <summary>Rotate right 90° (clockwise).</summary>
    RotateRight,

    /// <summary>Rotate left 90° (anticlockwise).</summary>
    RotateLeft,

    Rotate180,

    /// <summary>Flip vertical: top and bottom swap (upside down).</summary>
    FlipVertical,

    /// <summary>Flip horizontal: left and right swap (mirror image).</summary>
    FlipHorizontal,
}

/// <summary>Whole-bitmap pixel operations on row-major, premultiplied pixels.</summary>
internal static class PixelOps
{
    /// <summary>Whether <paramref name="transform"/> swaps width and height.</summary>
    public static bool SwapsSides(this ImageTransform transform) =>
        transform is ImageTransform.RotateRight or ImageTransform.RotateLeft;

    /// <summary>The transform that undoes <paramref name="transform"/>.</summary>
    public static ImageTransform Inverse(this ImageTransform transform) => transform switch
    {
        ImageTransform.RotateRight => ImageTransform.RotateLeft,
        ImageTransform.RotateLeft => ImageTransform.RotateRight,
        _ => transform,
    };

    /// <summary>Applies <paramref name="transform"/> to a <paramref name="width"/>×<paramref name="height"/> bitmap; the result is new.</summary>
    public static Rgba[] Transform(Rgba[] source, int width, int height, ImageTransform transform)
    {
        var result = GC.AllocateUninitializedArray<Rgba>(source.Length);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var target = transform switch
                {
                    ImageTransform.RotateRight => x * height + (height - 1 - y),
                    ImageTransform.RotateLeft => (width - 1 - x) * height + y,
                    ImageTransform.Rotate180 => (height - 1 - y) * width + (width - 1 - x),
                    ImageTransform.FlipVertical => (height - 1 - y) * width + x,
                    ImageTransform.FlipHorizontal => y * width + (width - 1 - x),
                    _ => throw new ArgumentOutOfRangeException(nameof(transform)),
                };
                result[target] = source[y * width + x];
            }
        }

        return result;
    }

    /// <summary>Copies <paramref name="region"/> (inside the bitmap) out of a bitmap <paramref name="width"/> pixels wide.</summary>
    public static Rgba[] Crop(Rgba[] source, int width, IntRect region)
    {
        var result = GC.AllocateUninitializedArray<Rgba>(region.Width * region.Height);
        for (var y = 0; y < region.Height; y++)
        {
            source.AsSpan((region.Y + y) * width + region.X, region.Width).CopyTo(result.AsSpan(y * region.Width));
        }

        return result;
    }

    /// <summary>
    /// Places a <paramref name="width"/>×<paramref name="height"/> bitmap at the top-left of a larger
    /// <paramref name="newWidth"/>×<paramref name="newHeight"/> one filled with <paramref name="fill"/>.
    /// </summary>
    public static Rgba[] Extend(Rgba[] source, int width, int height, int newWidth, int newHeight, Rgba fill)
    {
        var result = GC.AllocateUninitializedArray<Rgba>(newWidth * newHeight);
        result.AsSpan().Fill(fill);
        for (var y = 0; y < height; y++)
        {
            source.AsSpan(y * width, width).CopyTo(result.AsSpan(y * newWidth));
        }

        return result;
    }

    /// <summary>
    /// Resamples a <paramref name="width"/>×<paramref name="height"/> bitmap to
    /// <paramref name="newWidth"/>×<paramref name="newHeight"/> with a separable triangle (bilinear)
    /// filter, widened when shrinking so every source pixel contributes (area-weighted, no aliasing).
    /// Works on premultiplied pixels, so transparent pixels do not darken their neighbours.
    /// </summary>
    public static Rgba[] Resample(Rgba[] source, int width, int height, int newWidth, int newHeight)
    {
        // Horizontal pass into a newWidth×height bitmap, then vertical into the result.
        var horizontal = new Rgba[newWidth * height];
        var columns = Weights(width, newWidth);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < newWidth; x++)
            {
                horizontal[y * newWidth + x] = Sum(source, y * width + columns[x].Start, 1, columns[x].Weights);
            }
        }

        var result = new Rgba[newWidth * newHeight];
        var rows = Weights(height, newHeight);
        for (var y = 0; y < newHeight; y++)
        {
            for (var x = 0; x < newWidth; x++)
            {
                result[y * newWidth + x] = Sum(horizontal, rows[y].Start * newWidth + x, newWidth, rows[y].Weights);
            }
        }

        return result;
    }

    private static Rgba Sum(Rgba[] pixels, int start, int stride, float[] weights)
    {
        float r = 0, g = 0, b = 0, a = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            var p = pixels[start + i * stride];
            var w = weights[i];
            r += p.R * w;
            g += p.G * w;
            b += p.B * w;
            a += p.A * w;
        }

        return new Rgba(ToByte(r), ToByte(g), ToByte(b), ToByte(a));
    }

    private static byte ToByte(float value) => (byte)Math.Clamp((int)MathF.Round(value), 0, 255);

    // For each destination index along one axis: the first source index and the normalised weights
    // of the consecutive source pixels from there.
    private static (int Start, float[] Weights)[] Weights(int sourceSize, int destinationSize)
    {
        var scale = (double)sourceSize / destinationSize;
        var radius = Math.Max(1.0, scale);
        var result = new (int, float[])[destinationSize];
        for (var d = 0; d < destinationSize; d++)
        {
            // The centre lies in [-0.5, sourceSize - 0.5], so a source pixel is always within the radius.
            var centre = (d + 0.5) * scale - 0.5;
            var first = Math.Max(0, (int)Math.Floor(centre - radius) + 1);
            var last = Math.Min(sourceSize - 1, (int)Math.Ceiling(centre + radius) - 1);
            var weights = new float[last - first + 1];
            double total = 0;
            for (var s = first; s <= last; s++)
            {
                var weight = Math.Max(0, 1 - Math.Abs(s - centre) / radius);
                weights[s - first] = (float)weight;
                total += weight;
            }

            for (var i = 0; i < weights.Length; i++)
            {
                weights[i] = (float)(weights[i] / total);
            }

            result[d] = (first, weights);
        }

        return result;
    }
}
