namespace iCUERedGreen;

/// <summary>
/// Represents the FRITZ!DECT 200 switch state.
/// </summary>
public enum SwitchState
{
    /// <summary>
    /// The state is unknown.
    /// </summary>
    Unknown,
    /// <summary>
    /// The switch is off.
    /// </summary>
    Off,
    /// <summary>
    /// The switch is on.
    /// </summary>
    On
}

/// <summary>
/// Captures a switch state snapshot and iCUE availability.
/// </summary>
public sealed class SwitchStateSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwitchStateSnapshot"/> class.
    /// </summary>
    /// <param name="state">The switch state.</param>
    /// <param name="timestampUtc">The snapshot timestamp in UTC.</param>
    /// <param name="isCueAvailable">Whether iCUE is available.</param>
    public SwitchStateSnapshot(SwitchState state, DateTimeOffset timestampUtc, bool isCueAvailable)
    {
        State = state;
        TimestampUtc = timestampUtc;
        IsCueAvailable = isCueAvailable;
    }

    /// <summary>
    /// Gets the switch state.
    /// </summary>
    public SwitchState State { get; }

    /// <summary>
    /// Gets the snapshot timestamp in UTC.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; }

    /// <summary>
    /// Gets a value indicating whether iCUE is available.
    /// </summary>
    public bool IsCueAvailable { get; }
}

/// <summary>
/// Provides data for switch state change events.
/// </summary>
public sealed class SwitchStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SwitchStateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="previousState">The previous state.</param>
    /// <param name="current">The current snapshot.</param>
    public SwitchStateChangedEventArgs(SwitchState previousState, SwitchStateSnapshot current)
    {
        PreviousState = previousState;
        Current = current ?? throw new ArgumentNullException(nameof(current));
    }

    /// <summary>
    /// Gets the previous switch state.
    /// </summary>
    public SwitchState PreviousState { get; }

    /// <summary>
    /// Gets the current switch snapshot.
    /// </summary>
    public SwitchStateSnapshot Current { get; }
}
