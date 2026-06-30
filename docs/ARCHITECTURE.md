# Architecture

This document defines the target architecture for the tray-first application model.

## Scope
- Replace production usage with a single WinForms tray application.
- Keep a dev-only CLI host for diagnostics.
- Move shared logic into a core library.
- Store the FRITZ password in Windows Credential Manager (no plaintext in files).
- Apply settings immediately by restarting the worker.

## Non-Goals
- No Windows service (iCUE requires the interactive user session).
- No IPC layer in production (single process).

## Project Structure
1. `iCUERedGreen.Core` (class library)
2. `iCUERedGreen.Tray` (WinForms tray app, primary host)
3. `iCUERedGreen.Cli` (dev-only CLI host)

## Core Responsibilities
- Poll FRITZ!DECT state and toggle on demand.
- Control iCUE LED when available; run without iCUE when not.
- Keyboard hook for Scroll Lock toggle (configurable).
- State model for ON/OFF/UNKNOWN with timestamps.
- Shared iCUE lighting session for both Scroll Lock and Volume Mute LEDs.
- Windows global audio mute integration for the Sound Off feature.

## Tray Responsibilities
- Start and stop the core worker in-process.
- Show status via tray icon + tooltip.
- Provide a settings dialog with Save + Restart.
- Provide menu actions: DECT Power, Sound Off, Restart Worker, Open Log, Exit.
- Run the physical Volume Mute key observer independently from FRITZ worker availability.

## Settings Model
- `appsettings.json` (next to tray exe) stores non-secret settings:
  - `Fritz.Host`
  - `Fritz.Ain`
  - `Polling.IntervalSeconds`
  - `CueSdk.Path`
  - `ToggleOnKeypress`
  - `DevMode`
- FRITZ username and password are not stored in JSON.

## Credential Manager
- Target name: `iCUERedGreen/FritzPassword`
- Storage type: Generic credential, persisted for current user.
- Stores FRITZ username + password as UserName + secret.
- Read on startup and on Save in settings dialog.
- When `DevMode` is true, env-var fallback is enabled for all fields.
- When `DevMode` is false, env vars are ignored.

## Worker Lifecycle
- Worker starts when tray app starts.
- Save in settings dialog triggers:
  1. Stop worker
  2. Dispose FRITZ + iCUE objects
  3. Recreate worker with new settings
- Keyboard hook starts/stops with the worker.
- The Sound Off coordinator starts with the tray app and keeps running even when FRITZ configuration is missing.

## UI States
- Tooltip: `iCUERedGreen: ON|OFF|UNKNOWN` and iCUE availability.
- Tray icon color indicates state.
- Sound Off does not change the tray icon; it uses only the physical Volume Mute key LED.

## Autostart
- Task Scheduler runs `iCUERedGreen.Tray.exe` at logon.
- No hidden console launcher required for production.

## Logging
- File logging only (NLog).
- Console logging disabled in tray app.

## Dev-Only CLI Host
- Keeps existing CLI use cases for diagnostics.
- Not used for production Task Scheduler entries.

## Versioning Note
- The tray-first redesign is a major change; the next productive release should be `2.0.0.0`.
- Until the tray app is stable, continue using the usual version bump schema.

## Migration Steps (High-Level)
1. Create Core library; move polling + iCUE + hook into Core.
2. Add Tray app; host Core worker inside.
3. Convert current console app into dev-only CLI host.
4. Update settings handling (Credential Manager).
5. Update Task Scheduler XML to launch tray app.
6. Update docs and release notes.
