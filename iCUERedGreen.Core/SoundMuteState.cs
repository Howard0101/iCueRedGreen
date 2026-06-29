namespace iCUERedGreen;

/// <summary>
/// Represents the Windows global audio mute state.
/// </summary>
public enum SoundMuteState
{
    /// <summary>
    /// The mute state is unknown.
    /// </summary>
    Unknown,

    /// <summary>
    /// The audio output is not muted.
    /// </summary>
    Unmuted,

    /// <summary>
    /// The audio output is muted.
    /// </summary>
    Muted
}
