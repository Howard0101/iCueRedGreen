# DES-001 Sound-Off Key LED Feedback - READY Design

Status: READY
Date: 2026-06-29
Design ID: DES-001
Repository: iCUERedGreen
Handoff: `DES-001_sound-off-key-led_handoff_claude-hardened.md`

## 1. change - Final scope

DES-001 adds a new Sound-Off control for the Windows global output mute state.

The feature has two user-facing entry points:

- Primary entry point: the physical Windows Volume Mute key on the keyboard, normally `VK_VOLUME_MUTE` (`0xAD`).
- Secondary entry point: a new tray menu item labeled `Sound Off`.

Both entry points control the same Windows global audio mute state and share the same Sound-Off state model.

The feature is independent from the existing FRITZ/Rollen switch feature.

## 2. rule - Corrected terminology

The original design used the word "button". In this design, "button" means the physical Windows Volume Mute keyboard key unless explicitly stated otherwise.

The new tray menu item is an additional control for the same Sound-Off action, not the primary meaning of "button".

## 3. rule - Existing behavior preservation

DES-001 must not replace, rename, or change existing product behavior.

Preserve unchanged:

- existing `Toggle Switch` tray menu item;
- existing FRITZ/switch/Rollen behavior;
- existing tray icon state model;
- existing icon/glyph/Rollen status display;
- existing Scroll Lock/Rollen LED behavior;
- existing settings unrelated to Sound-Off.

The Sound-Off feature must not overwrite or reuse the existing Rollen/Scroll Lock indicator.

## 4. rule - State source

The only authoritative state source for DES-001 is the Windows global output mute state of the default render endpoint.

Required Sound-Off states:

- `Muted`
- `Unmuted`
- `Unknown`

The Sound-Off state must not be derived from FRITZ state, switch state, Rollen state, tray icon state, or iCUE availability alone.

## 5. rule - Color semantics

The physical Volume Mute key lighting represents the Windows global output mute state:

- Windows global sound muted/off: red.
- Windows global sound unmuted/on: green.
- Unknown, unavailable, iCUE unavailable, or state read failure: neutral/default.

Neutral/default means releasing or clearing only the dedicated Sound-Off key LED override. It must not clear or disturb the existing Scroll Lock/Rollen LED state.

## 6. proposal - User interaction model

### P-DES-001-01 - Physical Volume Mute key path

The physical key path must avoid a double-toggle bug.

Expected flow:

1. Detect `VK_VOLUME_MUTE` keydown once per physical press.
2. Do not suppress or consume the key event.
3. Do not call an additional programmatic mute toggle for this physical key path.
4. Let Windows process the key normally.
5. If the last confirmed Sound-Off state is known, set the Volume Mute key LED optimistically to the expected target color immediately.
6. After a short debounce/reconciliation delay, read the confirmed Windows global audio mute state.
7. Update the Volume Mute key LED to the confirmed color.
8. If the state cannot be confirmed, set the Volume Mute key LED to neutral/default and log once.

### P-DES-001-02 - Tray menu item path

Expected flow for the new `Sound Off` tray menu item:

1. Read the current Windows global audio mute state.
2. Derive the target state.
3. Set the Volume Mute key LED optimistically to the target color.
4. Programmatically apply the target Windows mute state.
5. Re-read the confirmed Windows global audio mute state.
6. Update the Volume Mute key LED to the confirmed color.
7. On failure, log once and set the Volume Mute key LED to neutral/default.

The tray item must not change the existing tray icon semantics.

## 7. proposal - Component model

### P-DES-001-03 - Windows audio service

Add a Windows-only audio service for the default render endpoint.

Responsibilities:

- read the Windows global output mute state;
- set or toggle the Windows global output mute state for the tray menu path;
- return `Unknown` instead of throwing through UI paths when the state cannot be read;
- avoid UI popups or console output.

Suggested implementation approach: Windows Core Audio (`IAudioEndpointVolume`) through P/Invoke or an already-approved dependency.

### P-DES-001-04 - Sound-Off coordinator

Add a Sound-Off coordinator separate from the existing FRITZ/switch coordinator.

Responsibilities:

- track the last confirmed Windows global mute state;
- apply optimistic LED feedback;
- reconcile with the confirmed Windows mute state;
- handle unknown/unavailable state;
- log failures once without user-facing noise.

### P-DES-001-05 - iCUE key LED support

Extend the iCUE abstraction to support the physical Volume Mute key LED independently from existing Scroll Lock/Rollen LED control.

Required behavior:

- set the Volume Mute key LED to red;
- set the Volume Mute key LED to green;
- release or clear only the Volume Mute key LED override;
- preserve existing Scroll Lock/Rollen LED behavior.

Hard blocker: if the Corsair SDK or the connected keyboard does not expose a controllable LED for the physical Volume Mute key, the implementer must stop and report the blocker. The implementation must not silently fall back to Scroll Lock or another key.

## 8. rule - iCUE LED identifier hardening

