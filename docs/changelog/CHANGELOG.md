# CHANGELOG

## Unreleased

## 1.1.0.22 — 2026-02-10 22:34:41
[Changed]
- Hide Dev Mode controls unless the tray app is started with `--dev-ui`.

## 1.1.0.21 — 2026-02-10 22:11:10
[Fixed]
- Prevented polling from overwriting optimistic LED updates during toggles.

## 1.1.0.20 — 2026-02-10 21:46:29
[Added]
- Added DEBUG log lines for optimistic LED updates during toggles.

## 1.1.0.19 — 2026-02-10 21:24:06
[Fixed]
- Generated distinct tray status icons at runtime and removed unused embedded icons.

## 1.1.0.18 — 2026-02-10 21:04:44
[Changed]
- Updated update-published to stop/restart the tray scheduled task before copying.

## 1.1.0.17 — 2026-02-10 20:52:22
[Fixed]
- Open Log now opens the log file with the associated app and warns if missing.

## 1.1.0.16 — 2026-02-10 20:40:12
[Fixed]
- Keyboard hook now uses the optimistic toggle path for immediate LED updates.

## 1.1.0.15 — 2026-02-10 20:35:40
[Fixed]
- Track last-known switch state on every poll to enable immediate LED updates.

## 1.1.0.14 — 2026-02-10 20:23:58
[Fixed]
- Apply optimistic LED updates on keypress to reduce perceived lag.

## 1.1.0.13 — 2026-02-10 20:18:32
[Added]
- Logged tray startup version line for diagnostics.

## 1.1.0.12 — 2026-02-10 20:12:21
[Fixed]
- Debounced Scroll Lock key toggles to avoid queued delays.

## 1.1.0.11 — 2026-02-10 19:59:48
[Changed]
- Use warning/error icons for configuration and worker failures.

## 1.1.0.10 — 2026-02-10 19:55:52
[Changed]
- Ignored TEMP folder and removed temporary problem log from tracking.

## 1.1.0.9 — 2026-02-10 19:51:32
[Fixed]
- Prefilled FRITZ host and AIN from env vars when Dev Mode is enabled.

## 1.1.0.8 — 2026-02-10 19:38:13
[Fixed]
- Improved settings layout spacing and checkbox alignment.
- Ensured validation indicators remain visible beside all input fields.

## 1.1.0.7 — 2026-02-10 19:07:32
[Fixed]
- Embedded tray icons to avoid missing runtime icon files.

## 1.1.0.6 — 2026-02-10 18:12:37
[Changed]
- Explicitly discard the worker-fault continuation task to silence CS4014.

## 1.1.0.5 — 2026-02-10 18:08:33
[Changed]
- Added XML documentation for tray validation and icon helpers.
- Ignored local iCUE SDK DLLs under the CLI asset path.

## 1.1.0.4 — 2026-02-10 17:58:28
[Added]
- Added tray icon variants and live state icon updates for ON/OFF/UNKNOWN.
- Added inline settings validation for required fields.

[Changed]
- Renamed the dev-only console host to `iCUERedGreen.Cli` (including exe name).
- Updated Credential Manager store to support test-specific targets.

[Docs]
- Updated usage and release workflow docs for tray + CLI naming.

[Tests]
- Added tray settings and Credential Manager tests (Windows-only).

## 1.1.0.3 — 2026-02-10 17:37:50
[Changed]
- Updated Task Scheduler XML to launch the tray app directly.

[Docs]
- Updated Task Scheduler background instructions for the tray app.
- Clarified hidden startup guidance for the tray app.

## 1.1.0.2 — 2026-02-10 16:29:40
[Added]
- Added a WinForms tray app project with menu controls and a settings dialog.
- Added Windows Credential Manager support for FRITZ credentials in the tray app.
- Added tray app settings file with dev mode and toggle-on-keypress options.

[Changed]
- Exposed worker toggle functionality for tray control.

## 1.1.0.1 — 2026-02-10 15:40:33
[Changed]
- Target framework upgraded to net10.0.
- Added global.json to pin the .NET 10 SDK.
- Updated migration checklist for net10.0.

## 1.1.0.0 — 2026-02-10 13:40:28
[Changed]
- Set production release version to 1.1.0.0.

## 1.0.2.6 — 2026-02-10 13:32:33
[Fixed]
- Reset iCUE SDK state on failures to allow reconnect after iCUE restarts.

## 1.0.2.5 — 2026-02-10 13:27:32
[Fixed]
- Restore iCUE recovery by using CorsairGetSessionDetails health checks.

## 1.0.2.4 — 2026-02-10 13:22:58
[Fixed]
- Reconnect to iCUE after restarts by validating session health each poll.

## 1.0.2.3 — 2026-02-10 13:09:13
[Changed]
- Enabled `--toggle-on-keypress` in the Task Scheduler XML templates.

## 1.0.2.2 — 2026-02-10 13:00:25
[Added]
- Added `--toggle-on-keypress` to toggle the switch on Scroll Lock keypress.

[Changed]
- Continue running without iCUE and enable LED control when iCUE becomes available.

[Docs]
- Documented toggle-on-keypress usage and iCUE availability behavior.

## 1.0.2.1 — 2026-02-10 09:44:27
[Fixed]
- Remove stale running.txt markers from previous boots before starting.

[Docs]
- Documented stale running.txt cleanup behavior.

## 1.0.2.0 — 2026-02-06 17:03:14
[Changed]
- Removed iCUE SDK files from the repository; use a local SDK download.
- Made SDK DLL inclusion an explicit opt-in (`IncludeCueSdk=true`).
- Suppressed repeated iCUE release-control errors; log once per change and on recovery.

[Docs]
- Converted documentation file references to clickable links.
- Removed README link to docs/diagrams (diagram folder removed).
- Added full NLog BSD-3 license text file and linked it.
- Documented iCUE SDK licensing terms and download requirement.

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
