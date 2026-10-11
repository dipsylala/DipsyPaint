using System.Text.Json;
using System.Text.Json.Serialization;

namespace DipsyPaint.Recovery;

/// <summary>What a recovery snapshot's sidecar file records, to offer it after a crash.</summary>
/// <param name="DocumentName">The document's file name (not its path), or null if it was untitled.</param>
/// <param name="SavedAt">When the snapshot was taken.</param>
public sealed record RecoveryInfo(string? DocumentName, DateTimeOffset SavedAt);

/// <summary>A snapshot left behind by a DipsyPaint session that did not exit cleanly.</summary>
/// <param name="Id">The session that wrote it.</param>
/// <param name="Path">The <c>.ora</c> file.</param>
/// <param name="DocumentName">The document's file name, or null if it was untitled or is unknown.</param>
/// <param name="SavedAt">When it was taken.</param>
public sealed record RecoverySnapshot(string Id, string Path, string? DocumentName, DateTimeOffset SavedAt);

/// <summary>
/// The per-user recovery folder (ADR-0013). Each session (one running DipsyPaint) has a random id and
/// holds <c>&lt;id&gt;.lock</c> open exclusively while it runs; its latest snapshot is <c>&lt;id&gt;.ora</c>
/// with a <c>&lt;id&gt;.json</c> sidecar (<see cref="RecoveryInfo"/>). A clean exit deletes all three. A
/// snapshot whose lock nobody holds was left by a crash; one whose lock is held belongs to another
/// running DipsyPaint and is never touched. Snapshots are written to a temporary file and then renamed,
/// so a crash mid-write never leaves a damaged one. Only this folder is ever written, never the user's
/// own files, and nothing in it is ever sent anywhere (ADR-0017).
/// </summary>
public sealed class RecoveryStore : IDisposable
{
    /// <summary>Snapshots left by a crash longer ago than this are deleted without being offered.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(7);

    private const string SnapshotExtension = ".ora";
    private const string InfoExtension = ".json";
    private const string LockExtension = ".lock";
    private const string TempExtension = ".tmp";

    /// <summary>Larger sidecars are not one DipsyPaint wrote, so they are ignored.</summary>
    private const long MaxInfoBytes = 64 * 1024;

    private readonly Lock _lock = new();
    private readonly TimeProvider _time;

    // The locks of crashed sessions whose snapshots are being offered, so no other instance offers them too.
    private readonly Dictionary<string, FileStream> _claimed = [];
    private FileStream? _sessionLock;

    // Write sequence numbers: the latest handed out, and the one whose result is on disk (or deleted).
    private long _issued;
    private long _committed;

    public RecoveryStore(string directory, TimeProvider? time = null)
    {
        Directory = directory;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// The per-user recovery folder: <c>DipsyPaint/Recovery</c> under local app data, beside the crash
    /// logs and settings.
    /// </summary>
    public static string DefaultDirectory { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify),
        "DipsyPaint",
        "Recovery");

    public string Directory { get; }

    /// <summary>This session's id.</summary>
    public string SessionId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>Where this session's snapshot is written.</summary>
    public string SnapshotPath => PathOf(SessionId, SnapshotExtension);

    /// <summary>Creates the folder and takes this session's lock. Throws if the folder can't be written.</summary>
    public void BeginSession()
    {
        System.IO.Directory.CreateDirectory(Directory);
        _sessionLock = Claim(SessionId) ?? throw new IOException("The recovery session lock could not be taken.");
    }

    /// <summary>A sequence number for a snapshot about to be taken; a later one supersedes it.</summary>
    public long Reserve() => Interlocked.Increment(ref _issued);

    /// <summary>
    /// Writes this session's snapshot with <paramref name="write"/> (any thread), unless something
    /// reserved after <paramref name="sequence"/> has already been written or deleted, or the session
    /// has ended. Returns whether it was written.
    /// </summary>
    public bool Write(long sequence, Action<Stream> write, RecoveryInfo info)
    {
        var temp = PathOf(SessionId, $".{sequence}{SnapshotExtension}{TempExtension}");
        var infoTemp = PathOf(SessionId, $".{sequence}{InfoExtension}{TempExtension}");
        try
        {
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            File.WriteAllBytes(infoTemp, JsonSerializer.SerializeToUtf8Bytes(info, RecoveryJsonContext.Default.RecoveryInfo));
            lock (_lock)
            {
                if (_sessionLock is null || sequence <= _committed)
                {
                    return false;
                }

                File.Move(temp, SnapshotPath, overwrite: true);
                File.Move(infoTemp, PathOf(SessionId, InfoExtension), overwrite: true);
                _committed = sequence;
                return true;
            }
        }
        finally
        {
            TryDelete(temp);
            TryDelete(infoTemp);
        }
    }

    /// <summary>Deletes this session's snapshot, and drops any write reserved before now.</summary>
    public void Delete()
    {
        lock (_lock)
        {
            _committed = Reserve();
            DeleteFiles(SessionId);
        }
    }

