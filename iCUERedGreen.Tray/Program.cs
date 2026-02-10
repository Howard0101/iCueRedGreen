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

        try
        {
            ApplicationConfiguration.Initialize();
            using TrayApplicationContext context = new TrayApplicationContext(logger);
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
}
