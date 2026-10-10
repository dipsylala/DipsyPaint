using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint;

public partial class MainWindow : Window
{
    public Document Document => Editor.Document;

    /// <summary>Edits <see cref="Document"/>; the DataContext that feature controls bind to.</summary>
    public DocumentEditor Editor { get; private set; }

    public MainWindow() : this(Document.CreateBlank())
    {
    }

    public MainWindow(Document document)
    {
        InitializeComponent();
        ReplaceDocument(document);
    }

    /// <summary>
    /// Shows <paramref name="document"/> instead of the current one, with a new editor and so an empty
    /// history. Feature controls follow the DataContext change.
    /// </summary>
    [MemberNotNull(nameof(Editor))]
    public void ReplaceDocument(Document document)
    {
        Editor = new DocumentEditor(document);
        DataContext = Editor;
        Canvas.Width = document.Width;
        Canvas.Height = document.Height;
        CanvasSizeText.Text = string.Format(Strings.CanvasSizeFormat, document.Width, document.Height);
    }
}
