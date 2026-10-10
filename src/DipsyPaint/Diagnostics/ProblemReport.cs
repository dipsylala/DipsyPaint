using DipsyPaint.Resources;

namespace DipsyPaint.Diagnostics;

/// <summary>
/// The pre-filled GitHub new-issue URL behind "Report a problem". The user's browser opens it and the
/// user reviews and submits the issue; the app itself sends nothing (ADR-0017).
/// </summary>
public static class ProblemReport
{
    public const string NewIssueUrl = "https://github.com/dipsylala/DipsyPaint/issues/new";

    /// <summary>Well under GitHub's roughly 8 KB request limit, with headroom for browsers and OS launchers.</summary>
    public const int MaxUrlLength = 4000;

    /// <summary>
    /// The issue URL: a title, and a body with the version and platform plus <paramref name="crashLog"/>
    /// (already scrubbed) if given, trimmed from the end so the URL fits in <paramref name="maxLength"/>.
    /// </summary>
    public static Uri BuildUrl(string version, string os, string runtime, string? crashLog, int maxLength = MaxUrlLength)
    {
        var body = string.Format(ReportStrings.IssueBody, version, os, runtime);
        var url = Compose(body);
        if (string.IsNullOrWhiteSpace(crashLog) || url.Length > maxLength)
        {
            return new Uri(url);
        }

        // Longest prefix of the log that fits.
        int fits = 0, tooLong = crashLog.Length + 1;
        while (tooLong - fits > 1)
        {
            var mid = (fits + tooLong) / 2;
            if (Compose(body + CrashSection(crashLog, mid)).Length <= maxLength)
            {
                fits = mid;
            }
            else
            {
                tooLong = mid;
            }
        }

        return new Uri(fits == 0 ? url : Compose(body + CrashSection(crashLog, fits)));
    }

    private static string CrashSection(string log, int length)
    {
        // Don't split a surrogate pair.
        if (length < log.Length && char.IsHighSurrogate(log[length - 1]))
        {
            length--;
        }

        var text = length < log.Length ? log[..length].TrimEnd() + Environment.NewLine + ReportStrings.Trimmed : log.TrimEnd();
        return string.Format(ReportStrings.CrashLogSection, text);
    }

    private static string Compose(string body) =>
        $"{NewIssueUrl}?title={Uri.EscapeDataString(ReportStrings.IssueTitle)}&body={Uri.EscapeDataString(body.ReplaceLineEndings("\n"))}";
}
