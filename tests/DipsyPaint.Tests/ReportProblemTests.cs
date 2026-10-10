using System.Reflection;
using System.Reflection.PortableExecutable;
using Metadata = System.Reflection.Metadata;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using DipsyPaint.Diagnostics;
using DipsyPaint.Engine;

namespace DipsyPaint.Tests;

public sealed class ReportProblemTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("dipsypaint-report-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static Dictionary<string, string> Query(Uri url) =>
        url.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

    [Fact]
    public void UrlOpensANewIssueWithTitleVersionAndPlatform()
    {
        var url = ProblemReport.BuildUrl("1.2.3+abc", "Windows 11 (X64)", ".NET 10.0.0 (X64)", crashLog: null);

        Assert.StartsWith("https://github.com/dipsylala/DipsyPaint/issues/new?title=", url.AbsoluteUri);
        var query = Query(url);
        Assert.Equal(["title", "body"], query.Keys);
        Assert.Equal("Problem: ", query["title"]);
        Assert.Contains("- DipsyPaint version: 1.2.3+abc\n", query["body"]);
        Assert.Contains("- OS: Windows 11 (X64)\n", query["body"]);
        Assert.EndsWith("- .NET: .NET 10.0.0 (X64)", query["body"]);
        Assert.DoesNotContain("crash log", query["body"]);
    }

    [Fact]
    public void UrlEncodesReservedCharacters()
    {
        const string log = "a&b=c #d ?e+f %20 \"g\" <h>\r\nline 2 ✓ 😀";

        var url = ProblemReport.BuildUrl("1.0 & more", "OS #1", "x?y", log);

        Assert.DoesNotContain(" ", url.AbsoluteUri);
        Assert.Equal(1, url.AbsoluteUri.Count(c => c == '&')); // only the title/body separator
        Assert.DoesNotContain("#", url.AbsoluteUri);
        var body = Query(url)["body"];
        Assert.Contains("- DipsyPaint version: 1.0 & more\n- OS: OS #1\n- .NET: x?y", body);
        Assert.Contains("```text\na&b=c #d ?e+f %20 \"g\" <h>\nline 2 ✓ 😀\n```", body);
    }

    [Fact]
    public void LongCrashLogsAreTrimmedToFitTheLengthCap()
    {
        var log = string.Concat(Enumerable.Range(0, 2000).Select(i => $"   at Frame{i}() 😀\n"));

        var url = ProblemReport.BuildUrl("1.0", "OS", ".NET", log);

        Assert.InRange(url.AbsoluteUri.Length, ProblemReport.MaxUrlLength - 100, ProblemReport.MaxUrlLength);
        var body = Query(url)["body"];
        Assert.Contains("   at Frame0() 😀\n   at Frame1()", body);
        Assert.Contains("\n… (trimmed)\n```", body);
        Assert.DoesNotContain("Frame1999", body);
    }

    [Fact]
    public void ShortCrashLogsAreIncludedWhole()
    {
        var body = Query(ProblemReport.BuildUrl("1.0", "OS", ".NET", "Boom\n   at X()\n"))["body"];

        Assert.EndsWith("**Most recent crash log** (home folder and user name removed; please check it before submitting)\n```text\nBoom\n   at X()\n```", body);
        Assert.DoesNotContain("trimmed", body);
    }

    [AvaloniaFact]
    public async Task ReportAProblemOnlyHandsTheIssueUrlToTheLauncher()
    {
        var window = new MainWindow(Document.CreateBlank(64, 48));
        window.Show();
        var menu = window.FindControl<HelpMenu>("HelpMenu")!;
        var launched = new List<Uri>();
        menu.Launch = url =>
        {
            launched.Add(url);
            return Task.FromResult(true);
        };
        menu.CrashLog = new CrashLog(_folder);
        menu.CrashLog.Write(new InvalidOperationException("boom"), "AppDomain", DateTimeOffset.UtcNow);

        var item = menu.FindControl<MenuItem>("ReportProblemItem")!;
        Assert.Equal("Report a problem…", item.Header);
        Assert.Equal("Report a problem…", Avalonia.Automation.AutomationProperties.GetName(item));
        Assert.True(item.Focusable);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.True(await menu.ReportProblemAsync());

        Assert.Equal(2, launched.Count);
        Assert.All(launched, url => Assert.Equal(
            ProblemReport.BuildUrl(AppInfo.Version, AppInfo.Os, AppInfo.Runtime, menu.CrashLog.ReadNewest()), url));
        Assert.Contains("System.InvalidOperationException: boom", Query(launched[0])["body"]);
    }

    // AC28: no network requests except the update check (ADR-0017), which Velopack makes. The shell
    // and engine reference no networking assemblies themselves.
    [Theory]
    [InlineData(typeof(App))]
    [InlineData(typeof(Document))]
    public void ShellAndEngineReferenceNoNetworkingAssemblies(Type type)
    {
        var references = type.Assembly.GetReferencedAssemblies().Select(r => r.Name);

        Assert.DoesNotContain(references, Networking.Contains);
    }

    // Among the third-party assemblies the app ships, only Velopack (the update check) may reach the
    // network. The others listed use sockets for local connections only. A new entry needs review.
    [Fact]
    public void OnlyTheUpdateCheckReferencesNetworkingAssemblies()
    {
        string[] allowed =
        [
            "Velopack -> System.Net.Http", // the update check
            "Tmds.DBus.Protocol -> System.Net.Sockets", // Linux D-Bus, over a local Unix socket
            "Avalonia.FreeDesktop.AtSpi -> System.Net.Sockets", // Linux accessibility, over D-Bus
            "Avalonia.Remote.Protocol -> System.Net.Sockets", // the IDE XAML previewer, only when it hosts the app
        ];

        var references = ShippedAssemblies(typeof(App).Assembly)
            .SelectMany(a => a.GetReferencedAssemblies().Where(r => Networking.Contains(r.Name)).Select(r => $"{a.GetName().Name} -> {r.Name}"))
            .ToList();

        Assert.Contains("Velopack -> System.Net.Http", references);
        Assert.Empty(references.Except(allowed));
    }

    // And the app uses Velopack only for the GitHub Releases update check: no other update source
    // (web URL, Velopack Flow, GitLab…) or downloader. A new entry here needs the same review.
    [Fact]
    public void AppUsesOnlyVelopacksGithubUpdatePath()
    {
        string[] allowed =
        [
            "Velopack.VelopackApp", "Velopack.UpdateManager", "Velopack.UpdateInfo", "Velopack.VelopackAsset",
            "Velopack.UpdateOptions", "Velopack.Locators.IVelopackLocator",
            "Velopack.Sources.GithubSource", "Velopack.Sources.IUpdateSource", "Velopack.Sources.IFileDownloader",
        ];

        Assert.Equal(allowed.Order(), VelopackTypesUsedBy(typeof(App).Assembly).Order());
    }

    private static readonly string[] Networking =
        ["System.Net.Http", "System.Net.Sockets", "System.Net.WebSockets", "System.Net.Requests", "System.Net.WebClient", "System.Net.Mail", "System.Net.Quic", "System.Net.HttpListener"];

    /// <summary>The app's assembly and every non-framework assembly it references, directly or not.</summary>
    private static IEnumerable<Assembly> ShippedAssemblies(Assembly app)
    {
        var runtime = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        var seen = new HashSet<string>();
        var pending = new Stack<Assembly>([app]);
        while (pending.TryPop(out var assembly))
        {
            if (!seen.Add(assembly.GetName().Name!) || assembly.Location.StartsWith(runtime, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yield return assembly;
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                pending.Push(Assembly.Load(reference));
            }
        }
    }

    private static IEnumerable<string> VelopackTypesUsedBy(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var metadata = Metadata.PEReaderExtensions.GetMetadataReader(pe);
        foreach (var handle in metadata.TypeReferences)
        {
            var type = metadata.GetTypeReference(handle);
            if (type.ResolutionScope.Kind == Metadata.HandleKind.AssemblyReference
                && metadata.GetString(metadata.GetAssemblyReference((Metadata.AssemblyReferenceHandle)type.ResolutionScope).Name) == "Velopack")
            {
                yield return $"{metadata.GetString(type.Namespace)}.{metadata.GetString(type.Name)}";
            }
        }
    }
}
