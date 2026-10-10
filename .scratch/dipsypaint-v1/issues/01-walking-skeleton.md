# 01: Walking skeleton: blank document in the shell

**What to build:** Launching DipsyPaint opens a window showing a blank white *document* in reference Paint's layout: command bar across the top, canvas in the centre, status bar at the bottom. The app follows the OS light/dark setting using the Avalonia Fluent theme and carries the DipsyPaint name and its own app icon. The *engine* (a UI-free .NET library) owns the document. The *shell* (Avalonia app) only displays it. CI builds and tests on Windows, macOS and Linux. Spec: Implementation Decisions (two-part architecture), ADR-0004, ADR-0012, ADR-0016.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The app launches and shows a blank document on Windows, macOS and Linux
- [ ] The engine library has no Avalonia dependency
- [ ] Light and dark mode follow the OS setting
- [ ] The window title and app icon say DipsyPaint; no "Microsoft"/"MS Paint" text or Microsoft icons anywhere
- [ ] No print command exists in the UI
- [ ] All UI strings come from `.resx` resources (English)
- [ ] Every command present is reachable by keyboard and carries an automation name
- [ ] An engine unit test and an Avalonia.Headless smoke test (window loads, document visible) pass
- [ ] A GitHub Actions matrix (Windows, macOS, Linux) runs build and tests on every PR
