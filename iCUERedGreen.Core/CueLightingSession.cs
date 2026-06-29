using NLog;

namespace iCUERedGreen;

/// <summary>
/// Abstraction for the dedicated Sound Off LED path.
/// </summary>
internal interface ISoundOffLedSession
{
    /// <summary>
    /// Ensures the iCUE session is initialized when available.
    /// </summary>
    void EnsureInitialized();

    /// <summary>
    /// Applies the Sound Off LED state.
    /// </summary>
    /// <param name="state">The mute state to represent.</param>
    void SetMuteState(SoundMuteState state);
}

/// <summary>
/// Coordinates shared iCUE LED access for the process.
/// </summary>
internal sealed class CueLightingSession : ISoundOffLedSession
{
    private readonly Logger _logger;
    private readonly object _sync = new();
    private string? _cueSdkPath;
    private CueKeyController? _controller;
    private bool _wasUnavailable;
    private string? _lastUnavailableSignature;
    private bool _muteCapabilityLogged;

    /// <summary>
    /// Initializes a new instance of the <see cref="CueLightingSession"/> class.
    /// </summary>
    /// <param name="cueSdkPath">The optional explicit SDK DLL path.</param>
    /// <param name="logger">The logger to use.</param>
    public CueLightingSession(string? cueSdkPath, Logger logger)
    {
        _cueSdkPath = cueSdkPath;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets a value indicating whether iCUE is currently available.
    /// </summary>
    public bool IsAvailable
    {
        get
        {
            lock (_sync)
            {
                return _controller is not null;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the current keyboard exposes the Volume Mute LED.
    /// </summary>
    public bool SupportsMuteKey
    {
        get
        {
            lock (_sync)
            {
                return _controller?.HasMuteKey ?? false;
            }
        }
    }

    /// <summary>
    /// Updates the explicit SDK DLL path for future initialization attempts.
    /// </summary>
    /// <param name="cueSdkPath">The optional explicit path.</param>
    public void UpdateCueSdkPath(string? cueSdkPath)
    {
        lock (_sync)
        {
            _cueSdkPath = cueSdkPath;
        }
    }

    /// <summary>
    /// Ensures that the iCUE SDK session is initialized when available.
    /// </summary>
    public void EnsureInitialized()
    {
        lock (_sync)
        {
            if (_controller is not null)
            {
                if (!IsSessionHealthy())
                {
                    HandleCueFailure(new InvalidOperationException("iCUE session is not connected."));
                }

                return;
            }

            try
            {
                CueSdkLoader.Install(_cueSdkPath);
                _controller = CueKeyController.Initialize(_logger);
                LogCueRecoveredIfNeeded();
                LogMuteCapabilityIfNeeded();
            }
            catch (Exception ex)
            {
                HandleCueFailure(ex);
            }
        }
    }

    /// <summary>
    /// Applies the Scroll Lock LED state.
    /// </summary>
    /// <param name="state">The switch state to represent.</param>
    public void SetScrollLockState(SwitchState state)
    {
        lock (_sync)
        {
            if (_controller is null)
            {
                return;
            }

            try
            {
                if (!IsSessionHealthy())
                {
                    return;
                }

                _controller.SetScrollLockState(state);
            }
            catch (Exception ex)
            {
                HandleCueFailure(ex);
            }
        }
    }

    /// <summary>
    /// Applies the Sound Off LED state.
    /// </summary>
    /// <param name="state">The mute state to represent.</param>
    public void SetMuteState(SoundMuteState state)
    {
        lock (_sync)
        {
            if (_controller is null)
            {
                return;
            }

            try
            {
                if (!IsSessionHealthy())
                {
                    return;
                }

                if (!_controller.HasMuteKey)
                {
                    LogMuteCapabilityIfNeeded();
                    return;
                }

                _controller.SetMuteState(state);
            }
            catch (Exception ex)
            {
                HandleCueFailure(ex);
            }
        }
    }

    /// <summary>
    /// Logs iCUE unavailability and clears the active controller.
    /// </summary>
    /// <param name="ex">The exception that triggered the failure.</param>
    private void HandleCueFailure(Exception ex)
    {
        string signature = $"{ex.GetType().Name}:{ex.Message}";
        if (!string.Equals(_lastUnavailableSignature, signature, StringComparison.Ordinal))
        {
            _lastUnavailableSignature = signature;
            _logger.Warn(ex, "iCUE not available; running without LED control.");
        }

        TryDisconnect();
        _wasUnavailable = true;
        _controller = null;
        _muteCapabilityLogged = false;
    }

    /// <summary>
    /// Checks whether the iCUE session is healthy and connected.
    /// </summary>
    /// <returns>True when connected; otherwise false.</returns>
    private bool IsSessionHealthy()
    {
        try
        {
            CorsairError result = CorsairNative.CorsairGetSessionDetails(out _);
            if (result == CorsairError.CE_Success)
            {
                return true;
            }

            CorsairNative.CorsairDisconnect();
            HandleCueFailure(new InvalidOperationException($"CorsairGetSessionDetails failed: {result}."));
            return false;
        }
        catch (Exception ex)
        {
            HandleCueFailure(ex);
            return false;
        }
    }

    /// <summary>
    /// Attempts to disconnect from iCUE to reset the SDK state.
    /// </summary>
    private void TryDisconnect()
    {
        try
        {
            CorsairNative.CorsairDisconnect();
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to disconnect from iCUE.");
        }
    }

    /// <summary>
    /// Logs recovery when iCUE becomes available again.
    /// </summary>
    private void LogCueRecoveredIfNeeded()
    {
        if (!_wasUnavailable)
        {
            return;
        }

        _wasUnavailable = false;
        _lastUnavailableSignature = null;
        _logger.Info("iCUE available; LED control enabled.");
    }

    /// <summary>
    /// Logs the mute-key capability once after initialization.
    /// </summary>
    private void LogMuteCapabilityIfNeeded()
    {
        if (_controller is null || _muteCapabilityLogged)
        {
            return;
        }

        _muteCapabilityLogged = true;
        if (_controller.HasMuteKey)
        {
            _logger.Info("Sound Off LED verified: CLK_Mute (100) available on keyboard device.");
        }
        else
        {
            _logger.Warn("Sound Off LED unavailable: iCUE keyboard does not expose CLK_Mute (100). Sound Off lighting stays neutral.");
        }
    }
}
