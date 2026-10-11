using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DipsyPaint.Resources;

namespace DipsyPaint.Recovery;

/// <summary>
/// Offers a snapshot left by a crash: Restore (Enter), Discard, or Decide later (Esc). Closing it any
/// other way is Decide later.
/// </summary>
public partial class RecoveryDialog : Window
{
    // The designer and XAML loader need a parameterless constructor.
    public RecoveryDialog() : this(new RecoverySnapshot("", "", null, DateTimeOffset.Now))
    {
    }

    public RecoveryDialog(RecoverySnapshot snapshot)
    {
        InitializeComponent();
        QuestionText.Text = Question(snapshot);
        Opened += (_, _) => RestoreButton.Focus();
    }

    /// <summary>The choice made; Later until a button is pressed.</summary>
    public RecoveryChoice Result { get; private set; } = RecoveryChoice.Later;

    /// <summary>The question asked about <paramref name="snapshot"/>.</summary>
    public static string Question(RecoverySnapshot snapshot) => string.Format(
        CultureInfo.CurrentCulture,
        RecoveryStrings.Question,
        snapshot.DocumentName ?? FileStrings.Untitled,
        snapshot.SavedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));

    private void Choose(RecoveryChoice choice)
    {
        Result = choice;
        Close();
    }

    private void OnRestoreClick(object? sender, RoutedEventArgs e) => Choose(RecoveryChoice.Restore);

    private void OnDiscardClick(object? sender, RoutedEventArgs e) => Choose(RecoveryChoice.Discard);

    private void OnLaterClick(object? sender, RoutedEventArgs e) => Choose(RecoveryChoice.Later);
}

/// <summary>Shows <see cref="RecoveryDialog"/> modally over <paramref name="owner"/>.</summary>
internal sealed class RecoveryDialogPrompt(Window owner) : IRecoveryPrompt
{
    public async Task<RecoveryChoice> AskAsync(RecoverySnapshot snapshot)
    {
        var dialog = new RecoveryDialog(snapshot);
        await dialog.ShowDialog(owner);
        return dialog.Result;
    }
}
