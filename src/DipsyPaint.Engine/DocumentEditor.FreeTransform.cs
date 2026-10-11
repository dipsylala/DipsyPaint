using System.Runtime.CompilerServices;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine;

// Freeform rotate (Rotate > Custom rotate) and skew (the Resize and skew dialog).
public sealed partial class DocumentEditor
{
    /// <summary>
    /// Turns <paramref name="degrees"/> clockwise (Custom rotate): pending content that can turn (a
    /// selection, shape or text box; see <see cref="IRotatableTool"/>) about its centre, staying pending;
    /// else every layer, about the canvas centre, with the canvas grown to the upright rectangle around
    /// the result, as one history step. Pixels are sampled bilinearly; the area the picture no longer
    /// covers is white on the bottom layer and transparent above, as in a new document. Refused beyond
    /// <see cref="Document.MaxSide"/> per side or <paramref name="budget"/> (default
    /// <see cref="MemoryBudget.Default"/>). Does nothing during a gesture.
    /// </summary>
    public ResizeResult Rotate(double degrees, MemoryBudget? budget = null)
    {
        if (_inGesture || Math.IEEERemainder(degrees, 360) == 0)
        {
            return ResizeResult.Resized;
        }

        if (ActiveTool is IRotatableTool { Adornment: not null } tool)
        {
            _recorder ??= new PixelRecorder(Document.ActiveLayer.Pixels);
            Refresh(tool.Rotate(Context(_recorder), degrees));
            return ResizeResult.Resized;
        }

        return WarpCanvas(Document.Width, Document.Height, LinearTransform.Rotation(degrees), budget);
    }

    /// <summary>
    /// The Resize and skew dialog: resamples to <paramref name="width"/>×<paramref name="height"/> as
    /// <see cref="Resize"/> does, then skews by <paramref name="horizontalDegrees"/> and then
    /// <paramref name="verticalDegrees"/> (see <see cref="LinearTransform.Skew"/>; each within
    /// ±<see cref="LinearTransform.MaxSkewDegrees"/>), as one history step. With a selection it applies
    /// to the selection, which keeps its top-left corner and stays pending; else to every layer, the
    /// canvas growing to fit, the uncovered area white on the bottom layer and transparent above.
    /// Skewing samples bilinearly. Refused as <see cref="Resize"/> is. Does nothing during a gesture.
    /// </summary>
    public ResizeResult ResizeAndSkew(int width, int height, int horizontalDegrees, int verticalDegrees, MemoryBudget? budget = null)
    {
        if (horizontalDegrees == 0 && verticalDegrees == 0)
        {
            return Resize(width, height, budget);
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        var skew = LinearTransform.Skew(horizontalDegrees, verticalDegrees);
        var tool = ActiveTool as SelectionTool;
        if (tool?.Selection is null)
        {
            var (oldWidth, oldHeight) = (Document.Width, Document.Height);
            if (width > Document.MaxSide || height > Document.MaxSide)
            {
                return ResizeResult.TooLarge;
            }

            if (_inGesture)
            {
                return ResizeResult.Resized;
            }

            return WarpCanvas(width, height, skew, budget, pixels =>
                (width, height) == (oldWidth, oldHeight) ? pixels : PixelOps.Resample(pixels, oldWidth, oldHeight, width, height));
        }

        var (newWidth, newHeight) = skew.SizeOf(width, height);
        if (Math.Max(Math.Max(width, height), Math.Max(newWidth, newHeight)) > Document.MaxSide)
        {
            return ResizeResult.TooLarge;
        }

        // The resampled and skewed selection, the resampler's intermediate and the overlay's copy.
        if (!(budget ?? MemoryBudget.Default).Allows(3 * Math.Max(width * (long)height, newWidth * (long)newHeight) * Unsafe.SizeOf<Rgba>()))
        {
            return ResizeResult.OverMemoryBudget;
        }

        if (!_inGesture)
        {
            ReplaceSelection(tool, newWidth, newHeight, (pixels, w, h) =>
            {
                var resized = (w, h) == (width, height) ? pixels : PixelOps.Resample(pixels, w, h, width, height);
                return PixelWarp.Warp(resized, width, height, skew, newWidth, newHeight);
            });
        }

        return ResizeResult.Resized;
    }

    // Commits pending content, then maps every layer (first through `prepare`, to width×height) through
    // `transform` into the upright rectangle around the result, which becomes the canvas, as one step.
    private ResizeResult WarpCanvas(int width, int height, LinearTransform transform, MemoryBudget? budget, Func<Rgba[], Rgba[]>? prepare = null)
    {
        var (newWidth, newHeight) = transform.SizeOf(width, height);
        if (newWidth > Document.MaxSide || newHeight > Document.MaxSide)
        {
            return ResizeResult.TooLarge;
        }

        // The new layers, an intermediate, the composite and the shell's bitmap.
        var largest = Math.Max(width * (long)height, newWidth * (long)newHeight);
        if (!(budget ?? MemoryBudget.Default).Allows((Document.Layers.Count + 3) * largest * Unsafe.SizeOf<Rgba>()))
        {
            return ResizeResult.OverMemoryBudget;
        }

        CommitPending();
        ReplaceCanvas(newWidth, newHeight, (layer, pixels) =>
            PixelWarp.Warp(prepare?.Invoke(pixels) ?? pixels, width, height, transform, newWidth, newHeight, layer == 0 ? Rgba.White : Rgba.Transparent));
        return ResizeResult.Resized;
    }
}
