using System.Threading;
using NLog;

namespace iCUERedGreen;

/// <summary>
/// Application entry point.
/// </summary>
internal static class Program
{
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

            WorkerSettings settings = new WorkerSettings
            {
                IntervalSeconds = options.IntervalSeconds,
                FritzHost = options.FritzHost,
                FritzUsername = options.FritzUsername,
                FritzPassword = options.FritzPassword,
                FritzAin = options.FritzAin,
                CueSdkPath = options.CueSdkPath,
                ToggleOnKeypress = options.ToggleOnKeypress
            };

            WorkerController controller = new WorkerController(settings, logger);
            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
                logger.Info("Cancellation requested.");
            };

            await controller.StartAsync(cts.Token).ConfigureAwait(false);
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
        NLog.Config.LoggingConfiguration? config = LogManager.Configuration;
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
    /// Determines whether the current session is interactive.
    /// </summary>
    /// <returns>True when running interactively; otherwise false.</returns>
    private static bool IsInteractiveSession()
    {
        return Environment.UserInteractive
            && !Console.IsOutputRedirected
            && !Console.IsErrorRedirected;
    }

    /// <summary>
    /// Removes console logging targets from the NLog configuration.
    /// </summary>
    /// <param name="config">The logging configuration.</param>
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

    /// <summary>
    /// Prints the application version.
    /// </summary>
    private static void PrintVersion()
    {
        Console.WriteLine(GetVersionString());
    }

    /// <summary>
    /// Gets the current version string.
    /// </summary>
    /// <returns>The version string.</returns>
    private static string GetVersionString()
    {
        var info = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return info?.ToString() ?? "unknown";
    }
}