Codex must not guess the Volume Mute key LED identifier.

The implementation must verify the exact LED identifier from at least one deterministic source:

- the Corsair SDK headers/bindings used by this repository;
- SDK documentation matching the bundled SDK version;
- runtime LED position enumeration, if it exposes enough metadata to identify the Volume Mute key safely.

The review artifact must state the exact identifier used and how it was verified.

## 9. rule - Failure behavior

Failure handling is intentionally quiet.

On Sound-Off failures:

- log once per failure condition or transition;
- do not show a MessageBox;
- do not show a tray balloon or notification;
- do not add console output for this feature;
- set the Volume Mute key LED to neutral/default if the confirmed Windows state cannot be read.

## 10. non-goals

### NG-DES-001-01 - No FRITZ redesign

Do not redesign FRITZ polling, credentials, settings, worker behavior, or old switch semantics.

### NG-DES-001-02 - No existing tray icon redesign

Do not change the existing tray icon color or glyph semantics.

### NG-DES-001-03 - No Scroll Lock fallback

Do not use the existing Rollen/Scroll Lock LED as a fallback for Sound-Off.

### NG-DES-001-04 - No arbitrary alternate key

Do not select another physical key if the Volume Mute key LED is unavailable. Report a blocker instead.

### NG-DES-001-05 - No CLI scope expansion

Do not add Sound-Off CLI behavior unless required for a minimal test seam or diagnostic during implementation.

## 11. acceptance criteria

### AC-DES-001-01 - Physical key action

Pressing the physical Windows Volume Mute key updates Windows global audio mute through normal Windows handling and updates the dedicated Volume Mute key lighting after reconciliation.

### AC-DES-001-02 - Physical key optimistic feedback

When the previous Windows mute state is known, pressing the physical Volume Mute key changes the dedicated key lighting immediately to the expected target color before final reconciliation.

### AC-DES-001-03 - Tray menu action

The new `Sound Off` tray menu item toggles Windows global audio mute programmatically and uses the same LED feedback model as the physical key path.

### AC-DES-001-04 - Color mapping

The dedicated Volume Mute key lighting follows the required mapping:

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

### AC-DES-001-10 - Verified Volume Mute LED identity

The implementation documents the exact iCUE/Corsair LED identifier used for the Volume Mute key and how it was verified.

## 12. decision log

### D-DES-001-01 - Add new control without replacing old control

Decision: add a new Sound-Off feature while preserving the existing old tray/switch control.

### D-DES-001-02 - Use `Sound Off` label for the new tray item

Decision: the new tray menu item label is `Sound Off`; existing labels remain unchanged.

### D-DES-001-03 - Use Windows global audio state only

Decision: FRITZ has no role in DES-001. The feature controls Windows global sound off/on only.

### D-DES-001-04 - Unknown state remains usable

Decision: the control remains usable when the state is unknown. It starts neutral, applies optimistic feedback when safe, then reconciles with confirmed Windows audio state.

### D-DES-001-05 - Preserve existing tray icon

Decision: the existing tray icon remains tied to old functionality and does not mirror Sound-Off state.

### D-DES-001-06 - Log-only failure feedback

Decision: Sound-Off failures are logged only; no MessageBox or tray notification.

### D-DES-001-07 - Only key lighting changes

Decision: Sound-Off visual feedback is only the physical Volume Mute key lighting. Existing icon/glyph/Rollen indicators are already occupied and must not be reused.

### D-DES-001-08 - Physical key identity

Decision: the physical key is the existing Windows Volume Mute key.

### D-DES-001-09 - Add an additional tray menu item

Decision: add a new tray menu item for Sound-Off so the same function can also be triggered from the tray menu.

## 13. implementation handoff reference

The implementation handoff is a separate artifact:

- `DES-001_sound-off-key-led_handoff_claude-hardened.md`

Codex must implement from the handoff and this READY design together. If they conflict, this READY design defines the final feature intent and the handoff defines implementation/review procedure. Report any remaining conflict before editing code.

## 14. review artifact requirement

The implementer must produce a self-contained review artifact for an independent reviewer. The review artifact must include:

- changed source files or patch/diff;
- this READY design;
- the implementation handoff;
- exact build/test/smoke outputs;
- manual validation notes;
- exact Volume Mute key LED identifier and verification method;
- declared assumptions, limitations, and hardware/iCUE checks that could not be performed.

## 15. ready status

All known user decision questions for DES-001 are resolved.

This design is READY for Codex implementation using the Claude-hardened handoff.

## 16. implementation note

Implementation note after Codex work:

- Volume Mute LED identifier used: `CLK_Mute = 100`
- Verification source: `iCUESDK/include/iCUESDK/iCUESDKLedIdEnum.h`
- Shared-lighting implementation detail: Scroll Lock and Volume Mute are tracked in one iCUE session so one override does not overwrite the other.
- Review artifact: [DES-001_sound-off-key-led_review-artifact.md](../review/DES-001_sound-off-key-led_review-artifact.md)
