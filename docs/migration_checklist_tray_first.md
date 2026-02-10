# Tray-First Migration Checklist (Draft)

## Phase A — Core Extraction
1. Create `iCUERedGreen.Core` project (class library, net8.0).
2. Move FRITZ client, polling loop, iCUE control, keyboard hook to Core.
3. Expose a `WorkerController` with `Start/Stop/Restart` methods.
4. Create state model: ON/OFF/UNKNOWN + timestamps + iCUE availability.
5. Add events or callbacks for state updates (for tray UI).

## Phase B — Tray App
1. Create `iCUERedGreen.Tray` (WinForms, WinExe).
2. Add `NotifyIcon` with menu: Toggle, Settings, Restart worker, Open logs, Exit.
3. Host `WorkerController` in tray process.
4. Update tooltip on state change.

## Phase C — Settings + Credential Manager
1. Implement `CredentialStore` (P/Invoke CredRead/CredWrite).
2. Store FRITZ username + password under `iCUERedGreen/FritzPassword`.
3. Keep non-secret settings in `appsettings.json`.
4. Add Settings dialog with Save and Cancel.
5. Save triggers worker restart (stop/dispose/start).

## Phase D — Dev-Only CLI
1. Convert existing console app to `iCUERedGreen.Cli`.
2. Keep CLI for dev/testing only; do not publish.

## Phase E — Autostart + Docs
1. Update Task Scheduler XML to run `iCUERedGreen.Tray.exe`.
2. Update docs: USAGE, BACKGROUND_TASK_SCHEDULER, ARCHITECTURE, MIGRATIONS.
3. Changelog entries for refactor + tray release.

## Phase F — Release
1. Set production version to `2.0.0.0` when tray app is stable.
2. Publish tray app only.
