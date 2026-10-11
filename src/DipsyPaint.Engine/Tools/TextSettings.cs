namespace DipsyPaint.Engine.Tools;

/// <summary>
/// How text is set, as a snapshot handed to an <see cref="ITextRenderer"/>.
/// </summary>
/// <param name="FontFamily">Font family name; empty for the platform's default font.</param>
/// <param name="Size">Font size in points, at 96 document pixels per inch (so 12 pt is 16 px), as in reference Paint.</param>
public readonly record struct TextStyle(string FontFamily, double Size, bool Bold, bool Italic, bool Underline, bool Strikethrough);

/// <summary>
/// The text tool's options, shared by its options panel and every <see cref="TextTool"/> it creates.
/// As in reference Paint they apply to all of the pending text at once. Changes raise
/// <see cref="Changed"/>; the shell then re-renders pending text with <see cref="DocumentEditor.RefreshPending"/>.
/// </summary>
public sealed class TextSettings
{
    public const double MinSize = 1;
    public const double MaxSize = 500;

    /// <summary>The font sizes reference Paint lists, in points. Other sizes can be typed.</summary>
    public static IReadOnlyList<int> Sizes { get; } = [8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72];

    private string _fontFamily = "";
    private double _size = 11;
    private bool _bold;
    private bool _italic;
    private bool _underline;
    private bool _strikethrough;
    private bool _fillBackground;

    /// <summary>Raised after any option changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Font family name; empty for the platform's default font.</summary>
    public string FontFamily
    {
        get => _fontFamily;
        set => Set(ref _fontFamily, value ?? "");
    }

    /// <summary>Font size in points, clamped to <see cref="MinSize"/>–<see cref="MaxSize"/>.</summary>
    public double Size
    {
        get => _size;
        set => Set(ref _size, double.IsFinite(value) ? Math.Clamp(value, MinSize, MaxSize) : _size);
    }

    public bool Bold
    {
        get => _bold;
        set => Set(ref _bold, value);
    }

    public bool Italic
    {
        get => _italic;
        set => Set(ref _italic, value);
    }

    public bool Underline
    {
        get => _underline;
        set => Set(ref _underline, value);
    }

    public bool Strikethrough
    {
        get => _strikethrough;
        set => Set(ref _strikethrough, value);
    }

    /// <summary>
    /// Whether the text box is filled with the secondary colour (reference Paint's opaque background);
    /// otherwise the background is transparent.
    /// </summary>
    public bool FillBackground
    {
        get => _fillBackground;
        set => Set(ref _fillBackground, value);
    }

    public TextStyle ToStyle() => new(FontFamily, Size, Bold, Italic, Underline, Strikethrough);

    private void Set<T>(ref T field, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
