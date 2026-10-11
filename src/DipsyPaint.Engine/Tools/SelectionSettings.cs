namespace DipsyPaint.Engine.Tools;

/// <summary>How a drag with the selection tool marks out a new selection.</summary>
public enum SelectionShape
{
    /// <summary>A rectangle between the press and the release.</summary>
    Rectangle,

    /// <summary>The area enclosed by the pointer's path, closed with a straight line back to the start.</summary>
    FreeForm,

    /// <summary>
    /// A click selects the contiguous area of colours like the clicked pixel's, within
    /// <see cref="SelectionSettings.Tolerance"/>: the magic wand (<see cref="Engine.MagicWand"/>), an
    /// addition beyond reference Paint (ADR-0011).
    /// </summary>
    MagicWand,
}

/// <summary>
/// Reference Paint's selection options, shared by the shell's Selection menu, every
/// <see cref="SelectionTool"/> it creates and <see cref="DocumentEditor.SelectionSettings"/> (for the
/// selection tools the editor creates itself, e.g. on paste or select all). Changing
/// <see cref="Transparent"/> applies to a lifted selection after <see cref="DocumentEditor.RefreshPending"/>;
/// <see cref="Shape"/> and <see cref="Tolerance"/> apply to the next selection.
/// </summary>
public sealed class SelectionSettings
{
    public const int MinTolerance = 0;
    public const int MaxTolerance = 100;
    public const int DefaultTolerance = 12;

    private SelectionShape _shape = SelectionShape.Rectangle;
    private int _tolerance = DefaultTolerance;

    public SelectionShape Shape
    {
        get => _shape;
        set
        {
            if (_shape != value)
            {
                _shape = value;
                ShapeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Raised when <see cref="Shape"/> changes, so the shell can show the magic wand's tolerance.</summary>
    public event EventHandler? ShapeChanged;

    /// <summary>
    /// The magic wand's colour tolerance in percent, clamped to <see cref="MinTolerance"/>–<see cref="MaxTolerance"/>;
    /// see <see cref="Engine.MagicWand.ChannelTolerance"/>.
    /// </summary>
    public int Tolerance
    {
        get => _tolerance;
        set => _tolerance = Math.Clamp(value, MinTolerance, MaxTolerance);
    }

    /// <summary>
    /// Transparent selection: pixels of exactly the secondary colour ("Colour 2") are left out of the
    /// selection when it is lifted, shown, copied or committed, so what is below shows through them.
    /// </summary>
    public bool Transparent { get; set; }
}
