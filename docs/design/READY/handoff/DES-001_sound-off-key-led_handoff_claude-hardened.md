# DES-001 Sound-Off Key LED Feedback - Implementation Handoff (Claude-Hardened)

Status: READY-HANDOFF
Date: 2026-06-29
Audience: Codex CLI implementer; Claude or ChatGPT reviewer
Repository: iCUERedGreen
Source design: `sound-off-button-led.md`

## 1. change - Final user decisions

### D-DES-001-01 - New control target
Add a new Sound-Off control without replacing or renaming the existing tray switch control.

The word "button" in the original design means a physical keyboard key, not primarily a WinForms button.

Required controls:
- Primary control: physical Windows Volume Mute key on the keyboard.
- Additional control: new tray menu item for the same Sound-Off action.

Do not remove or change the existing `Toggle Switch` tray item.

### D-DES-001-02 - User-facing label
The new tray menu item label is `Sound Off`.

This label applies only to the new Sound-Off action. Existing FRITZ/Rollen labels remain unchanged.

### D-DES-001-03 - State source
The new Sound-Off feature is based only on the Windows global audio mute state.

MUST NOT use FRITZ state for this feature.
MUST NOT reconcile this feature against FRITZ state.
MUST NOT route this feature through the existing FRITZ switch toggle.

### D-DES-001-04 - Color semantics
Use these colors for the physical Volume Mute key lighting:

- Windows global sound muted/off: red.
- Windows global sound unmuted/on: green.
- Unknown, unavailable, iCUE unavailable, or state read failure: neutral/default by releasing or clearing the dedicated Sound-Off key override.

### D-DES-001-05 - Unknown state behavior
If the Windows global audio mute state is unknown, keep the control usable:

- The key lighting stays neutral/default before the action.
- A user action may still be attempted.
- Apply optimistic target feedback only when a target state can be derived safely.
- Reconcile with the confirmed Windows global audio mute state after the action.
- If confirmation fails, return the Sound-Off key lighting to neutral/default and log once.

### D-DES-001-06 - Existing tray icon behavior
The existing tray icon and existing icon/glyph/Rollen semantics remain unchanged.

The new Windows Sound-Off state MUST NOT overwrite the tray icon state.
The existing tray icon remains tied to the old existing functionality.

### D-DES-001-07 - Failure feedback
On Sound-Off failures:

- Log once per failure condition or transition.
- Do not show a MessageBox.
- Do not show a tray balloon/notification.
- Return the Sound-Off key lighting to neutral/default if confirmed state cannot be read.

### D-DES-001-08 - Physical key identity
The physical key is the Windows Volume Mute key, normally virtual key `VK_VOLUME_MUTE` (`0xAD`).

## 2. rule - Critical Claude hardening notes

### H-DES-001-01 - No double-toggle on physical key
The physical Windows Volume Mute key is normally handled by Windows itself. Do not create a double-toggle bug.

For the physical key path:
- Observe `VK_VOLUME_MUTE` keydown.
- Do not suppress the key event.
- Do not blindly call a second mute toggle when Windows will already process the key.
- Set optimistic LED feedback to the inverse of the last confirmed Windows global audio mute state when available.
- Schedule a short delayed reconciliation read of the Windows global audio mute state after Windows has processed the key.
- If the confirmed state differs from the optimistic state, update the LED to the confirmed state.

For the new tray menu item path:
- Programmatically toggle the Windows global audio mute state.
- Apply optimistic LED feedback immediately to the target state.
- Reconcile with confirmed Windows global audio mute state after the call.

### H-DES-001-02 - FRITZ isolation
The new Sound-Off implementation must be independent from FRITZ.

Do not reuse `SwitchState` for Sound-Off if that would mix FRITZ and Windows audio semantics. Prefer a separate enum/model, for example:

- `SoundMuteState.Muted`
- `SoundMuteState.Unmuted`
- `SoundMuteState.Unknown`

