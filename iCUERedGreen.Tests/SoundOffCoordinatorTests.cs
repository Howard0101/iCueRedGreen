using iCUERedGreen;
using NLog;

namespace iCUERedGreen.Tests;

/// <summary>
/// Tests for the Sound Off coordinator behavior.
/// </summary>
public sealed class SoundOffCoordinatorTests
{
    /// <summary>
    /// Verifies a refresh to an unknown state clears the dedicated LED.
    /// </summary>
    [Fact]
    public async Task RefreshStateAsyncUnknownClearsLedAsync()
    {
        FakeAudioMuteService audioMuteService = new FakeAudioMuteService(SoundMuteState.Unknown);
        FakeSoundOffLedSession ledSession = new FakeSoundOffLedSession();
        SoundOffCoordinator coordinator = new SoundOffCoordinator(audioMuteService, ledSession, CreateLogger());

        await coordinator.RefreshStateAsync(CancellationToken.None);

        Assert.True(ledSession.EnsureInitializedCalled);
        Assert.Equal(new[] { SoundMuteState.Unknown }, ledSession.AppliedStates);
    }

    /// <summary>
    /// Verifies the tray path derives the target mute state and applies it.
    /// </summary>
    [Fact]
    public async Task ToggleFromTrayAsyncKnownStateSetsTargetMuteAsync()
    {
        FakeAudioMuteService audioMuteService = new FakeAudioMuteService(SoundMuteState.Unmuted, SoundMuteState.Muted);
        FakeSoundOffLedSession ledSession = new FakeSoundOffLedSession();
        SoundOffCoordinator coordinator = new SoundOffCoordinator(audioMuteService, ledSession, CreateLogger());

        await coordinator.ToggleFromTrayAsync(CancellationToken.None);

        Assert.True(audioMuteService.SetMutedCalled);
        Assert.True(audioMuteService.LastSetMutedValue);
        Assert.Contains(SoundMuteState.Muted, ledSession.AppliedStates);
        Assert.Equal(SoundMuteState.Muted, ledSession.AppliedStates[^1]);
    }

    /// <summary>
    /// Verifies the physical key path uses the last confirmed state for optimistic feedback.
    /// </summary>
    [Fact]
    public async Task HandlePhysicalKeyPressAsyncUsesOptimisticTargetAsync()
    {
        FakeAudioMuteService audioMuteService = new FakeAudioMuteService(SoundMuteState.Unmuted, SoundMuteState.Muted);
        FakeSoundOffLedSession ledSession = new FakeSoundOffLedSession();
        SoundOffCoordinator coordinator = new SoundOffCoordinator(audioMuteService, ledSession, CreateLogger());

        await coordinator.RefreshStateAsync(CancellationToken.None);
        ledSession.AppliedStates.Clear();

        await coordinator.HandlePhysicalKeyPressAsync(CancellationToken.None);

        Assert.True(ledSession.AppliedStates.Count >= 2);
        Assert.Equal(SoundMuteState.Muted, ledSession.AppliedStates[0]);
        Assert.Equal(SoundMuteState.Muted, ledSession.AppliedStates[^1]);
    }

    /// <summary>
    /// Creates a logger for test usage.
    /// </summary>
    /// <returns>The logger.</returns>
    private static Logger CreateLogger()
    {
        return LogManager.GetLogger(nameof(SoundOffCoordinatorTests));
    }

    /// <summary>
    /// Fake audio service for deterministic coordinator tests.
    /// </summary>
    private sealed class FakeAudioMuteService : IWindowsAudioMuteService
    {
        private readonly Queue<SoundMuteState> _states;

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeAudioMuteService"/> class.
        /// </summary>
        /// <param name="states">The queued mute states to return from reads.</param>
        public FakeAudioMuteService(params SoundMuteState[] states)
        {
            _states = new Queue<SoundMuteState>(states);
        }

        /// <summary>
        /// Gets a value indicating whether SetMuted was called.
        /// </summary>
        public bool SetMutedCalled { get; private set; }

        /// <summary>
        /// Gets the last mute value requested via SetMuted.
        /// </summary>
        public bool LastSetMutedValue { get; private set; }

        /// <summary>
        /// Reads the next queued mute state.
        /// </summary>
        /// <returns>The queued mute state, or the last known one when exhausted.</returns>
        public SoundMuteState TryGetMuteState()
        {
            if (_states.Count == 0)
            {
                return SoundMuteState.Unknown;
            }

            SoundMuteState state = _states.Dequeue();
            if (_states.Count == 0)
            {
                _states.Enqueue(state);
            }

            return state;
        }

        /// <summary>
        /// Records a mute write request.
        /// </summary>
        /// <param name="muted">True to mute; false to unmute.</param>
        /// <returns>Always true for the fake.</returns>
        public bool TrySetMuted(bool muted)
        {
            SetMutedCalled = true;
            LastSetMutedValue = muted;
            return true;
        }
    }

    /// <summary>
    /// Fake Sound Off LED session for coordinator tests.
    /// </summary>
    private sealed class FakeSoundOffLedSession : ISoundOffLedSession
    {
        /// <summary>
        /// Gets a value indicating whether initialization was requested.
        /// </summary>
        public bool EnsureInitializedCalled { get; private set; }

        /// <summary>
        /// Gets the sequence of applied LED states.
        /// </summary>
        public List<SoundMuteState> AppliedStates { get; } = new List<SoundMuteState>();

        /// <summary>
        /// Records an initialization request.
        /// </summary>
        public void EnsureInitialized()
        {
            EnsureInitializedCalled = true;
        }

        /// <summary>
        /// Records the applied LED state.
        /// </summary>
        /// <param name="state">The state to record.</param>
        public void SetMuteState(SoundMuteState state)
        {
            AppliedStates.Add(state);
        }
    }
}
