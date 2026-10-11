using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Drawing;

/// <summary>
/// Shows the composited document at one device-independent pixel per document pixel and routes
/// pointer input to the <see cref="DocumentEditor"/> in its <see cref="StyledElement.DataContext"/>.
/// Pointer positions are taken in this control's own coordinates, so a transform applied around
/// it (e.g. zoom) still yields document pixels. Transparent pixels show a checkerboard, as in
/// reference Paint. Also draws the frame and handles around pending content
/// (<see cref="DocumentEditor.Adornment"/>) at a constant on-screen size, with the rotate handle
/// above it, and handles Enter (commit; a selection stays) and Esc (cancel) for it, and Delete and the
/// arrow keys (<see cref="DocumentEditor.Nudge"/>: 1 pixel, 10 with Shift) for a selection. Handles
/// that stick out past the canvas still take presses (<see cref="ICustomHitTest"/>).
/// </summary>
public sealed class DocumentCanvas : Control, ICustomHitTest
{
    /// <summary>Pixels an arrow key moves a selection with Shift held; 1 without.</summary>
    private const int ShiftNudge = 10;

    /// <summary>Side of a handle square, in device-independent pixels on screen.</summary>
    private const double HandleSize = 7;

    /// <summary>How far from a handle's centre the pointer grabs it, in device-independent pixels on screen.</summary>
    private const double HandleReach = HandleSize / 2 + 1;

    private static readonly IBrush FrameBrush = new SolidColorBrush(Color.FromRgb(0, 103, 192));

