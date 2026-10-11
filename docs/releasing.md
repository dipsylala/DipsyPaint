# Releasing

DipsyPaint ships unsigned through GitHub Releases, packaged by [Velopack](https://velopack.io)
(ADR-0015). `.github/workflows/release.yml` builds every package. Installed copies update
themselves from the latest published release.

## One-time setup

Do these before the first release. ADR-0015 relies on them: until signing is added, the security
of updates depends on the GitHub account and on who can release.

1. **Licence key.** Add the repository secret `SIXLABORS_LICENSE_KEY` (Settings → Secrets and
   variables → Actions) containing the full `sixlabors.lic`. Release builds need it (ADR-0020).
   Without it, a tag push fails with "Six Labors licence key missing".
2. **Two-factor authentication** on every account that can push tags or publish releases
   (GitHub → Settings → Password and authentication). Required, not optional: anyone who can
   publish a release can push an update to every installed copy.
3. **Restrict who can release.**
   - Keep the repository's write access to the maintainer. Anyone with write access can push
     tags and edit releases.
   - Add a tag ruleset (Settings → Rules → Rulesets → New tag ruleset): target `v*`, enable
     *Restrict creations*, *Restrict updates* and *Restrict deletions*, and add only the
     maintainer (or the admin role) to the bypass list.
   - The upload job runs in the `release` environment. It requires the maintainer's approval and
     accepts only tags matching `v*` (Settings → Environments → `release`). An environment that
     doesn't exist yet is created on first use **without** these rules, so set them up before the
     first tag on a new repository.
   - Settings → Actions → General → Workflow permissions: keep *Read repository contents*. Only
     the upload job asks for write access.

## Cutting a release

1. Make sure `main` is green.
2. Tag the commit on `main` and push the tag. The tag sets the version; nothing in the repository
   needs bumping.

   ```sh
   git switch main && git pull
   git tag -a v0.1.0 -m "DipsyPaint 0.1.0"
   git push origin v0.1.0
   ```

   A tag with a pre-release suffix (`v0.2.0-beta.1`) becomes a GitHub pre-release. Installed
   copies only take stable releases.
3. The **Release** workflow:
   - publishes the app with NativeAOT for `win-x64`, `win-arm64`, `osx-arm64`, `osx-x64`,
     `linux-x64` and `linux-arm64`, each on a native runner (ADR-0018). Linux builds in
     AlmaLinux 8, so the app needs glibc 2.28 or newer;
   - packs each one with `vpk`, using the RID as the Velopack channel. If a stable release
     already exists, it also builds a delta from it;
   - launches each portable package with the startup probe (opens the window, round-trips PNG
     and JPEG) and lists package sizes in the run summary;
   - after approval in the `release` environment, uploads every RID's packages to one **draft**
     release named "DipsyPaint 0.1.0".
4. Check the draft: install it on at least one machine per OS, and write the release notes.
5. Publish the draft. Installed copies download the update the next time they start.

If a run fails, fix the cause on `main`, delete the tag (`git push origin :v0.1.0`; also delete
the draft if one was created), then tag again.

## Files in a release

Per RID (`<rid>` is the channel):

| File | What it is |
| --- | --- |
| `DipsyPaint-<rid>-Setup.exe` | Windows installer |
| `DipsyPaint-<rid>-Setup.pkg` | macOS installer |
| `DipsyPaint-<rid>.AppImage` | Linux app (no separate portable package needed) |
| `DipsyPaint-<rid>-Portable.zip` | Windows and macOS portable copy; it updates itself too |
| `DipsyPaint-<version>-<rid>-full.nupkg` | Full update package |
| `DipsyPaint-<version>-<rid>-delta.nupkg` | Delta from the previous release, when there is one |
| `releases.<rid>.json` | The update feed installed copies read |

Do not delete or rename these after publishing; installed copies look them up by name.

## Checking the pipeline without a release

Actions → Release → Run workflow, with **validate** ticked. This builds Debug with optimisations,
which needs no licence key, and runs every step except the upload. Its artifacts are named
`validation-only-…` and are **not for distribution**: they are Debug builds made without the
licence key.

Running the workflow without *validate* builds Release (needs the key) and keeps the packages as
workflow artifacts, without releasing them.

## The update check

On startup, an installed copy asks the GitHub API for the repository's latest releases and
downloads the newest package for its channel in the background. This is DipsyPaint's only
automatic network request (ADR-0017); it sends no token, ID or usage data. When a download
finishes, a **Restart to update** button appears. Without it, the update installs at the next
start. A copy run from a build or publish folder, or with `DIPSYPAINT_STARTUP_PROBE` set, makes
no request. Failures (offline, rate-limited) are silent and retried at the next start.

Code: `src/DipsyPaint/Updates/`; `VelopackApp.Build().Run()` in `Program.Main`.
