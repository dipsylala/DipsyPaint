using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using DipsyPaint.Resources;

namespace DipsyPaint;

/// <summary>
/// Status-bar zoom control. Scales <see cref="Target"/> (a wrapper around the canvas inside a
/// <see cref="ScrollViewer"/>) and handles the zoom shortcuts for the whole window.
/// </summary>
/// <remarks>
/// A zoom factor of 1 (100%) shows one document pixel per device pixel, so the canvas stays sharp
/// on HiDPI displays. Pointer positions taken relative to the canvas (<c>e.GetPosition(canvas)</c>)
/// are already in document pixels, because the scale is a transform applied around the canvas.
/// </remarks>
public partial class ZoomControl : UserControl
{
    /// <summary>The zoom presets that zoom in/out step through; the first and last bound the zoom range.</summary>
    public static IReadOnlyList<double> Levels { get; } = [0.125, 0.25, 0.5, 1, 2, 3, 4, 5, 6, 7, 8];

    public static readonly StyledProperty<LayoutTransformControl?> TargetProperty =
        AvaloniaProperty.Register<ZoomControl, LayoutTransformControl?>(nameof(Target));

    public static readonly StyledProperty<double> ZoomFactorProperty =
        AvaloniaProperty.Register<ZoomControl, double>(nameof(ZoomFactor), 1,
            coerce: (_, value) => Math.Clamp(value, Levels[0], Levels[^1]));

    private TopLevel? _topLevel;

    public ZoomControl()
    {
        InitializeComponent();
        UpdateZoomText();
    }

    /// <summary>The element scaled by the zoom; it must sit inside a <see cref="ScrollViewer"/>.</summary>
    public LayoutTransformControl? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>The current zoom: 1 is 100%, i.e. one document pixel per device pixel.</summary>
    public double ZoomFactor
    {
        get => GetValue(ZoomFactorProperty);
        set => SetValue(ZoomFactorProperty, value);
    }

    private ScrollViewer? Scroller => Target?.FindAncestorOfType<ScrollViewer>();

    private double RenderScaling => _topLevel?.RenderScaling ?? 1;

    public void ZoomIn(Point? anchor = null) =>
        ZoomTo(Levels.FirstOrDefault(l => l > ZoomFactor + 1e-9, Levels[^1]), anchor);

    public void ZoomOut(Point? anchor = null) =>
        ZoomTo(Levels.LastOrDefault(l => l < ZoomFactor - 1e-9, Levels[0]), anchor);

    /// <summary>Zooms so the whole canvas fits the visible area.</summary>
    public void FitToWindow()
    {
        if (Scroller is not { } scroller || Target?.Child is not { } canvas
            || canvas.Bounds.Width <= 0 || canvas.Bounds.Height <= 0)
        {
            return;
        }

        var margin = Target.Margin;
        var scale = Math.Min(
            (scroller.Viewport.Width - margin.Left - margin.Right) / canvas.Bounds.Width,
            (scroller.Viewport.Height - margin.Top - margin.Bottom) / canvas.Bounds.Height);
        ZoomTo(scale * RenderScaling);
    }

    /// <summary>
    /// Sets the zoom, keeping the document point under <paramref name="anchor"/> (a point in the
    /// scroll viewer, defaulting to its centre) in place.
    /// </summary>
    public void ZoomTo(double factor, Point? anchor = null)
    {
        if (Scroller is not { } scroller || Target is null)
        {
            ZoomFactor = factor;
            return;
        }

        var a = anchor ?? new Point(scroller.Viewport.Width / 2, scroller.Viewport.Height / 2);
        var margin = Target.Margin;
        var oldScale = ZoomFactor / RenderScaling;
        var documentPoint = new Point(
            (scroller.Offset.X + a.X - margin.Left) / oldScale,
            (scroller.Offset.Y + a.Y - margin.Top) / oldScale);

        ZoomFactor = factor;
        scroller.UpdateLayout();

        var newScale = ZoomFactor / RenderScaling;
        var max = scroller.Extent - scroller.Viewport;
        scroller.Offset = new Vector(
            Math.Clamp(margin.Left + documentPoint.X * newScale - a.X, 0, Math.Max(0, max.Width)),
            Math.Clamp(margin.Top + documentPoint.Y * newScale - a.Y, 0, Math.Max(0, max.Height)));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ZoomFactorProperty)
        {
            ApplyScale();
            UpdateZoomText();
        }
        else if (change.Property == TargetProperty)
        {
            ApplyScale();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is null)
        {
            return;
        }

        // Tunnel so the shortcuts win over focused controls (e.g. ScrollViewer's own PageUp/PageDown).
        _topLevel.AddHandler(KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Tunnel);
        _topLevel.AddHandler(PointerWheelChangedEvent, OnTopLevelPointerWheelChanged, RoutingStrategies.Tunnel);
        _topLevel.ScalingChanged += OnScalingChanged;
        ApplyScale();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel is not null)
        {
            _topLevel.RemoveHandler(KeyDownEvent, OnTopLevelKeyDown);
            _topLevel.RemoveHandler(PointerWheelChangedEvent, OnTopLevelPointerWheelChanged);
            _topLevel.ScalingChanged -= OnScalingChanged;
            _topLevel = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private KeyModifiers CommandModifiers =>
        this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != CommandModifiers)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.PageUp:
                ZoomIn();
                e.Handled = true;
                break;
            case Key.PageDown:
                ZoomOut();
                e.Handled = true;
                break;
        }
    }

    private void OnTopLevelPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers != CommandModifiers || e.Delta.Y == 0 || Scroller is not { } scroller
            || e.Source is not Visual source || !scroller.IsVisualAncestorOf(source))
        {
            return;
        }

        var anchor = e.GetPosition(scroller);
        if (e.Delta.Y > 0)
        {
            ZoomIn(anchor);
        }
        else
        {
            ZoomOut(anchor);
        }

        e.Handled = true;
    }

    private void OnScalingChanged(object? sender, EventArgs e) => ApplyScale();

    private void ApplyScale()
    {
        var scale = ZoomFactor / RenderScaling;
        Target?.LayoutTransform = new ScaleTransform(scale, scale);
    }

    private void UpdateZoomText() =>
        ZoomText.Text = string.Format(ZoomStrings.ZoomLevelFormat, ZoomFactor * 100);

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => ZoomIn();

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => ZoomOut();

    private void OnFitToWindowClick(object? sender, RoutedEventArgs e) => FitToWindow();
}
