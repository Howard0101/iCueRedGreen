# CHANGELOG

## Unreleased

## 1.0.1.0 — 2026-02-06 14:57:07
[Changed]
- Log switch state errors only when the error changes, and log a recovery once.
- Treat "inval" responses as DECT200 unreachable or invalid AIN.
- Replaced internal company references with personal author name.
- Task Scheduler XML now uses placeholder user/path values for public sharing.
- Renamed AI workflow diagram artifacts to `ai_codex_workflow.*`.
- Made AI workflow diagram optional for documentation checks.

[Docs]
- Added third-party notices.
- Documented path placeholders for install and publish directories.
- Removed the AI workflow diagram from the repository.

## 1.0.0.0 — 2026-02-06 13:34:39
[Added]
- Added custom application icon (keyboard + dot) for the executable.
- Added alternative icon options (toggle + LED, plug + LED).
- Added file-based logging for background runs.
- Added size-based log rotation (2 MB) with a single backup file.
- Added the iCUESDK folder to the repository for reference.
- Added a heartbeat-based running.txt stop mechanism.
- Added an update script for published deployments.
- Added a hidden launcher script for Task Scheduler.
- Added `--version` CLI option and versioned startup log line.

[Changed]
- Inverted LED mapping: ON is red, OFF is green.
- On unknown/error state, release iCUE lighting control to restore default lighting.
- Logged switch state changes only (instead of every poll).
- Ignored published log output folder under artifacts.
- Marked Task Scheduler XML as binary to preserve UTF-16 encoding.
- Switched to shared lighting control to avoid overriding non-target keys.
- Task Scheduler instructions now use the hidden launcher.
- Removed trailing period from versioned startup log line.
- Task Scheduler XML saved as UTF-16 for schtasks compatibility.

[Docs]
- Added Task Scheduler background run guide.
- Added future tray app note.
- Documented the log file location for background runs.
- Added Task Scheduler import XML file.
- Noted UTF-16 requirement for Task Scheduler XML imports.
- Added guidance for SYSTEM environment variable changes.
