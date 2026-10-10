# 13: Local crash log + "Report a problem"

**What to build:** When DipsyPaint hits an unhandled exception it writes a local crash log to app data. A "Report a problem" command opens a pre-filled GitHub issue in the browser for the user to review and submit. Nothing is sent automatically. Spec: AC28 to AC30, ADR-0017.

**Blocked by:** 01 (Walking skeleton)

**Status:** ready-for-agent

- [ ] An unhandled exception writes a crash log containing the version, the OS and the stack trace, and no image data
- [ ] File paths and usernames are stripped from the log where practical
- [ ] "Report a problem" opens a pre-filled GitHub issue in the browser and submits nothing itself
- [ ] The app makes no network requests (verified by test or inspection)
