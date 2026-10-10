using DipsyPaint.Diagnostics;

namespace DipsyPaint.Tests;

public sealed class CrashLogTests : IDisposable
{
    private static readonly DateTimeOffset Time = new(2026, 10, 10, 12, 34, 56, 789, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Directory.CreateTempSubdirectory("dipsypaint-crash-").FullName, "CrashLogs");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_folder)!, recursive: true);

    private static Exception Thrown(string message)
    {
        try
        {
            throw new InvalidOperationException(message, new FormatException("inner cause"));
        }
        catch (Exception e)
        {
            return e;
        }
    }

    [Fact]
    public void WriteCreatesTheFolderAndLogsVersionPlatformAndStackTrace()
    {
        var log = new CrashLog(_folder);

        var path = log.Write(Thrown("boom"), "UI thread", Time);

        Assert.Equal(Path.Combine(_folder, "crash-20261010-123456-789.txt"), path);
        var text = File.ReadAllText(path);
        Assert.StartsWith("DipsyPaint crash log", text);
        Assert.Contains("Time: 2026-10-10 12:34:56.789 UTC", text);
        Assert.Contains($"Version: {AppInfo.Version}", text);
        Assert.Contains($"OS: {Scrubber.Scrub(AppInfo.Os)}", text);
        Assert.Contains($".NET: {AppInfo.Runtime}", text);
        Assert.Contains("Source: UI thread", text);
        Assert.Contains("System.InvalidOperationException: boom", text);
        Assert.Contains("System.FormatException: inner cause", text);
        Assert.Contains($"at {typeof(CrashLogTests).FullName}.{nameof(Thrown)}", text);
    }

    [Fact]
    public void WriteScrubsTheHomeFolderAndUserName()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var picture = Path.Combine(home, "Pictures", "holiday.png");

        var text = File.ReadAllText(new CrashLog(_folder).Write(Thrown($"Could not open '{picture}' for {Environment.UserName}"), "AppDomain", Time));

        Assert.DoesNotContain(home, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"'~{Path.DirectorySeparatorChar}Pictures{Path.DirectorySeparatorChar}holiday.png'", text);
        if (Environment.UserName.Length >= Scrubber.MinUserNameLength)
        {
            Assert.Contains("for <user>", text);
        }
    }

    [Fact]
    public void KeepsOnlyTheNewestLogs()
    {
        var log = new CrashLog(_folder, maxLogs: 3);

        for (var i = 0; i < 5; i++)
        {
            log.Write(Thrown($"crash {i}"), "AppDomain", Time.AddMinutes(i));
        }

        var names = Directory.GetFiles(_folder).Select(p => Path.GetFileName(p)).Order().ToArray();
        Assert.Equal(["crash-20261010-123656-789.txt", "crash-20261010-123756-789.txt", "crash-20261010-123856-789.txt"], names);
        Assert.Contains("crash 4", log.ReadNewest());
    }

    [Fact]
    public void ReadNewestIsNullWithoutLogs() => Assert.Null(new CrashLog(_folder).ReadNewest());

    [Theory]
    [InlineData(@"at C:\Users\Bob\Pictures\a.png", @"at ~\Pictures\a.png")]
    [InlineData(@"at c:/users/bob/Pictures/a.png", @"at ~/Pictures/a.png")]
    [InlineData(@"'C:\Users\Bob'", @"'~'")]
    [InlineData(@"C:\Users\Bobby\a.png", @"C:\Users\Bobby\a.png")] // a different user's folder
    [InlineData("user bob, Bob's file, BOB", "user <user>, <user>'s file, <user>")]
    [InlineData("bobsleigh and kebob", "bobsleigh and kebob")]
    public void ScrubReplacesAWindowsHomeAndUserName(string text, string expected) =>
        Assert.Equal(expected, Scrubber.Scrub(text, @"C:\Users\Bob", "bob"));

    [Theory]
    [InlineData("in /home/alice/src/x.cs:line 4", "in ~/src/x.cs:line 4")]
    [InlineData("/home/alice", "~")]
    [InlineData("/home/alicia/x", "/home/alicia/x")]
    public void ScrubReplacesAUnixHome(string text, string expected) =>
        Assert.Equal(expected, Scrubber.Scrub(text, "/home/alice/", "alice"));

    [Fact]
    public void ScrubLeavesShortUserNamesAlone() =>
        Assert.Equal("~/x is a file", Scrubber.Scrub("/home/a/x is a file", "/home/a", "a"));

    [Fact]
    public void HandlerLogsEachExceptionOnceAndThenRaisesCrashed()
    {
        var handler = new CrashHandler(new CrashLog(_folder));
        var crashed = new List<Exception>();
        handler.Crashed += _ => throw new InvalidOperationException("a failing hook is ignored");
        handler.Crashed += crashed.Add;
        var exception = Thrown("boom");

        handler.Handle(exception, "UI thread");
        handler.Handle(exception, "AppDomain"); // the same exception reaching the next handler

        Assert.Single(Directory.GetFiles(_folder));
        Assert.Contains("Source: UI thread", handler.Log.ReadNewest());
        Assert.Equal([exception], crashed);
    }

    [Fact]
    public void HandlerSurvivesAnUnwritableLogFolder()
    {
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(_folder)!, "file"), "");
        var handler = new CrashHandler(new CrashLog(Path.Combine(Path.GetDirectoryName(_folder)!, "file", "CrashLogs")));
        var crashed = 0;
        handler.Crashed += _ => crashed++;

        handler.Handle(Thrown("boom"), "AppDomain");

        Assert.Equal(1, crashed);
    }
}
