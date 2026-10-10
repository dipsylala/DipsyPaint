using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Drawing;

/// <summary>
/// Shows the composited document at one device-independent pixel per document pixel and routes
/// pointer input to the <see cref="DocumentEditor"/> in its <see cref="StyledElement.DataContext"/>.
/// Pointer positions are taken in this control's own coordinates, so a transform applied around
/// it (e.g. zoom) still yields document pixels.
/// </summary>
public sealed class DocumentCanvas : Control
{
    private DocumentEditor? _editor;
    private WriteableBitmap? _bitmap;
    private PointerButton? _strokeButton;
    private Point _lastPosition;

    public DocumentCanvas() => RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_editor is not null)
        {
            _editor.CompositeChanged -= OnCompositeChanged;
        }

        _bitmap?.Dispose();
        _bitmap = null;
        _editor = DataContext as DocumentEditor;
        if (_editor is not null)
        {
            var compositor = _editor.Compositor;
            _bitmap = new WriteableBitmap(
                new PixelSize(compositor.Width, compositor.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);
            CopyToBitmap(new IntRect(0, 0, compositor.Width, compositor.Height));
            _editor.CompositeChanged += OnCompositeChanged;
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        _editor is null ? default : new Size(_editor.Compositor.Width, _editor.Compositor.Height);

    public override void Render(DrawingContext context)
    {
        if (_bitmap is not null)
        {
            context.DrawImage(_bitmap, new Rect(_bitmap.Size));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        PointerButton? button = e.GetCurrentPoint(this).Properties.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonPressed => PointerButton.Left,
            PointerUpdateKind.RightButtonPressed => PointerButton.Right,
            _ => null,
        };
        if (_editor is null || _strokeButton is not null || button is null)
        {
            return;
        }

        _strokeButton = button;
        e.Pointer.Capture(this);
        e.Handled = true;
        Apply(PointerAction.Down, e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_strokeButton is not null)
        {
            Apply(PointerAction.Move, e);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var released = e.InitialPressMouseButton switch
        {
            MouseButton.Left => PointerButton.Left,
            MouseButton.Right => PointerButton.Right,
            _ => (PointerButton?)null,
        };
        if (_strokeButton is null || released != _strokeButton)
        {
            return;
        }

        Apply(PointerAction.Up, e);
        _strokeButton = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_strokeButton is { } button)
        {
            _strokeButton = null;
            _editor?.Apply(new PointerInput(PointerAction.Up, _lastPosition.X, _lastPosition.Y, button));
        }
    }

    private void Apply(PointerAction action, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        _lastPosition = point.Position;
        _editor?.Apply(new PointerInput(action, point.Position.X, point.Position.Y, _strokeButton!.Value, point.Properties.Pressure));
    }

    private void OnCompositeChanged(object? sender, IntRect dirty)
    {
        CopyToBitmap(dirty);
        InvalidateVisual();
    }

    private unsafe void CopyToBitmap(IntRect region)
    {
        using var buffer = _bitmap!.Lock();
        var composite = _editor!.Compositor.Pixels;
        var width = _editor.Compositor.Width;
        for (var y = region.Y; y < region.Bottom; y++)
        {
            var row = MemoryMarshal.AsBytes(composite.Slice(y * width + region.X, region.Width));
            row.CopyTo(new Span<byte>((byte*)buffer.Address + y * buffer.RowBytes + region.X * 4, row.Length));
        }
    }
}
