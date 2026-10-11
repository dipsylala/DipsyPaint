using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;
using DipsyPaint.Layout;
using DipsyPaint.Resources;

namespace DipsyPaint.Tools;

/// <summary>
/// The shape tool's options panel, as reference Paint's Shapes group: the shape gallery, outline
/// and fill styles (outline in colour 1, fill in colour 2) and outline size. Option changes apply
/// to the pending shape; picking another shape commits it.
/// </summary>
public partial class ShapeOptions : UserControl
{
    // Reference Paint's gallery, in its order.
    private static readonly (ShapeKind Kind, string Name)[] Gallery =
    [
        (ShapeKind.Line, ShapeStrings.Line),
        (ShapeKind.Curve, ShapeStrings.Curve),
        (ShapeKind.Ellipse, ShapeStrings.Ellipse),
        (ShapeKind.Rectangle, ShapeStrings.Rectangle),
        (ShapeKind.RoundedRectangle, ShapeStrings.RoundedRectangle),
        (ShapeKind.Polygon, ShapeStrings.Polygon),
        (ShapeKind.Triangle, ShapeStrings.Triangle),
        (ShapeKind.RightTriangle, ShapeStrings.RightTriangle),
        (ShapeKind.Diamond, ShapeStrings.Diamond),
        (ShapeKind.Pentagon, ShapeStrings.Pentagon),
        (ShapeKind.Hexagon, ShapeStrings.Hexagon),
        (ShapeKind.RightArrow, ShapeStrings.RightArrow),
        (ShapeKind.LeftArrow, ShapeStrings.LeftArrow),
        (ShapeKind.UpArrow, ShapeStrings.UpArrow),
        (ShapeKind.DownArrow, ShapeStrings.DownArrow),
        (ShapeKind.FourPointStar, ShapeStrings.FourPointStar),
        (ShapeKind.FivePointStar, ShapeStrings.FivePointStar),
        (ShapeKind.SixPointStar, ShapeStrings.SixPointStar),
        (ShapeKind.RoundedRectangularCallout, ShapeStrings.RoundedRectangularCallout),
        (ShapeKind.OvalCallout, ShapeStrings.OvalCallout),
        (ShapeKind.CloudCallout, ShapeStrings.CloudCallout),
        (ShapeKind.Heart, ShapeStrings.Heart),
        (ShapeKind.Lightning, ShapeStrings.Lightning),
    ];

    /// <summary>Gallery columns: two rows on a top or bottom edge, a narrow column on a side edge.</summary>
    private const int HorizontalColumns = 12;
    private const int VerticalColumns = 4;

    private const double IconSize = 16;

    private readonly ShapeSettings _settings;
    private readonly Dictionary<ShapeKind, ToggleButton> _shapeButtons = [];

    public ShapeOptions() : this(new ShapeSettings())
    {
    }