Do not emit FRITZ log messages from Sound-Off code paths.
Do not change FRITZ polling, FRITZ credentials, or FRITZ worker behavior unless strictly needed to avoid coupling.

### H-DES-001-03 - Existing Rollen/Scroll Lock LED must remain untouched
The current product already uses the Rollen/Scroll Lock LED and tray icon/glyph to show the existing state.

The Sound-Off implementation MUST NOT reuse the existing Scroll Lock LED override for Sound-Off.
The Sound-Off implementation MUST NOT overwrite the existing Rollen/Scroll Lock state.
The Sound-Off implementation MUST target only the physical Volume Mute key LED, if iCUE exposes it.

If iCUE or the Corsair SDK does not expose a controllable LED for the Volume Mute key on the user's keyboard, stop and report a blocker instead of silently repurposing another key.

### H-DES-001-04 - Verify the Corsair LED identifier
The uploaded repo currently contains an explicit Scroll Lock LED enum only. Do not guess the Volume Mute key LED ID.

Implementation must verify the exact LED identifier from one of these sources:
- the Corsair SDK headers/bindings used by this repo;
- the SDK documentation matching the bundled SDK version;
- runtime LED position enumeration, if it exposes enough metadata to identify the Volume Mute key safely.

If the exact Volume Mute LED cannot be identified deterministically, create a small, reviewable blocker note and do not fake the feature by using Scroll Lock.

### H-DES-001-05 - Neutral/default means release only the Sound-Off key override
Returning the Sound-Off key to neutral/default must not release or clear the existing Scroll Lock/Rollen LED state used by the old feature.

If the current SDK abstraction only releases all lighting control for the whole keyboard/device, refactor carefully so the Sound-Off key can be neutralized without breaking the existing LED feature. If this is impossible with the SDK, document the limitation as a blocker.

### H-DES-001-06 - No user-facing noise
Sound-Off failure feedback is log-only.

No new console output.
No MessageBox.
No tray notification.
No audible alert.

## 3. proposal - Implementation shape

### P-DES-001-01 - Add a Windows audio service
Add a small Windows-only audio service responsible for reading and setting the default render endpoint mute state.

Suggested responsibilities:
- `TryGetMuteState()` returns muted/unmuted/unknown.
- `SetMuted(bool muted)` or `ToggleMute()` changes the Windows global output mute state.
- Failures are logged by caller or through an injected logger, but do not produce UI popups.

Suggested implementation approach:
- Use Windows Core Audio (`IAudioEndpointVolume`) through P/Invoke or an already-approved dependency.
- Default render endpoint only.
- Keep this in tray/core code as appropriate, but avoid introducing unrelated dependencies.

### P-DES-001-02 - Add a separate Sound-Off state coordinator
Add a coordinator separate from the existing FRITZ polling coordinator.

Responsibilities:
- Track last confirmed Windows mute state.
- Apply immediate optimistic Sound-Off key LED color on user action.
- Reconcile after action.
- Return key LED to neutral/default on unknown/unavailable.
- Log one failure per transition/failure class to avoid log spam.

### P-DES-001-03 - Add iCUE support for the Volume Mute key LED
Extend the iCUE abstraction so it can control a dedicated key LED by key identity, not only the existing Scroll Lock LED.

Required behavior:
- Existing Scroll Lock/Rollen LED behavior remains unchanged.
- New Sound-Off key LED can be set to red/green independently.
- New Sound-Off key LED can be returned to neutral/default without disturbing existing Scroll Lock/Rollen lighting.

### P-DES-001-04 - Add physical key observation for `VK_VOLUME_MUTE`
Extend or add a keyboard hook path for the Windows Volume Mute key.

Required behavior:
- Detect keydown once per physical press, with debounce.
- Do not suppress or consume the key.
- Do not call a second Windows mute toggle for this path.
- Optimistically update LED based on last confirmed state when available.
- Reconcile after a short delay by reading the Windows mute state.

