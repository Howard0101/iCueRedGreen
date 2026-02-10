using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using NLog;

namespace iCUERedGreen;

/// <summary>
/// Coordinates polling, LED control, and keypress toggles.
/// </summary>
public sealed class WorkerController
{
    private readonly WorkerSettings _settings;
    private readonly Logger _logger;
    private bool _wasInError;
    private string? _lastErrorSignature;
    private bool _wasInReleaseError;
    private string? _lastReleaseErrorSignature;
    private CancellationTokenSource? _runCts;
    private Task? _runTask;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerController"/> class.
    /// </summary>
    /// <param name="settings">The resolved settings.</param>
    /// <param name="logger">The logger to use.</param>
    public WorkerController(WorkerSettings settings, Logger logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.Validate();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Occurs when the switch state changes.
    /// </summary>
    public event EventHandler<SwitchStateChangedEventArgs>? SwitchStateChanged;

    /// <summary>
    /// Starts the worker loop.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the worker stops.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_runTask is not null)
        {
            throw new InvalidOperationException("Worker is already running.");
        }

        _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _runTask = RunInternalAsync(_runCts.Token);
        return _runTask;
    }

    /// <summary>
    /// Requests the worker to stop and waits for completion.
    /// </summary>
    /// <returns>A task that completes when the worker stops.</returns>
    public async Task StopAsync()
    {
        if (_runCts is null)
        {
            return;
        }

        _runCts.Cancel();
        if (_runTask is not null)
        {
            try
            {
                await _runTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping the worker loop.
            }
        }

        _runTask = null;
        _runCts.Dispose();
        _runCts = null;
    }