    // 8×8 document-pixel squares, light grey on white.
    internal static readonly IBrush Checkerboard = new DrawingBrush(new DrawingGroup
    {
        Children =
        {
            new GeometryDrawing { Brush = Brushes.White, Geometry = Geometry.Parse("M0,0 H16 V16 H0 Z") },
            new GeometryDrawing { Brush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)), Geometry = Geometry.Parse("M0,0 H8 V8 H0 Z M8,8 H16 V16 H8 Z") },
        },
    })
    {
        TileMode = TileMode.Tile,
        DestinationRect = new RelativeRect(0, 0, 16, 16, RelativeUnit.Absolute),
    };

    private DocumentEditor? _editor;
    private WriteableBitmap? _bitmap;
    private PointerButton? _strokeButton;
    private Point _lastPosition;
    private PointerInput _lastInput;
    private readonly StrokeRepeater _repeater = new();
    private TopLevel? _topLevel;
    private FrameHandle _hoveredHandle;

    // Device-independent pixels on screen per document pixel (the zoom).
    private double _scale = 1;

    public DocumentCanvas() => RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);

    /// <summary>
    /// The clock that times pointer input (<see cref="PointerInput.Time"/>) and repeats it while a
    /// stroke of an <see cref="IRepeatingTool"/> is held; tests may replace it.
    /// </summary>
    public TimeProvider Clock { get; set; } = TimeProvider.System;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_editor is not null)
        {
            _editor.CompositeChanged -= OnCompositeChanged;
            _editor.SizeChanged -= OnSizeChanged;
            _editor.ActiveToolChanged -= OnAdornmentChanged;
            _editor.SelectionChanged -= OnAdornmentChanged;
        }

        _editor = DataContext as DocumentEditor;
        if (_editor is not null)
        {
            _editor.HandleTolerance = HandleReach / _scale;
            _editor.CompositeChanged += OnCompositeChanged;
            _editor.SizeChanged += OnSizeChanged;
            _editor.ActiveToolChanged += OnAdornmentChanged;
            _editor.SelectionChanged += OnAdornmentChanged;
        }

        CreateBitmap();
    }

    // A new bitmap the size of the composite, holding all of it.
    private void CreateBitmap()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        if (_editor is not null)
        {
            var compositor = _editor.Compositor;
            _bitmap = new WriteableBitmap(
                new PixelSize(compositor.Width, compositor.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);
            CopyToBitmap(new IntRect(0, 0, compositor.Width, compositor.Height));
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnSizeChanged(object? sender, EventArgs e) => CreateBitmap();

    // A selection may change, or be drawn with or without handles (ADR-0025), without the composite changing.
    private void OnAdornmentChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override Size MeasureOverride(Size availableSize) =>
        _editor is null ? default : new Size(_editor.Compositor.Width, _editor.Compositor.Height);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnTopLevelKeyDown);
        _topLevel?.AddHandler(KeyDownEvent, OnTopLevelArrowKeyDown, RoutingStrategies.Tunnel);
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        _topLevel?.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
        _topLevel?.RemoveHandler(KeyDownEvent, OnTopLevelArrowKeyDown);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        if (_bitmap is not null)
        {
            context.FillRectangle(Checkerboard, new Rect(_bitmap.Size));
            context.DrawImage(_bitmap, new Rect(_bitmap.Size));
        }

        if (_editor?.Adornment is { } adornment)
        {
            DrawAdornment(context, adornment);
        }

        if ((_editor?.ActiveTool as SelectionTool)?.FreeFormPath is { Count: > 1 } path)
        {
            DrawFreeFormPath(context, path);
        }
    }

    // The path of a free-form selection being dragged, through pixel centres: black dashes on white,
    // one screen pixel wide, so it shows on any colour.
    private void DrawFreeFormPath(DrawingContext context, IReadOnlyList<(int X, int Y)> path)
    {
        var unit = 1 / _scale;
        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(new Point(path[0].X + 0.5, path[0].Y + 0.5), false);
            foreach (var (x, y) in path.Skip(1))
            {
                figure.LineTo(new Point(x + 0.5, y + 0.5));
            }

            figure.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(Brushes.White, unit), geometry);
        context.DrawGeometry(null, new Pen(Brushes.Black, unit, new DashStyle([4, 4], 0)), geometry);
    }

    // Drawn in document coordinates with sizes divided by the zoom, so it is the same size on screen at any zoom.
    private void DrawAdornment(DrawingContext context, Adornment adornment)
    {
        var unit = 1 / _scale;
        var frame = adornment.Frame;
        var dashed = new Pen(FrameBrush, unit, new DashStyle([4, 4], 0));
        // Just outside the content, so it covers none of its pixels and stays crisp.
        context.DrawRectangle(null, dashed, new Rect(frame.X, frame.Y, frame.Width, frame.Height).Inflate(unit / 2));
        var solid = new Pen(FrameBrush, unit);
        var half = HandleSize / 2 * unit;
        foreach (var (_, x, y) in adornment.Handles)
        {
            context.DrawRectangle(Brushes.White, solid, new Rect(x - half, y - half, 2 * half, 2 * half));
        }

        if (!adornment.HasHandles)
        {
            return;
        }

        // The rotate handle: a circle above the top edge's handle, joined to it by a line.
        var (rx, ry) = adornment.RotateHandle(HandleReach * unit);
        context.DrawLine(solid, new Point(rx, frame.Y - half), new Point(rx, ry + half));
        context.DrawEllipse(Brushes.White, solid, new Point(rx, ry), half, half);
    }

    /// <summary>The canvas, and any handle of the adornment, even where it sticks out past the canvas.</summary>
    public bool HitTest(Point point) =>
        new Rect(Bounds.Size).Contains(point)
        || (_editor?.Adornment?.HitTest(point.X, point.Y, _editor.HandleTolerance) ?? FrameHandle.None) is not (FrameHandle.None or FrameHandle.Move);

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var scale = _topLevel is null ? 1 : this.TransformToVisual(_topLevel)?.M11 ?? 1;
        if (scale > 0 && scale != _scale)
        {
            _scale = scale;
            _editor?.HandleTolerance = HandleReach / scale;
            InvalidateVisual();
        }
    }

    // Tunnelling, so the arrow keys move a selection rather than scroll the canvas or move the focus, unless
    // a text box has the focus. With no Move tool or no selection they are left to the focused control.
    private void OnTopLevelArrowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor is { HasPending: true } && e.Source is not TextBox
            && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift
            && NudgeOf(e.Key) is var (dx, dy) && (dx, dy) != (0, 0))
        {
            var step = e.KeyModifiers == KeyModifiers.Shift ? ShiftNudge : 1;
            if (_editor.Nudge(dx * step, dy * step))
            {
                e.Handled = true;
                InvalidateVisual();
            }
        }
    }

    // Bubbling, so a focused control that uses Enter or Esc itself handles the key first.
    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor is not { HasPending: true } || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                _editor.CommitPending(keepSelection: true);
                break;
            case Key.Escape:
                _editor.CancelPending();
                break;
            case Key.Delete when _editor.Selection is not null:
                _editor.DeleteSelection();
                break;
            default:
                return;
        }

        e.Handled = true;
        InvalidateVisual();
    }

    private static (int X, int Y) NudgeOf(Key key) => key switch
    {
        Key.Left => (-1, 0),
        Key.Right => (1, 0),
        Key.Up => (0, -1),
        Key.Down => (0, 1),
        _ => (0, 0),
    };

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
        if (_editor?.ActiveTool is IRepeatingTool repeating)
        {
            _repeater.Start(Clock, repeating.RepeatInterval, RepeatLastInput);
        }
    }

    // The pointer is held still: the tool gets the same position again, at the current time.
    private void RepeatLastInput()
    {
        if (_strokeButton is not null && _editor is not null)
        {
            _editor.Apply(_lastInput with { Action = PointerAction.Move, Time = Clock.GetElapsedTime(0) });
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_strokeButton is not null)
        {
            Apply(PointerAction.Move, e);
        }
        else if (_editor is not null)
        {
            var position = e.GetPosition(this);
            // Select makes a new selection wherever it is pressed, so it shows no move cursor (ADR-0025).
            var handle = _editor.ActiveTool is SelectionTool { Mode: SelectionToolMode.Select }
                ? FrameHandle.None
                : _editor.Adornment?.HitTest(position.X, position.Y, _editor.HandleTolerance) ?? FrameHandle.None;
            if (handle != _hoveredHandle)
            {
                _hoveredHandle = handle;
                Cursor = CursorFor(handle);
            }
        }
    }

    private static Cursor? CursorFor(FrameHandle handle) => handle switch
    {
        FrameHandle.None => null,
        FrameHandle.Move => new Cursor(StandardCursorType.SizeAll),
        FrameHandle.TopLeft => new Cursor(StandardCursorType.TopLeftCorner),
        FrameHandle.TopRight => new Cursor(StandardCursorType.TopRightCorner),
        FrameHandle.BottomRight => new Cursor(StandardCursorType.BottomRightCorner),
        FrameHandle.BottomLeft => new Cursor(StandardCursorType.BottomLeftCorner),
        FrameHandle.Top or FrameHandle.Bottom => new Cursor(StandardCursorType.SizeNorthSouth),
        FrameHandle.Rotate => new Cursor(StandardCursorType.Hand),
        _ => new Cursor(StandardCursorType.SizeWestEast),
    };

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

        _repeater.Stop();
        Apply(PointerAction.Up, e);
        _strokeButton = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _repeater.Stop();
        if (_strokeButton is { } button)
        {
            _strokeButton = null;
            _editor?.Apply(new PointerInput(PointerAction.Up, _lastPosition.X, _lastPosition.Y, button));
            InvalidateVisual();
        }
    }

    private void Apply(PointerAction action, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        _lastPosition = point.Position;
        _lastInput = new PointerInput(
            action, point.Position.X, point.Position.Y, _strokeButton!.Value, PressureOf(e.Pointer.Type, point.Properties.Pressure),
            e.KeyModifiers.HasFlag(KeyModifiers.Shift), Clock.GetElapsedTime(0));
        _editor?.Apply(_lastInput);
        InvalidateVisual(); // the adornment may change without the composite changing
    }

    /// <summary>
    /// The pressure (0–1) the engine takes for a pointer of <paramref name="type"/> that reports
    /// <paramref name="reported"/>. Only a pen's pressure counts: Avalonia reports a fixed placeholder
    /// for a mouse or touch, which is "no pressure" (1). A pen reporting 0 while it touches has no
    /// pressure sensor, so it is "no pressure" too.
    /// </summary>
    public static float PressureOf(PointerType type, float reported) =>
        type == PointerType.Pen && reported > 0f ? Math.Min(reported, 1f) : 1f;

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