### P-DES-001-05 - Add new tray menu entry
Add a new tray context menu item labeled `Sound Off`.

Required behavior:
- Clicking it toggles Windows global audio mute programmatically.
- It uses the same Sound-Off coordinator and LED state model as the physical key path.
- It does not affect existing `Toggle Switch` behavior.
- It does not change the existing tray icon.

Optional UI text hardening:
- The item may show a checkmark or text suffix only if that does not conflict with existing tray status semantics.
- If in doubt, keep label stable as `Sound Off` and rely on key lighting/logs.

## 4. implementation tasks

### T-DES-001-01 - Inspect current LED abstraction
Inspect these existing areas before editing:
- `iCUERedGreen.Core/CorsairSdk.cs`
- `iCUERedGreen.Core/WorkerController.cs`
- `iCUERedGreen.Tray/TrayApplicationContext.cs`
- `iCUERedGreen.Tray/SettingsForm.cs`
- existing tests under `iCUERedGreen.Tests/`

Confirm whether the current iCUE SDK abstraction can control multiple independent LEDs.

### T-DES-001-02 - Add Windows audio mute read/toggle support
Implement a Windows global output mute state service with tests around state mapping where possible.

Do not bind this service to FRITZ settings.
Do not require FRITZ configuration for Sound-Off tray/key behavior unless the current app startup architecture makes that unavoidable; if unavoidable, keep the dependency minimal and document it.

### T-DES-001-03 - Add Sound-Off LED target
Implement or extend key LED control for the Volume Mute key.

Hard blocker: do not use Scroll Lock/Rollen as fallback.

### T-DES-001-04 - Add physical key path
Add observation for `VK_VOLUME_MUTE`.

Physical key flow:
1. Keydown detected.
2. If last confirmed state is known, set LED to the inverse state immediately.
3. Allow Windows to process the key normally.
4. Re-read Windows mute state after a short debounce/reconcile delay.
5. Update LED to confirmed state, or neutral/default on failure.

### T-DES-001-05 - Add tray menu path
Add `Sound Off` tray item.

Tray menu flow:
1. Read current Windows mute state.
2. Derive target state.
3. Set LED optimistically to target color.
4. Apply target state programmatically.
5. Re-read and reconcile.
6. On failure, log once and set Sound-Off key LED neutral/default.

### T-DES-001-06 - Preserve old behavior
Verify these old behaviors remain unchanged:
- Existing `Toggle Switch` tray item still toggles old FRITZ/switch behavior.
- Existing tray icon still follows old switch state.
- Existing Scroll Lock/Rollen LED behavior still follows old state.
- Existing settings still work.
- Existing CLI behavior is not changed unless tests require shared model adjustments.

### T-DES-001-07 - Tests and build
Run the smallest relevant validation set.

Recommended commands for implementer on Windows:
- `dotnet test iCUERedGreen.sln`
- if available, run the tray app manually with iCUE available and with iCUE unavailable.

Do not fabricate hardware test results. If hardware/iCUE validation cannot be performed, state that clearly in the review artifact.

## 5. acceptance criteria

### AC-DES-001-01 - Physical key action
Pressing the physical Windows Volume Mute key updates Windows global audio mute through normal Windows handling and updates the dedicated Volume Mute key lighting after reconciliation.

### AC-DES-001-02 - Physical key optimistic feedback
When the previous Windows mute state is known, pressing the physical Volume Mute key changes the dedicated key lighting immediately to the expected target color before final reconciliation.

### AC-DES-001-03 - Tray menu action
The new `Sound Off` tray menu item toggles Windows global audio mute programmatically and uses the same LED feedback model.

### AC-DES-001-04 - Color mapping
The dedicated Volume Mute key lighting follows:
- muted/off = red;
- unmuted/on = green;
- unknown/unavailable = neutral/default.

