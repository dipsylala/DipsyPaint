using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DipsyPaint.Engine;
using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Text;

/// <summary>
/// Edits the text tool's pending text in place. Mounted over the <see cref="Drawing.DocumentCanvas"/>,
/// in document pixels, it shows a text box over <see cref="TextTool.TextBounds"/> while text is
/// pending. The box draws only the caret and selection: the text is the engine's overlay, so what is
/// shown is exactly what a commit writes. Typing updates <see cref="TextTool.Text"/>; Ctrl+B, Ctrl+I
/// and Ctrl+U (Cmd on macOS) toggle bold, italic and underline, as in reference Paint.
/// </summary>
public partial class TextEntry : UserControl
{
    private DocumentEditor? _editor;
    private TextTool? _tool;
    private TopLevel? _topLevel;
    private double _scale = 1;
    private bool _updating;

    public TextEntry()
    {
        InitializeComponent();
        Box.TextChanged += OnBoxTextChanged;
        Box.KeyDown += OnBoxKeyDown;
        Box.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Border>("PART_BorderElement") is { } border)
            {
                border.Background = Brushes.Transparent;
                border.BorderThickness = default;
            }
        };
    }

    /// <summary>Puts the keyboard focus back in the text box, if one is open (e.g. after a click on a text option).</summary>
    public void FocusText()
    {
        if (Box.IsVisible)
        {
            Box.Focus();
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _editor?.ActiveToolChanged -= OnActiveToolChanged;
        _editor = DataContext as DocumentEditor;
        _editor?.ActiveToolChanged += OnActiveToolChanged;
        OnActiveToolChanged(null, EventArgs.Empty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        LayoutUpdated += OnLayoutUpdated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActiveToolChanged(object? sender, EventArgs e)
    {
        var tool = _editor?.ActiveTool as TextTool;
        if (ReferenceEquals(tool, _tool))
        {
            return;
        }

        if (_tool is not null)
        {
            _tool.Changed -= OnToolChanged;
            _tool.Settings.Changed -= OnSettingsChanged;
        }

        _tool = tool;
        if (_tool is not null)
        {
            _tool.Changed += OnToolChanged;
            _tool.Settings.Changed += OnSettingsChanged;
        }

        Update();
    }

    private void OnToolChanged(object? sender, EventArgs e) => Update();

    // Options apply to all of the pending text at once.
    private void OnSettingsChanged(object? sender, EventArgs e) => _editor?.RefreshPending();

    // The frame's grab band follows the zoom (see TextTool); DocumentCanvas updates the editor's handle
    // tolerance in its own layout pass, so re-render once that has run.
    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var scale = _topLevel is null ? 1 : this.TransformToVisual(_topLevel)?.M11 ?? 1;
        if (scale > 0 && scale != _scale)
        {
            _scale = scale;
            if (_tool?.TextBounds is not null)
            {
                Dispatcher.UIThread.Post(() => _editor?.RefreshPending(), DispatcherPriority.Background);
            }
        }
    }

    private void Update()
    {
        if (_tool?.TextBounds is not { } bounds)
        {
            Box.IsVisible = false;
            return;
        }

        var style = _tool.Settings.ToStyle();
        Box.FontFamily = TextRenderer.FontFamilyOf(style);
        Box.FontSize = TextRenderer.FontSizeOf(style);
        Box.FontWeight = style.Bold ? FontWeight.Bold : FontWeight.Normal;
        Box.FontStyle = style.Italic ? FontStyle.Italic : FontStyle.Normal;
        Box.CaretBrush = new SolidColorBrush(TextRenderer.ColorOf(_editor?.PrimaryColor.ToOpaque() ?? Rgba.Black));
        Canvas.SetLeft(Box, bounds.X);
        Canvas.SetTop(Box, bounds.Y);
        Box.Width = bounds.Width;
        Box.Height = bounds.Height;
        if (Box.Text != _tool.Text)
        {
            _updating = true;
            Box.Text = _tool.Text;
            _updating = false;
        }

        if (!Box.IsVisible)
        {
            Box.IsVisible = true;
            Box.Focus();
        }
    }

    private void OnBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updating || _tool?.TextBounds is null)
        {
            return;
        }

        _tool.Text = Box.Text ?? "";
        _editor?.RefreshPending();
    }

    private KeyModifiers CommandModifiers =>
        this.GetPlatformSettings()?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    private void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_tool is null || e.KeyModifiers != CommandModifiers)
        {
            return;
        }

        var settings = _tool.Settings;
        switch (e.Key)
        {
            case Key.B:
                settings.Bold = !settings.Bold;
                break;
            case Key.I:
                settings.Italic = !settings.Italic;
                break;
            case Key.U:
                settings.Underline = !settings.Underline;
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
