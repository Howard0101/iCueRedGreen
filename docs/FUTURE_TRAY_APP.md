# Future: Tray App

This note tracks the future implementation of a small tray application.

## Goal
- Provide a visible, user-friendly way to start/stop `iCUERedGreen`.
- Show current status (ON/OFF/unknown) in the tray icon tooltip.
- Allow opening a small settings dialog (interval, FRITZ host, DLL path).

## Implementation Notes
- Use WinForms or WPF with a `NotifyIcon`/tray icon.
- Keep the core polling logic in the existing console project and host it from the tray app.
- Add a single-instance guard to avoid duplicate polling loops.
