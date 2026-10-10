using Velopack;
using Velopack.Sources;

namespace DipsyPaint.Updates;

/// <summary>
/// Updates from the project's GitHub Releases through Velopack (ADR-0015). This is DipsyPaint's only
/// automatic network request (ADR-0017): it asks the public GitHub API for the latest releases and
/// downloads the release index and package for the channel the app was packaged with (one per RID).
/// No token, user ID or usage data is sent.
/// </summary>
public sealed class VelopackUpdater : IUpdater
{
    public const string RepositoryUrl = "https://github.com/dipsylala/DipsyPaint";

    private readonly UpdateManager _manager;
    private VelopackAsset? _downloaded;

    private VelopackUpdater(UpdateManager manager) => _manager = manager;

    /// <summary>
    /// The updater for a copy installed by Velopack (installer or portable package), or null when the
    /// app runs from a build or publish folder, where there is nothing to update.
    /// </summary>
    public static VelopackUpdater? CreateIfInstalled()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
            return manager.IsInstalled ? new VelopackUpdater(manager) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DownloadUpdateAsync()
    {
        var update = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
        if (update is null)
        {
            return false;
        }

        await _manager.DownloadUpdatesAsync(update).ConfigureAwait(false);
        _downloaded = update.TargetFullRelease;
        return true;
    }

    public void ApplyAfterExitAndRestart()
    {
        if (_downloaded is not null)
        {
            _manager.WaitExitThenApplyUpdates(_downloaded, silent: true, restart: true);
        }
    }
}
