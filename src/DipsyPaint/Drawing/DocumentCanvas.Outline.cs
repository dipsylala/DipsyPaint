using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Drawing;

// The edge of a free-form or magic-wand selection (issue #111): black and white dashes one screen pixel wide, just
// outside the selected pixels, at any zoom. A ragged edge (a magic-wand selection of a noisy 4K image has millions
// of corners) is too slow to stroke as a path on every frame, so the engine draws it into a bitmap of window pixels
// over the part of the window the selection covers (SelectionOutline.Draw), whose cost depends on that area alone.
// The bitmap is redrawn only when the selection or the view (zoom, scrolling, window size) changes.
public sealed partial class DocumentCanvas
{
    // Canvas coordinates to window coordinates (device-independent pixels), as at the last layout.
    private Matrix? _toWindow;

    private WriteableBitmap? _outline;

    // The part of _outline that holds the edge, in window pixels (the bitmap is as large as the window).
    private PixelRect _outlinePixels;

    // What _outline was drawn for: the selection, a rotate drag's preview turn and the view.
    private object? _outlineDrawnFor;

    // A scroll or zoom moves the selection on the window, so its edge is drawn again.
    private void OnViewChanged()
    {
        if (_editor?.Selection is { Mask: not null })
        {
            InvalidateVisual();
        }
    }

    private void DrawOutline(DrawingContext context, Selection? selection)
    {
        if (selection is not { Mask: { } mask } shape || _topLevel is null || _toWindow is not { } toWindow)
        {
            _outline?.Dispose();
            (_outline, _outlineDrawnFor) = (null, null);
            return;
        }

        var turn = (_editor?.ActiveTool as SelectionTool)?.PreviewTurn;
        var window = PixelSize.FromSize(_topLevel.Bounds.Size, 1);
        var key = (shape.Bounds, mask, turn, toWindow, window);
        if (!key.Equals(_outlineDrawnFor))
        {
            RedrawOutline(shape.Bounds, mask, turn, toWindow, window);
            _outlineDrawnFor = key;
        }

        if (_outline is not null && _outlinePixels is { Width: > 0, Height: > 0 } part && toWindow.TryInvert(out var toCanvas))
        {
            var source = new Rect(part.X, part.Y, part.Width, part.Height);
            context.DrawImage(_outline, source, source.TransformToAABB(toCanvas));
        }
    }

    private unsafe void RedrawOutline(IntRect bounds, bool[] mask, (double Degrees, double CentreX, double CentreY)? turn, Matrix toWindow, PixelSize window)
    {
        // Document points to window points: during a rotate drag, turned as the preview is.
        var toScreen = toWindow;
        if (turn is var (degrees, centreX, centreY))
        {
            toScreen = Matrix.CreateTranslation(-centreX, -centreY) * Matrix.CreateRotation(degrees * Math.PI / 180)
                * Matrix.CreateTranslation(centreX, centreY) * toWindow;
        }

        // The selection's box on the window, with a pixel all round for the edge, within the window.
        var box = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height).TransformToAABB(toScreen).Inflate(1)
            .Intersect(new Rect(0, 0, window.Width, window.Height));
        int left = (int)Math.Floor(box.X), top = (int)Math.Floor(box.Y);
        _outlinePixels = new PixelRect(left, top, (int)Math.Ceiling(box.Right) - left, (int)Math.Ceiling(box.Bottom) - top);
        if (_outlinePixels.Width <= 0 || _outlinePixels.Height <= 0 || !toScreen.TryInvert(out var toDocument))
        {
            _outlinePixels = default;
            return;
        }

        if (_outline?.PixelSize != window)
        {
            _outline?.Dispose();
            _outline = new WriteableBitmap(window, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);
        }

        // Raster pixel (u, v) is window pixel (left + u, top + v); dashes count from the selection's top-left corner.
        var origin = toDocument.Transform(new Point(left, top));
        var map = new ScreenMap(toDocument.M11, toDocument.M12, toDocument.M21, toDocument.M22, origin.X, origin.Y);
        var corner = toScreen.Transform(new Point(bounds.X, bounds.Y));
        using var buffer = _outline.Lock();
        var address = (byte*)buffer.Address + top * buffer.RowBytes + left * 4;
        var rowBytes = buffer.RowBytes;
        var width = _outlinePixels.Width;
        SelectionOutline.Draw(
            bounds, mask, map, width, _outlinePixels.Height, y => new Span<Rgba>(address + y * rowBytes, width),
            left - (int)Math.Round(corner.X), top - (int)Math.Round(corner.Y));
    }
}
