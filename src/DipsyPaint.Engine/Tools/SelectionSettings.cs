namespace DipsyPaint.Engine.Tools;

/// <summary>How a drag with the selection tool marks out a new selection.</summary>
public enum SelectionShape
{
    /// <summary>A rectangle between the press and the release.</summary>
    Rectangle,

    /// <summary>The area enclosed by the pointer's path, closed with a straight line back to the start.</summary>
    FreeForm,
}

/// <summary>
/// Reference Paint's selection options, shared by the shell's Selection menu, every
/// <see cref="SelectionTool"/> it creates and <see cref="DocumentEditor.SelectionSettings"/> (for the
/// selection tools the editor creates itself, e.g. on paste or select all). Changing
/// <see cref="Transparent"/> applies to a lifted selection after <see cref="DocumentEditor.RefreshPending"/>;
/// <see cref="Shape"/> applies to the next selection.
/// </summary>
public sealed class SelectionSettings
{
    public SelectionShape Shape { get; set; } = SelectionShape.Rectangle;

    /// <summary>
    /// Transparent selection: pixels of exactly the secondary colour ("Colour 2") are left out of the
    /// selection when it is lifted, shown, copied or committed, so what is below shows through them.
    /// </summary>
    public bool Transparent { get; set; }
}
