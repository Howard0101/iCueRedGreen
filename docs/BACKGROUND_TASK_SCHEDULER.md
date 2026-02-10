# Run in Background (Task Scheduler)

This guide runs the tray app automatically at user logon.

## Steps
1. Open **Task Scheduler**.
2. Choose **Create Task...** (not “Basic Task”).
3. **General** tab:
   - Name: `iCUERedGreen`
   - Run only when user is logged on
   - Configure for: Windows 10/11
4. **Triggers** tab:
   - New... → Begin the task: **At log on**
5. **Actions** tab:
   - New... → Action: **Start a program**
   - Program/script: `<INSTALL_DIR>\iCUERedGreen.Tray.exe`
   - Start in: `<INSTALL_DIR>\`
6. **Conditions** tab:
   - Uncheck “Start the task only if the computer is on AC power” (optional)
7. **Settings** tab:
   - Allow task to be run on demand
   - If the task fails, restart every 1 minute (optional)

## XML Import (Alternative)
1. In Task Scheduler, choose **Import Task...**.
2. Select [docs/task-scheduler/iCUERedGreen.task.xml](task-scheduler/iCUERedGreen.task.xml).
3. Review the program path and working directory (update if needed).
4. Save the task.
5. If you import via `schtasks`, the XML must be UTF-16 (the provided file is already in that format).

## Notes
- iCUE must be running in the same user session for LED updates.
- If iCUE is not running, the switch toggle still works but LED control is disabled.
- After iCUE restarts, the next poll reconnects and restores LED updates.
- Use `appsettings.json` next to the tray exe for non-secret settings.
- FRITZ credentials are stored in Windows Credential Manager (set in the tray Settings).
- Environment variable fallback is only used when Dev Mode is enabled in Settings.
- Logs are written to `logs\iCUERedGreen.log` next to the executable; console logging is disabled for non-interactive runs.
- Log rotation keeps a single backup file (`iCUERedGreen.log.1`) when the active log exceeds 2 MB.
- To stop the task gracefully, delete `running.txt` next to the executable.
- When using toggle-on-keypress (tray Settings), disable any iCUE scripts bound to Scroll Lock to avoid double triggers.
- On startup, a stale `running.txt` (heartbeat older than the last system boot) is removed automatically.
- If you change SYSTEM environment variables, restart the scheduled task (or log off/on) to apply them.
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
- `<INSTALL_DIR>` is the install directory containing `iCUERedGreen.Tray.exe`.
