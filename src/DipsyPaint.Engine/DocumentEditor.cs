using DipsyPaint.Engine.Tools;

namespace DipsyPaint.Engine;

/// <summary>
/// Routes pointer input to the active tool and keeps the composite up to date.
/// The shell and scripted tests both drive editing through this class.
/// </summary>
public sealed class DocumentEditor
{
    public DocumentEditor(Document document)
    {
        Document = document;
        Compositor = new Compositor(document);
    }

    public Document Document { get; }
    public Compositor Compositor { get; }

    public ITool? ActiveTool { get; set; }

    /// <summary>Colour for the left button. Fixed default until colour selection exists.</summary>
    public Rgba PrimaryColor { get; set; } = Rgba.Black;

    /// <summary>Colour for the right button. Fixed default until colour selection exists.</summary>
    public Rgba SecondaryColor { get; set; } = Rgba.White;

    /// <summary>Raised with the document-space rectangle whose composited pixels changed.</summary>
    public event EventHandler<IntRect>? CompositeChanged;

    public void Apply(PointerInput input)
    {
        if (ActiveTool is null)
        {
            return;
        }

        var context = new ToolContext(Document.ActiveLayer.Pixels, PrimaryColor, SecondaryColor);
        var dirty = ActiveTool.Handle(context, input);
        if (dirty.IsEmpty)
        {
            return;
        }

        Compositor.Update(dirty);
        CompositeChanged?.Invoke(this, dirty);
    }
}
