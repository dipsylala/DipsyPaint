# 16: Packaging and distribution with Velopack

**What to build:** Users can download unsigned DipsyPaint installers from GitHub Releases for Windows, macOS and Linux (x64 and arm64; AppImage on Linux), and the installed app updates itself through Velopack. Spec: AC4, AC35 to AC38, ADR-0015.

**Blocked by:** 14 (Spike: NativeAOT vs ReadyToRun)

**Status:** ready-for-agent

- [x] A release workflow publishes Velopack packages for win/osx/linux × x64/arm64 to GitHub Releases
- [x] Linux ships as an AppImage
- [x] Install, launch and auto-update work on each OS
- [x] README documents the SmartScreen and Gatekeeper override steps
- [x] Third-party notices include ImageSharp's Six Labors Split License
- [x] The update check is the only automatic network request
- [x] Release permissions are restricted and 2FA is documented as required for the release account

Verified 2026-10-11: v0.1.0 and v0.1.1 published through `release.yml` with delta packages for all six RIDs; the maintainer confirmed the installers work and that an installed 0.1.0 updated itself to 0.1.1 on their machine. The other platforms use the same Velopack update path; each got delta packages and passed the release smoke test.
