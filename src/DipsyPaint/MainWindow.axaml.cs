using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using DipsyPaint.Engine;
using DipsyPaint.Layout;
using DipsyPaint.Resources;
using DipsyPaint.Settings;

namespace DipsyPaint;

public partial class MainWindow : Window
{
    // The status bar says the opened file's resolution was invalid until the document is saved (ADR-0024).
    private bool _resolutionWasInvalid;

    public Document Document => Editor.Document;

    /// <summary>Edits <see cref="Document"/>; the DataContext that feature controls bind to.</summary>
    public DocumentEditor Editor { get; private set; }

    public MainWindow() : this(Document.CreateBlank())
    {
    }

    /// <param name="document">The document to show.</param>
    /// <param name="settings">
    /// Where the user's settings (the command bar layout, the View menu's display options) are restored from and saved to; null keeps
    /// them for this window only, as in tests.
    /// </param>
    public MainWindow(Document document, SettingsStore? settings = null)
    {
        InitializeComponent();
        CommandBarLayout = new CommandBarDock(this, UndockedGroups, DragOutline, TopEdge, BottomEdge, LeftEdge, RightEdge);
        ViewMenu.Connect(Zoom, StatusBar, Rulers, Gridlines,
            [CommandBar, (Control)BottomEdge.Parent!, (Control)LeftEdge.Parent!, (Control)RightEdge.Parent!, UndockedGroups]);
        if (settings is not null)
        {
            var saved = settings.Load();
            CommandBarLayout.Restore(saved.CommandBar);
            CommandBarLayout.Changed += (_, _) =>
            {
                saved.CommandBar = CommandBarLayout.Arrangement.ToSettings();
                settings.Save(saved);
            };
            ViewMenu.Restore(saved.View);
            ViewMenu.Changed += (_, _) =>
            {
                saved.View = ViewMenu.ToSettings();
                settings.Save(saved);
            };
        }

        ReplaceDocument(document);
    }

    /// <summary>Where the command groups are docked; moves are saved to the settings, if any.</summary>
    public CommandBarDock CommandBarLayout { get; }

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
        _resolutionWasInvalid = document.ResolutionWasInvalid;
        ShowCanvasSize();
    }

    /// <summary>Called once the document has been saved: the file now holds the document's resolution.</summary>
    internal void OnDocumentSaved()
    {
        _resolutionWasInvalid = false;
        ShowCanvasSize();
    }

    private void OnCanvasSizeChanged(object? sender, EventArgs e) => ShowCanvasSize();

    private void ShowCanvasSize()
    {
        Canvas.Width = Document.Width;
        Canvas.Height = Document.Height;
        CanvasSizeText.Text = string.Format(ResolutionStrings.StatusFormat,
            string.Format(Strings.CanvasSizeFormat, Document.Width, Document.Height),
            ResolutionText.Format(Document.Resolution, _resolutionWasInvalid));
    }
}
