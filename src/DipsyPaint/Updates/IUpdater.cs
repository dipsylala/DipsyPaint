namespace DipsyPaint.Updates;

/// <summary>Finds, downloads and applies a newer release. <see cref="VelopackUpdater"/> is the real one; tests fake it.</summary>
public interface IUpdater
{
    /// <summary>Checks for a newer release and downloads it; true once one is ready to apply.</summary>
    Task<bool> DownloadUpdateAsync();

    /// <summary>Applies the downloaded release once this process has exited, then starts the new version.</summary>
    void ApplyAfterExitAndRestart();
}