### AC-DES-001-05 - FRITZ isolation
The Sound-Off feature works without reading FRITZ state and without changing existing FRITZ toggle logic.

### AC-DES-001-06 - Existing UI isolation
Existing tray icon/glyph/Rollen state remains unchanged by Sound-Off state changes.

### AC-DES-001-07 - Failure behavior
Failures are logged once, no MessageBox or tray notification appears, and the Sound-Off key lighting returns to neutral/default when state cannot be confirmed.

### AC-DES-001-08 - No Scroll Lock fallback
The implementation does not use the existing Scroll Lock/Rollen LED as a fallback for the new Sound-Off feature.

### AC-DES-001-09 - No double-toggle
Pressing the physical Windows Volume Mute key must not toggle mute twice and end up with no net audio state change.

## 6. review artifact requirements

The implementer must produce a review artifact for independent review. The artifact must include:

### RA-DES-001-01 - Source changes
Include changed source files or a patch/diff covering all changes.

### RA-DES-001-02 - Design and handoff
Include or reference:
- this handoff;
- the updated DES-001 design file if Codex updates it.

### RA-DES-001-03 - Verification results
Include exact command outputs for:
- build/test commands run;
- manual tray/key/iCUE checks performed;
- any hardware checks that could not be performed.

### RA-DES-001-04 - Assumptions and limitations
Explicitly report:
- the exact Corsair LED identifier used for the Volume Mute key;
- how it was verified;
- whether the SDK can neutralize only the Sound-Off key without disturbing existing lighting;
- whether physical key testing was performed on real hardware.

### RA-DES-001-05 - Reviewer output format
The reviewer must answer with:

```text
DES-001 Review Verdict: ACCEPT / FIX_REQUIRED

Criteria:
AC-DES-001-01: PASS/FAIL/NOT_VERIFIED - note
AC-DES-001-02: PASS/FAIL/NOT_VERIFIED - note
AC-DES-001-03: PASS/FAIL/NOT_VERIFIED - note
AC-DES-001-04: PASS/FAIL/NOT_VERIFIED - note
AC-DES-001-05: PASS/FAIL/NOT_VERIFIED - note
AC-DES-001-06: PASS/FAIL/NOT_VERIFIED - note
AC-DES-001-07: PASS/FAIL/NOT_VERIFIED - note
AC-DES-001-08: PASS/FAIL/NOT_VERIFIED - note
AC-DES-001-09: PASS/FAIL/NOT_VERIFIED - note

Findings:
F-DES-001-xx: severity BLOCKER/HIGH/MEDIUM/LOW - description - required fix

Overall recommendation:
...
```

## 7. non-goals

### NG-DES-001-01 - No FRITZ redesign
Do not redesign FRITZ polling, FRITZ settings, credentials, or old switch behavior.

### NG-DES-001-02 - No tray icon redesign
Do not change existing tray icon color semantics.

### NG-DES-001-03 - No CLI scope expansion
Do not add Sound-Off CLI behavior unless it becomes necessary for shared tests or a minimal internal diagnostic. The requested user-facing controls are the physical key and the tray menu item.

### NG-DES-001-04 - No arbitrary keyboard fallback
Do not choose another physical key if the Volume Mute LED is unavailable. Stop and report the blocker.

## 8. handoff order for Codex

1. Read `AGENTS.md`.
2. Load the external general instructions and project-local instructions as required by `AGENTS.md`.
3. Read this handoff fully.
4. Inspect current iCUE SDK LED support before writing code.
5. Implement in small commits/diffs.
6. Run the smallest relevant tests.
7. Produce the review artifact described above.

## 9. ready status

All user decision questions known at handoff time are resolved.

Open implementation blockers are allowed only for hardware/SDK facts that cannot be known from the uploaded design alone, especially the exact controllable iCUE LED identity for the physical Volume Mute key.
