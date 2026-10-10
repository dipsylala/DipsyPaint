using Avalonia.Controls;
using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint;

public partial class MainWindow : Window
{
    public Document Document { get; }

    /// <summary>Edits <see cref="Document"/>; the DataContext that feature controls bind to.</summary>
    public DocumentEditor Editor { get; }

    public MainWindow() : this(Document.CreateBlank())
    {
    }

    public MainWindow(Document document)
    {
        Document = document;
        Editor = new DocumentEditor(document);
        DataContext = Editor;
        InitializeComponent();

        Canvas.Width = document.Width;
        Canvas.Height = document.Height;
        CanvasSizeText.Text = string.Format(Strings.CanvasSizeFormat, document.Width, document.Height);
    }
}
