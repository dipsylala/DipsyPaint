namespace DipsyPaint.Files;

/// <summary>The answer to the "Save changes?" prompt.</summary>
public enum SaveChangesChoice
{
    Save,
    DontSave,
    Cancel,
}

/// <summary>
/// Asks whether to save unsaved changes before they are discarded. <see cref="SaveChangesDialog"/>
/// is the real prompt; tests substitute their own.
/// </summary>
public interface ISaveChangesPrompt
{
    /// <summary>Asks about the document named <paramref name="documentName"/> ("Untitled" or its file name).</summary>
    Task<SaveChangesChoice> AskAsync(string documentName);
}
