# Planned Feature Handoff - Toggle On Keypress

## Summary
Implement `--toggle-on-keypress` to toggle the FRITZ!DECT 200 when Scroll Lock is pressed.

## Intent
- Install a low-level keyboard hook (`WH_KEYBOARD_LL`).
- On Scroll Lock press, call the AHA `setswitchtoggle` command.
- Refresh the LED state immediately afterward.

## Notes
- This feature is intentionally not implemented yet.
- Scroll Lock state may change at the OS level when the key is pressed.
- Ensure errors are logged and do not crash the polling loop.
