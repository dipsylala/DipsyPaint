using System.Text;
using DipsyPaint.Recovery;
using DipsyPaint.Settings;

namespace DipsyPaint.Tests;

/// <summary>The recovery folder (ADR-0013), always a temporary one here, never the user's.</summary>
public sealed class RecoveryStoreTests : IDisposable
{
    private static readonly DateTimeOffset Time = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Directory.CreateTempSubdirectory("dipsypaint-recovery-store-").FullName;
    private readonly List<RecoveryStore> _stores = [];

    public void Dispose()
    {
        foreach (var store in _stores)
        {
            store.Dispose();
        }

        Directory.Delete(_folder, recursive: true);
    }

    private RecoveryStore Begin()
    {
        var store = new RecoveryStore(_folder);
        _stores.Add(store);
        store.BeginSession();
        return store;
    }

    private static void Write(RecoveryStore store, string content, string? name = "cat.png") =>
        Assert.True(store.Write(store.Reserve(), s => s.Write(Encoding.UTF8.GetBytes(content)), new RecoveryInfo(name, Time)));

    // A crash: the operating system releases the session's lock and leaves its files.
    private static RecoverySnapshot Crashed(RecoveryStore store, RecoveryStore next)
    {
        store.Dispose();
        return Assert.Single(next.FindOrphans());
    }

    [Fact]
    public void Write_ReplacesTheSnapshotInTheRecoveryFolder_LeavingNoTemporaryFiles()
    {
        var store = Begin();

        Write(store, "first");
        Write(store, "second");

        Assert.Equal(_folder, Path.GetDirectoryName(store.SnapshotPath));
        Assert.Equal("second", File.ReadAllText(store.SnapshotPath));
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Fact]
    public void AWriteThatFailsPartWay_LeavesThePreviousSnapshotWhole()
    {
        var store = Begin();
        Write(store, "whole");

        Assert.Throws<IOException>(() => store.Write(store.Reserve(), s =>
        {
            s.Write("half"u8);
            throw new IOException("disk full");
        }, new RecoveryInfo(null, Time)));

        Assert.Equal("whole", File.ReadAllText(store.SnapshotPath));
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Fact]
    public void AWriteReservedBeforeANewerWriteOrADelete_IsDropped()
    {
        var store = Begin();
        var older = store.Reserve();
        Write(store, "newer");

        Assert.False(store.Write(older, s => s.Write("older"u8), new RecoveryInfo(null, Time)));
        Assert.Equal("newer", File.ReadAllText(store.SnapshotPath));

        var beforeDelete = store.Reserve();
        store.Delete();
        Assert.False(store.Write(beforeDelete, s => s.Write("late"u8), new RecoveryInfo(null, Time)));
        Assert.False(File.Exists(store.SnapshotPath));
    }

    [Fact]
    public void ARunningSessionsSnapshot_IsNotAnOrphan_UntilItsProcessDies()
    {
        var first = Begin();
        Write(first, "work");
        var second = Begin();

        Assert.Empty(second.FindOrphans());
        Assert.True(File.Exists(first.SnapshotPath));

        var orphan = Crashed(first, second);
        Assert.Equal(first.SessionId, orphan.Id);
        Assert.Equal("cat.png", orphan.DocumentName);
        Assert.Equal(Time, orphan.SavedAt);

        // Claimed while offered, so a third instance doesn't offer it too.
        Assert.Empty(Begin().FindOrphans());
    }

    [Fact]
    public void ACleanExit_LeavesNothingBehind()
    {
        var store = Begin();
        Write(store, "work");

        store.EndSession();

        Assert.Empty(Directory.GetFiles(_folder));
        Assert.Empty(Begin().FindOrphans());
    }

    [Fact]
    public void Discard_DeletesTheOrphan_Release_KeepsItForNextTime()
    {
        var crashed = Begin();
        Write(crashed, "work");
        var next = Begin();
        var orphan = Crashed(crashed, next);

        next.Release(orphan);
        var again = Assert.Single(Begin().FindOrphans());
        Assert.Equal(orphan.Id, again.Id);
        _stores[^1].Discard(again);

        Assert.False(File.Exists(orphan.Path));
        Assert.Empty(Begin().FindOrphans());
        Assert.Empty(Directory.GetFiles(_folder, crashed.SessionId + ".*"));
    }

