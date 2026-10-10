using Avalonia.Controls;
using DipsyPaint.Engine;
using DipsyPaint.Resources;

namespace DipsyPaint;

public partial class MainWindow : Window
{
    public Document Document { get; }

    public MainWindow() : this(Document.CreateBlank())
    {
    }

    public MainWindow(Document document)
    {
        Document = document;
        InitializeComponent();

        Canvas.Width = document.Width;
        Canvas.Height = document.Height;
        CanvasSizeText.Text = string.Format(Strings.CanvasSizeFormat, document.Width, document.Height);
    }
}
