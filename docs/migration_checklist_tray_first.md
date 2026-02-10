# Tray-First Migration Checklist (Draft)

## Phase A — Core Extraction (Complete)
1. [x] Create `iCUERedGreen.Core` project (class library, net10.0).
2. [x] Move FRITZ client, polling loop, iCUE control, keyboard hook to Core.
3. [x] Expose a `WorkerController` with `Start/Stop/Restart` methods.
4. [x] Create state model: ON/OFF/UNKNOWN + timestamps + iCUE availability.
5. [x] Add events or callbacks for state updates (for tray UI).

## Phase B — Tray App (Complete)
1. [x] Create `iCUERedGreen.Tray` (WinForms, WinExe).
2. [x] Add `NotifyIcon` with menu: Toggle, Settings, Restart worker, Open logs, Exit.
3. [x] Host `WorkerController` in tray process.
4. [x] Update tooltip on state change.

## Phase C — Settings + Credential Manager (Complete)
1. [x] Implement `CredentialStore` (P/Invoke CredRead/CredWrite).
2. [x] Store FRITZ username + password under `iCUERedGreen/FritzPassword`.
3. [x] Keep non-secret settings in `appsettings.json`.
4. [x] Add Settings dialog with Save and Cancel.
5. [x] Save triggers worker restart (stop/dispose/start).

## Phase D — Dev-Only CLI (Complete)
1. [x] Convert existing console app to `iCUERedGreen.Cli`.
2. [x] Keep CLI for dev/testing only; do not publish.

## Phase E — Autostart + Docs (Complete)
1. [x] Update Task Scheduler XML to run `iCUERedGreen.Tray.exe`.
2. [x] Update docs: USAGE, BACKGROUND_TASK_SCHEDULER, ARCHITECTURE, MIGRATIONS.
3. [x] Changelog entries for refactor + tray release.

## Phase F — Release (Pending)
1. [ ] Set production version to `2.0.0.0` when tray app is stable.
2. [ ] Publish tray app only.
