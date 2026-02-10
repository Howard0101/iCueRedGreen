using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Windows.Forms;
using iCUERedGreen;
using NLog;

namespace iCUERedGreen.Tray;

/// <summary>
/// Provides the tray application context.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly Logger _logger;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _restartItem;
    private readonly ToolStripMenuItem _openLogsItem;
    private readonly ToolStripMenuItem _devModeItem;
    private readonly ToolStripMenuItem _infoItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly TrayIcons _icons;
    private readonly SemaphoreSlim _actionGate = new(1, 1);
    private readonly TraySettingsStore _settingsStore;
    private readonly CredentialStore _credentialStore;
    private readonly SynchronizationContext _uiContext;
    private WorkerController? _worker;
    private CancellationTokenSource? _workerCts;
    private Task? _workerTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayApplicationContext"/> class.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    public TrayApplicationContext(Logger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _settingsStore = new TraySettingsStore(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        _credentialStore = new CredentialStore(_logger);

        _toggleItem = new ToolStripMenuItem("Toggle Switch", null, OnToggleRequested);
        _settingsItem = new ToolStripMenuItem("Settings...", null, OnSettingsRequested);
        _restartItem = new ToolStripMenuItem("Restart Worker", null, OnRestartRequested);
        _openLogsItem = new ToolStripMenuItem("Open Log", null, OnOpenLogsRequested);
        _devModeItem = new ToolStripMenuItem("Dev Mode", null, OnDevModeToggled) { CheckOnClick = true };
        _infoItem = new ToolStripMenuItem("Info...", null, OnInfoRequested);
        _exitItem = new ToolStripMenuItem("Exit", null, OnExitRequested);

        _icons = TrayIcons.Load();
        ContextMenuStrip menu = BuildMenu();
        _notifyIcon = new NotifyIcon
        {
            Icon = _icons.Unknown,
            Visible = true,
            ContextMenuStrip = menu,
            Text = "iCUERedGreen: starting"
        };
        _notifyIcon.MouseUp += OnNotifyIconMouseUp;

        SyncDevModeMenu();
        QueueAction(() => StartWorkerAsync(showErrors: false));
    }

    /// <summary>
    /// Releases managed resources.
    /// </summary>
    /// <param name="disposing">True when disposing; otherwise false.</param>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            base.Dispose(disposing);
            return;
        }

        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _icons.Dispose();
            _workerCts?.Dispose();
            _actionGate.Dispose();
        }

        _disposed = true;
        base.Dispose(disposing);
    }

    /// <summary>
    /// Builds the tray context menu.
    /// </summary>
    /// <returns>The context menu.</returns>
    private ContextMenuStrip BuildMenu()
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Items.Add(_toggleItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(_restartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_openLogsItem);
        menu.Items.Add(_devModeItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_infoItem);
        menu.Items.Add(_exitItem);
        return menu;
    }

    /// <summary>
    /// Loads the tray icon from disk.
    /// </summary>
    /// <returns>The tray icon.</returns>
    private sealed class TrayIcons : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TrayIcons"/> class.
        /// </summary>
        /// <param name="onIcon">The icon used when the switch is on.</param>
        /// <param name="offIcon">The icon used when the switch is off.</param>
        /// <param name="unknownIcon">The icon used when the switch state is unknown.</param>
        /// <param name="fallbackIcon">The fallback icon.</param>
        public TrayIcons(Icon onIcon, Icon offIcon, Icon unknownIcon, Icon fallbackIcon)
        {
            On = onIcon ?? throw new ArgumentNullException(nameof(onIcon));
            Off = offIcon ?? throw new ArgumentNullException(nameof(offIcon));
            Unknown = unknownIcon ?? throw new ArgumentNullException(nameof(unknownIcon));
            _fallback = fallbackIcon ?? throw new ArgumentNullException(nameof(fallbackIcon));
        }

        /// <summary>
        /// Gets the icon used when the switch is on.
        /// </summary>
        public Icon On { get; }

        /// <summary>
        /// Gets the icon used when the switch is off.
        /// </summary>
        public Icon Off { get; }

        /// <summary>
        /// Gets the icon used when the switch state is unknown.
        /// </summary>
        public Icon Unknown { get; }

        private readonly Icon _fallback;
        private const string ResourcePrefix = "iCUERedGreen.Tray.Asset.";
        private const string DefaultIconFileName = "iCUERedGreen.keyboard-dot.ico";

        /// <summary>
        /// Loads tray icons from embedded resources.
        /// </summary>
        /// <returns>The loaded tray icons.</returns>
        public static TrayIcons Load()
        {
            Icon fallback = LoadEmbeddedIcon(DefaultIconFileName) ?? (Icon)SystemIcons.Application.Clone();
            Icon onIcon = CreateOverlayIcon(fallback, Color.Red);
            Icon offIcon = CreateOverlayIcon(fallback, Color.LimeGreen);
            Icon unknownIcon = CreateOverlayIcon(fallback, Color.DodgerBlue);

            return new TrayIcons(onIcon, offIcon, unknownIcon, fallback);
        }

        /// <summary>
        /// Releases icon resources.
        /// </summary>
        public void Dispose()
        {
            On.Dispose();
            Off.Dispose();
            Unknown.Dispose();
            if (!ReferenceEquals(_fallback, On)
                && !ReferenceEquals(_fallback, Off)
                && !ReferenceEquals(_fallback, Unknown))
            {
                _fallback.Dispose();
            }
        }

        /// <summary>
        /// Loads an icon embedded resource by file name.
        /// </summary>
        /// <param name="fileName">The icon file name.</param>
        /// <returns>The icon, or null when missing.</returns>
        private static Icon? LoadEmbeddedIcon(string fileName)
        {
            string resourceName = $"{ResourcePrefix}{fileName}";
            System.Reflection.Assembly assembly = typeof(TrayIcons).Assembly;
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                return null;
            }

            using Icon temp = new Icon(stream);
            return (Icon)temp.Clone();
        }

        /// <summary>
        /// Creates a new icon by overlaying a colored status dot on the base icon.
        /// </summary>
        /// <param name="baseIcon">The base icon to clone and draw on.</param>
        /// <param name="dotColor">The overlay dot color.</param>
        /// <returns>The new icon with the status dot overlay.</returns>
        private static Icon CreateOverlayIcon(Icon baseIcon, Color dotColor)
        {
            if (baseIcon is null)
            {
                throw new ArgumentNullException(nameof(baseIcon));
            }

            using Bitmap bitmap = baseIcon.ToBitmap();
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int size = Math.Min(bitmap.Width, bitmap.Height);
            int diameter = Math.Max(4, size / 3);
            int padding = Math.Max(1, diameter / 6);
            int x = bitmap.Width - diameter - padding;
            int y = bitmap.Height - diameter - padding;

            // Draw a status dot overlay in the bottom-right corner.
            using SolidBrush brush = new SolidBrush(dotColor);
            using Pen outline = new Pen(Color.Black, 1);
            graphics.FillEllipse(brush, x, y, diameter, diameter);
            graphics.DrawEllipse(outline, x, y, diameter, diameter);

            IntPtr iconHandle = bitmap.GetHicon();
            try
            {
                using Icon temp = Icon.FromHandle(iconHandle);
                return (Icon)temp.Clone();
            }
            finally
            {
                NativeMethods.DestroyIcon(iconHandle);
            }
        }

        /// <summary>
        /// Native methods for icon handle cleanup.
        /// </summary>
        private static class NativeMethods
        {
            /// <summary>
            /// Destroys an icon handle.
            /// </summary>
            /// <param name="handle">The handle to destroy.</param>
            /// <returns>True when destroyed; otherwise false.</returns>
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DestroyIcon(IntPtr handle);
        }
    }

    /// <summary>
    /// Displays the context menu on left click.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event args.</param>
    private void OnNotifyIconMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _notifyIcon.ContextMenuStrip?.Show(Cursor.Position);
        }
    }

    /// <summary>
    /// Queues an asynchronous action to run sequentially.
    /// </summary>
    /// <param name="action">The action to run.</param>
    private void QueueAction(Func<Task> action)
    {
        _ = RunQueuedAsync(action);
    }

    /// <summary>
    /// Runs a queued action under the action gate.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <returns>A task that completes when the action finishes.</returns>
    private async Task RunQueuedAsync(Func<Task> action)
    {
        await _actionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Tray action failed.");
        }
        finally
        {
            _actionGate.Release();
        }
    }
    /// <summary>
    /// Starts the worker loop.
    /// </summary>
    /// <param name="showErrors">True to show errors to the user.</param>
    /// <returns>A task that completes when startup finishes.</returns>
    private async Task StartWorkerAsync(bool showErrors)
    {
        if (_worker is not null)
        {
            return;
        }

        if (!TryBuildWorkerSettings(out WorkerSettings settings, out string? failureMessage))
        {
            SetTrayStatus("iCUERedGreen: not configured");
            if (showErrors && !string.IsNullOrWhiteSpace(failureMessage))
            {
                ShowMessage("Configuration missing", failureMessage, MessageBoxIcon.Warning);
            }

            return;
        }

        try
        {
            _workerCts = new CancellationTokenSource();
            _worker = new WorkerController(settings, _logger);
            _worker.SwitchStateChanged += OnSwitchStateChanged;
            _workerTask = _worker.StartAsync(_workerCts.Token);
            // Fire-and-forget the continuation that logs background task faults.
            _ = _workerTask.ContinueWith(
                task => LogWorkerFault(task.Exception),
                TaskScheduler.Default);

            LogSettings(settings);
            SetTrayStatus("iCUERedGreen: running");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start worker.");
            SetTrayStatus("iCUERedGreen: failed to start");
            if (showErrors)
            {
            ShowMessage("Worker failed to start", ex.Message, MessageBoxIcon.Error);
            }
        }
    }

    /// <summary>
    /// Stops the worker loop.
    /// </summary>
    /// <returns>A task that completes when the worker stops.</returns>
    private async Task StopWorkerAsync()
    {
        if (_worker is null)
        {
            return;
        }

        _worker.SwitchStateChanged -= OnSwitchStateChanged;
        _workerCts?.Cancel();
        if (_workerCts is not null)
        {
            _workerCts.Dispose();
            _workerCts = null;
        }

        try
        {
            await _worker.StopAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to stop worker.");
        }
        finally
        {
            _worker = null;
            _workerTask = null;
        }
    }

    /// <summary>
    /// Restarts the worker loop.
    /// </summary>
    /// <param name="showErrors">True to show errors to the user.</param>
    /// <returns>A task that completes when the restart finishes.</returns>
    private async Task RestartWorkerAsync(bool showErrors)
    {
        await StopWorkerAsync().ConfigureAwait(false);
        await StartWorkerAsync(showErrors).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles switch state updates from the worker.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="args">The event args.</param>
    private void OnSwitchStateChanged(object? sender, SwitchStateChangedEventArgs args)
    {
        SwitchStateSnapshot snapshot = args.Current;
        _uiContext.Post(_ => UpdateTooltip(snapshot), null);
    }

    /// <summary>
    /// Updates the tray tooltip text.
    /// </summary>
    /// <param name="snapshot">The latest switch state snapshot.</param>
    private void UpdateTooltip(SwitchStateSnapshot snapshot)
    {
        string stateLabel = snapshot.State switch
        {
            SwitchState.On => "ON",
            SwitchState.Off => "OFF",
            _ => "UNKNOWN"
        };

        string cueLabel = snapshot.IsCueAvailable ? "iCUE OK" : "iCUE off";
        SetTrayStatus($"iCUERedGreen: {stateLabel} ({cueLabel})");
        UpdateIcon(snapshot.State);
    }

    /// <summary>
    /// Updates the tray icon based on the switch state.
    /// </summary>
    /// <param name="state">The current switch state.</param>
    private void UpdateIcon(SwitchState state)
    {
        _notifyIcon.Icon = state switch
        {
            SwitchState.On => _icons.On,
            SwitchState.Off => _icons.Off,
            _ => _icons.Unknown
        };
    }

    /// <summary>
    /// Sets the tray tooltip text with length protection.
    /// </summary>
    /// <param name="text">The tooltip text.</param>
    private void SetTrayStatus(string text)
    {
        string safeText = text;
        if (safeText.Length > 63)
        {
            safeText = safeText[..63];
        }

        _notifyIcon.Text = safeText;
    }

    /// <summary>
    /// Logs the worker settings for diagnostics.
    /// </summary>
    /// <param name="settings">The settings to log.</param>
    private void LogSettings(WorkerSettings settings)
    {
        _logger.Info("FRITZ host: {0}", settings.FritzHost);
        _logger.Info("FRITZ username: {0}", settings.FritzUsername);
        _logger.Info("FRITZ AIN: {0}", settings.FritzAin);
        _logger.Info("Polling interval: {0} seconds", settings.IntervalSeconds);
        _logger.Info("FRITZ password length: {0}", settings.FritzPassword?.Length ?? 0);
    }

    /// <summary>
    /// Logs unexpected worker task faults.
    /// </summary>
    /// <param name="exception">The worker exception.</param>
    private void LogWorkerFault(AggregateException? exception)
    {
        if (exception is null)
        {
            return;
        }

        _logger.Error(exception.Flatten(), "Worker task faulted.");
    }

    /// <summary>
    /// Handles the toggle menu click.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event args.</param>
    private void OnToggleRequested(object? sender, EventArgs e)
    {
        QueueAction(ToggleSwitchAsync);
    }

    /// <summary>
    /// Toggles the switch via the worker.
    /// </summary>
    /// <returns>A task that completes when the toggle finishes.</returns>
    private async Task ToggleSwitchAsync()
    {
        if (_worker is null)
        {
            ShowMessage("Worker not running", "Start the worker before toggling the switch.", MessageBoxIcon.Warning);
            return;
        }

        try
        {
            await _worker.ToggleAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Toggle request failed.");
            ShowMessage("Toggle failed", ex.Message, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Handles the settings menu click.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event args.</param>
    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        QueueAction(ShowSettingsAsync);
    }

    /// <summary>
    /// Shows the settings dialog.
    /// </summary>
    /// <returns>A task that completes when the dialog closes.</returns>
    private async Task ShowSettingsAsync()
    {
        TraySettings settings = _settingsStore.LoadOrDefault(_logger);
        CredentialEntry? credentialEntry = GetCredentialEntry(settings.DevMode);
        string? envHost = settings.DevMode ? GetEnvironmentVariable("FRITZ_HOST") : null;
        string? envAin = settings.DevMode ? GetEnvironmentVariable("FRITZ_AIN") : null;

        SettingsViewModel model = new SettingsViewModel
        {
            FritzHost = string.IsNullOrWhiteSpace(settings.Fritz.Host) ? envHost : settings.Fritz.Host,
            FritzAin = string.IsNullOrWhiteSpace(settings.Fritz.Ain) ? envAin : settings.Fritz.Ain,
            FritzUsername = credentialEntry?.UserName,
            FritzPassword = credentialEntry?.Password,
            IntervalSeconds = settings.Polling.IntervalSeconds,
            CueSdkPath = settings.CueSdk.Path,
            ToggleOnKeypress = settings.ToggleOnKeypress,
            DevMode = settings.DevMode
        };

        using SettingsForm form = new SettingsForm(model);
        if (form.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        SettingsViewModel updated = form.GetUpdatedModel();
        if (!TryPersistSettings(updated, out string? failureMessage))
        {
            ShowMessage("Save failed", failureMessage ?? "Settings could not be saved.", MessageBoxIcon.Warning);
            return;
        }

        SyncDevModeMenu(updated.DevMode);
        await RestartWorkerAsync(showErrors: true).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves settings and credentials.
    /// </summary>
    /// <param name="model">The updated settings model.</param>
    /// <param name="failureMessage">The failure message if save fails.</param>
    /// <returns>True when saved successfully; otherwise false.</returns>
    private bool TryPersistSettings(SettingsViewModel model, out string? failureMessage)
    {
        failureMessage = null;

        bool hasUser = !string.IsNullOrWhiteSpace(model.FritzUsername);
        bool hasPassword = !string.IsNullOrWhiteSpace(model.FritzPassword);
        if (!model.DevMode && (!hasUser || !hasPassword))
        {
            failureMessage = "Username and password are required unless dev mode is enabled.";
            return false;
        }

        if (hasUser ^ hasPassword)
        {
            failureMessage = "Username and password must both be provided.";
            return false;
        }

        TraySettings settings = new TraySettings
        {
            DevMode = model.DevMode,
            ToggleOnKeypress = model.ToggleOnKeypress,
            Fritz = new FritzSettings
            {
                Host = model.FritzHost,
                Ain = model.FritzAin
            },
            Polling = new PollingSettings
            {
                IntervalSeconds = model.IntervalSeconds
            },
            CueSdk = new CueSdkSettings
            {
                Path = model.CueSdkPath
            }
        };

        _settingsStore.Save(settings);

        if (hasUser && hasPassword)
        {
            _credentialStore.Write(new CredentialEntry(model.FritzUsername ?? string.Empty, model.FritzPassword ?? string.Empty));
        }
        else
        {
            _credentialStore.Delete();
        }

        return true;
    }

    /// <summary>
    /// Retrieves stored credentials or env fallback values.
    /// </summary>
    /// <param name="allowFallback">True to allow environment fallback.</param>
    /// <returns>The credential entry, or null when unavailable.</returns>
    private CredentialEntry? GetCredentialEntry(bool allowFallback)
    {
        if (_credentialStore.TryRead(out CredentialEntry? entry))
        {
            return entry;
        }

        if (!allowFallback)
        {
            return null;
        }

        string? user = GetEnvironmentVariable("FRITZ_USERNAME");
        string? password = GetEnvironmentVariable("FRITZ_PASSWORD");
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        return new CredentialEntry(user, password);
    }
    /// <summary>
    /// Builds worker settings from stored configuration.
    /// </summary>
    /// <param name="settings">The resolved worker settings.</param>
    /// <param name="failureMessage">The failure message if resolution fails.</param>
    /// <returns>True when settings are valid; otherwise false.</returns>
    private bool TryBuildWorkerSettings(out WorkerSettings settings, out string? failureMessage)
    {
        failureMessage = null;
        settings = new WorkerSettings();

        TraySettings traySettings = _settingsStore.LoadOrDefault(_logger);
        CredentialEntry? credentialEntry = GetCredentialEntry(traySettings.DevMode);

        string? host = traySettings.Fritz.Host;
        string? ain = traySettings.Fritz.Ain;
        int intervalSeconds = traySettings.Polling.IntervalSeconds;
        string? cueSdkPath = traySettings.CueSdk.Path;
        bool toggleOnKeypress = traySettings.ToggleOnKeypress;
        string? userName = credentialEntry?.UserName;
        string? password = credentialEntry?.Password;

        if (traySettings.DevMode)
        {
            host = GetEnvironmentVariable("FRITZ_HOST") ?? host;
            userName = GetEnvironmentVariable("FRITZ_USERNAME") ?? userName;
            password = GetEnvironmentVariable("FRITZ_PASSWORD") ?? password;
            ain = GetEnvironmentVariable("FRITZ_AIN") ?? ain;

            string? envInterval = GetEnvironmentVariable("POLL_INTERVAL_SECONDS");
            if (!string.IsNullOrWhiteSpace(envInterval))
            {
                if (!int.TryParse(envInterval, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedInterval))
                {
                    failureMessage = "POLL_INTERVAL_SECONDS must be a valid integer.";
                    return false;
                }

                intervalSeconds = parsedInterval;
            }
        }

        settings = new WorkerSettings
        {
            IntervalSeconds = intervalSeconds,
            FritzHost = host,
            FritzUsername = userName,
            FritzPassword = password,
            FritzAin = ain,
            CueSdkPath = cueSdkPath,
            ToggleOnKeypress = toggleOnKeypress
        };

        try
        {
            settings.Validate();
        }
        catch (Exception ex)
        {
            failureMessage = ex.Message;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Syncs the dev mode menu state from stored settings.
    /// </summary>
    private void SyncDevModeMenu()
    {
        TraySettings settings = _settingsStore.LoadOrDefault(_logger);
        SyncDevModeMenu(settings.DevMode);
    }

    /// <summary>
    /// Syncs the dev mode menu state.
    /// </summary>
    /// <param name="isEnabled">True when dev mode is enabled.</param>
    private void SyncDevModeMenu(bool isEnabled)
    {
        _devModeItem.Checked = isEnabled;
    }

    /// <summary>
    /// Reads a trimmed environment variable.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The value, or null if missing.</returns>
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
    /// Handles the restart menu click.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event args.</param>
    private void OnRestartRequested(object? sender, EventArgs e)
    {
        QueueAction(() => RestartWorkerAsync(showErrors: true));
    }

    /// <summary>
    /// Handles the open log menu click.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event args.</param>
    private void OnOpenLogsRequested(object? sender, EventArgs e)
    {
        QueueAction(OpenLogsAsync);
    }

    /// <summary>
    /// Opens the log file with the OS-associated app.
    /// </summary>
    /// <returns>A completed task.</returns>
    private Task OpenLogsAsync()
    {
        string logPath = Path.Combine(AppContext.BaseDirectory, "logs", "iCUERedGreen.log");
        if (!File.Exists(logPath))
        {
            ShowMessage("Log file missing", "The log file does not exist yet.", MessageBoxIcon.Warning);
            return Task.CompletedTask;
        }

        // Open the log with the default associated application.
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = logPath,
            UseShellExecute = true
        };

        Process.Start(startInfo);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the dev mode menu click.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event args.</param>
    private void OnDevModeToggled(object? sender, EventArgs e)
    {
        QueueAction(UpdateDevModeAsync);
    }

    /// <summary>
    /// Updates dev mode in settings and restarts the worker.
    /// </summary>
    /// <returns>A task that completes when the update finishes.</returns>
    private async Task UpdateDevModeAsync()
    {
        TraySettings settings = _settingsStore.LoadOrDefault(_logger);
        settings = new TraySettings
        {
            DevMode = _devModeItem.Checked,
            ToggleOnKeypress = settings.ToggleOnKeypress,
            Fritz = settings.Fritz,
            Polling = settings.Polling,
            CueSdk = settings.CueSdk
        };

        _settingsStore.Save(settings);
        await RestartWorkerAsync(showErrors: true).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles the info menu click.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event args.</param>
    private void OnInfoRequested(object? sender, EventArgs e)
    {
        string version = typeof(TrayApplicationContext).Assembly.GetName().Version?.ToString() ?? "unknown";
        ShowMessage("iCUERedGreen", $"Tray version: {version}", MessageBoxIcon.Information);
    }

    /// <summary>
    /// Handles the exit menu click.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event args.</param>
    private void OnExitRequested(object? sender, EventArgs e)
    {
        QueueAction(ExitAsync);
    }

    /// <summary>
    /// Stops the worker and exits the tray application.
    /// </summary>
    /// <returns>A task that completes when exit is scheduled.</returns>
    private async Task ExitAsync()
    {
        await StopWorkerAsync().ConfigureAwait(false);
        _uiContext.Post(_ => ExitThread(), null);
    }

    /// <summary>
    /// Shows a message box on the UI thread.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="message">The dialog message.</param>
    /// <param name="icon">The message box icon.</param>
    private void ShowMessage(string title, string message, MessageBoxIcon icon)
    {
        _uiContext.Post(_ =>
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
        }, null);
    }
}
