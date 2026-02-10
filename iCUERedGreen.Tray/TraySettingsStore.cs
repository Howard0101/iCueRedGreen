using System.Text.Json;
using NLog;

namespace iCUERedGreen.Tray;

/// <summary>
/// Loads and saves tray settings.
/// </summary>
internal sealed class TraySettingsStore
{
    private readonly string _path;

    /// <summary>
    /// Initializes a new instance of the <see cref="TraySettingsStore"/> class.
    /// </summary>
    /// <param name="path">The settings file path.</param>
    public TraySettingsStore(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>
    /// Loads settings from disk or returns defaults.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <returns>The loaded settings.</returns>
    public TraySettings LoadOrDefault(Logger logger)
    {
        if (logger is null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        if (!File.Exists(_path))
        {
            return new TraySettings();
        }

        try
        {
            string json = File.ReadAllText(_path);
            TraySettings? loaded = JsonSerializer.Deserialize<TraySettings>(json, CreateOptions());
            return Normalize(loaded);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to read tray settings; using defaults.");
            return new TraySettings();
        }
    }

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    public void Save(TraySettings settings)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        string directory = Path.GetDirectoryName(_path) ?? AppContext.BaseDirectory;
        Directory.CreateDirectory(directory);

        string json = JsonSerializer.Serialize(settings, CreateOptions());
        string tempPath = _path + ".tmp";

        File.WriteAllText(tempPath, json, new System.Text.UTF8Encoding(false));
        File.Move(tempPath, _path, overwrite: true);
    }

    /// <summary>
    /// Normalizes deserialized settings and ensures defaults.
    /// </summary>
    /// <param name="loaded">The loaded settings.</param>
    /// <returns>The normalized settings.</returns>
    private static TraySettings Normalize(TraySettings? loaded)
    {
        return new TraySettings
        {
            DevMode = loaded?.DevMode ?? false,
            ToggleOnKeypress = loaded?.ToggleOnKeypress ?? false,
            Fritz = loaded?.Fritz ?? new FritzSettings(),
            Polling = loaded?.Polling ?? new PollingSettings(),
            CueSdk = loaded?.CueSdk ?? new CueSdkSettings()
        };
    }

    /// <summary>
    /// Creates serializer options for the settings file.
    /// </summary>
    /// <returns>The serializer options.</returns>
    private static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
    }
}

/// <summary>
/// Holds tray configuration values.
/// </summary>
internal sealed class TraySettings
{
    /// <summary>
    /// Gets or sets a value indicating whether dev mode is enabled.
    /// </summary>
    public bool DevMode { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether toggle-on-keypress is enabled.
    /// </summary>
    public bool ToggleOnKeypress { get; init; }

    /// <summary>
    /// Gets or sets the FRITZ!Box settings.
    /// </summary>
    public FritzSettings Fritz { get; init; } = new();

    /// <summary>
    /// Gets or sets the polling settings.
    /// </summary>
    public PollingSettings Polling { get; init; } = new();

    /// <summary>
    /// Gets or sets the CUE SDK settings.
    /// </summary>
    public CueSdkSettings CueSdk { get; init; } = new();
}

/// <summary>
/// Holds FRITZ!Box settings from the tray configuration.
/// </summary>
internal sealed class FritzSettings
{
    /// <summary>
    /// Gets or sets the FRITZ!Box host.
    /// </summary>
    public string? Host { get; init; }

    /// <summary>
    /// Gets or sets the FRITZ!DECT AIN.
    /// </summary>
    public string? Ain { get; init; }
}

/// <summary>
/// Holds polling settings from the tray configuration.
/// </summary>
internal sealed class PollingSettings
{
    /// <summary>
    /// Gets or sets the polling interval in seconds.
    /// </summary>
    public int IntervalSeconds { get; init; } = 5;
}

/// <summary>
/// Holds CUE SDK settings from the tray configuration.
/// </summary>
internal sealed class CueSdkSettings
{
    /// <summary>
    /// Gets or sets the optional CUE SDK DLL path.
    /// </summary>
    public string? Path { get; init; }
}