    [Fact]
    public void Adopt_MakesTheOrphanThisSessionsSnapshot()
    {
        var crashed = Begin();
        Write(crashed, "work");
        var next = Begin();
        var orphan = Crashed(crashed, next);

        next.Adopt(orphan);

        Assert.Equal("work", File.ReadAllText(next.SnapshotPath));
        Assert.Empty(Directory.GetFiles(_folder, crashed.SessionId + ".*"));
        next.EndSession();
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void SnapshotsOlderThanAWeek_ArePurgedWithoutBeingOffered()
    {
        var stale = Begin();
        Write(stale, "old");
        var recent = Begin();
        Write(recent, "new");
        stale.Dispose();
        recent.Dispose();
        File.SetLastWriteTimeUtc(stale.SnapshotPath, DateTime.UtcNow - RecoveryStore.StaleAfter - TimeSpan.FromHours(1));
        File.SetLastWriteTimeUtc(recent.SnapshotPath, DateTime.UtcNow - RecoveryStore.StaleAfter + TimeSpan.FromHours(1));

        var orphan = Assert.Single(Begin().FindOrphans());

        Assert.Equal(recent.SessionId, orphan.Id);
        Assert.Empty(Directory.GetFiles(_folder, stale.SessionId + ".*"));
    }

    [Fact]
    public void LeftoversOfACrashWithoutASnapshot_AreCleanedUp_AndOtherFilesAreLeftAlone()
    {
        var crashed = Begin();
        crashed.Dispose(); // crashed before any snapshot: only its lock is left
        File.WriteAllText(Path.Combine(_folder, "readme.txt"), "not ours");

        Assert.Empty(Begin().FindOrphans());

        Assert.Empty(Directory.GetFiles(_folder, crashed.SessionId + ".*"));
        Assert.True(File.Exists(Path.Combine(_folder, "readme.txt")));
    }

    [Fact]
    public void ADamagedSidecar_StillOffersTheSnapshot_WithItsFileTime()
    {
        var crashed = Begin();
        Write(crashed, "work");
        crashed.Dispose();
        File.WriteAllText(Path.ChangeExtension(crashed.SnapshotPath, ".json"), "{ not json");

        var orphan = Assert.Single(Begin().FindOrphans());

        Assert.Null(orphan.DocumentName);
        Assert.Equal(File.GetLastWriteTimeUtc(crashed.SnapshotPath), orphan.SavedAt.UtcDateTime);
    }

    [Fact]
    public void TheSnapshotInterval_DefaultsToTwoMinutes_AndIsClamped()
    {
        Assert.Equal(TimeSpan.FromMinutes(2), RecoverySettings.IntervalOf(null));
        Assert.Equal(TimeSpan.FromMinutes(2), RecoverySettings.IntervalOf(new RecoverySettings()));
        Assert.Equal(TimeSpan.FromSeconds(30), RecoverySettings.IntervalOf(new RecoverySettings { SnapshotIntervalSeconds = 30 }));
        Assert.Equal(TimeSpan.FromSeconds(15), RecoverySettings.IntervalOf(new RecoverySettings { SnapshotIntervalSeconds = 1 }));
        Assert.Equal(TimeSpan.FromHours(1), RecoverySettings.IntervalOf(new RecoverySettings { SnapshotIntervalSeconds = int.MaxValue }));
    }

    [Fact]
    public void TheSnapshotInterval_IsReadFromTheSettingsFile()
    {
        var path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, """{ "recovery": { "snapshotIntervalSeconds": 300 } }""");

        var settings = new SettingsStore(path).Load();

        Assert.Equal(TimeSpan.FromMinutes(5), RecoverySettings.IntervalOf(settings.Recovery));
    }
}
