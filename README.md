# DipsyPaint

DipsyPaint is a free, open-source equivalent of Windows 11 Paint for Windows, macOS and Linux.
It is built with .NET 10, [Avalonia](https://avaloniaui.net/), SkiaSharp and ImageSharp, and is
MIT-licensed.

## Download and install

Download the file for your system from the
[latest release](https://github.com/dipsylala/DipsyPaint/releases/latest):

| System | Installer | Portable (no install) |
| --- | --- | --- |
| Windows, Intel/AMD | `DipsyPaint-win-x64-Setup.exe` | `DipsyPaint-win-x64-Portable.zip` |
| Windows on Arm | `DipsyPaint-win-arm64-Setup.exe` | `DipsyPaint-win-arm64-Portable.zip` |
| macOS, Apple silicon | `DipsyPaint-osx-arm64-Setup.pkg` | `DipsyPaint-osx-arm64-Portable.zip` |
| macOS, Intel | `DipsyPaint-osx-x64-Setup.pkg` | `DipsyPaint-osx-x64-Portable.zip` |
| Linux, x64 | | `DipsyPaint-linux-x64.AppImage` |
| Linux, arm64 | | `DipsyPaint-linux-arm64.AppImage` |

On Linux, make the AppImage executable (`chmod +x DipsyPaint-linux-x64.AppImage`) and run it. It
needs glibc 2.28 or newer. If your system has no FUSE, run it with `--appimage-extract-and-run`.

DipsyPaint updates itself. When it starts, it checks GitHub Releases for a newer version and
downloads it in the background; **Restart to update** then appears in the toolbar. If you ignore
it, the update installs the next time you start DipsyPaint. This check is the only network
request DipsyPaint makes on its own: it asks GitHub for the list of releases, and sends nothing
about you or how you use DipsyPaint. There is no telemetry.

### Running an unsigned app

DipsyPaint's releases are not code-signed yet, so Windows and macOS warn you the first time.

**Windows (SmartScreen).** If Windows shows "Windows protected your PC", click **More info**, then
**Run anyway**. Your browser may also warn that the file isn't commonly downloaded; choose to keep
it.

**macOS (Gatekeeper).** macOS says it can't verify that DipsyPaint is free of malware. Click
**Done** (not *Move to Bin*), then:

1. Open **System Settings → Privacy & Security**.
2. Scroll to **Security**, where it says DipsyPaint was blocked, and click **Open Anyway**.
3. Enter your password, then click **Open Anyway** again.

Do this for the `.pkg` installer, or for `DipsyPaint.app` if you use the portable zip. You only
need to do it once.

Alternatively, remove the quarantine flag in Terminal, then open the file as usual:

```sh
xattr -dr com.apple.quarantine ~/Downloads/DipsyPaint-osx-arm64-Setup.pkg   # or the .app
```

## Build from source

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet run --project src/DipsyPaint   # run the app
dotnet test                           # run the tests
```

Debug builds work without any setup. **Release** builds need a Six Labors licence key for
ImageSharp ([ADR-0020](docs/decisions/0020-imagesharp-licence-key.md)): set the
`SixLaborsLicenseKey` environment variable, or put your `sixlabors.lic` at the repository root
(it is git-ignored; never commit it). Contributors can apply for their own key at
<https://licensing.sixlabors.com/>.

Releases are published with NativeAOT (`PublishAot` is set for Release). That needs the platform
C/C++ toolchain: the Visual Studio *Desktop development with C++* workload on Windows, Xcode on
macOS, and clang and zlib on Linux.

```sh
dotnet publish src/DipsyPaint/DipsyPaint.csproj -c Release -r <rid> -o publish
```

See [docs/releasing.md](docs/releasing.md) for how releases are cut.

## Contributing

`CLAUDE.md` describes the ticket workflow, and [docs/decisions](docs/decisions/README.md) records
the design decisions. To report a problem, use **Help → Report a problem…** in the app, or open
an [issue](https://github.com/dipsylala/DipsyPaint/issues).

## Licence

DipsyPaint is released under the [MIT licence](LICENSE). It includes third-party components under
their own licences, including ImageSharp under the Six Labors Split License; see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
