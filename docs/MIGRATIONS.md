# Migrations

## 2026-02-10 — Tray-First Redesign (Planned)

### Summary
This migration replaces the production console worker with a WinForms tray app as the primary host.
The CLI remains in the solution for dev/debug builds only.

### Motivation
- Provide a user-friendly tray UX (status + settings).
- Remove the need for IPC and background console launchers.
- Store FRITZ credentials in Windows Credential Manager (no plaintext).

### Scope
- Add `iCUERedGreen.Core` (shared logic).
- Add `iCUERedGreen.Tray` (WinForms app).
- Convert existing console app to dev-only CLI host.
- Update Task Scheduler to launch the tray app.

### Versioning
- This is a major redesign; the next productive release should be `2.0.0.0`.
- Until the tray app is stable, continue the usual version bump schema.

### Migration Steps (High-Level)
1. Extract shared logic into Core.
2. Implement tray app + settings dialog.
3. Move CLI to dev-only host.
4. Update Task Scheduler XML and docs.
5. Release/publish tray app only.
