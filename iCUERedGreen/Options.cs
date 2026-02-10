using System.Globalization;
using System.Text.Json;

namespace iCUERedGreen;

/// <summary>
/// Provides configuration values resolved from CLI arguments, environment variables, and appsettings.json.
/// </summary>
internal sealed class Options
{
    /// <summary>
    /// Gets the polling interval in seconds.
    /// </summary>
    public int IntervalSeconds { get; init; } = 5;

    /// <summary>
    /// Gets the FRITZ!Box host name or IP address.
    /// </summary>
    public string? FritzHost { get; init; }

    /// <summary>
    /// Gets the FRITZ!Box user name.
    /// </summary>
    public string? FritzUsername { get; init; }

    /// <summary>
    /// Gets the FRITZ!Box password.
    /// </summary>
    public string? FritzPassword { get; init; }

    /// <summary>
    /// Gets the AIN of the FRITZ!DECT 200 device.
    /// </summary>
    public string? FritzAin { get; init; }

    /// <summary>
    /// Gets the optional explicit path to the CUE SDK DLL.
    /// </summary>
    public string? CueSdkPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether the switch should toggle on keypress.
    /// </summary>
    public bool ToggleOnKeypress { get; init; }

    /// <summary>
    /// Loads options using the precedence CLI &gt; environment &gt; appsettings.json &gt; defaults.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <param name="showHelp">Outputs whether help was requested.</param>
    /// <returns>The resolved options.</returns>
    /// <exception cref="ArgumentException">Thrown when CLI arguments are invalid.</exception>
    /// <exception cref="NotSupportedException">Thrown when unsupported switches are used.</exception>
    public static Options Load(string[] args, out bool showHelp)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        CliOptions cli = ParseArgs(args);
        showHelp = cli.ShowHelp;

        AppSettings? appSettings = TryReadAppSettings();

        int intervalSeconds = 5;
        string? host = null;
        string? username = null;
        string? password = null;
        string? ain = null;
        string? cueSdkPath = null;

        if (appSettings?.Polling?.IntervalSeconds is int appInterval)
        {
            intervalSeconds = appInterval;
        }

        if (!string.IsNullOrWhiteSpace(appSettings?.Fritz?.Host))
        {
            host = appSettings!.Fritz!.Host;
        }

        if (!string.IsNullOrWhiteSpace(appSettings?.Fritz?.Username))
        {
            username = appSettings!.Fritz!.Username;
        }

        if (!string.IsNullOrWhiteSpace(appSettings?.Fritz?.Password))
        {
            password = appSettings!.Fritz!.Password;
        }

        if (!string.IsNullOrWhiteSpace(appSettings?.Fritz?.Ain))
        {
            ain = appSettings!.Fritz!.Ain;
        }

        if (!string.IsNullOrWhiteSpace(appSettings?.CueSdk?.Path))
        {
            cueSdkPath = appSettings!.CueSdk!.Path;
        }

        string? envInterval = GetEnvironmentVariable("POLL_INTERVAL_SECONDS");
        if (!string.IsNullOrWhiteSpace(envInterval))
        {
            if (!int.TryParse(envInterval, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInterval))
            {
                throw new ArgumentException("POLL_INTERVAL_SECONDS must be a valid integer.");
            }

            intervalSeconds = parsedInterval;
        }

        host = GetEnvironmentVariable("FRITZ_HOST") ?? host;
        username = GetEnvironmentVariable("FRITZ_USERNAME") ?? username;
        password = GetEnvironmentVariable("FRITZ_PASSWORD") ?? password;
        ain = GetEnvironmentVariable("FRITZ_AIN") ?? ain;

        if (cli.IntervalSeconds.HasValue)
        {
            intervalSeconds = cli.IntervalSeconds.Value;
        }

        host = cli.Host ?? host;
        username = cli.Username ?? username;
        ain = cli.Ain ?? ain;
        cueSdkPath = cli.CueSdkPath ?? cueSdkPath;

