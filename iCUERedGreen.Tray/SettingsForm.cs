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

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsForm"/> class.
    /// </summary>
    /// <param name="model">The settings model.</param>
    public SettingsForm(SettingsViewModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));

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
        _toggleCheckBox = new CheckBox { Text = "Toggle on Scroll Lock keypress" };
        _devModeCheckBox = new CheckBox { Text = "Dev mode (enable env var fallback)" };

        TableLayoutPanel layout = BuildLayout();
        Controls.Add(layout);

        ApplyModel();
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

        layout.Controls.Add(_toggleCheckBox, 0, layout.RowCount);
        layout.SetColumnSpan(_toggleCheckBox, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowCount++;

        layout.Controls.Add(_devModeCheckBox, 0, layout.RowCount);
        layout.SetColumnSpan(_devModeCheckBox, 2);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowCount++;

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
        Button saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK
        };

        exitButton.Anchor = AnchorStyles.Right;
        saveButton.Anchor = AnchorStyles.Right;
        saveButton.Click += OnSaveClicked;

        CancelButton = exitButton;
        AcceptButton = saveButton;

        FlowLayoutPanel panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true
        };

        panel.Controls.Add(exitButton);
        panel.Controls.Add(saveButton);

        return panel;
    }

    /// <summary>
    /// Creates a standard text box control.
    /// </summary>
    /// <returns>The text box.</returns>
    private static TextBox CreateTextBox()
    {
        return new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right, Width = 320 };
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
    /// Validates input before saving.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event args.</param>
    private void OnSaveClicked(object? sender, EventArgs e)
    {
        if (!TryParseInterval(out _))
        {
            MessageBox.Show(
                "Polling interval must be a positive integer.",
                "Invalid value",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
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
}
