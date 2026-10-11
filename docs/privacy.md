# Privacy

DipsyPaint has no telemetry, analytics or crash upload ([ADR-0017](decisions/0017-no-telemetry.md)).
This page lists everything it sends over the network and everything it stores on your computer.

## Network

DipsyPaint makes one network request on its own: the update check. When an installed copy starts,
it asks the public GitHub API for DipsyPaint's releases. If there is a newer one, it downloads
it from GitHub Releases. No account, token, user ID or usage data is sent. Copies run without
installing never check.

**Report a problem** (Help menu) opens a pre-filled GitHub issue in your browser. Nothing is sent
until you submit it yourself. The issue contains DipsyPaint's version, your OS and .NET runtime
version and, if there is one, the latest crash log. Read it before you submit: it becomes public.

## Files on your computer

DipsyPaint keeps its files in a `DipsyPaint` folder in your local app data:

| OS | Folder |
| --- | --- |
| Windows | `%LOCALAPPDATA%\DipsyPaint` |
| macOS | `~/Library/Application Support/DipsyPaint` |
| Linux | `$XDG_DATA_HOME/DipsyPaint` (default `~/.local/share/DipsyPaint`) |

| File | What it holds | When it is removed |
| --- | --- | --- |
| `settings.json` | Your command-bar layout and the recovery snapshot interval | Never; delete it to reset |
| `CrashLogs/` | One plain-text log per crash: DipsyPaint's version, the OS and the error's stack trace. No image data. Your home folder and user name are replaced with placeholders where they appear. | Only the newest 10 are kept |
| `Recovery/` | Recovery snapshots of your unsaved work (see below) | See below |

### Recovery snapshots contain your image

While a picture has unsaved changes, DipsyPaint saves a copy of it to `Recovery/` every 2 minutes
([ADR-0013](decisions/0013-crash-recovery-snapshots.md)). If DipsyPaint crashes, it also tries to
save one last copy. The next time it starts, it offers to restore the copy.

- **What's stored:** each snapshot is a full OpenRaster (`.ora`) copy of the picture, every layer
  included. Beside it is a small `.json` file with the picture's file name (not its folder) and
  the time it was saved.
- **Who can read them:** they are ordinary files in your user folder, unencrypted. Anyone who can
  read your user folder can read them.
- **When they're deleted:** when you save, discard your changes, choose **Discard** when
  DipsyPaint offers a restore, or close DipsyPaint normally. Any snapshot older than 7 days is
  deleted when DipsyPaint next starts.
- **To remove them yourself:** with DipsyPaint closed, delete the `Recovery` folder.
- **Interval:** set `recovery.snapshotIntervalSeconds` in `settings.json` (15 to 3600 seconds).
  Snapshots can't currently be turned off.

## Clipboard

DipsyPaint reads the clipboard only when you paste, and writes to it only when you copy or cut.
