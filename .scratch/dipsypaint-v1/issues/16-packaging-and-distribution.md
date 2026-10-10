# 16: Packaging and distribution with Velopack

**What to build:** Users can download unsigned DipsyPaint installers from GitHub Releases for Windows, macOS and Linux (x64 and arm64; AppImage on Linux), and the installed app updates itself through Velopack. Spec: AC4, AC35 to AC38, ADR-0015.

**Blocked by:** 14 (Spike: NativeAOT vs ReadyToRun)

**Status:** ready-for-agent

- [ ] A release workflow publishes Velopack packages for win/osx/linux × x64/arm64 to GitHub Releases
- [x] Linux ships as an AppImage
- [ ] Install, launch and auto-update work on each OS
- [x] README documents the SmartScreen and Gatekeeper override steps
- [x] Third-party notices include ImageSharp's Six Labors Split License
- [x] The update check is the only automatic network request
- [ ] Release permissions are restricted and 2FA is documented as required for the release account
