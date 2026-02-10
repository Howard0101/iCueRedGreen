using System.Globalization;
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

            CueSdkLoader.Install(options.CueSdkPath);
            CueKeyController cue = CueKeyController.Initialize(logger);

            using var fritz = new FritzAhaClient(options, logger);
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                logger.Info("Cancellation requested.");
            };

            await RunAsync(cue, fritz, options.IntervalSeconds, logger, cts.Token).ConfigureAwait(false);
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
        CueKeyController cue,
        FritzAhaClient fritz,
        int intervalSeconds,
        Logger logger,
        CancellationToken cancellationToken)
    {
        string runningFilePath = GetRunningFilePath();
        CleanupStaleRunningFile(runningFilePath, logger);
        WriteHeartbeat(runningFilePath);

        SwitchState lastState = SwitchState.Unknown;
        if (IsStopRequested(runningFilePath, logger))
        {
            return;
        }

        lastState = await PollOnceAsync(cue, fritz, lastState, logger, cancellationToken).ConfigureAwait(false);
        WriteHeartbeat(runningFilePath);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            if (IsStopRequested(runningFilePath, logger))
            {
                break;
            }

            lastState = await PollOnceAsync(cue, fritz, lastState, logger, cancellationToken).ConfigureAwait(false);
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
        CueKeyController cue,
        FritzAhaClient fritz,
        SwitchState lastState,
        Logger logger,
        CancellationToken cancellationToken)
    {
        SwitchState newState = SwitchState.Unknown;

        try
        {
            bool isOn = await fritz.GetSwitchStateAsync(cancellationToken).ConfigureAwait(false);
            if (isOn)
            {
                cue.SetRed();
                newState = SwitchState.On;
            }
            else
            {
                cue.SetGreen();
                newState = SwitchState.Off;
            }

            LogRecoveryIfNeeded(logger);
        }
        catch (Exception ex)
        {
            LogErrorIfNeeded(ex, logger);
            try
            {
                cue.ReleaseControl();
                LogReleaseRecoveryIfNeeded(logger);
            }
            catch (Exception colorEx)
            {
                LogReleaseErrorIfNeeded(colorEx, logger);
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
}
