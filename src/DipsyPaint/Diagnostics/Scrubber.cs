using System.Text.RegularExpressions;

namespace DipsyPaint.Diagnostics;

/// <summary>
/// Removes the user's home folder and user name from text that may be pasted publicly (ADR-0017).
/// The home folder becomes <c>~</c> and the user name becomes <c>&lt;user&gt;</c>.
/// </summary>
public static class Scrubber
{
    public const string UserPlaceholder = "<user>";

    /// <summary>Shorter user names are left alone: replacing them would mangle ordinary words.</summary>
    public const int MinUserNameLength = 3;

    public static string Scrub(string text) =>
        Scrub(text, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.UserName);

    public static string Scrub(string text, string homePath, string userName)
    {
        // Either separator, any case, and not when the home folder is only a prefix of a longer name.
        var parts = homePath.Split('/', '\\').Where(p => p.Length > 0).Select(Regex.Escape).ToArray();
        if (parts.Length > 0)
        {
            var root = homePath[0] is '/' or '\\' ? @"[\\/]" : "";
            text = Regex.Replace(text, root + string.Join(@"[\\/]", parts) + @"(?![\p{L}\p{N}])", "~", Options);
        }

        if (userName.Length >= MinUserNameLength)
        {
            text = Regex.Replace(text, @"(?<![\p{L}\p{N}])" + Regex.Escape(userName) + @"(?![\p{L}\p{N}])", UserPlaceholder, Options);
        }

        return text;
    }

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
}