        return new Options
        {
            IntervalSeconds = intervalSeconds,
            FritzHost = host,
            FritzUsername = username,
            FritzPassword = password,
            FritzAin = ain,
            CueSdkPath = cueSdkPath,
            ShowVersion = cli.ShowVersion,
            ToggleOnKeypress = cli.ToggleOnKeypress
        };
    }

    /// <summary>
    /// Gets a value indicating whether version output was requested.
    /// </summary>
    public bool ShowVersion { get; init; }

    /// <summary>
    /// Validates that required configuration is present and consistent.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when required values are missing.</exception>
    public void Validate()
    {
        if (IntervalSeconds <= 0)
        {
            throw new InvalidOperationException("IntervalSeconds must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(FritzHost))
        {
            throw new InvalidOperationException("FRITZ host is required. Use FRITZ_HOST or --host.");
        }

        if (string.IsNullOrWhiteSpace(FritzUsername))
        {
            throw new InvalidOperationException("FRITZ username is required. Use FRITZ_USERNAME or --username.");
        }

        if (string.IsNullOrWhiteSpace(FritzPassword))
        {
            throw new InvalidOperationException("FRITZ password is required. Use FRITZ_PASSWORD.");
        }

        if (string.IsNullOrWhiteSpace(FritzAin))
        {
            throw new InvalidOperationException("FRITZ AIN is required. Use FRITZ_AIN or --ain.");
        }
    }

    /// <summary>
    /// Parses CLI arguments into a structured options model.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>The parsed CLI options.</returns>
    /// <exception cref="ArgumentException">Thrown when arguments are malformed.</exception>
    private static CliOptions ParseArgs(string[] args)
    {
        int? intervalSeconds = null;
        string? host = null;
        string? username = null;
        string? ain = null;
        string? cueSdkPath = null;
        bool showHelp = false;
        bool showVersion = false;
        bool toggleOnKeypress = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "/?", StringComparison.OrdinalIgnoreCase))
            {
                showHelp = true;
                continue;
            }

            if (string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase))
            {
                showVersion = true;
                continue;
            }

            if (string.Equals(arg, "--interval", StringComparison.OrdinalIgnoreCase))
            {
                intervalSeconds = ReadIntValue(args, ref i, "--interval");
                continue;
            }

            if (string.Equals(arg, "--host", StringComparison.OrdinalIgnoreCase))
            {
                host = ReadStringValue(args, ref i, "--host");
                continue;
            }

            if (string.Equals(arg, "--username", StringComparison.OrdinalIgnoreCase))
            {
                username = ReadStringValue(args, ref i, "--username");
                continue;
            }

            if (string.Equals(arg, "--ain", StringComparison.OrdinalIgnoreCase))
            {
                ain = ReadStringValue(args, ref i, "--ain");
                continue;
            }

            if (string.Equals(arg, "--cuesdk-path", StringComparison.OrdinalIgnoreCase))
            {
                cueSdkPath = ReadStringValue(args, ref i, "--cuesdk-path");
                continue;
            }

            if (string.Equals(arg, "--no-toggle", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(arg, "--toggle-on-keypress", StringComparison.OrdinalIgnoreCase))
            {
                toggleOnKeypress = true;
                continue;
            }

            throw new ArgumentException($"Unknown argument: {arg}");
        }

        return new CliOptions
        {
            IntervalSeconds = intervalSeconds,
            Host = host,
            Username = username,
            Ain = ain,
            CueSdkPath = cueSdkPath,
            ShowHelp = showHelp,
            ShowVersion = showVersion,
            ToggleOnKeypress = toggleOnKeypress
        };
    }

    /// <summary>
    /// Reads a required integer value from the argument list.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <param name="index">The index of the current switch (incremented when value is read).</param>
    /// <param name="name">The switch name for error messages.</param>
    /// <returns>The parsed integer value.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is missing or invalid.</exception>
    private static int ReadIntValue(string[] args, ref int index, string name)
    {
        string raw = ReadStringValue(args, ref index, name);
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new ArgumentException($"Invalid value for {name}: {raw}");
        }

        return value;
    }

    /// <summary>
    /// Reads a required string value from the argument list.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <param name="index">The index of the current switch (incremented when value is read).</param>
    /// <param name="name">The switch name for error messages.</param>
    /// <returns>The parsed string value.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is missing.</exception>
    private static string ReadStringValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {name}.");
        }

        index++;
        return args[index];
    }

    /// <summary>
    /// Reads appsettings.json from the application base directory when present.
    /// </summary>
    /// <returns>The deserialized settings, or null if none exist.</returns>
    private static AppSettings? TryReadAppSettings()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
        {
            return null;
        }

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    /// <summary>
    /// Returns a trimmed environment variable value if present.
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <returns>The trimmed value or null.</returns>
    private static string? GetEnvironmentVariable(string name)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    /// <summary>
    /// Holds parsed CLI arguments.
    /// </summary>
    private sealed class CliOptions
    {
        /// <summary>
        /// Gets the CLI override interval.
        /// </summary>
        public int? IntervalSeconds { get; init; }

        /// <summary>
        /// Gets the CLI override host.
        /// </summary>
        public string? Host { get; init; }

        /// <summary>
        /// Gets the CLI override username.
        /// </summary>
        public string? Username { get; init; }

        /// <summary>
        /// Gets the CLI override AIN.
        /// </summary>
        public string? Ain { get; init; }

        /// <summary>
        /// Gets the CLI override for the CUE SDK DLL path.
        /// </summary>
        public string? CueSdkPath { get; init; }

        /// <summary>
        /// Gets a value indicating whether help output was requested.
        /// </summary>
        public bool ShowHelp { get; init; }

        /// <summary>
        /// Gets a value indicating whether version output was requested.
        /// </summary>
        public bool ShowVersion { get; init; }

        /// <summary>
        /// Gets a value indicating whether the keypress toggle switch was requested.
        /// </summary>
        public bool ToggleOnKeypress { get; init; }
    }

    /// <summary>
    /// Represents the structure of appsettings.json.
    /// </summary>
    private sealed class AppSettings
    {
        /// <summary>
        /// Gets the FRITZ!Box settings section.
        /// </summary>
        public FritzSettings? Fritz { get; init; }

        /// <summary>
        /// Gets the polling settings section.
        /// </summary>
        public PollingSettings? Polling { get; init; }

        /// <summary>
        /// Gets the CUE SDK settings section.
        /// </summary>
        public CueSdkSettings? CueSdk { get; init; }
    }

    /// <summary>
    /// Represents FRITZ!Box settings from appsettings.json.
    /// </summary>
    private sealed class FritzSettings
    {
        /// <summary>
        /// Gets the FRITZ!Box host.
        /// </summary>
        public string? Host { get; init; }

        /// <summary>
        /// Gets the FRITZ!Box username.
        /// </summary>
        public string? Username { get; init; }

        /// <summary>
        /// Gets the FRITZ!Box password.
        /// </summary>
        public string? Password { get; init; }

        /// <summary>
        /// Gets the FRITZ!Box AIN.
        /// </summary>
        public string? Ain { get; init; }
    }

    /// <summary>
    /// Represents polling settings from appsettings.json.
    /// </summary>
    private sealed class PollingSettings
    {
        /// <summary>
        /// Gets the polling interval in seconds.
        /// </summary>
        public int IntervalSeconds { get; init; } = 5;
    }

    /// <summary>
    /// Represents CUE SDK settings from appsettings.json.
    /// </summary>
    private sealed class CueSdkSettings
    {
        /// <summary>
        /// Gets the optional CUE SDK DLL path.
        /// </summary>
        public string? Path { get; init; }
    }
}
