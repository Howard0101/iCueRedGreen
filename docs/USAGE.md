# Usage & Configuration

This document explains how to run `iCUERedGreen` and configure FRITZ!DECT 200 polling.

## Prerequisites
- Windows 10/11
- iCUE running in the user session (required for LED updates; optional for switch toggling)
- 64-bit CUE SDK DLL available (download separately; see References)

## Getting Started
1. Run `iCUERedGreen.Tray.exe` and open **Settings** to configure FRITZ credentials.
2. (Dev-only CLI) Set the required environment variables in PowerShell:
```powershell
$env:FRITZ_HOST="fritz.box"
$env:FRITZ_USERNAME="your-user"
$env:FRITZ_PASSWORD="your-password"
$env:FRITZ_AIN="12345 6789012"
```

3. (Dev-only CLI) Run the app (from the project folder):
```powershell
dotnet run --project .\iCUERedGreen.Cli\iCUERedGreen.Cli.csproj -- --interval 5
```

4. Download the iCUE SDK from Corsair (see References). For builds/publish, place `iCUESDK.x64_2019.dll` in `iCUERedGreen.Cli\Asset\`.
   - To include the DLL in publish output, run: `dotnet publish ... -p:IncludeCueSdk=true`.
5. To run the published CLI executable, place the CUE SDK DLL next to the exe or provide the path:
```powershell
.\iCUERedGreen.Cli.exe --cuesdk-path "C:\Path\To\iCUESDK.x64_2019.dll"
```

## Path Placeholders
- `<INSTALL_DIR>` = install directory containing `iCUERedGreen.Tray.exe`.
- `<PUBLISH_DIR>` = local publish output folder (e.g., `artifacts\publish\win-x64`).
- Optional: keep a local mapping in `docs/LOCAL_PATHS.md` (local-only; ignored by git).

## Configuration Precedence
CLI configuration is resolved in this order:
1. CLI arguments
2. Environment variables
3. `appsettings.json`
4. Defaults

## Environment Variables
- `FRITZ_HOST` (e.g. `fritz.box` or `192.168.178.1`)
- `FRITZ_USERNAME`
- `FRITZ_PASSWORD`
- `FRITZ_AIN` (include spaces exactly as shown by FRITZ!Box)
- `POLL_INTERVAL_SECONDS` (default: `5`)
Note: Environment variables are only used when Dev Mode is enabled in the tray app or when running the CLI.

## Changing SYSTEM Environment Variables
- SYSTEM environment variables are read when the process starts.
- After changing SYSTEM variables, you must restart the scheduled task (or log off/on) for the app to pick them up.
- Example (elevated PowerShell):
```powershell
setx /M FRITZ_HOST "fritz.box"
setx /M FRITZ_USERNAME "your-user"
setx /M FRITZ_PASSWORD "your-password"
setx /M FRITZ_AIN "12345 6789012"
setx /M POLL_INTERVAL_SECONDS "5"
schtasks /End /TN "iCUERedGreen"
schtasks /Run /TN "iCUERedGreen"
```

## appsettings.json
Tray app settings are stored in `appsettings.json` next to the tray executable.
Example structure:
```json
{
  "DevMode": false,
  "ToggleOnKeypress": false,
  "Fritz": {
    "Host": "",
    "Ain": ""
  },
  "Polling": {
    "IntervalSeconds": 5
  },
  "CueSdk": {
    "Path": ""
  }
}
```
Credentials are stored in Windows Credential Manager and are not written to the settings file.

CLI appsettings.json (dev-only) supports additional fields:
```json
{
  "Fritz": {
    "Host": "",
    "Username": "",
    "Password": "",
    "Ain": ""
  },
  "Polling": {
    "IntervalSeconds": 5
  },
  "CueSdk": {
    "Path": ""
  }
}
```

## CLI Options
These options apply to the dev-only CLI (`iCUERedGreen.Cli.exe`).
```
--interval <sec>       Polling interval in seconds (default 5)
--host <host>          FRITZ!Box host (e.g., fritz.box)
--username <user>      FRITZ!Box username
--ain <ain>            FRITZ!DECT AIN (include spaces)
--cuesdk-path <path>   Path to CUE SDK DLL
--toggle-on-keypress   Toggle the switch on Scroll Lock keypress
--no-toggle            Reserved for future use
--version              Show version information
--help                 Show help
```

## Notes
- The Scroll Lock LED must be exposed by your keyboard in the CUE SDK; otherwise LED control stays disabled.
- LED mapping: ON → red, OFF → green.
- If the switch state is unknown or an error occurs, the app releases control so iCUE returns to its default lighting.
- If iCUE is not running, the app still polls/toggles the switch but LED control is disabled (neutral).
- When iCUE starts later, LED control resumes automatically.
- If iCUE was restarted, the next poll will reconnect and re-enable LED updates.
- When using `--toggle-on-keypress`, disable any iCUE scripts bound to Scroll Lock to avoid double triggers.
- The app uses shared iCUE control and only overrides the Scroll Lock LED; other lighting remains under iCUE control.

## Logging
- Logs are written to `logs\iCUERedGreen.log` next to the executable.
- Interactive runs also log to the console; non-interactive runs (e.g., Task Scheduler) do not.
- Log rotation keeps a single backup file (`iCUERedGreen.log.1`) when the active log exceeds 2 MB.

## Graceful Stop
- The app writes a heartbeat file named `running.txt` next to the executable.
- Delete `running.txt` to request a graceful stop.
- The file is updated on each poll to show the latest heartbeat timestamp.
- On startup, a stale `running.txt` (heartbeat older than the last system boot) is deleted automatically.

## Update Script
- `scripts\update-published.ps1` (local-only; ignored by git) updates the published deployment in `<INSTALL_DIR>`.
- It preserves `appsettings.json`, `nlog.config`, and `logs\` by default.

## Hidden Startup
- The tray app is a WinForms executable and does not show a console window.
- Use [iCUERedGreen.Cli/start-hidden.ps1](../iCUERedGreen.Cli/start-hidden.ps1) only for the console app.
- The Task Scheduler XML now runs `iCUERedGreen.Tray.exe` directly.

## Troubleshooting
- iCUE not running: The app continues without LED control. Start iCUE to re-enable LED updates. If you see `ServerNotFound` during handshake, iCUE is not available.
- No control permission: If you see `NoControl`, open iCUE and grant SDK control for the device profile.
- Scroll Lock LED missing: LED control is disabled if `CorsairLedId_ScrollLock` is not in the LED list. Verify the keyboard exposes the LED in iCUE.
- LED resets to default on errors: The app releases control to iCUE when the state is unknown; check logs to see the root error.
- DLL not found: Place `CUESDK.x64_*.dll` next to the executable or pass `--cuesdk-path`.
- FRITZ auth failed: Recheck `FRITZ_USERNAME` and `FRITZ_PASSWORD`. The app does not log passwords, only their length.
- FRITZ host/AIN wrong: Verify `FRITZ_HOST` and `FRITZ_AIN` (AIN must include spaces exactly).
- Timeouts: The FRITZ HTTP timeout is 3 seconds. If your network is slow, stabilize connectivity or reduce retries by ensuring the first login succeeds.

## References
- iCUE SDK GitHub source: [https://github.com/CorsairOfficial/cue-sdk](https://github.com/CorsairOfficial/cue-sdk)
- iCUE SDK documentation: [https://corsairofficial.github.io/cue-sdk/](https://corsairofficial.github.io/cue-sdk/)
