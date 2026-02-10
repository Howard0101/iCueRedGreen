using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace iCUERedGreen.Tray;

/// <summary>
/// Holds editable settings values for the tray dialog.
/// </summary>
internal sealed class SettingsViewModel
{
    /// <summary>
    /// Gets or sets the FRITZ!Box host.
    /// </summary>
    public string? FritzHost { get; set; }

    /// <summary>
    /// Gets or sets the FRITZ!DECT AIN.
    /// </summary>
    public string? FritzAin { get; set; }

    /// <summary>
    /// Gets or sets the FRITZ!Box user name.
    /// </summary>
    public string? FritzUsername { get; set; }

    /// <summary>
    /// Gets or sets the FRITZ!Box password.
    /// </summary>
    public string? FritzPassword { get; set; }

    /// <summary>
    /// Gets or sets the polling interval in seconds.
    /// </summary>
    public int IntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the optional CUE SDK DLL path.
    /// </summary>
    public string? CueSdkPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether toggle-on-keypress is enabled.
    /// </summary>
    public bool ToggleOnKeypress { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether dev mode is enabled.
    /// </summary>
    public bool DevMode { get; set; }
}

/// <summary>
/// Provides a settings dialog for the tray app.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly SettingsViewModel _model;
    private readonly TextBox _hostTextBox;
    private readonly TextBox _ainTextBox;
    private readonly TextBox _usernameTextBox;
    private readonly TextBox _passwordTextBox;
    private readonly TextBox _intervalTextBox;
    private readonly TextBox _cueSdkPathTextBox;
    private readonly CheckBox _toggleCheckBox;
    private readonly CheckBox _devModeCheckBox;
    private readonly Button _saveButton;
    private readonly ErrorProvider _errorProvider;
    private readonly bool _showDevMode;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsForm"/> class.
    /// </summary>
    /// <param name="model">The settings model.</param>
    public SettingsForm(SettingsViewModel model, bool showDevMode)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _showDevMode = showDevMode;

