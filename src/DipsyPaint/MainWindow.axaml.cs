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
        Editor?.SizeChanged -= OnCanvasSizeChanged;
        Editor = new DocumentEditor(document);
        Editor.SizeChanged += OnCanvasSizeChanged;
        DataContext = Editor;
        ShowCanvasSize();
    }

    private void OnCanvasSizeChanged(object? sender, EventArgs e) => ShowCanvasSize();

    private void ShowCanvasSize()
    {
        Canvas.Width = Document.Width;
        Canvas.Height = Document.Height;
        CanvasSizeText.Text = string.Format(Strings.CanvasSizeFormat, Document.Width, Document.Height);
    }
}
