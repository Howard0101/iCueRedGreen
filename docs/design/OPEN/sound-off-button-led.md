# DES-001 Sound-Off Button LED Feedback

Status: OPEN
Date: 2026-06-29

## Context
- The tray-first app already tracks FRITZ switch state, iCUE availability, and optimistic state updates.
- The existing UI already exposes state through tray icon and tooltip.
- The requested addition is a new "Sound Off" button that also needs direct color feedback.

## Problem
- The button needs to show green/red feedback based on the current state and on the state requested by the user when the button is pressed.
- The control should not feel passive; users need to see the requested change immediately, then see the confirmed state after the FRITZ toggle completes.

## Goals
- Show a visible green/red state on the button itself.
- Update the button immediately on press with the target state.
- Reconcile the button with the confirmed FRITZ state after the action completes.
- Keep the unknown/default state visually distinct.

## Proposed Behavior
Working assumption: the button follows the existing ON/OFF color convention already used elsewhere in the app.

- Current OFF state is shown as green.
- Current ON state is shown as red.
- Unknown or unavailable state uses the default/neutral appearance.
- When the user presses the button, the button switches to the target color immediately.
- If the toggle succeeds, the button stays on the confirmed state color.
- If the toggle fails or the state cannot be confirmed, the button falls back to the unknown/default appearance and the failure is logged once.

## UI Placement Assumption
- Working assumption: the control lives in the tray app UI, not in the dev-only CLI.
- The design does not yet fix whether it belongs in the tray menu, settings dialog, or a dedicated main window.

## Open Questions
- Should this control be a tray menu item, a settings dialog button, or both?
- Does "Sound Off" mean a literal mute action, or is it the name of the existing FRITZ toggle control?
- Should the tray icon mirror the same state change, or is the button the only new visual indicator?

## Acceptance Criteria
- The button reflects the confirmed switch state with green/red colors.
- The button shows the pressed target state immediately after activation.
- Unknown/unavailable state remains visually distinct.
- No new console output is introduced for this feature.
