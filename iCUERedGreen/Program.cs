using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using NLog;

namespace iCUERedGreen;

/// <summary>
/// Application entry point.
/// </summary>
internal static class Program
{
    private static bool _wasInError;
    private static string? _lastErrorSignature;
    private static bool _wasInReleaseError;
    private static string? _lastReleaseErrorSignature;

    private enum SwitchState
    {
        Unknown,
        Off,
        On
    }

    /// <summary>
    /// Runs the application.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> Main(string[] args)
    {
        ConfigureLogging();
        Logger logger = LogManager.GetCurrentClassLogger();

        try
        {
            Options options = Options.Load(args, out bool showHelp);
            if (showHelp)
            {
                PrintUsage();
                return 0;
            }

            if (options.ShowVersion)
            {
                PrintVersion();
                return 0;
            }

            options.Validate();

            logger.Info("Starting iCUERedGreen {0}", GetVersionString());
            logger.Info("FRITZ host: {0}", options.FritzHost);
            logger.Info("FRITZ username: {0}", options.FritzUsername);
            logger.Info("FRITZ AIN: {0}", options.FritzAin);
            logger.Info("Polling interval: {0} seconds", options.IntervalSeconds);
            logger.Info("FRITZ password length: {0}", options.FritzPassword?.Length ?? 0);

            using var fritz = new FritzAhaClient(options, logger);
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                logger.Info("Cancellation requested.");
            };

            await RunAsync(fritz, options, logger, cts.Token).ConfigureAwait(false);
            return 0;
        }
        catch (NotSupportedException ex)
        {
            logger.Error(ex, "Unsupported option.");
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Fatal error.");
            return 2;
        }
        finally
        {
            LogManager.Shutdown();
        }
    }

    /// <summary>
    /// Configures NLog using the local configuration file.
    /// </summary>
    private static void ConfigureLogging()
    {
        LogManager.Setup().LoadConfigurationFromFile("nlog.config", optional: true);
        var config = LogManager.Configuration;
        if (config is null)
        {
            return;
        }

        if (!IsInteractiveSession())
        {
            // Keep console logging only for interactive runs.
            RemoveConsoleTarget(config);
            LogManager.Configuration = config;
        }
    }

    /// <summary>
    /// Runs the polling loop.
    /// </summary>
    /// <param name="cue">The cue controller.</param>
    /// <param name="fritz">The FRITZ!Box client.</param>
    /// <param name="intervalSeconds">The polling interval in seconds.</param>
    /// <param name="logger">The logger to use.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the loop exits.</returns>
    private static async Task RunAsync(
        FritzAhaClient fritz,
        Options options,
        Logger logger,
        CancellationToken cancellationToken)
    {
        var cueSession = new CueSession(options, logger);
        var coordinator = new PollingCoordinator(fritz, cueSession, logger);
        using KeyboardHookRunner? hook = options.ToggleOnKeypress
            ? KeyboardHookRunner.TryStart(
                () => coordinator.ToggleAndRefreshAsync(cancellationToken),
                logger)
            : null;

        if (options.ToggleOnKeypress && hook is null)
        {
            logger.Warn("Keyboard hook unavailable; --toggle-on-keypress disabled.");
        }
        else if (hook is not null)
        {
            logger.Info("Keyboard hook enabled; Scroll Lock toggles the switch.");
        }

        string runningFilePath = GetRunningFilePath();
        CleanupStaleRunningFile(runningFilePath, logger);
        WriteHeartbeat(runningFilePath);

        if (IsStopRequested(runningFilePath, logger))
        {
            return;
        }

        await coordinator.PollAsync(cancellationToken).ConfigureAwait(false);
        WriteHeartbeat(runningFilePath);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.IntervalSeconds));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            if (IsStopRequested(runningFilePath, logger))
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
    /// <param name="cue">The cue controller.</param>
    /// <param name="fritz">The FRITZ!Box client.</param>
    /// <param name="lastState">The previous switch state.</param>
    /// <param name="logger">The logger to use.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated switch state.</returns>
    private static async Task<SwitchState> PollOnceAsync(
        CueSession cueSession,
        FritzAhaClient fritz,
        SwitchState lastState,
        Logger logger,
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

            LogRecoveryIfNeeded(logger);
        }
        catch (Exception ex)
        {
            LogErrorIfNeeded(ex, logger);
            if (cueSession.TryReleaseControl(out Exception? releaseError))
            {
                LogReleaseRecoveryIfNeeded(logger);
            }
            else if (releaseError is not null)
            {
                LogReleaseErrorIfNeeded(releaseError, logger);
            }
            newState = SwitchState.Unknown;
        }

        LogStateChangeIfNeeded(newState, lastState, logger);
        return newState;
    }

    private static void LogStateChangeIfNeeded(SwitchState newState, SwitchState lastState, Logger logger)
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
        logger.Info("Switch state: {0}", label);
    }

    private static void LogErrorIfNeeded(Exception ex, Logger logger)
    {
        string signature = $"{ex.GetType().Name}:{ex.Message}";
        if (string.Equals(_lastErrorSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        _lastErrorSignature = signature;
        _wasInError = true;
        logger.Error(ex, "Failed to retrieve switch state.");
    }

    private static void LogRecoveryIfNeeded(Logger logger)
    {
        if (!_wasInError)
        {
            return;
        }

        _wasInError = false;
        _lastErrorSignature = null;
        logger.Info("Switch state recovered.");
    }

    /// <summary>
    /// Logs iCUE release errors only when the failure changes.
    /// </summary>
    /// <param name="ex">The release exception.</param>
    /// <param name="logger">The logger to use.</param>
    private static void LogReleaseErrorIfNeeded(Exception ex, Logger logger)
    {
        string signature = $"{ex.GetType().Name}:{ex.Message}";
        if (string.Equals(_lastReleaseErrorSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        _lastReleaseErrorSignature = signature;
        _wasInReleaseError = true;
        logger.Error(ex, "Failed to release iCUE control.");
    }

    /// <summary>
    /// Logs iCUE release recovery when a previous release failure is resolved.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    private static void LogReleaseRecoveryIfNeeded(Logger logger)
    {
        if (!_wasInReleaseError)
        {
            return;
        }

        _wasInReleaseError = false;
        _lastReleaseErrorSignature = null;
        logger.Info("iCUE release recovered.");
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

    private static bool IsInteractiveSession()
    {
        return Environment.UserInteractive
            && !Console.IsOutputRedirected
            && !Console.IsErrorRedirected;
    }

    private static void RemoveConsoleTarget(NLog.Config.LoggingConfiguration config)
    {
        var consoleTarget = config.FindTargetByName("console");
        if (consoleTarget is null)
        {
            return;
        }

        for (int i = config.LoggingRules.Count - 1; i >= 0; i--)
        {
            var rule = config.LoggingRules[i];
            if (rule.Targets.Contains(consoleTarget))
            {
                rule.Targets.Remove(consoleTarget);
            }

            if (rule.Targets.Count == 0)
            {
                config.LoggingRules.RemoveAt(i);
            }
        }

        config.RemoveTarget("console");
    }

    /// <summary>
    /// Prints command line usage information.
    /// </summary>
    private static void PrintUsage()
    {
        Console.WriteLine("iCUERedGreen - FRITZ!DECT 200 status to iCUE Scroll Lock LED");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  iCUERedGreen.exe [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --interval <sec>       Polling interval in seconds (default 5)");
        Console.WriteLine("  --host <host>          FRITZ!Box host (e.g., fritz.box)");
        Console.WriteLine("  --username <user>      FRITZ!Box username");
        Console.WriteLine("  --ain <ain>            FRITZ!DECT AIN (include spaces)");
        Console.WriteLine("  --cuesdk-path <path>   Path to CUE SDK DLL");
        Console.WriteLine("  --toggle-on-keypress   Toggle the switch on Scroll Lock keypress");
        Console.WriteLine("  --no-toggle            Reserved for future use");
        Console.WriteLine("  --version              Show version information");
        Console.WriteLine("  --help                 Show this help");
        Console.WriteLine();
        Console.WriteLine("Environment variables:");
        Console.WriteLine("  FRITZ_HOST, FRITZ_USERNAME, FRITZ_PASSWORD, FRITZ_AIN, POLL_INTERVAL_SECONDS");
        Console.WriteLine();
        Console.WriteLine("Config file:");
        Console.WriteLine("  appsettings.json (optional, located next to the executable)");
    }

    private static void PrintVersion()
    {
        Console.WriteLine(GetVersionString());
    }

    private static string GetVersionString()
    {
        var info = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return info?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Coordinates polling and keypress toggles without overlapping requests.
    /// </summary>
    private sealed class PollingCoordinator
    {
        private readonly FritzAhaClient _fritz;
        private readonly CueSession _cueSession;
        private readonly Logger _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private SwitchState _lastState = SwitchState.Unknown;

        /// <summary>
        /// Initializes a new instance of the <see cref="PollingCoordinator"/> class.
        /// </summary>
        /// <param name="fritz">The FRITZ!Box client.</param>
        /// <param name="cueSession">The cue session manager.</param>
        /// <param name="logger">The logger to use.</param>
        public PollingCoordinator(FritzAhaClient fritz, CueSession cueSession, Logger logger)
        {
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
            _lastState = await PollOnceAsync(
                _cueSession,
                _fritz,
                _lastState,
                _logger,
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

            _lastState = await PollOnceAsync(
                _cueSession,
                _fritz,
                _lastState,
                _logger,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Manages iCUE availability and LED control state.
    /// </summary>
    private sealed class CueSession
    {
        private readonly Options _options;
        private readonly Logger _logger;
        private CueKeyController? _controller;
        private bool _wasUnavailable;
        private string? _lastUnavailableSignature;

        /// <summary>
        /// Initializes a new instance of the <see cref="CueSession"/> class.
        /// </summary>
        /// <param name="options">Resolved options.</param>
        /// <param name="logger">The logger to use.</param>
        public CueSession(Options options, Logger logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Ensures the iCUE SDK is initialized when available.
        /// </summary>
        public void EnsureInitialized()
        {
            if (_controller is not null)
            {
                return;
            }

            try
            {
                CueSdkLoader.Install(_options.CueSdkPath);
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

            _wasUnavailable = true;
            _controller = null;
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
            var runner = new KeyboardHookRunner(onToggleAsync, logger);
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