    /// <summary>
    /// Snapshots left by sessions that did not exit cleanly, newest first. Each is claimed (its lock
    /// taken) until it is <see cref="Discard">discarded</see>, <see cref="Adopt">adopted</see> or
    /// <see cref="Release">released</see>. Snapshots older than <see cref="StaleAfter"/>, and leftovers
    /// of crashed sessions without a snapshot, are deleted on the way.
    /// </summary>
    public IReadOnlyList<RecoverySnapshot> FindOrphans()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }

        var found = new List<RecoverySnapshot>();
        var ids = System.IO.Directory.EnumerateFiles(Directory)
            .Select(f => IdOf(System.IO.Path.GetFileName(f)))
            .OfType<string>()
            .Distinct()
            .Where(id => id != SessionId)
            .ToList();
        foreach (var id in ids)
        {
            if (!_claimed.ContainsKey(id))
            {
                if (Claim(id) is not { } claim)
                {
                    continue; // another running DipsyPaint's
                }

                _claimed[id] = claim;
            }

            var path = PathOf(id, SnapshotExtension);
            DeleteTemporaryFiles(id);
            var written = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : (DateTime?)null;
            if (written is not { } time || _time.GetUtcNow() - new DateTimeOffset(time, TimeSpan.Zero) > StaleAfter)
            {
                Discard(id);
                continue;
            }

            var info = ReadInfo(id);
            found.Add(new RecoverySnapshot(id, path, info?.DocumentName, info?.SavedAt ?? new DateTimeOffset(time, TimeSpan.Zero)));
        }

        return [.. found.OrderByDescending(s => s.SavedAt)];
    }

    /// <summary>Opens a crashed session's snapshot for reading.</summary>
    public static Stream OpenRead(RecoverySnapshot snapshot) => File.OpenRead(snapshot.Path);

    /// <summary>Deletes a crashed session's snapshot and leftovers.</summary>
    public void Discard(RecoverySnapshot snapshot) => Discard(snapshot.Id);

    /// <summary>Leaves a crashed session's snapshot to be offered again next time.</summary>
    public void Release(RecoverySnapshot snapshot)
    {
        if (_claimed.Remove(snapshot.Id, out var claim))
        {
            claim.Dispose();
        }
    }

    /// <summary>
    /// Makes a crashed session's snapshot this session's (it was restored), replacing this session's,
    /// so it stays recoverable until the restored document is saved or discarded.
    /// </summary>
    public void Adopt(RecoverySnapshot snapshot)
    {
        lock (_lock)
        {
            _committed = Reserve();
            DeleteFiles(SessionId);
            File.Move(snapshot.Path, SnapshotPath);
            var info = PathOf(snapshot.Id, InfoExtension);
            if (File.Exists(info))
            {
                File.Move(info, PathOf(SessionId, InfoExtension));
            }
        }

        Discard(snapshot.Id);
    }

    /// <summary>A clean exit: deletes this session's snapshot and lock and releases claimed snapshots.</summary>
    public void EndSession()
    {
        lock (_lock)
        {
            DeleteFiles(SessionId);
            if (_sessionLock is not null)
            {
                _sessionLock.Dispose();
                _sessionLock = null;
                TryDelete(PathOf(SessionId, LockExtension));
            }
        }

        Dispose();
    }

    /// <summary>
    /// Releases every lock but leaves the files, as the operating system does when the process dies;
    /// the session's snapshot is then offered by the next launch.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _sessionLock?.Dispose();
            _sessionLock = null;
        }

        foreach (var claim in _claimed.Values)
        {
            claim.Dispose();
        }

        _claimed.Clear();
    }

    private void Discard(string id)
    {
        DeleteFiles(id);
        DeleteTemporaryFiles(id);
        if (_claimed.Remove(id, out var claim))
        {
            claim.Dispose();
        }

        TryDelete(PathOf(id, LockExtension));
    }

    // Takes a session's lock: null if another process (or this one) holds it.
    private FileStream? Claim(string id)
    {
        try
        {
            return new FileStream(PathOf(id, LockExtension), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private RecoveryInfo? ReadInfo(string id)
    {
        try
        {
            var file = new FileInfo(PathOf(id, InfoExtension));
            if (!file.Exists || file.Length > MaxInfoBytes)
            {
                return null;
            }

            using var stream = file.OpenRead();
            return JsonSerializer.Deserialize(stream, RecoveryJsonContext.Default.RecoveryInfo);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private void DeleteFiles(string id)
    {
        TryDelete(PathOf(id, SnapshotExtension));
        TryDelete(PathOf(id, InfoExtension));
    }

    private void DeleteTemporaryFiles(string id)
    {
        foreach (var temp in System.IO.Directory.EnumerateFiles(Directory, id + ".*" + TempExtension))
        {
            TryDelete(temp);
        }
    }

    private string PathOf(string id, string suffix) => System.IO.Path.Combine(Directory, id + suffix);

    // The session id a recovery file name starts with, or null for any other file.
    private static string? IdOf(string fileName)
    {
        var dot = fileName.IndexOf('.', StringComparison.Ordinal);
        var id = dot < 0 ? fileName : fileName[..dot];
        return id.Length == 32 && id.All(char.IsAsciiHexDigitLower) ? id : null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left for the next clean-up.
        }
    }
}

/// <summary>Source-generated JSON metadata for the sidecar, so it needs no reflection (NativeAOT, ADR-0018).</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RecoveryInfo))]
internal sealed partial class RecoveryJsonContext : JsonSerializerContext;
