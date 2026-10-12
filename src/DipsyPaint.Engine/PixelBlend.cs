using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace DipsyPaint.Engine;

/// <summary>
/// Source-over compositing of whole rows, as <see cref="Rgba.Over"/> does pixel by pixel, a few pixels at a time
/// where the CPU has vector instructions (issue #115). The arithmetic is the same integer arithmetic as
/// <see cref="Rgba.Over"/>, so the pixels are the same on every CPU.
/// </summary>
internal static class PixelBlend
{
    /// <summary>Composites each pixel of <paramref name="source"/> over the pixel of <paramref name="target"/> at the same index.</summary>
    public static void Over(ReadOnlySpan<Rgba> source, Span<Rgba> target)
    {
        var length = Math.Min(source.Length, target.Length);
        var from = MemoryMarshal.Cast<Rgba, uint>(source[..length]);
        var to = MemoryMarshal.Cast<Rgba, uint>(target[..length]);
        var i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            ref var fromStart = ref MemoryMarshal.GetReference(from);
            ref var toStart = ref MemoryMarshal.GetReference(to);
            var opaque = Vector128.Create(0xFF000000u);
            for (; i <= length - Vector128<uint>.Count; i += Vector128<uint>.Count)
            {
                var pixels = Vector128.LoadUnsafe(ref fromStart, (nuint)i);
                if (pixels == Vector128<uint>.Zero)
                {
                    continue; // all transparent: nothing changes
                }

                if ((pixels & opaque) == opaque)
                {
                    pixels.StoreUnsafe(ref toStart, (nuint)i); // all opaque: they replace what is there
                    continue;
                }

                Over(pixels, Vector128.LoadUnsafe(ref toStart, (nuint)i)).StoreUnsafe(ref toStart, (nuint)i);
            }
        }

        for (; i < length; i++)
        {
            // Shortcuts that give what Over gives: an opaque pixel replaces, a transparent one changes nothing.
            var pixel = source[i];
            if (pixel.A == 255)
            {
                target[i] = pixel;
            }
            else if (pixel != Rgba.Transparent)
            {
                target[i] = pixel.Over(target[i]);
            }
        }
    }

    // Rgba.Over for four pixels: each channel is source + Div255(background × (255 − source alpha)), wrapping as a byte.
    private static Vector128<uint> Over(Vector128<uint> source, Vector128<uint> background)
    {
        // 255 − alpha in every byte of each pixel.
        var inverse = ~((source >>> 24) * 0x01010101u);
        var (sourceLow, sourceHigh) = Vector128.Widen(source.AsByte());
        var (backgroundLow, backgroundHigh) = Vector128.Widen(background.AsByte());
        var (inverseLow, inverseHigh) = Vector128.Widen(inverse.AsByte());
        var low = sourceLow + Div255(backgroundLow * inverseLow);
        var high = sourceHigh + Div255(backgroundHigh * inverseHigh);
        return Vector128.Narrow(low, high).AsUInt32();
    }

    // As Rgba's Div255, for values up to 255 × 255, which keeps every step within 16 bits.
    private static Vector128<ushort> Div255(Vector128<ushort> value)
    {
        var rounded = value + Vector128.Create((ushort)128);
        return (rounded + (rounded >>> 8)) >>> 8;
    }
}
