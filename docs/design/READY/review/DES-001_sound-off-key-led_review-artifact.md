# DES-001 Review Artifact

## Scope
- Feature: Sound Off tray control + physical Volume Mute key LED feedback.
- Source design: [DES-001_sound-off-key-led_READY_design.md](../designs/DES-001_sound-off-key-led_READY_design.md)
- Source handoff: [DES-001_sound-off-key-led_handoff_claude-hardened.md](../handoff/DES-001_sound-off-key-led_handoff_claude-hardened.md)

## Implementation Notes
- Windows mute state is read and set through Windows Core Audio `IAudioEndpointVolume`.
- The physical key path observes `VK_VOLUME_MUTE` (`0xAD`) and does not suppress or re-toggle the key.
- The tray path toggles Windows mute programmatically through the same coordinator.
- The shared iCUE lighting session now tracks both Scroll Lock and Volume Mute together so one feature does not overwrite the other.
- The Volume Mute LED identifier is `CLK_Mute = 100`, verified from `iCUESDK/include/iCUESDK/iCUESDKLedIdEnum.h`.
- Neutral/default for the Sound Off key is implemented as a transparent (`alpha = 0`) color on the app layer for only that key, preserving Scroll Lock and the rest of the keyboard.

## Changed Source Files
- `iCUERedGreen.Core/CorsairSdk.cs`
- `iCUERedGreen.Core/CueLightingSession.cs`
- `iCUERedGreen.Core/SoundMuteState.cs`
- `iCUERedGreen.Core/SoundOffCoordinator.cs`
- `iCUERedGreen.Core/WindowsAudioMuteService.cs`
- `iCUERedGreen.Core/WorkerController.cs`
- `iCUERedGreen.Tray/TrayApplicationContext.cs`
- `iCUERedGreen.Tests/SoundOffCoordinatorTests.cs`
- `global.json`

## Validation
- `dotnet build .\iCUERedGreen.sln`
  - App projects compiled successfully after the .NET 10 SDK pin was updated to match the locally installed `10.0.301`.
  - A parallel build/test run caused a transient test-assembly file lock; rerunning validation serially avoided that issue.
- `dotnet test .\iCUERedGreen.Tests\iCUERedGreen.Tests.csproj`
  - Passed locally after the DES-001 changes.

## Manual Verification Status
- Real hardware/iCUE verification: not performed in this implementation session.
- Physical Volume Mute key behavior on the target keyboard: not verified in this implementation session.
- Exact keyboard LED exposure for the target device: implementation verifies `CLK_Mute (100)` at runtime and logs once if the keyboard does not expose it.

## Assumptions and Limitations
- The implementation assumes the iCUE alpha channel behavior allows a per-key transparent layer entry to return only that key to default lighting.
- If the keyboard does not expose `CLK_Mute (100)`, Sound Off still controls Windows mute but leaves the key lighting neutral.
- The tray icon remains tied to the FRITZ switch state and intentionally does not reflect Sound Off state.
