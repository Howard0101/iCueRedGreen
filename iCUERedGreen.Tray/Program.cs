using System.Threading;
using System.Windows.Forms;
using NLog;

namespace iCUERedGreen.Tray;

/// <summary>
/// Application entry point for the tray UI.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Runs the tray application.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        ConfigureLogging();
        Logger logger = LogManager.GetCurrentClassLogger();
        bool showDevUi = HasDevUiFlag(Environment.GetCommandLineArgs());

        try
        {
            // Log startup version for diagnostics.
            logger.Info("Start iCUERedGreen {0}", GetVersionString());
            ApplicationConfiguration.Initialize();
            using TrayApplicationContext context = new TrayApplicationContext(logger, showDevUi);
            Application.Run(context);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Fatal error.");
            MessageBox.Show(
                "The tray application encountered a fatal error. See the log for details.",
                "iCUERedGreen",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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
        NLog.Targets.Target? consoleTarget = config.FindTargetByName("console");
        if (consoleTarget is null)
        {
            return;
        }

        for (int i = config.LoggingRules.Count - 1; i >= 0; i--)
        {
            NLog.Config.LoggingRule rule = config.LoggingRules[i];
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
    /// Determines whether the dev UI should be visible.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns>True when the dev UI flag is present; otherwise false.</returns>
    private static bool HasDevUiFlag(string[] args)
    {
        if (args is null || args.Length == 0)
        {
            return false;
        }

        foreach (string arg in args)
        {
            if (string.Equals(arg, "--dev-ui", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the current version string.
    /// </summary>
    /// <returns>The version string.</returns>
    private static string GetVersionString()
    {
        System.Version? info = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return info?.ToString() ?? "unknown";
    }
}
