namespace iCUERedGreen;

/// <summary>
/// Provides settings used by the core worker.
/// </summary>
public sealed class WorkerSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerSettings"/> class.
    /// </summary>
    public WorkerSettings()
    {
    }

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
}