    /// <summary>
    /// Restarts the worker loop.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the worker restarts.</returns>
    public async Task RestartAsync(CancellationToken cancellationToken)
    {
        await StopAsync().ConfigureAwait(false);
        await StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs the polling loop.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the loop exits.</returns>
    private async Task RunInternalAsync(CancellationToken cancellationToken)
    {
        using FritzAhaClient fritz = new FritzAhaClient(_settings, _logger);
        CueSession cueSession = new CueSession(_settings, _logger);
        PollingCoordinator coordinator = new PollingCoordinator(this, fritz, cueSession, _logger);
        using KeyboardHookRunner? hook = _settings.ToggleOnKeypress
            ? KeyboardHookRunner.TryStart(
                () => coordinator.ToggleAndRefreshAsync(cancellationToken),
                _logger)
            : null;

        if (_settings.ToggleOnKeypress && hook is null)
        {
            _logger.Warn("Keyboard hook unavailable; --toggle-on-keypress disabled.");
        }
        else if (hook is not null)
        {
            _logger.Info("Keyboard hook enabled; Scroll Lock toggles the switch.");
        }

        string runningFilePath = GetRunningFilePath();
        CleanupStaleRunningFile(runningFilePath, _logger);
        WriteHeartbeat(runningFilePath);

        if (IsStopRequested(runningFilePath, _logger))
        {
            return;
        }

        await coordinator.PollAsync(cancellationToken).ConfigureAwait(false);
        WriteHeartbeat(runningFilePath);

        using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromSeconds(_settings.IntervalSeconds));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            if (IsStopRequested(runningFilePath, _logger))
            {
                break;
            }

            await coordinator.PollAsync(cancellationToken).ConfigureAwait(false);
            WriteHeartbeat(runningFilePath);
        }
    }
    /// <summary>
    /// Executes a single poll cycle.
    /// </summary>
    /// <param name="cueSession">The cue session manager.</param>
    /// <param name="fritz">The FRITZ!Box client.</param>
    /// <param name="lastState">The previous switch state.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated switch state.</returns>
    private async Task<SwitchState> PollOnceAsync(
        CueSession cueSession,
        FritzAhaClient fritz,
        SwitchState lastState,
        CancellationToken cancellationToken)
    {
        SwitchState newState = SwitchState.Unknown;
        cueSession.EnsureInitialized();

        try
        {
            bool isOn = await fritz.GetSwitchStateAsync(cancellationToken).ConfigureAwait(false);
            if (isOn)
            {
                cueSession.SetRed();
                newState = SwitchState.On;
            }
            else
            {
                cueSession.SetGreen();
                newState = SwitchState.Off;
            }

            LogRecoveryIfNeeded();
        }
        catch (Exception ex)
        {
            LogErrorIfNeeded(ex);
            if (cueSession.TryReleaseControl(out Exception? releaseError))
            {
                LogReleaseRecoveryIfNeeded();
            }
            else if (releaseError is not null)
            {
                LogReleaseErrorIfNeeded(releaseError);
            }

            newState = SwitchState.Unknown;
        }

        LogStateChangeIfNeeded(newState, lastState, cueSession);
        return newState;
    }

    /// <summary>
    /// Logs and broadcasts state changes when the switch state transitions.
    /// </summary>
    /// <param name="newState">The new switch state.</param>
    /// <param name="lastState">The previous switch state.</param>
    /// <param name="cueSession">The cue session manager.</param>
    private void LogStateChangeIfNeeded(SwitchState newState, SwitchState lastState, CueSession cueSession)
    {
        if (newState == lastState)
        {
            return;
        }

        string label = newState switch
        {
            SwitchState.On => "ON",
            SwitchState.Off => "OFF",
            _ => "UNKNOWN"
        };

        // Log only on transitions to reduce log noise.
        _logger.Info("Switch state: {0}", label);

        SwitchStateSnapshot snapshot = new SwitchStateSnapshot(newState, DateTimeOffset.UtcNow, cueSession.IsAvailable);
        OnSwitchStateChanged(new SwitchStateChangedEventArgs(lastState, snapshot));
    }

    /// <summary>
    /// Raises the <see cref="SwitchStateChanged"/> event.
    /// </summary>
    /// <param name="args">The event arguments.</param>
    private void OnSwitchStateChanged(SwitchStateChangedEventArgs args)
    {
        SwitchStateChanged?.Invoke(this, args);
    }

    /// <summary>
    /// Logs switch state failures only when the failure changes.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    private void LogErrorIfNeeded(Exception ex)
    {
        string signature = $"{ex.GetType().Name}:{ex.Message}";
        if (string.Equals(_lastErrorSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        _lastErrorSignature = signature;
        _wasInError = true;
        _logger.Error(ex, "Failed to retrieve switch state.");
    }

    /// <summary>
    /// Logs switch state recovery when errors clear.
    /// </summary>
    private void LogRecoveryIfNeeded()
    {
        if (!_wasInError)
        {
            return;
        }

        _wasInError = false;
        _lastErrorSignature = null;
        _logger.Info("Switch state recovered.");
    }

    /// <summary>
    /// Logs iCUE release errors only when the failure changes.
    /// </summary>
    /// <param name="ex">The release exception.</param>
    private void LogReleaseErrorIfNeeded(Exception ex)
    {
        string signature = $"{ex.GetType().Name}:{ex.Message}";
        if (string.Equals(_lastReleaseErrorSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        _lastReleaseErrorSignature = signature;
        _wasInReleaseError = true;
        _logger.Error(ex, "Failed to release iCUE control.");
    }

    /// <summary>
    /// Logs iCUE release recovery when a previous release failure is resolved.
    /// </summary>
    private void LogReleaseRecoveryIfNeeded()
    {
        if (!_wasInReleaseError)
        {
            return;
        }

        _wasInReleaseError = false;
        _lastReleaseErrorSignature = null;
        _logger.Info("iCUE release recovered.");
    }
    /// <summary>
    /// Gets the path for the running marker file.
    /// </summary>
    /// <returns>The full running file path.</returns>
    private static string GetRunningFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "running.txt");
    }

    /// <summary>
    /// Writes a heartbeat timestamp to the running marker file.
    /// </summary>
    /// <param name="path">The running file path.</param>
    private static void WriteHeartbeat(string path)
    {
        string payload = $"LastHeartbeatUtc: {DateTimeOffset.UtcNow:O}";
        File.WriteAllText(path, payload);
    }

    /// <summary>
    /// Removes a stale running marker file from a previous boot.
    /// </summary>
    /// <param name="path">The running file path.</param>
    /// <param name="logger">The logger to use.</param>
    private static void CleanupStaleRunningFile(string path, Logger logger)
    {
        if (!File.Exists(path))
        {
            return;
        }

        if (!TryReadHeartbeatUtc(path, out DateTimeOffset heartbeatUtc))
        {
            logger.Warn("Running file heartbeat invalid; deleting stale marker.");
            TryDeleteRunningFile(path, logger);
            return;
        }

        // Treat any heartbeat before the last boot as stale.
        DateTimeOffset bootTimeUtc = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
        if (heartbeatUtc < bootTimeUtc)
        {
            logger.Warn("Running file heartbeat predates last boot; deleting stale marker.");
            TryDeleteRunningFile(path, logger);
        }
    }

    /// <summary>
    /// Reads the heartbeat value from the running marker file.
    /// </summary>
    /// <param name="path">The running file path.</param>
    /// <param name="heartbeatUtc">The parsed heartbeat timestamp.</param>
    /// <returns>True when the heartbeat can be parsed; otherwise false.</returns>
    private static bool TryReadHeartbeatUtc(string path, out DateTimeOffset heartbeatUtc)
    {
        heartbeatUtc = default;

        string content;
        try
        {
            content = File.ReadAllText(path).Trim();
        }
        catch
        {
            return false;
        }

        const string Prefix = "LastHeartbeatUtc:";
        if (!content.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string value = content[Prefix.Length..].Trim();
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out heartbeatUtc);
    }

    /// <summary>
    /// Deletes the running marker file and logs a warning when deletion fails.
    /// </summary>
    /// <param name="path">The running file path.</param>
    /// <param name="logger">The logger to use.</param>
    private static void TryDeleteRunningFile(string path, Logger logger)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            logger.Warn(ex, "Failed to delete stale running file.");
        }
    }

    /// <summary>
    /// Checks whether the running marker file was removed.
    /// </summary>
    /// <param name="path">The running file path.</param>
    /// <param name="logger">The logger to use.</param>
    /// <returns>True when the app should stop.</returns>
    private static bool IsStopRequested(string path, Logger logger)
    {
        if (File.Exists(path))
        {
            return false;
        }

        logger.Info("Stop requested: running file removed.");
        return true;
    }

    /// <summary>
    /// Coordinates polling and keypress toggles without overlapping requests.
    /// </summary>
    private sealed class PollingCoordinator
    {
        private readonly WorkerController _owner;
        private readonly FritzAhaClient _fritz;
        private readonly CueSession _cueSession;
        private readonly Logger _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private SwitchState _lastState = SwitchState.Unknown;

        /// <summary>
        /// Initializes a new instance of the <see cref="PollingCoordinator"/> class.
        /// </summary>
        /// <param name="owner">The worker controller.</param>
        /// <param name="fritz">The FRITZ!Box client.</param>
        /// <param name="cueSession">The cue session manager.</param>
        /// <param name="logger">The logger to use.</param>
        public PollingCoordinator(WorkerController owner, FritzAhaClient fritz, CueSession cueSession, Logger logger)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _fritz = fritz ?? throw new ArgumentNullException(nameof(fritz));
            _cueSession = cueSession ?? throw new ArgumentNullException(nameof(cueSession));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Runs a single poll cycle.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when the poll finishes.</returns>
        public Task PollAsync(CancellationToken cancellationToken)
        {
            return ExecuteAsync(PollCoreAsync, cancellationToken);
        }

        /// <summary>
        /// Toggles the switch and refreshes the LED state.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when the toggle finishes.</returns>
        public Task ToggleAndRefreshAsync(CancellationToken cancellationToken)
        {
            return ExecuteAsync(ToggleCoreAsync, cancellationToken);
        }

        /// <summary>
        /// Executes a serialized operation against the FRITZ!Box API.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when the action finishes.</returns>
        private async Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await action(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Executes the polling logic.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when polling finishes.</returns>
        private async Task PollCoreAsync(CancellationToken cancellationToken)
        {
            _lastState = await _owner.PollOnceAsync(
                _cueSession,
                _fritz,
                _lastState,
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes the toggle and refresh logic.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A task that completes when the toggle finishes.</returns>
        private async Task ToggleCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _fritz.ToggleSwitchAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to toggle switch.");
                return;
            }

            _lastState = await _owner.PollOnceAsync(
                _cueSession,
                _fritz,
                _lastState,
                cancellationToken).ConfigureAwait(false);
        }
    }
    /// <summary>
    /// Manages iCUE availability and LED control state.
    /// </summary>
    private sealed class CueSession
    {
        private readonly WorkerSettings _settings;
        private readonly Logger _logger;
        private CueKeyController? _controller;
        private bool _wasUnavailable;
        private string? _lastUnavailableSignature;

        /// <summary>
        /// Initializes a new instance of the <see cref="CueSession"/> class.
        /// </summary>
        /// <param name="settings">Resolved settings.</param>
        /// <param name="logger">The logger to use.</param>
        public CueSession(WorkerSettings settings, Logger logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets a value indicating whether iCUE is available.
        /// </summary>
        public bool IsAvailable => _controller is not null;

        /// <summary>
        /// Ensures the iCUE SDK is initialized when available.
        /// </summary>
        public void EnsureInitialized()
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
                CueSdkLoader.Install(_settings.CueSdkPath);
                _controller = CueKeyController.Initialize(_logger);
                LogCueRecoveredIfNeeded();
            }
            catch (Exception ex)
            {
                HandleCueFailure(ex);
            }
        }

        /// <summary>
        /// Sets the LED to red when iCUE is available.
        /// </summary>
        public void SetRed()
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

                _controller.SetRed();
            }
            catch (Exception ex)
            {
                HandleCueFailure(ex);
            }
        }

        /// <summary>
        /// Sets the LED to green when iCUE is available.
        /// </summary>
        public void SetGreen()
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

                _controller.SetGreen();
            }
            catch (Exception ex)
            {
                HandleCueFailure(ex);
            }
        }

        /// <summary>
        /// Releases control back to iCUE when possible.
        /// </summary>
        /// <param name="error">The release error, when one occurs.</param>
        /// <returns>True when release succeeds; otherwise false.</returns>
        public bool TryReleaseControl(out Exception? error)
        {
            error = null;
            if (_controller is null)
            {
                return false;
            }

            try
            {
                if (!IsSessionHealthy())
                {
                    return false;
                }

                _controller.ReleaseControl();
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                HandleCueFailure(ex);
                return false;
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
                _logger.Debug(ex, "DEBUG: Failed to disconnect from iCUE.");
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
    }
    /// <summary>
    /// Runs a low-level keyboard hook to toggle the switch on Scroll Lock.
    /// </summary>
    private sealed class KeyboardHookRunner : IDisposable
    {
        private const int WhKeyboardLl = 13;
        private const int WmKeyDown = 0x0100;
        private const int WmKeyUp = 0x0101;
        private const int WmSysKeyDown = 0x0104;
        private const int WmSysKeyUp = 0x0105;
        private const uint WmQuit = 0x0012;
        private const uint VkScroll = 0x91;

        private readonly Func<Task> _onToggleAsync;
        private readonly Logger _logger;
        private readonly ManualResetEventSlim _startedEvent = new(false);
        private Thread? _thread;
        private IntPtr _hookHandle = IntPtr.Zero;
        private HookProc? _hookProc;
        private uint _threadId;
        private bool _scrollLockDown;
        private Exception? _startException;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyboardHookRunner"/> class.
        /// </summary>
        /// <param name="onToggleAsync">The async toggle handler.</param>
        /// <param name="logger">The logger to use.</param>
        private KeyboardHookRunner(Func<Task> onToggleAsync, Logger logger)
        {
            _onToggleAsync = onToggleAsync ?? throw new ArgumentNullException(nameof(onToggleAsync));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Starts the hook runner and returns it when successful.
        /// </summary>
        /// <param name="onToggleAsync">The async toggle handler.</param>
        /// <param name="logger">The logger to use.</param>
        /// <returns>The running hook instance, or null on failure.</returns>
        public static KeyboardHookRunner? TryStart(Func<Task> onToggleAsync, Logger logger)
        {
            KeyboardHookRunner runner = new KeyboardHookRunner(onToggleAsync, logger);
            if (!runner.Start())
            {
                runner.Dispose();
                return null;
            }

            return runner;
        }

        /// <summary>
        /// Releases resources and stops the hook thread.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_threadId != 0)
            {
                PostThreadMessage(_threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
            }

            if (_thread is not null && _thread.IsAlive)
            {
                _thread.Join(TimeSpan.FromSeconds(2));
            }

            _startedEvent.Dispose();
        }

        /// <summary>
        /// Starts the hook thread and waits for initialization.
        /// </summary>
        /// <returns>True when the hook starts; otherwise false.</returns>
        private bool Start()
        {
            _thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "iCUERedGreen.KeyboardHook"
            };
            _thread.Start();
            _startedEvent.Wait();

            if (_startException is not null)
            {
                _logger.Error(_startException, "Failed to start keyboard hook.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Initializes the hook and runs the message loop.
        /// </summary>
        private void ThreadMain()
        {
            try
            {
                _threadId = GetCurrentThreadId();
                _hookProc = HookCallback;
                IntPtr moduleHandle = GetModuleHandle(null);
                if (moduleHandle == IntPtr.Zero)
                {
                    _startException = new InvalidOperationException("Failed to resolve module handle for keyboard hook.");
                    return;
                }

                _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookProc, moduleHandle, 0);
                if (_hookHandle == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    _startException = new InvalidOperationException($"SetWindowsHookEx failed: {error}.");
                    return;
                }

                _startedEvent.Set();
                RunMessageLoop();
            }
            catch (Exception ex)
            {
                _startException = ex;
            }
            finally
            {
                _startedEvent.Set();
                if (_hookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookHandle);
                    _hookHandle = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Processes Windows messages while the hook is active.
        /// </summary>
        private void RunMessageLoop()
        {
            while (true)
            {
                int result = GetMessage(out Msg message, IntPtr.Zero, 0, 0);
                if (result <= 0)
                {
                    break;
                }

                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }

        /// <summary>
        /// Handles Scroll Lock keypresses and triggers toggles.
        /// </summary>
        /// <param name="code">The hook code.</param>
        /// <param name="wParam">The message identifier.</param>
        /// <param name="lParam">The message payload.</param>
        /// <returns>The hook result.</returns>
        private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                int message = wParam.ToInt32();
                if (message == WmKeyDown || message == WmSysKeyDown || message == WmKeyUp || message == WmSysKeyUp)
                {
                    var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                    if (data.vkCode == VkScroll)
                    {
                        if (message == WmKeyDown || message == WmSysKeyDown)
                        {
                            if (!_scrollLockDown)
                            {
                                _scrollLockDown = true;
                                QueueToggleAction();
                            }
                        }
                        else
                        {
                            _scrollLockDown = false;
                        }
                    }
                }
            }

            return CallNextHookEx(_hookHandle, code, wParam, lParam);
        }

        /// <summary>
        /// Executes the toggle handler without blocking the hook thread.
        /// </summary>
        private void QueueToggleAction()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _onToggleAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected when the worker stops.
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Toggle on keypress failed.");
                }
            });
        }

        /// <summary>
        /// Defines the low-level hook callback.
        /// </summary>
        /// <param name="code">The hook code.</param>
        /// <param name="wParam">The message identifier.</param>
        /// <param name="lParam">The message payload.</param>
        /// <returns>The hook result.</returns>
        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Defines a low-level keyboard hook payload.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct KbdLlHookStruct
        {
            /// <summary>
            /// The virtual key code.
            /// </summary>
            public uint vkCode;
            /// <summary>
            /// The hardware scan code.
            /// </summary>
            public uint scanCode;
            /// <summary>
            /// The hook flags.
            /// </summary>
            public uint flags;
            /// <summary>
            /// The timestamp for the event.
            /// </summary>
            public uint time;
            /// <summary>
            /// Extra info pointer.
            /// </summary>
            public UIntPtr dwExtraInfo;
        }

        /// <summary>
        /// Represents a point in a message.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            /// <summary>
            /// X coordinate.
            /// </summary>
            public int x;
            /// <summary>
            /// Y coordinate.
            /// </summary>
            public int y;
        }

        /// <summary>
        /// Defines a Windows message.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct Msg
        {
            /// <summary>
            /// Window handle.
            /// </summary>
            public IntPtr hwnd;
            /// <summary>
            /// Message identifier.
            /// </summary>
            public uint message;
            /// <summary>
            /// wParam payload.
            /// </summary>
            public UIntPtr wParam;
            /// <summary>
            /// lParam payload.
            /// </summary>
            public IntPtr lParam;
            /// <summary>
            /// Timestamp.
            /// </summary>
            public uint time;
            /// <summary>
            /// Cursor location.
            /// </summary>
            public Point pt;
        }

        /// <summary>
        /// Installs a low-level keyboard hook.
        /// </summary>
        /// <param name="idHook">The hook identifier.</param>
        /// <param name="callback">The hook callback.</param>
        /// <param name="moduleHandle">The module handle.</param>
        /// <param name="threadId">The thread identifier.</param>
        /// <returns>The hook handle.</returns>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr moduleHandle, uint threadId);

        /// <summary>
        /// Removes a previously installed hook.
        /// </summary>
        /// <param name="hookHandle">The hook handle.</param>
        /// <returns>True when successful; otherwise false.</returns>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

        /// <summary>
        /// Passes a hook event to the next hook.
        /// </summary>
        /// <param name="hookHandle">The hook handle.</param>
        /// <param name="code">The hook code.</param>
        /// <param name="wParam">The message identifier.</param>
        /// <param name="lParam">The message payload.</param>
        /// <returns>The hook result.</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Retrieves the next message from the queue.
        /// </summary>
        /// <param name="message">The message output.</param>
        /// <param name="windowHandle">The window handle filter.</param>
        /// <param name="minFilter">Minimum message id.</param>
        /// <param name="maxFilter">Maximum message id.</param>
        /// <returns>The message result.</returns>
        [DllImport("user32.dll")]
        private static extern int GetMessage(out Msg message, IntPtr windowHandle, uint minFilter, uint maxFilter);

        /// <summary>
        /// Translates message key data.
        /// </summary>
        /// <param name="message">The message to translate.</param>
        /// <returns>True when successful; otherwise false.</returns>
        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref Msg message);

        /// <summary>
        /// Dispatches a message to the window procedure.
        /// </summary>
        /// <param name="message">The message to dispatch.</param>
        /// <returns>The result pointer.</returns>
        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref Msg message);

        /// <summary>
        /// Posts a message to the specified thread.
        /// </summary>
        /// <param name="threadId">The target thread id.</param>
        /// <param name="msg">The message identifier.</param>
        /// <param name="wParam">The wParam payload.</param>
        /// <param name="lParam">The lParam payload.</param>
        /// <returns>True when successful; otherwise false.</returns>
        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// Gets the current thread identifier.
        /// </summary>
        /// <returns>The thread identifier.</returns>
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        /// <summary>
        /// Resolves a module handle for the current process.
        /// </summary>
        /// <param name="moduleName">The module name, or null for the current process.</param>
        /// <returns>The module handle.</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string? moduleName);
    }
}
