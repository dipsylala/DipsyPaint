using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DipsyPaint.Resources;

namespace DipsyPaint.Files;

/// <summary>
/// Reference Paint's "Do you want to save changes to …?" prompt: Save (Enter), Don't save, or
/// Cancel (Esc). Closing it any other way is Cancel.
/// </summary>
public partial class SaveChangesDialog : Window
{
    // The designer and XAML loader need a parameterless constructor.
    public SaveChangesDialog() : this(FileStrings.Untitled)
    {
    }

    /// <param name="documentName">The document's file name, or "Untitled".</param>
    public SaveChangesDialog(string documentName)
    {
        InitializeComponent();
        QuestionText.Text = string.Format(SaveChangesStrings.Question, documentName);
        Opened += (_, _) => SaveButton.Focus();
    }

    /// <summary>The choice made; Cancel until a button is pressed.</summary>
    public SaveChangesChoice Result { get; private set; } = SaveChangesChoice.Cancel;

    private void Choose(SaveChangesChoice choice)
    {
        Result = choice;
        Close();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => Choose(SaveChangesChoice.Save);

    private void OnDontSaveClick(object? sender, RoutedEventArgs e) => Choose(SaveChangesChoice.DontSave);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Choose(SaveChangesChoice.Cancel);
}

/// <summary>Shows <see cref="SaveChangesDialog"/> modally over the window <paramref name="anchor"/> is in.</summary>
internal sealed class SaveChangesDialogPrompt(Visual anchor) : ISaveChangesPrompt
{
    public async Task<SaveChangesChoice> AskAsync(string documentName)
    {
        if (TopLevel.GetTopLevel(anchor) is not Window owner)
        {
            return SaveChangesChoice.Cancel;
        }

        var dialog = new SaveChangesDialog(documentName);
        await dialog.ShowDialog(owner);
        return dialog.Result;
    }
}
