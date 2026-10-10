# ADR-0017: No telemetry; local crash logs and user-submitted reports

- **Status:** Accepted
- **Date:** 2026-10-10
- **Depends on:** [ADR-0007](0007-licence-mit.md), [ADR-0013](0013-crash-recovery-snapshots.md)

## Context

Options: none (local only); opt-in crash reporting (e.g. Sentry); opt-in crash
plus usage analytics. Third-party reporting needs a privacy policy and consent
UI; open-source image-editor users expect strong privacy.

## Decision

- **Nothing is sent automatically. No analytics, no crash upload.**
- Unhandled exceptions write a **local crash log** (version, OS, stack trace;
  no image data) to app data, alongside the emergency recovery snapshot
  (ADR-0013).
- A **"Report a problem"** action opens a **pre-filled GitHub issue** in the
  browser; the user reviews and submits it themselves.

## Consequences

- No visibility into crash rates or tool usage; prioritisation relies on
  issues and feedback.
- No privacy policy or consent flow needed for telemetry.
- Crash logs must avoid file paths/usernames where practical, since users may
  paste them publicly.