    public ShapeOptions(ShapeSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        foreach (var (kind, name) in Gallery)
        {
            // Two rows of these fit the 40 px tool buttons' height, so the gallery doesn't make the bar taller.
            var button = new ToggleButton { Width = 20, Height = 20, Padding = new Thickness(2), MinWidth = 0, MinHeight = 0 };
            var icon = new Avalonia.Controls.Shapes.Path
            {
                Data = Icon(kind),
                StrokeThickness = 1.25,
                StrokeJoin = PenLineJoin.Round,
                Width = IconSize,
                Height = IconSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            icon.Bind(Shape.StrokeProperty, button.GetObservable(TemplatedControl.ForegroundProperty));
            button.Content = icon;
            AutomationProperties.SetName(button, name);
            ToolTip.SetTip(button, name);
            button.Click += (_, _) => SelectShape(kind);
            ShapeButtons.Children.Add(button);
            _shapeButtons[kind] = button;
        }

        SizeBox.ItemsSource = ShapeSettings.Sizes.Select(size => string.Format(ShapeStrings.SizeFormat, size)).ToList();
        OutlineBox.SelectedIndex = (int)settings.Outline;
        FillBox.SelectedIndex = (int)settings.Fill;
        SizeBox.SelectedIndex = Math.Max(0, ShapeSettings.Sizes.ToList().IndexOf(settings.Size));
        UpdateShapeButtons();
        ArrangeGallery(CommandGroup.GetOrientation(this));
    }

    private DocumentEditor? Editor => DataContext as DocumentEditor;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CommandGroup.OrientationProperty)
        {
            ArrangeGallery(change.GetNewValue<Orientation>());
        }
    }

    private void ArrangeGallery(Orientation orientation) =>
        ShapeButtons.Columns = orientation == Orientation.Vertical ? VerticalColumns : HorizontalColumns;

    // Bespoke icons drawn from the shapes' own outlines (ADR-0012); line, curve and polygon get a sample.
    private static Geometry Icon(ShapeKind kind)
    {
        const double inset = 1, full = IconSize - 2 * inset;
        IReadOnlyList<(double X, double Y)[]> contours;
        var closed = true;
        switch (kind)
        {
            case ShapeKind.Line:
                (contours, closed) = ([[(2, 14), (14, 2)]], false);
                break;
            case ShapeKind.Curve:
                (contours, closed) = ([ShapeOutlines.FlattenCubic((1, 12), (5, -2), (11, 18), (15, 4), 16)], false);
                break;
            case ShapeKind.Polygon:
                contours = [[(1, 14), (3, 3), (9, 7), (15, 2), (13, 14)]];
                break;
            default:
                // Wide shapes look wide, as in reference Paint's gallery.
                var (width, height) = kind switch
                {
                    ShapeKind.Rectangle or ShapeKind.Ellipse or ShapeKind.RoundedRectangle or ShapeKind.RoundedRectangularCallout
                        or ShapeKind.OvalCallout or ShapeKind.CloudCallout => (full, 11.0),
                    ShapeKind.RightArrow or ShapeKind.LeftArrow => (full, 10.0),
                    ShapeKind.UpArrow or ShapeKind.DownArrow => (10.0, full),
                    _ => (full, full),
                };
                var (left, top) = ((IconSize - width) / 2, (IconSize - height) / 2);
                contours = ShapeOutlines.Of(kind, width, height)
                    .Select(c => c.Select(p => (p.X + left, p.Y + top)).ToArray()).ToList();
                break;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            foreach (var contour in contours)
            {
                context.BeginFigure(new Point(contour[0].X, contour[0].Y), isFilled: false);
                foreach (var (x, y) in contour.Skip(1))
                {
                    context.LineTo(new Point(x, y));
                }

                context.EndFigure(closed);
            }
        }

        return geometry;
    }

    private void SelectShape(ShapeKind kind)
    {
        Editor?.CommitPending();
        _settings.Kind = kind;
        UpdateShapeButtons();
    }

    private void UpdateShapeButtons()
    {
        foreach (var (kind, button) in _shapeButtons)
        {
            button.IsChecked = kind == _settings.Kind;
        }

        FillBox.IsEnabled = ShapeSettings.CanFill(_settings.Kind); // a line or curve has no inside
    }

    private void OnOutlineChanged(object? sender, SelectionChangedEventArgs e) =>
        Apply(() => _settings.Outline = (ShapeStyle)OutlineBox.SelectedIndex, OutlineBox);

    private void OnFillChanged(object? sender, SelectionChangedEventArgs e) =>
        Apply(() => _settings.Fill = (ShapeStyle)FillBox.SelectedIndex, FillBox);

    private void OnSizeChanged(object? sender, SelectionChangedEventArgs e) =>
        Apply(() => _settings.Size = ShapeSettings.Sizes[SizeBox.SelectedIndex], SizeBox);

    private void Apply(Action change, ComboBox box)
    {
        if (box.SelectedIndex < 0)
        {
            return;
        }

        change();
        Editor?.RefreshPending();
    }
}
