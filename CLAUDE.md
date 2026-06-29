# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Windows app that polls a FRITZ!DECT 200 smart-plug and mirrors its on/off state on the Corsair iCUE Scroll Lock key LED (ON → red, OFF → green). It also includes a "Sound Off" feature that mirrors the Windows global audio mute state on the Volume Mute key LED (muted → red, unmuted → green).

## Build / Test / Run

Requires the .NET SDK pinned in `global.json` (10.0.301). Use PowerShell.

```powershell
dotnet build iCUERedGreen.sln
dotnet test iCUERedGreen.sln                              # xUnit; net10.0-windows
dotnet test --filter "FullyQualifiedName~SoundOffCoordinator"   # single test class/method

# Run the tray app (primary production host)
dotnet run --project .\iCUERedGreen.Tray\iCUERedGreen.Tray.csproj
dotnet run --project .\iCUERedGreen.Tray\iCUERedGreen.Tray.csproj -- --dev-ui   # show Dev Mode controls

# Run the dev-only CLI host (diagnostics; uses env vars / CLI args)
dotnet run --project .\iCUERedGreen.Cli\iCUERedGreen.Cli.csproj -- --interval 5
```

The CUE SDK DLL (`iCUESDK.x64_2019.dll`) is **not** in the repo — download it from Corsair and place it in `iCUERedGreen.Cli\Asset\`. It is only copied into build/publish output when `-p:IncludeCueSdk=true` is passed. Local publish must always use `-p:IncludeCueSdk=true` (project rule).

## Architecture

Three projects plus tests (see `docs/ARCHITECTURE.md` for the full design):

- **`iCUERedGreen.Core`** (`net10.0`, class library) — all shared logic. `internal` types are exposed to the other projects via `InternalsVisibleTo`. Key types:
  - `WorkerController` — orchestrates the FRITZ poll loop, the optional Scroll Lock keyboard hook, and LED updates. Its nested `PollingCoordinator` serializes poll/toggle through a `SemaphoreSlim` gate and supports optimistic LED updates; its nested `KeyboardHookRunner` is a low-level Win32 `WH_KEYBOARD_LL` hook on its own thread.
  - `FritzAhaClient` — FRITZ!Box AHA HTTP client (3s timeout, does not log passwords).
  - `CueLightingSession` — the single shared iCUE handle for the process. Both the Scroll Lock path and the Sound Off path (`ISoundOffLedSession`) go through it; it only overrides those two keys and leaves the rest under iCUE control.
  - `SoundOffCoordinator` / `WindowsAudioMuteService` — Sound Off feature; runs independently of FRITZ availability.
  - `SwitchState` (On/Off/Unknown), `SoundMuteState`, `WorkerSettings`.
- **`iCUERedGreen.Tray`** (`net10.0-windows`, WinForms `WinExe`) — production host. Hosts the Core worker in-process, tray icon/menu, `SettingsForm`. `TraySettingsStore` reads/writes `appsettings.json` (non-secret); `CredentialStore` stores FRITZ username+password in **Windows Credential Manager** under target `iCUERedGreen/FritzPassword` (never in JSON). Save in settings stops, disposes, and recreates the worker.
- **`iCUERedGreen.Cli`** (`net10.0`, console `Exe`) — dev-only diagnostics host. `Options.cs` parses CLI args. Config precedence: CLI args → env vars → `appsettings.json` → defaults. Env vars are honored only in Dev Mode (tray) or in the CLI.

Notable runtime behaviors: file logging only via NLog (`logs\iCUERedGreen.log`, 2 MB rotation, 1 backup); a `running.txt` heartbeat file next to the exe acts as a graceful-stop signal (delete it to stop; stale-on-boot files are auto-removed). iCUE absence is non-fatal — switch polling/toggling continues, LED control resumes when iCUE returns.

## Repository conventions (important)

- **Instruction hierarchy:** `AGENTS.md` points to an external authoritative instruction set at `D:\Source\ai\_ai_general.md`, then `docs/ai/_ai_instructions_project.md` (project overrides). Read those for release work; do **not** auto-load the release-workflow files for normal coding tasks.
- **Line endings:** normalize Windows line endings before committing files AI/CI tools touched: `pwsh -NoProfile -File .\scripts\check-line-endings.ps1` / `.\scripts\normalize-line-endings.ps1`. **Exception:** `docs/task-scheduler/*.xml` must stay UTF-16 LE with BOM — never normalize those.
- **Commits:** per project rule, commit (and push to `origin/main`) after any meaningful change in the same work cycle unless told otherwise; run git writes sequentially, never in parallel.
- **Changelog:** `docs/changelog/CHANGELOG.md` is authoritative for implemented changes. Entries under `## Unreleased` must carry exactly one scope tag (`[meta-only]` or `[payload-impact]`), optionally `[priority]`; tags are stripped when moved into a released version section. `docs/RELEASES.md` is the human-readable release list (newest-first).
- **TEMP lifecycle / template tooling:** use the wrapper `pwsh -NoProfile -File .\scripts\invoke-temp-lifecycle-check.ps1 -Action <...>` rather than ad-hoc `Remove-Item`/TEMP commands. `.template/` and `TEMP/` hold template-machinery and scratch artifacts — generally not application code.
- Doc-only changes to human-readable markdown do not require a version bump (template governance is overridden here).