        Text = "iCUERedGreen Settings";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(12);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        _hostTextBox = CreateTextBox();
        _ainTextBox = CreateTextBox();
        _usernameTextBox = CreateTextBox();
        _passwordTextBox = CreateTextBox();
        _passwordTextBox.UseSystemPasswordChar = true;
        _intervalTextBox = CreateTextBox();
        _cueSdkPathTextBox = CreateTextBox();
        _toggleCheckBox = new CheckBox();
        _devModeCheckBox = new CheckBox();
        _saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK
        };
        _errorProvider = new ErrorProvider
        {
            BlinkStyle = ErrorBlinkStyle.NeverBlink
        };
        _errorProvider.ContainerControl = this;

        TableLayoutPanel layout = BuildLayout();
        Controls.Add(layout);

        ConfigureCheckboxes();
        ConfigureErrorProvider();
        ApplyModel();
        WireValidationEvents();
        UpdateValidationState();
    }

    /// <summary>
    /// Gets the updated settings model.
    /// </summary>
    /// <returns>The updated settings.</returns>
    public SettingsViewModel GetUpdatedModel()
    {
        _model.FritzHost = _hostTextBox.Text.Trim();
        _model.FritzAin = _ainTextBox.Text.Trim();
        _model.FritzUsername = _usernameTextBox.Text.Trim();
        _model.FritzPassword = _passwordTextBox.Text;
        _model.CueSdkPath = _cueSdkPathTextBox.Text.Trim();
        _model.ToggleOnKeypress = _toggleCheckBox.Checked;
        _model.DevMode = _devModeCheckBox.Checked;

        if (TryParseInterval(out int interval))
        {
            _model.IntervalSeconds = interval;
        }

        return _model;
    }

    /// <summary>
    /// Applies model values to the UI controls.
    /// </summary>
    private void ApplyModel()
    {
        _hostTextBox.Text = _model.FritzHost ?? string.Empty;
        _ainTextBox.Text = _model.FritzAin ?? string.Empty;
        _usernameTextBox.Text = _model.FritzUsername ?? string.Empty;
        _passwordTextBox.Text = _model.FritzPassword ?? string.Empty;
        _intervalTextBox.Text = _model.IntervalSeconds.ToString(CultureInfo.InvariantCulture);
        _cueSdkPathTextBox.Text = _model.CueSdkPath ?? string.Empty;
        _toggleCheckBox.Checked = _model.ToggleOnKeypress;
        _devModeCheckBox.Checked = _model.DevMode;
    }

    /// <summary>
    /// Builds the main layout panel.
    /// </summary>
    /// <returns>The layout panel.</returns>
    private TableLayoutPanel BuildLayout()
    {
        TableLayoutPanel layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            Dock = DockStyle.Fill
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        AddRow(layout, "FRITZ host:", _hostTextBox);
        AddRow(layout, "FRITZ AIN:", _ainTextBox);
        AddRow(layout, "FRITZ username:", _usernameTextBox);
        AddRow(layout, "FRITZ password:", _passwordTextBox);
        AddRow(layout, "Polling interval (seconds):", _intervalTextBox);
        AddRow(layout, "CUE SDK path:", _cueSdkPathTextBox);

        AddSpacerRow(layout, 8);
        AddCheckboxRow(layout, "Toggle on Scroll Lock keypress:", _toggleCheckBox);
        if (_showDevMode)
        {
            AddCheckboxRow(layout, "Dev mode (enable env var fallback):", _devModeCheckBox);
        }

        FlowLayoutPanel buttonPanel = BuildButtonPanel();
        layout.Controls.Add(buttonPanel, 0, layout.RowCount);
        layout.SetColumnSpan(buttonPanel, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowCount++;

        return layout;
    }

    /// <summary>
    /// Builds the dialog button panel.
    /// </summary>
    /// <returns>The button panel.</returns>
    private FlowLayoutPanel BuildButtonPanel()
    {
        Button exitButton = new Button
        {
            Text = "Exit",
            DialogResult = DialogResult.Cancel
        };

        exitButton.Anchor = AnchorStyles.Right;
        _saveButton.Anchor = AnchorStyles.Right;
        _saveButton.Click += OnSaveClicked;

        CancelButton = exitButton;
        AcceptButton = _saveButton;

        FlowLayoutPanel panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true
        };

        panel.Controls.Add(exitButton);
        panel.Controls.Add(_saveButton);

        return panel;
    }

    /// <summary>
    /// Creates a standard text box control.
    /// </summary>
    /// <returns>The text box.</returns>
    private static TextBox CreateTextBox()
    {
        return new TextBox
        {
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Width = 320,
            Margin = new Padding(0, 3, 20, 3)
        };
    }

    /// <summary>
    /// Adds a label/control row to the layout panel.
    /// </summary>
    /// <param name="layout">The layout panel.</param>
    /// <param name="labelText">The label text.</param>
    /// <param name="input">The input control.</param>
    private static void AddRow(TableLayoutPanel layout, string labelText, Control input)
    {
        Label label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 6, 0, 0)
        };

        layout.Controls.Add(label, 0, layout.RowCount);
        layout.Controls.Add(input, 1, layout.RowCount);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowCount++;
    }

    /// <summary>
    /// Adds a checkbox row to the layout panel.
    /// </summary>
    /// <param name="layout">The layout panel.</param>
    /// <param name="labelText">The label text.</param>
    /// <param name="checkBox">The checkbox control.</param>
    private static void AddCheckboxRow(TableLayoutPanel layout, string labelText, CheckBox checkBox)
    {
        Label label = new Label
        {
            Text = labelText,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 4, 0, 0)
        };

        layout.Controls.Add(label, 0, layout.RowCount);
        layout.Controls.Add(checkBox, 1, layout.RowCount);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowCount++;
    }

    /// <summary>
    /// Adds a spacer row to the layout panel.
    /// </summary>
    /// <param name="layout">The layout panel.</param>
    /// <param name="height">The spacer height in pixels.</param>
    private static void AddSpacerRow(TableLayoutPanel layout, int height)
    {
        Panel spacer = new Panel
        {
            Height = height,
            Dock = DockStyle.Fill
        };

        layout.Controls.Add(spacer, 0, layout.RowCount);
        layout.SetColumnSpan(spacer, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.RowCount++;
    }

    /// <summary>
    /// Validates input before saving.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event args.</param>
    private void OnSaveClicked(object? sender, EventArgs e)
    {
        UpdateValidationState();
        if (!_saveButton.Enabled)
        {
            DialogResult = DialogResult.None;
        }
    }

    /// <summary>
    /// Parses the polling interval text box value.
    /// </summary>
    /// <param name="interval">The parsed interval.</param>
    /// <returns>True when parsing succeeded; otherwise false.</returns>
    private bool TryParseInterval(out int interval)
    {
        interval = 0;
        string raw = _intervalTextBox.Text.Trim();
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out interval))
        {
            return false;
        }

        return interval > 0;
    }

    /// <summary>
    /// Configures checkbox alignment and spacing.
    /// </summary>
    private void ConfigureCheckboxes()
    {
        ConfigureCheckbox(_toggleCheckBox);
        ConfigureCheckbox(_devModeCheckBox);
    }

    /// <summary>
    /// Applies common checkbox settings.
    /// </summary>
    /// <param name="checkBox">The checkbox to configure.</param>
    private void ConfigureCheckbox(CheckBox checkBox)
    {
        checkBox.Text = string.Empty;
        checkBox.AutoSize = true;
        checkBox.Anchor = AnchorStyles.Left;
        checkBox.CheckAlign = ContentAlignment.MiddleLeft;
        checkBox.Margin = new Padding(0, 3, 0, 3);
    }

    /// <summary>
    /// Configures error provider icon padding for inputs.
    /// </summary>
    private void ConfigureErrorProvider()
    {
        _errorProvider.SetIconPadding(_hostTextBox, 4);
        _errorProvider.SetIconPadding(_ainTextBox, 4);
        _errorProvider.SetIconPadding(_usernameTextBox, 4);
        _errorProvider.SetIconPadding(_passwordTextBox, 4);
        _errorProvider.SetIconPadding(_intervalTextBox, 4);
        _errorProvider.SetIconPadding(_cueSdkPathTextBox, 4);
    }

    /// <summary>
    /// Wires validation handlers to inputs.
    /// </summary>
    private void WireValidationEvents()
    {
        _hostTextBox.TextChanged += (_, _) => UpdateValidationState();
        _ainTextBox.TextChanged += (_, _) => UpdateValidationState();
        _usernameTextBox.TextChanged += (_, _) => UpdateValidationState();
        _passwordTextBox.TextChanged += (_, _) => UpdateValidationState();
        _intervalTextBox.TextChanged += (_, _) => UpdateValidationState();
        _devModeCheckBox.CheckedChanged += (_, _) => UpdateValidationState();
    }

    /// <summary>
    /// Re-evaluates validation status and updates error messages.
    /// </summary>
    private void UpdateValidationState()
    {
        bool isValid = true;

        isValid &= ValidateRequired(_hostTextBox, "FRITZ host is required.");
        isValid &= ValidateRequired(_ainTextBox, "FRITZ AIN is required.");

        string user = _usernameTextBox.Text.Trim();
        string password = _passwordTextBox.Text;
        bool devMode = _devModeCheckBox.Checked;

        if (!devMode)
        {
            bool hasUser = !string.IsNullOrWhiteSpace(user);
            bool hasPassword = !string.IsNullOrWhiteSpace(password);
            if (!hasUser || !hasPassword)
            {
                _errorProvider.SetError(_usernameTextBox, "Username is required unless Dev Mode is enabled.");
                _errorProvider.SetError(_passwordTextBox, "Password is required unless Dev Mode is enabled.");
                isValid = false;
            }
            else
            {
                _errorProvider.SetError(_usernameTextBox, string.Empty);
                _errorProvider.SetError(_passwordTextBox, string.Empty);
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(user) || !string.IsNullOrWhiteSpace(password))
            {
                if (string.IsNullOrWhiteSpace(user))
                {
                    _errorProvider.SetError(_usernameTextBox, "Username is required when a password is provided.");
                    isValid = false;
                }
                else
                {
                    _errorProvider.SetError(_usernameTextBox, string.Empty);
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    _errorProvider.SetError(_passwordTextBox, "Password is required when a username is provided.");
                    isValid = false;
                }
                else
                {
                    _errorProvider.SetError(_passwordTextBox, string.Empty);
                }
            }
            else
            {
                _errorProvider.SetError(_usernameTextBox, string.Empty);
                _errorProvider.SetError(_passwordTextBox, string.Empty);
            }
        }

        if (!TryParseInterval(out int interval) || interval <= 0)
        {
            _errorProvider.SetError(_intervalTextBox, "Polling interval must be a positive integer.");
            isValid = false;
        }
        else
        {
            _errorProvider.SetError(_intervalTextBox, string.Empty);
        }

        _saveButton.Enabled = isValid;
    }

    /// <summary>
    /// Validates that a control contains a non-empty value.
    /// </summary>
    /// <param name="control">The control to validate.</param>
    /// <param name="message">The error message to show.</param>
    /// <returns>True when the control contains a value; otherwise false.</returns>
    private bool ValidateRequired(Control control, string message)
    {
        string text = control.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            _errorProvider.SetError(control, message);
            return false;
        }

        _errorProvider.SetError(control, string.Empty);
        return true;
    }
}
