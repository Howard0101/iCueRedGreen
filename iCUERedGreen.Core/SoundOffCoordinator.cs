using System.Threading;
using NLog;

namespace iCUERedGreen;

/// <summary>
/// Coordinates Windows mute state handling and dedicated Sound Off LED feedback.
/// </summary>
internal sealed class SoundOffCoordinator
{
    private static readonly TimeSpan PhysicalKeyReconcileDelay = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan TrayToggleReconcileDelay = TimeSpan.FromMilliseconds(75);

    private readonly IWindowsAudioMuteService _audioMuteService;
    private readonly ISoundOffLedSession _cueLightingSession;
    private readonly Logger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private SoundMuteState _lastConfirmedState = SoundMuteState.Unknown;
    private string? _lastFailureSignature;
    private bool _wasInFailure;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoundOffCoordinator"/> class.
    /// </summary>
    /// <param name="audioMuteService">The audio mute service.</param>
    /// <param name="cueLightingSession">The shared iCUE lighting session.</param>
    /// <param name="logger">The logger to use.</param>
    public SoundOffCoordinator(IWindowsAudioMuteService audioMuteService, ISoundOffLedSession cueLightingSession, Logger logger)
    {
        _audioMuteService = audioMuteService ?? throw new ArgumentNullException(nameof(audioMuteService));
        _cueLightingSession = cueLightingSession ?? throw new ArgumentNullException(nameof(cueLightingSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Occurs when the displayed mute state changes (optimistic or confirmed).
    /// </summary>
    public event EventHandler<SoundMuteState>? MuteStateChanged;

    /// <summary>
    /// Refreshes the confirmed Windows mute state and LED feedback.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the refresh finishes.</returns>
    public Task RefreshStateAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplyConfirmedState(_audioMuteService.TryGetMuteState());
            return Task.CompletedTask;
        }, cancellationToken);
    }

    /// <summary>
    /// Handles the physical Volume Mute key press path.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when reconciliation finishes.</returns>
    public Task HandlePhysicalKeyPressAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            SoundMuteState optimisticState = Invert(_lastConfirmedState);
            if (optimisticState != SoundMuteState.Unknown)
            {
                ApplyLedState(optimisticState);
            }

            await Task.Delay(PhysicalKeyReconcileDelay, cancellationToken).ConfigureAwait(false);
            ApplyConfirmedState(_audioMuteService.TryGetMuteState());
        }, cancellationToken);
    }

    /// <summary>
    /// Handles the tray-menu toggle path.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the toggle finishes.</returns>
    public Task ToggleFromTrayAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync(async () =>
        {
            SoundMuteState currentState = _audioMuteService.TryGetMuteState();
            if (currentState == SoundMuteState.Unknown)
            {
                ApplyConfirmedState(SoundMuteState.Unknown);
                return;
            }

            SoundMuteState targetState = Invert(currentState);
            bool targetMuted = targetState == SoundMuteState.Muted;

            ApplyLedState(targetState);
            if (!_audioMuteService.TrySetMuted(targetMuted))
            {
                ApplyConfirmedState(SoundMuteState.Unknown);
                return;
            }

            await Task.Delay(TrayToggleReconcileDelay, cancellationToken).ConfigureAwait(false);
            ApplyConfirmedState(_audioMuteService.TryGetMuteState());
        }, cancellationToken);
    }

    /// <summary>
    /// Executes a serialized Sound Off action.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the action finishes.</returns>
    private async Task ExecuteAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Applies the confirmed state to the LED and failure log state.
    /// </summary>
    /// <param name="state">The confirmed state.</param>
    private void ApplyConfirmedState(SoundMuteState state)
    {
        _lastConfirmedState = state;
        ApplyLedState(state);

        if (state == SoundMuteState.Unknown)
        {
            LogFailureIfNeeded("SoundOffStateUnknown", "Windows global audio mute state unavailable; Sound Off LED returned to neutral.");
            return;
        }

        LogRecoveryIfNeeded();
    }

    /// <summary>
    /// Applies the current Sound Off LED state.
    /// </summary>
    /// <param name="state">The state to show.</param>
    private void ApplyLedState(SoundMuteState state)
    {
        _cueLightingSession.EnsureInitialized();
        _cueLightingSession.SetMuteState(state);
        MuteStateChanged?.Invoke(this, state);
    }

    /// <summary>
    /// Logs a failure only when the failure condition changes.
    /// </summary>
    /// <param name="signature">The failure signature.</param>
    /// <param name="message">The message to log.</param>
    private void LogFailureIfNeeded(string signature, string message)
    {
        if (string.Equals(_lastFailureSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        _lastFailureSignature = signature;
        _wasInFailure = true;
        _logger.Warn(message);
    }

    /// <summary>
    /// Logs recovery when a prior failure clears.
    /// </summary>
    private void LogRecoveryIfNeeded()
    {
        if (!_wasInFailure)
        {
            return;
        }

        _wasInFailure = false;
        _lastFailureSignature = null;
        _logger.Info("Windows global audio mute state recovered.");
    }

    /// <summary>
    /// Inverts a known mute state.
    /// </summary>
    /// <param name="state">The state to invert.</param>
    /// <returns>The inverted state, or <see cref="SoundMuteState.Unknown"/> when the source state is unknown.</returns>
    private static SoundMuteState Invert(SoundMuteState state)
    {
        return state switch
        {
            SoundMuteState.Muted => SoundMuteState.Unmuted,
            SoundMuteState.Unmuted => SoundMuteState.Muted,
            _ => SoundMuteState.Unknown
        };
    }
}
