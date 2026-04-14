using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Timer = System.Windows.Forms.Timer;

namespace PcMonitorHost;

internal sealed class MainForm : Form
{
    private static readonly Color WindowBack = Color.FromArgb(20, 20, 22);
    private static readonly Color PanelBack = Color.FromArgb(28, 28, 32);
    private static readonly Color SectionBack = Color.FromArgb(32, 32, 38);
    private static readonly Color InputBack = Color.FromArgb(45, 45, 52);
    private static readonly Color InputBorder = Color.FromArgb(70, 70, 80);
    private static readonly Color ForeText = Color.FromArgb(240, 240, 245);
    private static readonly Color SecondaryText = Color.FromArgb(160, 160, 170);
    private static readonly Color AccentColor = Color.FromArgb(100, 200, 255);
    private static readonly Color AccentGreen = Color.FromArgb(100, 220, 150);
    private static readonly Color ErrorText = Color.FromArgb(255, 120, 120);
    private static readonly Color SuccessText = Color.FromArgb(120, 200, 120);

    private readonly Label _portLabel = new() { AutoSize = true, Padding = new Padding(0, 7, 0, 0) };
    private readonly Label _baudLabel = new() { AutoSize = true, Padding = new Padding(14, 7, 0, 0) };
    private readonly Label _intervalLabel = new() { AutoSize = true, Padding = new Padding(14, 7, 0, 0) };

    private readonly ComboBox _portCombo = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 110
    };

    private readonly NumericUpDown _baudInput = new()
    {
        Minimum = 9600,
        Maximum = 2_000_000,
        Increment = 9_600,
        Value = 115_200,
        Width = 95
    };

    private readonly NumericUpDown _intervalInput = new()
    {
        Minimum = 40,
        Maximum = 5_000,
        Increment = 10,
        Value = 70,
        Width = 95
    };

    private readonly Button _refreshPortsButton = new() { AutoSize = true };
    private readonly Button _connectButton = new() { AutoSize = true };
    private readonly Button _toggleLogButton = new() { AutoSize = true };

    private readonly Label _settingsLanguageLabel = new() { AutoSize = true, Padding = new Padding(0, 7, 0, 0) };
    private readonly ComboBox _languageCombo = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 110
    };

    private readonly Label _settingsDefaultComLabel = new() { AutoSize = true, Padding = new Padding(14, 7, 0, 0) };
    private readonly ComboBox _defaultPortCombo = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 110
    };

    private readonly CheckBox _startupCheck = new() { AutoSize = true, Padding = new Padding(14, 5, 0, 0) };
    private readonly CheckBox _monitorSleepCheck = new() { AutoSize = true, Padding = new Padding(14, 5, 0, 0) };
    private readonly CheckBox _autoReconnectCheck = new() { AutoSize = true, Padding = new Padding(14, 5, 0, 0) };
    private readonly CheckBox _lowPowerCheck = new() { AutoSize = true, Padding = new Padding(14, 5, 0, 0) };
    private readonly Label _idleSecondsLabel = new() { AutoSize = true, Padding = new Padding(6, 7, 0, 0) };
    private readonly NumericUpDown _idleSecondsInput = new()
    {
        Minimum = 10,
        Maximum = 3600,
        Increment = 10,
        Value = 60,
        Width = 70
    };
    private readonly Label _lowPowerIntervalLabel = new() { AutoSize = true, Padding = new Padding(6, 7, 0, 0) };
    private readonly NumericUpDown _lowPowerIntervalInput = new()
    {
        Minimum = 80,
        Maximum = 5000,
        Increment = 20,
        Value = 250,
        Width = 80
    };

    private readonly Label _gpuTempPrefLabel = new() { AutoSize = true, Padding = new Padding(14, 7, 0, 0) };
    private readonly ComboBox _gpuTempPrefCombo = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 110
    };

    private readonly Label _cpuTempPrefLabel = new() { AutoSize = true, Padding = new Padding(14, 7, 0, 0) };
    private readonly ComboBox _cpuTempPrefCombo = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 110
    };
    private readonly Button _saveSettingsButton = new() { AutoSize = true };

    private readonly Label _connectionLabel = new() { AutoSize = true };
    private readonly Label _lastSendLabel = new() { AutoSize = true };
    private readonly StatusIndicator _statusIndicator = new() { Width = 140, Height = 20 };

    private readonly Label _lastSendTitleLabel = new() { AutoSize = true, Padding = new Padding(12, 3, 0, 0) };
    private readonly Label _lastAckTitleLabel = new() { AutoSize = true, Padding = new Padding(12, 3, 0, 0) };
    private readonly Label _lastAckLabel = new() { AutoSize = true };
    private readonly Label _sampleMsTitleLabel = new() { AutoSize = true, Padding = new Padding(12, 3, 0, 0) };
    private readonly Label _sampleMsLabel = new() { AutoSize = true };

    // Dashboard cards
    private readonly TelemetryCard _cpuCard = new() { Title = "CPU", Unit = "%", AccentColor = AccentGreen, Maximum = 100f };
    private readonly TelemetryCard _gpuCard = new() { Title = "GPU", Unit = "%", AccentColor = Color.FromArgb(80, 160, 255), Maximum = 100f };
    private readonly TelemetryCard _cpuTempCard = new() { Title = "CPU Teplota", Unit = "°C", AccentColor = Color.FromArgb(255, 180, 0), Maximum = 110f };
    private readonly TelemetryCard _gpuTempCard = new() { Title = "GPU Teplota", Unit = "°C", AccentColor = Color.FromArgb(255, 120, 60), Maximum = 110f };
    private readonly TelemetryCard _ramCard = new() { Title = "RAM", Unit = "%", AccentColor = Color.FromArgb(180, 120, 255), Maximum = 100f };
    private readonly TelemetryCard _powerCard = new() { Title = "Spotřeba", Unit = "W", AccentColor = Color.FromArgb(255, 200, 0), Maximum = 600f };
    private readonly SparklineGraph _netSparkline = new() { LineColor = AccentGreen, FillColor = Color.FromArgb(30, 0, 200, 140), Height = 50, Maximum = 100f };
    private readonly SparklineGraph _diskSparkline = new() { LineColor = Color.FromArgb(200, 140, 255), FillColor = Color.FromArgb(30, 200, 140, 255), Height = 50, Maximum = 100f };
    private readonly Label _netLabel = new() { Text = "🌐 SÍŤ", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(180, 180, 190), AutoSize = false, Height = 20 };
    private readonly Label _diskLabel = new() { Text = "💾 DISKY", Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(180, 180, 190), AutoSize = false, Height = 20 };
    private readonly Label _netValueLabel = new() { Text = "↓ 0  ↑ 0 MB/s", Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(160, 160, 170), AutoSize = true };
    private readonly Label _diskValueLabel = new() { Text = "0%", Font = new Font("Segoe UI", 8F), ForeColor = Color.FromArgb(160, 160, 170), AutoSize = true };

    private readonly TextBox _logBox = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill
    };

    private bool _logPanelVisible = false;

    private readonly Timer _sendTimer = new();
    private readonly Timer _reconnectTimer = new();
    private readonly SerialService _serialService = new();
    private readonly HardwareMonitorService _monitorService = new();
    private readonly TelemetrySmoother _smoother = new();

    private readonly NotifyIcon _trayIcon = new();
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly ToolStripMenuItem _trayOpenItem = new();
    private readonly ToolStripMenuItem _trayExitItem = new();

    private readonly AppSettings _settings;
    private readonly Icon _appIcon;
    private readonly bool _ownsAppIcon;

    private UiLanguage _language;
    private bool _languageComboUpdating;
    private bool _isExiting;
    private bool _trayHintShown;
    private bool _sendInProgress;
    private DisplayState _displayState = DisplayState.Unknown;
    private nint _powerSettingNotificationHandle = nint.Zero;
    private DateTime _lastAckUtc = DateTime.MinValue;
    private bool _ackSeen;
    private bool _manualDisconnect;
    private DateTime _lastAutoReconnectLogUtc = DateTime.MinValue;

    private const int WM_POWERBROADCAST = 0x0218;
    private const int PBT_POWERSETTINGCHANGE = 0x8013;
    private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;
    private static readonly Guid GuidSessionDisplayStatus = new("2B84C20E-AD23-4DDF-93DB-05FFBD7EFCA5");
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan AutoReconnectLogInterval = TimeSpan.FromSeconds(6);

    public MainForm()
    {
        _settings = AppSettingsStore.Load();
        _language = _settings.Language;
        (_appIcon, _ownsAppIcon) = CreateAppIcon();

        Text = "PC Monitor Host (ESP32)";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 620);
        Size = new Size(980, 680);
        Icon = _appIcon;

        BuildUi();
        ApplyDarkTheme();
        WireEvents();
        InitializeTray();
        ApplyLocalization();

        _sendTimer.Interval = (int)_intervalInput.Value;
        _reconnectTimer.Interval = 1500;
        FormClosing += OnFormClosing;

        ApplyLoadedSettings();
        RefreshPorts();
        SetDisconnectedStatus();

        if (!IsRunningAsAdministrator())
        {
            Log(T("log.tipAdmin"));
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterDisplayPowerNotification();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterDisplayPowerNotification();
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_POWERBROADCAST && m.WParam == (nint)PBT_POWERSETTINGCHANGE)
        {
            ProcessPowerBroadcast(m.LParam);
        }

        base.WndProc(ref m);
    }

    private void RegisterDisplayPowerNotification()
    {
        if (_powerSettingNotificationHandle != nint.Zero || !IsHandleCreated)
        {
            return;
        }

        _powerSettingNotificationHandle = RegisterPowerSettingNotification(
            Handle,
            GuidSessionDisplayStatus,
            DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    private void UnregisterDisplayPowerNotification()
    {
        if (_powerSettingNotificationHandle == nint.Zero)
        {
            return;
        }

        UnregisterPowerSettingNotification(_powerSettingNotificationHandle);
        _powerSettingNotificationHandle = nint.Zero;
    }

    private void ProcessPowerBroadcast(nint lParam)
    {
        if (lParam == nint.Zero)
        {
            return;
        }

        POWERBROADCAST_SETTING setting = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(lParam);
        if (setting.PowerSetting != GuidSessionDisplayStatus)
        {
            return;
        }

        nint dataPtr = nint.Add(lParam, Marshal.OffsetOf<POWERBROADCAST_SETTING>(nameof(POWERBROADCAST_SETTING.Data)).ToInt32());

        int stateValue;
        if (setting.DataLength >= sizeof(int))
        {
            stateValue = Marshal.ReadInt32(dataPtr);
        }
        else if (setting.DataLength >= sizeof(byte))
        {
            stateValue = Marshal.ReadByte(dataPtr);
        }
        else
        {
            return;
        }

        _displayState = stateValue switch
        {
            0 => DisplayState.Off,
            1 => DisplayState.On,
            2 => DisplayState.Dimmed,
            _ => DisplayState.Unknown
        };
    }

    private void WireEvents()
    {
        _refreshPortsButton.Click += (_, _) => RefreshPorts();
        _connectButton.Click += (_, _) => ToggleConnection();
        _toggleLogButton.Click += (_, _) => ToggleLogPanel();
        _intervalInput.ValueChanged += (_, _) => UpdateSendIntervalForIdle(force: true);
        _sendTimer.Tick += (_, _) => OnSendTimerTick();
        _reconnectTimer.Tick += (_, _) => TryAutoReconnect();
        _serialService.LineReceived += OnSerialLineReceived;

        _languageCombo.SelectedIndexChanged += (_, _) => OnLanguageSelectionChanged();
        _saveSettingsButton.Click += (_, _) => SaveSettings();

        Resize += OnFormResize;
    }

    private void InitializeTray()
    {
        _trayOpenItem.Click += (_, _) => RestoreFromTray();
        _trayExitItem.Click += (_, _) => ExitFromTray();

        _trayMenu.Items.Add(_trayOpenItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(_trayExitItem);

        _trayIcon.Icon = _appIcon;
        _trayIcon.ContextMenuStrip = _trayMenu;
        _trayIcon.Visible = true;
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void ApplyLoadedSettings()
    {
        _startupCheck.Checked = _settings.StartWithWindows;
        _monitorSleepCheck.Checked = _settings.SleepWhenMonitorsOff;
        _autoReconnectCheck.Checked = _settings.AutoReconnect;
        _lowPowerCheck.Checked = _settings.LowPowerWhenIdle;

        _idleSecondsInput.Value = Math.Clamp(_settings.IdleSeconds, (int)_idleSecondsInput.Minimum, (int)_idleSecondsInput.Maximum);
        _lowPowerIntervalInput.Value = Math.Clamp(_settings.LowPowerIntervalMs, (int)_lowPowerIntervalInput.Minimum, (int)_lowPowerIntervalInput.Maximum);

        try
        {
            bool startupEnabled = StartupManager.IsEnabled();
            _startupCheck.Checked = startupEnabled;
            _settings.StartWithWindows = startupEnabled;
        }
        catch
        {
            // Keep loaded value when registry check is unavailable.
        }

        PopulateLanguageCombo(_language);
        PopulateTempPreferenceCombos();
        _monitorService.SetTempPreferences(_settings.CpuTempPreference, _settings.GpuTempPreference);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10, 10, 10, 10),
            BackColor = WindowBack
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Connection bar
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));  // Settings
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // Dashboard + Log
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));  // Status bar

        root.Controls.Add(BuildConnectionSection(), 0, 0);
        root.Controls.Add(BuildSettingsSection(), 0, 1);
        root.Controls.Add(BuildContentArea(), 0, 2);
        root.Controls.Add(BuildStatusBar(), 0, 3);

        Controls.Add(root);
    }

    private Control BuildConnectionSection()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SectionBack,
            Padding = new Padding(12, 8, 12, 8)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 9,
            RowCount = 1,
            Padding = new Padding(0)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));     // 0: COM label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // 1: COM combo
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));     // 2: Baud label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));  // 3: Baud input
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));     // 4: Interval label
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // 5: Interval input
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8));   // 6: spacer
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));     // 7: buttons
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f)); // 8: filler

        _portLabel.Text = "COM port:";
        _portLabel.Font = new Font(new FontFamily("Segoe UI"), 9f, FontStyle.Regular);
        _portLabel.ForeColor = SecondaryText;
        _portLabel.Dock = DockStyle.Fill;
        _portLabel.TextAlign = ContentAlignment.MiddleLeft;

        _portCombo.Font = new Font(new FontFamily("Segoe UI"), 9f);
        _portCombo.BackColor = InputBack;
        _portCombo.ForeColor = ForeText;
        _portCombo.Dock = DockStyle.Fill;

        _baudLabel.Text = "Rychlost:";
        _baudLabel.Font = new Font(new FontFamily("Segoe UI"), 9f, FontStyle.Regular);
        _baudLabel.ForeColor = SecondaryText;
        _baudLabel.Dock = DockStyle.Fill;
        _baudLabel.TextAlign = ContentAlignment.MiddleLeft;

        _baudInput.Font = new Font(new FontFamily("Segoe UI"), 9f);
        _baudInput.BackColor = InputBack;
        _baudInput.ForeColor = ForeText;
        _baudInput.Dock = DockStyle.Fill;

        _intervalLabel.Text = "Interval (ms):";
        _intervalLabel.Font = new Font(new FontFamily("Segoe UI"), 9f, FontStyle.Regular);
        _intervalLabel.ForeColor = SecondaryText;
        _intervalLabel.Dock = DockStyle.Fill;
        _intervalLabel.TextAlign = ContentAlignment.MiddleLeft;

        _intervalInput.Font = new Font(new FontFamily("Segoe UI"), 9f);
        _intervalInput.BackColor = InputBack;
        _intervalInput.ForeColor = ForeText;
        _intervalInput.Dock = DockStyle.Fill;

        // Buttons panel
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        _refreshPortsButton.Text = "Obnovit porty";
        _refreshPortsButton.Font = new Font(new FontFamily("Segoe UI"), 8.5f, FontStyle.Regular);
        _refreshPortsButton.MinimumSize = new Size(90, 28);
        _refreshPortsButton.Height = 28;
        _refreshPortsButton.FlatStyle = FlatStyle.Flat;
        _refreshPortsButton.BackColor = InputBack;
        _refreshPortsButton.ForeColor = ForeText;
        _refreshPortsButton.FlatAppearance.BorderColor = InputBorder;
        _refreshPortsButton.FlatAppearance.BorderSize = 1;
        _refreshPortsButton.Margin = new Padding(0, 0, 6, 0);

        _connectButton.Text = "Připojit";
        _connectButton.Font = new Font(new FontFamily("Segoe UI"), 8.5f, FontStyle.Bold);
        _connectButton.MinimumSize = new Size(80, 28);
        _connectButton.Height = 28;
        _connectButton.FlatStyle = FlatStyle.Flat;
        _connectButton.BackColor = AccentGreen;
        _connectButton.ForeColor = Color.FromArgb(20, 20, 22);
        _connectButton.FlatAppearance.BorderColor = AccentGreen;
        _connectButton.FlatAppearance.BorderSize = 0;
        _connectButton.Margin = new Padding(0, 0, 6, 0);

        _toggleLogButton.Text = "📋 Log";
        _toggleLogButton.Font = new Font(new FontFamily("Segoe UI"), 8.5f, FontStyle.Regular);
        _toggleLogButton.MinimumSize = new Size(70, 28);
        _toggleLogButton.Height = 28;
        _toggleLogButton.FlatStyle = FlatStyle.Flat;
        _toggleLogButton.BackColor = InputBack;
        _toggleLogButton.ForeColor = ForeText;
        _toggleLogButton.FlatAppearance.BorderColor = InputBorder;
        _toggleLogButton.FlatAppearance.BorderSize = 1;
        _toggleLogButton.Margin = new Padding(0, 0, 0, 0);

        btnPanel.Controls.Add(_refreshPortsButton);
        btnPanel.Controls.Add(_connectButton);
        btnPanel.Controls.Add(_toggleLogButton);

        layout.Controls.Add(_portLabel, 0, 0);
        layout.Controls.Add(_portCombo, 1, 0);
        layout.Controls.Add(_baudLabel, 2, 0);
        layout.Controls.Add(_baudInput, 3, 0);
        layout.Controls.Add(_intervalLabel, 4, 0);
        layout.Controls.Add(_intervalInput, 5, 0);
        layout.Controls.Add(btnPanel, 7, 0);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildSettingsSection()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SectionBack,
            Padding = new Padding(12, 6, 12, 6)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(0)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

        // === Row 0: Combos ===
        layout.Controls.Add(MakeSettingRow(_settingsLanguageLabel, "Jazyk:", _languageCombo), 0, 0);
        layout.Controls.Add(MakeSettingRow(_settingsDefaultComLabel, "Výchozí COM:", _defaultPortCombo), 1, 0);
        layout.Controls.Add(MakeSettingRow(_cpuTempPrefLabel, "CPU teplota:", _cpuTempPrefCombo), 2, 0);
        layout.Controls.Add(MakeSettingRow(_gpuTempPrefLabel, "GPU teplota:", _gpuTempPrefCombo), 3, 0);

        // === Row 1: Checkboxes + intervals + save ===
        var checksPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 2, 0, 0)
        };
        _startupCheck.Text = "Spouštět s Windows";
        _startupCheck.Font = new Font(new FontFamily("Segoe UI"), 8.5f);
        _startupCheck.ForeColor = ForeText;
        _startupCheck.AutoSize = true;

        _monitorSleepCheck.Text = "Uspat ESP displej při vypnutém monitoru";
        _monitorSleepCheck.Font = new Font(new FontFamily("Segoe UI"), 8.5f);
        _monitorSleepCheck.ForeColor = ForeText;
        _monitorSleepCheck.AutoSize = true;

        _autoReconnectCheck.Text = "Automaticky připojit";
        _autoReconnectCheck.Font = new Font(new FontFamily("Segoe UI"), 8.5f);
        _autoReconnectCheck.ForeColor = ForeText;
        _autoReconnectCheck.AutoSize = true;

        _lowPowerCheck.Text = "Úsporný režim při neaktivitě";
        _lowPowerCheck.Font = new Font(new FontFamily("Segoe UI"), 8.5f);
        _lowPowerCheck.ForeColor = ForeText;
        _lowPowerCheck.AutoSize = true;

        checksPanel.Controls.Add(_startupCheck);
        checksPanel.Controls.Add(_monitorSleepCheck);
        checksPanel.Controls.Add(_autoReconnectCheck);
        checksPanel.Controls.Add(_lowPowerCheck);
        layout.Controls.Add(checksPanel, 0, 1);

        layout.Controls.Add(MakeSettingRow(_idleSecondsLabel, "Neaktivita (s):", _idleSecondsInput), 1, 1);
        layout.Controls.Add(MakeSettingRow(_lowPowerIntervalLabel, "Interval úspory (ms):", _lowPowerIntervalInput), 2, 1);

        var savePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 2, 0, 0)
        };
        _saveSettingsButton.Text = "Uložit nastavení";
        _saveSettingsButton.Font = new Font(new FontFamily("Segoe UI"), 8.5f, FontStyle.Bold);
        _saveSettingsButton.MinimumSize = new Size(120, 26);
        _saveSettingsButton.Height = 26;
        _saveSettingsButton.FlatStyle = FlatStyle.Flat;
        _saveSettingsButton.BackColor = AccentColor;
        _saveSettingsButton.ForeColor = Color.FromArgb(20, 20, 22);
        _saveSettingsButton.FlatAppearance.BorderColor = AccentColor;
        _saveSettingsButton.FlatAppearance.BorderSize = 0;
        savePanel.Controls.Add(_saveSettingsButton);
        layout.Controls.Add(savePanel, 3, 1);

        panel.Controls.Add(layout);
        return panel;
    }

    private static Panel MakeSettingRow(Label label, string labelText, Control input)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 2, 0, 0),
            WrapContents = false
        };

        label.Text = labelText;
        label.Font = new Font(new FontFamily("Segoe UI"), 8.5f, FontStyle.Regular);
        label.ForeColor = SecondaryText;
        label.AutoSize = true;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Margin = new Padding(0, 3, 6, 0);

        input.Font = new Font(new FontFamily("Segoe UI"), 8.5f);
        input.BackColor = InputBack;
        input.ForeColor = ForeText;
        input.Width = 110;
        input.Height = 26;

        panel.Controls.Add(label);
        panel.Controls.Add(input);
        return panel;
    }

    private Control BuildContentArea()
    {
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 320,
            Panel2Collapsed = true,
            SplitterWidth = 3,
            BackColor = WindowBack,
            FixedPanel = FixedPanel.Panel1
        };

        // === Dashboard panel ===
        var dashboardPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowBack
        };

        // Row 1: CPU + GPU (large cards) - 110px height
        var row1 = new Panel
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = Color.Transparent
        };
        _cpuCard.Size = new Size(200, 106);
        _cpuCard.Dock = DockStyle.Left;
        _gpuCard.Size = new Size(200, 106);
        _gpuCard.Dock = DockStyle.Left;
        row1.Controls.Add(_cpuCard);
        row1.Controls.Add(_gpuCard);

        // Row 2: Temps + RAM + Power - 110px height
        var row2 = new Panel
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = Color.Transparent
        };
        _cpuTempCard.Size = new Size(150, 106);
        _cpuTempCard.Dock = DockStyle.Left;
        _gpuTempCard.Size = new Size(150, 106);
        _gpuTempCard.Dock = DockStyle.Left;
        _ramCard.Size = new Size(150, 106);
        _ramCard.Dock = DockStyle.Left;
        _powerCard.Size = new Size(150, 106);
        _powerCard.Dock = DockStyle.Left;
        row2.Controls.Add(_cpuTempCard);
        row2.Controls.Add(_gpuTempCard);
        row2.Controls.Add(_ramCard);
        row2.Controls.Add(_powerCard);

        // Row 3: Network + Disk sparklines - 90px height
        var row3 = new Panel
        {
            Dock = DockStyle.Top,
            Height = 90,
            BackColor = Color.Transparent
        };

        var netPanel = new RoundedPanel
        {
            Dock = DockStyle.Left,
            Width = 160,
            CornerRadius = 8,
            Padding = new Padding(8, 4, 8, 4)
        };
        _netLabel.Dock = DockStyle.Top;
        _netValueLabel.Dock = DockStyle.Top;
        _netValueLabel.Padding = new Padding(0, 2, 0, 0);
        _netSparkline.Dock = DockStyle.Fill;
        netPanel.Controls.Add(_netSparkline);
        netPanel.Controls.Add(_netValueLabel);
        netPanel.Controls.Add(_netLabel);

        var diskPanel = new RoundedPanel
        {
            Dock = DockStyle.Left,
            Width = 160,
            CornerRadius = 8,
            Padding = new Padding(8, 4, 8, 4)
        };
        _diskLabel.Dock = DockStyle.Top;
        _diskValueLabel.Dock = DockStyle.Top;
        _diskValueLabel.Padding = new Padding(0, 2, 0, 0);
        _diskSparkline.Dock = DockStyle.Fill;
        diskPanel.Controls.Add(_diskSparkline);
        diskPanel.Controls.Add(_diskValueLabel);
        diskPanel.Controls.Add(_diskLabel);

        row3.Controls.Add(netPanel);
        row3.Controls.Add(diskPanel);

        dashboardPanel.Controls.Add(row3);
        dashboardPanel.Controls.Add(row2);
        dashboardPanel.Controls.Add(row1);

        // === Log panel ===
        var logPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = WindowBack
        };

        var logHeader = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 30,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Padding = new Padding(8, 3, 8, 3)
        };

        var logTitle = new Label
        {
            Text = T("group.log"),
            Font = new Font(new FontFamily("Segoe UI"), 9F, FontStyle.Bold),
            ForeColor = SecondaryText,
            AutoSize = true,
            Padding = new Padding(4, 3, 0, 0)
        };

        var clearLogBtn = new Button
        {
            Text = "Vymazat",
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = InputBack,
            ForeColor = ForeText,
            Font = new Font(new FontFamily("Segoe UI"), 8f),
            Margin = new Padding(8, 0, 0, 0)
        };
        clearLogBtn.Click += (_, _) => _logBox.Clear();

        logHeader.Controls.Add(logTitle);
        logHeader.Controls.Add(clearLogBtn);

        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.Dock = DockStyle.Fill;
        _logBox.BackColor = Color.FromArgb(16, 16, 20);
        _logBox.ForeColor = Color.FromArgb(200, 200, 210);
        _logBox.Font = new Font("Consolas", 8.5F);
        _logBox.BorderStyle = BorderStyle.None;

        var logContainer = new Panel { Dock = DockStyle.Fill };
        logContainer.Controls.Add(_logBox);
        logContainer.Controls.Add(logHeader);

        logPanel.Controls.Add(logContainer);

        mainSplit.Panel1.Controls.Add(dashboardPanel);
        mainSplit.Panel2.Controls.Add(logPanel);

        return mainSplit;
    }

    private Control BuildStatusBar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SectionBack
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 1,
            Padding = new Padding(12, 6, 12, 6)
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));

        var labelFont = new Font(new FontFamily("Segoe UI"), 8f, FontStyle.Regular);
        var valueFont = new Font(new FontFamily("Segoe UI"), 8.5f, FontStyle.Bold);



        _connectionLabel.Font = valueFont;
        _connectionLabel.ForeColor = ErrorText;
        _connectionLabel.Text = "Odpojeno";

        _lastSendTitleLabel.Font = labelFont;
        _lastSendTitleLabel.ForeColor = SecondaryText;
        _lastSendTitleLabel.Text = "Poslední odeslání:";

        _lastSendLabel.Font = valueFont;
        _lastSendLabel.ForeColor = ForeText;
        _lastSendLabel.Text = "Nikdy";

        _lastAckTitleLabel.Font = labelFont;
        _lastAckTitleLabel.ForeColor = SecondaryText;
        _lastAckTitleLabel.Text = "Poslední ACK:";

        _lastAckLabel.Font = valueFont;
        _lastAckLabel.ForeColor = ForeText;
        _lastAckLabel.Text = "Nikdy";

        _sampleMsTitleLabel.Font = labelFont;
        _sampleMsTitleLabel.ForeColor = SecondaryText;
        _sampleMsTitleLabel.Text = "Vzorek (ms):";

        _sampleMsLabel.Font = valueFont;
        _sampleMsLabel.ForeColor = ForeText;
        _sampleMsLabel.Text = "N/A";

        var statusFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Padding = new Padding(0)
        };
        _statusIndicator.Width = 140;
        _statusIndicator.Height = 20;
        _statusIndicator.DotColor = ErrorText;
        _statusIndicator.Text = "Odpojeno";
        statusFlow.Controls.Add(_statusIndicator);

        table.Controls.Add(statusFlow, 0, 0);
        table.Controls.Add(_connectionLabel, 1, 0);
        table.Controls.Add(_lastSendTitleLabel, 2, 0);
        table.Controls.Add(_lastSendLabel, 3, 0);
        table.Controls.Add(_lastAckTitleLabel, 4, 0);
        table.Controls.Add(_lastAckLabel, 5, 0);
        table.Controls.Add(_sampleMsTitleLabel, 6, 0);
        table.Controls.Add(_sampleMsLabel, 7, 0);

        panel.Controls.Add(table);
        return panel;
    }

    private void RefreshPorts()
    {
        IReadOnlyList<string> ports = SerialService.GetPortNames();

        string? preferredCurrent = _portCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(preferredCurrent))
        {
            preferredCurrent = _settings.DefaultComPort;
        }

        string? preferredDefault = _defaultPortCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(preferredDefault))
        {
            preferredDefault = _settings.DefaultComPort;
        }

        PopulatePortCombo(_portCombo, ports, preferredCurrent, includePreferredIfMissing: false);
        PopulatePortCombo(_defaultPortCombo, ports, preferredDefault, includePreferredIfMissing: true);

        if (!_serialService.IsConnected &&
            !string.IsNullOrWhiteSpace(_settings.DefaultComPort) &&
            _portCombo.Items.Contains(_settings.DefaultComPort))
        {
            _portCombo.SelectedItem = _settings.DefaultComPort;
        }

        string listed = ports.Count == 0 ? T("misc.none") : string.Join(", ", ports);
        Log(F("log.portsRefreshed", listed));
    }

    private static void PopulatePortCombo(
        ComboBox combo,
        IReadOnlyList<string> ports,
        string? preferred,
        bool includePreferredIfMissing)
    {
        combo.BeginUpdate();
        try
        {
            combo.Items.Clear();
            foreach (string port in ports)
            {
                combo.Items.Add(port);
            }

            if (includePreferredIfMissing &&
                !string.IsNullOrWhiteSpace(preferred) &&
                !combo.Items.Contains(preferred))
            {
                combo.Items.Add(preferred);
            }
        }
        finally
        {
            combo.EndUpdate();
        }

        if (!string.IsNullOrWhiteSpace(preferred) && combo.Items.Contains(preferred))
        {
            combo.SelectedItem = preferred;
        }
        else if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
        else
        {
            combo.SelectedItem = null;
        }
    }

    private void ToggleConnection()
    {
        if (_serialService.IsConnected)
        {
            _manualDisconnect = true;
            Disconnect(T("log.manualDisconnect"), allowAutoReconnect: false);
            return;
        }

        if (_portCombo.SelectedItem is not string selectedPort || string.IsNullOrWhiteSpace(selectedPort))
        {
            MessageBox.Show(this, T("msg.selectCom"), T("msg.portRequiredTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            int baud = (int)_baudInput.Value;
            _serialService.Connect(selectedPort, baud);
            _smoother.Reset();
            _manualDisconnect = false;
            _ackSeen = false;
            _lastAckUtc = DateTime.MinValue;
            UpdateAckLabel();
            _sendTimer.Start();
            _reconnectTimer.Stop();

            SetConnectedStatus(selectedPort, baud);
            Log(F("log.connected", selectedPort, baud));
            SendSample();
        }
        catch (Exception ex)
        {
            SetDisconnectedStatus();
            Log(F("log.connectError", ex.Message));
            MessageBox.Show(this, ex.Message, T("msg.connectionFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ToggleLogPanel()
    {
        _logPanelVisible = !_logPanelVisible;

        if (Controls[0] is not TableLayoutPanel root)
            return;

        if (root.Controls[2] is not SplitContainer split)
            return;

        split.Panel2Collapsed = !_logPanelVisible;
        _toggleLogButton.BackColor = _logPanelVisible ? AccentColor : InputBack;
        _toggleLogButton.ForeColor = _logPanelVisible ? Color.FromArgb(20, 20, 22) : ForeText;
    }

    private async void SendSample()
    {
        if (!_serialService.IsConnected || _sendInProgress)
        {
            return;
        }

        _sendInProgress = true;
        try
        {
            var sw = Stopwatch.StartNew();
            HardwareSample rawSample = await Task.Run(_monitorService.ReadSample);
            sw.Stop();
            if (!_serialService.IsConnected || _isExiting)
            {
                return;
            }

            HardwareSample sample = _smoother.Apply(rawSample);
            bool displaySleepRequested = _settings.SleepWhenMonitorsOff && _displayState == DisplayState.Off;
            string packet = SerialPacketFormatter.BuildDataPacket(sample, displaySleepRequested, false);

            _serialService.SendLine(packet);
            _lastSendLabel.Text = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            _sampleMsLabel.Text = $"{sw.ElapsedMilliseconds} ms";
            UpdateDashboard(sample);
        }
        catch (Exception ex)
        {
            if (!_isExiting)
            {
                Disconnect(F("log.runtimeError", ex.Message));
            }
        }
        finally
        {
            _sendInProgress = false;
        }
    }

    private void UpdateDashboard(HardwareSample sample)
    {
        // Update card values
        _cpuCard.Value = sample.CpuUsagePercent;
        _cpuTempCard.Value = sample.CpuTempC;
        _gpuCard.Value = sample.GpuUsagePercent;
        _gpuTempCard.Value = sample.GpuTempC;
        _ramCard.Value = sample.RamUsagePercent;
        _powerCard.Value = sample.TotalPowerW;

        // Sparklines for CPU, GPU, RAM, Power
        _cpuCard.AddSparklineValue(sample.CpuUsagePercent);
        _gpuCard.AddSparklineValue(sample.GpuUsagePercent);
        _ramCard.AddSparklineValue(sample.RamUsagePercent);
        _powerCard.AddSparklineValue(sample.TotalPowerW);

        // Network sparkline
        _netSparkline.AddValue(sample.NetDownloadMBps + sample.NetUploadMBps);
        _netValueLabel.Text = $"↓ {sample.NetDownloadMBps:0.0}  ↑ {sample.NetUploadMBps:0.0} MB/s";

        // Disk sparkline
        float diskMax = sample.DiskUsages.Count > 0 ? sample.DiskUsages.Max(d => d.UsagePercent) : 0f;
        _diskSparkline.AddValue(diskMax);
        _diskValueLabel.Text = FormatDisks(sample.DiskUsages);
    }

    private void OnSendTimerTick()
    {
        UpdateSendIntervalForIdle(force: false);
        CheckAckTimeout();
        SendSample();
    }

    private void UpdateSendIntervalForIdle(bool force)
    {
        int baseInterval = (int)_intervalInput.Value;
        int targetInterval = baseInterval;

        if (_settings.LowPowerWhenIdle)
        {
            bool idle = UserIdleDetector.IsIdleForSeconds(_settings.IdleSeconds);
            if (idle)
            {
                targetInterval = _settings.LowPowerIntervalMs;
            }
        }

        if (force || _sendTimer.Interval != targetInterval)
        {
            _sendTimer.Interval = targetInterval;
        }
    }

    private void OnSerialLineReceived(string line)
    {
        if (!string.Equals(line, "ACK", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!IsHandleCreated)
        {
            return;
        }

        BeginInvoke(() =>
        {
            _ackSeen = true;
            _lastAckUtc = DateTime.UtcNow;
            UpdateAckLabel();
        });
    }

    private void UpdateAckLabel()
    {
        _lastAckLabel.Text = _ackSeen
            ? DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
            : T("status.never");
    }

    private void CheckAckTimeout()
    {
        if (!_serialService.IsConnected || !_ackSeen)
        {
            return;
        }

        if (DateTime.UtcNow - _lastAckUtc > AckTimeout)
        {
            Disconnect(T("log.ackTimeout"), allowAutoReconnect: _settings.AutoReconnect);
        }
    }

    private void TryAutoReconnect()
    {
        if (_isExiting || _manualDisconnect || _serialService.IsConnected || !_settings.AutoReconnect)
        {
            return;
        }

        string? port = _portCombo.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(port))
        {
            port = _settings.DefaultComPort;
        }

        if (string.IsNullOrWhiteSpace(port))
        {
            return;
        }

        try
        {
            int baud = (int)_baudInput.Value;
            _serialService.Connect(port, baud);
            _smoother.Reset();
            _ackSeen = false;
            _lastAckUtc = DateTime.MinValue;
            UpdateAckLabel();
            _sendTimer.Start();
            SetConnectedStatus(port, baud);
            Log(F("log.autoReconnect", port, baud));
            _reconnectTimer.Stop();
        }
        catch (Exception ex)
        {
            if (DateTime.UtcNow - _lastAutoReconnectLogUtc > AutoReconnectLogInterval)
            {
                Log(F("log.autoReconnectError", ex.Message));
                _lastAutoReconnectLogUtc = DateTime.UtcNow;
            }
        }
    }

    private void SaveSettings()
    {
        _settings.Language = _language;
        _settings.StartWithWindows = _startupCheck.Checked;
        _settings.SleepWhenMonitorsOff = _monitorSleepCheck.Checked;
        _settings.AutoReconnect = _autoReconnectCheck.Checked;
        _settings.LowPowerWhenIdle = _lowPowerCheck.Checked;
        _settings.IdleSeconds = (int)_idleSecondsInput.Value;
        _settings.LowPowerIntervalMs = (int)_lowPowerIntervalInput.Value;
        _settings.CpuTempPreference = GetSelectedCpuTempPreference();
        _settings.GpuTempPreference = GetSelectedGpuTempPreference();
        _settings.DefaultComPort = (_defaultPortCombo.SelectedItem as string ?? string.Empty).Trim();

        try
        {
            StartupManager.SetEnabled(_settings.StartWithWindows);
        }
        catch (Exception ex)
        {
            Log(F("log.startupSetError", ex.Message));
        }

        AppSettingsStore.Save(_settings);
        _monitorService.SetTempPreferences(_settings.CpuTempPreference, _settings.GpuTempPreference);
        UpdateSendIntervalForIdle(force: true);
        if (!_settings.AutoReconnect)
        {
            _reconnectTimer.Stop();
        }

        if (!_serialService.IsConnected &&
            !string.IsNullOrWhiteSpace(_settings.DefaultComPort) &&
            _portCombo.Items.Contains(_settings.DefaultComPort))
        {
            _portCombo.SelectedItem = _settings.DefaultComPort;
        }

        Log(T("log.settingsSaved"));
        MessageBox.Show(this, T("msg.settingsSaved"), T("msg.settingsTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void OnLanguageSelectionChanged()
    {
        if (_languageComboUpdating)
        {
            return;
        }

        if (_languageCombo.SelectedItem is not LanguageOption selected)
        {
            return;
        }

        _language = selected.Language;
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        Text = T("app.title");

        _portLabel.Text = T("label.com");
        _baudLabel.Text = T("label.baud");
        _intervalLabel.Text = T("label.interval");

        _refreshPortsButton.Text = T("button.refresh");
        _connectButton.Text = _serialService.IsConnected ? T("button.disconnect") : T("button.connect");
        _toggleLogButton.Text = T("button.toggleLog");

        _settingsLanguageLabel.Text = T("settings.language");
        _settingsDefaultComLabel.Text = T("settings.defaultCom");
        _startupCheck.Text = T("settings.startWithWindows");
        _monitorSleepCheck.Text = T("settings.sleepWhenMonitorsOff");
        _autoReconnectCheck.Text = T("settings.autoReconnect");
        _lowPowerCheck.Text = T("settings.lowPowerWhenIdle");
        _idleSecondsLabel.Text = T("settings.idleSeconds");
        _lowPowerIntervalLabel.Text = T("settings.lowPowerInterval");
        _cpuTempPrefLabel.Text = T("settings.cpuTempPref");
        _gpuTempPrefLabel.Text = T("settings.gpuTempPref");
        _saveSettingsButton.Text = T("settings.save");


        _lastSendTitleLabel.Text = T("status.lastSend");
        _lastAckTitleLabel.Text = T("status.lastAck");
        _sampleMsTitleLabel.Text = T("status.sampleMs");

        _trayOpenItem.Text = T("tray.open");
        _trayExitItem.Text = T("tray.exit");
        _trayIcon.Text = T("tray.title");

        if (!_serialService.IsConnected)
        {
            _lastSendLabel.Text = T("status.never");
            _lastAckLabel.Text = T("status.never");
            _sampleMsLabel.Text = T("status.na");
            SetDisconnectedStatus();
        }

        PopulateLanguageCombo(_language);
        PopulateTempPreferenceCombos();
    }

    private void PopulateLanguageCombo(UiLanguage selectedLanguage)
    {
        _languageComboUpdating = true;
        try
        {
            _languageCombo.Items.Clear();
            _languageCombo.Items.Add(new LanguageOption(UiLanguage.Czech, T("lang.czech")));
            _languageCombo.Items.Add(new LanguageOption(UiLanguage.English, T("lang.english")));

            for (int i = 0; i < _languageCombo.Items.Count; i++)
            {
                if (_languageCombo.Items[i] is LanguageOption option && option.Language == selectedLanguage)
                {
                    _languageCombo.SelectedIndex = i;
                    break;
                }
            }
        }
        finally
        {
            _languageComboUpdating = false;
        }
    }

    private void PopulateTempPreferenceCombos()
    {
        PopulateCpuTempCombo(_settings.CpuTempPreference);
        PopulateGpuTempCombo(_settings.GpuTempPreference);
    }

    private void PopulateCpuTempCombo(CpuTempPreference selected)
    {
        _cpuTempPrefCombo.BeginUpdate();
        try
        {
            _cpuTempPrefCombo.Items.Clear();
            _cpuTempPrefCombo.Items.Add(new CpuTempOption(CpuTempPreference.Auto, T("pref.auto")));
            _cpuTempPrefCombo.Items.Add(new CpuTempOption(CpuTempPreference.Package, T("pref.cpu.package")));
            _cpuTempPrefCombo.Items.Add(new CpuTempOption(CpuTempPreference.CoreMax, T("pref.cpu.coreMax")));
            _cpuTempPrefCombo.Items.Add(new CpuTempOption(CpuTempPreference.CoreAverage, T("pref.cpu.coreAvg")));
            _cpuTempPrefCombo.Items.Add(new CpuTempOption(CpuTempPreference.Core, T("pref.cpu.core")));
            _cpuTempPrefCombo.Items.Add(new CpuTempOption(CpuTempPreference.TdieTctl, T("pref.cpu.tdie")));
            _cpuTempPrefCombo.Items.Add(new CpuTempOption(CpuTempPreference.Board, T("pref.cpu.board")));

            for (int i = 0; i < _cpuTempPrefCombo.Items.Count; i++)
            {
                if (_cpuTempPrefCombo.Items[i] is CpuTempOption option && option.Preference == selected)
                {
                    _cpuTempPrefCombo.SelectedIndex = i;
                    break;
                }
            }
        }
        finally
        {
            _cpuTempPrefCombo.EndUpdate();
        }
    }

    private void PopulateGpuTempCombo(GpuTempPreference selected)
    {
        _gpuTempPrefCombo.BeginUpdate();
        try
        {
            _gpuTempPrefCombo.Items.Clear();
            _gpuTempPrefCombo.Items.Add(new GpuTempOption(GpuTempPreference.Auto, T("pref.auto")));
            _gpuTempPrefCombo.Items.Add(new GpuTempOption(GpuTempPreference.Gpu, T("pref.gpu.gpu")));
            _gpuTempPrefCombo.Items.Add(new GpuTempOption(GpuTempPreference.GpuCore, T("pref.gpu.gpuCore")));
            _gpuTempPrefCombo.Items.Add(new GpuTempOption(GpuTempPreference.Core, T("pref.gpu.core")));
            _gpuTempPrefCombo.Items.Add(new GpuTempOption(GpuTempPreference.Edge, T("pref.gpu.edge")));
            _gpuTempPrefCombo.Items.Add(new GpuTempOption(GpuTempPreference.HotSpot, T("pref.gpu.hotspot")));

            for (int i = 0; i < _gpuTempPrefCombo.Items.Count; i++)
            {
                if (_gpuTempPrefCombo.Items[i] is GpuTempOption option && option.Preference == selected)
                {
                    _gpuTempPrefCombo.SelectedIndex = i;
                    break;
                }
            }
        }
        finally
        {
            _gpuTempPrefCombo.EndUpdate();
        }
    }

    private CpuTempPreference GetSelectedCpuTempPreference()
    {
        if (_cpuTempPrefCombo.SelectedItem is CpuTempOption option)
        {
            return option.Preference;
        }
        return CpuTempPreference.Auto;
    }

    private GpuTempPreference GetSelectedGpuTempPreference()
    {
        if (_gpuTempPrefCombo.SelectedItem is GpuTempOption option)
        {
            return option.Preference;
        }
        return GpuTempPreference.Auto;
    }

    private void OnFormResize(object? sender, EventArgs e)
    {
        if (_isExiting || WindowState != FormWindowState.Minimized)
        {
            return;
        }

        MinimizeToTray();
    }

    private void MinimizeToTray()
    {
        Hide();
        ShowInTaskbar = false;

        if (_trayHintShown)
        {
            return;
        }

        _trayHintShown = true;
        _trayIcon.ShowBalloonTip(
            1400,
            T("tray.title"),
            T("tray.running"),
            ToolTipIcon.Info);
    }

    private void RestoreFromTray()
    {
        if (_isExiting)
        {
            return;
        }

        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitFromTray()
    {
        _isExiting = true;
        Close();
    }

    private void SetConnectedStatus(string port, int baud)
    {
        _connectButton.Text = T("button.disconnect");
        _connectionLabel.Text = F("status.connected", port, baud);
        _connectionLabel.ForeColor = AccentGreen;
        _sampleMsLabel.Text = T("status.na");
        _lastAckLabel.Text = T("status.never");
    }

    private void SetDisconnectedStatus()
    {
        _connectButton.Text = T("button.connect");
        _connectionLabel.Text = T("status.disconnected");
        _connectionLabel.ForeColor = ErrorText;

        if (!_serialService.IsConnected)
        {
            _lastSendLabel.Text = T("status.never");
            _lastAckLabel.Text = T("status.never");
            _sampleMsLabel.Text = T("status.na");
        }
    }

    private void Disconnect(string reason, bool allowAutoReconnect = true)
    {
        _sendTimer.Stop();
        _serialService.Disconnect();
        _smoother.Reset();
        _ackSeen = false;
        _lastAckUtc = DateTime.MinValue;

        SetDisconnectedStatus();
        Log(reason);

        if (!allowAutoReconnect || _manualDisconnect)
        {
            _reconnectTimer.Stop();
            return;
        }

        if (_settings.AutoReconnect && !_isExiting)
        {
            _reconnectTimer.Start();
        }
    }

    private void Log(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        _logBox.AppendText(line);
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _isExiting = true;
        _sendTimer.Stop();
        _reconnectTimer.Stop();
        UnregisterDisplayPowerNotification();
        _serialService.Dispose();
        _monitorService.Dispose();

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayMenu.Dispose();

        if (_ownsAppIcon)
        {
            _appIcon.Dispose();
        }
    }

    private void ApplyDarkTheme()
    {
        BackColor = WindowBack;
        ForeColor = ForeText;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        _logBox.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);

        ApplyThemeRecursive(this);
    }

    private void ApplyThemeRecursive(Control control)
    {
        switch (control)
        {
            case Form:
                control.BackColor = WindowBack;
                control.ForeColor = ForeText;
                break;
            case TableLayoutPanel:
            case FlowLayoutPanel:
            case Panel:
                control.BackColor = PanelBack;
                control.ForeColor = ForeText;
                break;
            case SplitContainer split:
                split.BackColor = WindowBack;
                split.ForeColor = ForeText;
                split.Panel1.BackColor = PanelBack;
                split.Panel2.BackColor = PanelBack;
                break;
            case GroupBox:
                control.BackColor = PanelBack;
                control.ForeColor = ForeText;
                break;
            case Label:
                control.ForeColor = ForeText;
                break;
            case TextBox text:
                text.BackColor = InputBack;
                text.ForeColor = ForeText;
                text.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox combo:
                combo.BackColor = InputBack;
                combo.ForeColor = ForeText;
                combo.FlatStyle = FlatStyle.Flat;
                break;
            case NumericUpDown numeric:
                numeric.BackColor = InputBack;
                numeric.ForeColor = ForeText;
                break;
            case CheckBox check:
                check.BackColor = PanelBack;
                check.ForeColor = ForeText;
                break;
            case Button button:
                button.BackColor = InputBack;
                button.ForeColor = ForeText;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(58, 58, 58);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(68, 68, 68);
                break;
        }

        foreach (Control child in control.Controls)
        {
            ApplyThemeRecursive(child);
        }
    }

    private static (Icon Icon, bool Owns) CreateAppIcon()
    {
        Icon? loaded = TryLoadPngIcon();
        if (loaded is not null)
        {
            return (loaded, true);
        }

        return (SystemIcons.Application, false);
    }

    private static Icon? TryLoadPngIcon()
    {
        string? path = FindFanPngPath();
        if (path is null)
        {
            return null;
        }

        try
        {
            using var image = Image.FromFile(path);
            using var bitmap = new Bitmap(image, new Size(32, 32));
            nint handle = bitmap.GetHicon();

            try
            {
                using Icon fromHandle = Icon.FromHandle(handle);
                return (Icon)fromHandle.Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }
        catch
        {
            return null;
        }
    }

    private static string? FindFanPngPath()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "fan.png"),
            Path.Combine(AppContext.BaseDirectory, "fan.PNG"),
            Path.Combine(Environment.CurrentDirectory, "fan.png"),
            Path.Combine(Environment.CurrentDirectory, "fan.PNG")
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(nint hIcon);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint RegisterPowerSettingNotification(
        nint hRecipient,
        in Guid powerSettingGuid,
        int flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterPowerSettingNotification(nint handle);

    private static bool IsRunningAsAdministrator()
    {
        WindowsIdentity? identity = WindowsIdentity.GetCurrent();
        if (identity is null)
        {
            return false;
        }

        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private string F(string key, params object[] args)
    {
        return string.Format(CultureInfo.InvariantCulture, T(key), args);
    }

    private string T(string key)
    {
        if (_language == UiLanguage.English)
        {
            return key switch
            {
                "app.title" => "PC Monitor Host (ESP32)",
                "label.com" => "COM Port:",
                "label.baud" => "Baud Rate:",
                "label.interval" => "Interval (ms):",
                "button.refresh" => "Refresh Ports",
                "button.connect" => "Connect",
                "button.disconnect" => "Disconnect",
                "button.toggleLog" => "📋 Log",
                "settings.language" => "Language:",
                "settings.defaultCom" => "Default COM:",
                "settings.startWithWindows" => "Start with Windows",
                "settings.sleepWhenMonitorsOff" => "Sleep ESP display when monitor is OFF",
                "settings.autoReconnect" => "Auto reconnect",
                "settings.lowPowerWhenIdle" => "Low power when idle",
                "settings.idleSeconds" => "Idle timeout (s):",
                "settings.lowPowerInterval" => "Low power interval (ms):",
                "settings.cpuTempPref" => "CPU temp sensor:",
                "settings.gpuTempPref" => "GPU temp sensor:",
                "settings.save" => "Save Settings",
                "group.preview" => "Current Telemetry",
                "group.log" => "Log",
                "status.title" => "Status:",
                "status.lastSend" => "Last send:",
                "status.lastAck" => "Last ACK:",
                "status.sampleMs" => "Sample (ms):",
                "status.disconnected" => "Disconnected",
                "status.connected" => "Connected to {0} @ {1}",
                "status.never" => "Never",
                "status.na" => "N/A",
                "tray.open" => "Open",
                "tray.exit" => "Exit",
                "tray.title" => "PC Monitor Host",
                "tray.running" => "Application is running in tray. Double-click icon to restore.",
                "msg.selectCom" => "Select COM port.",
                "msg.portRequiredTitle" => "Port required",
                "msg.connectionFailed" => "Connection failed",
                "msg.settingsSaved" => "Settings saved.",
                "msg.settingsTitle" => "Settings",
                "log.tipAdmin" => "Tip: Run app as Administrator for better CPU temperature access in LibreHardwareMonitor.",
                "log.portsRefreshed" => "Ports refreshed: {0}",
                "log.connected" => "Connected: {0} @ {1}",
                "log.connectError" => "Connection error: {0}",
                "log.manualDisconnect" => "Manual disconnect.",
                "log.autoReconnect" => "Auto reconnect: {0} @ {1}",
                "log.autoReconnectError" => "Auto reconnect error: {0}",
                "log.ackTimeout" => "ACK timeout, reconnecting.",
                "log.runtimeError" => "Runtime error: {0}",
                "log.settingsSaved" => "Settings saved.",
                "log.startupSetError" => "Unable to set startup with Windows: {0}",
                "preview.cpuUsage" => "CPU usage:",
                "preview.cpuTemp" => "CPU temp:",
                "preview.cpuCt" => "CPU cores/threads:",
                "preview.ramUsage" => "RAM usage:",
                "preview.ramUsed" => "RAM used:",
                "preview.gpuUsage" => "GPU usage:",
                "preview.gpuTemp" => "GPU temp:",
                "preview.netDown" => "Net download:",
                "preview.netUp" => "Net upload:",
                "preview.disks" => "Disks:",
                "preview.power" => "Power:",
                "preview.monitorState" => "Monitor state:",
                "preview.displaySleep" => "Display sleep request:",
                "preview.packet" => "Packet:",
                "lang.czech" => "Czech",
                "lang.english" => "English",
                "pref.auto" => "Auto",
                "pref.cpu.package" => "Package",
                "pref.cpu.coreMax" => "Core max",
                "pref.cpu.coreAvg" => "Core avg",
                "pref.cpu.core" => "Core",
                "pref.cpu.tdie" => "Tdie/Tctl",
                "pref.cpu.board" => "Board",
                "pref.gpu.gpu" => "GPU",
                "pref.gpu.gpuCore" => "GPU Core",
                "pref.gpu.core" => "Core",
                "pref.gpu.edge" => "Edge",
                "pref.gpu.hotspot" => "Hot Spot",
                "misc.none" => "none",
                "misc.yes" => "YES",
                "misc.no" => "NO",
                "misc.displayOff" => "OFF",
                "misc.displayOn" => "ON",
                "misc.displayDimmed" => "DIMMED",
                "misc.displayUnknown" => "UNKNOWN",
                _ => key
            };
        }

        return key switch
        {
            "app.title" => "PC Monitor Host (ESP32)",
            "label.com" => "COM port:",
            "label.baud" => "Rychlost:",
            "label.interval" => "Interval (ms):",
            "button.refresh" => "Obnovit porty",
            "button.connect" => "Připojit",
            "button.disconnect" => "Odpojit",
            "button.toggleLog" => "📋 Log",
            "settings.language" => "Jazyk:",
            "settings.defaultCom" => "Výchozí COM:",
            "settings.startWithWindows" => "Spouštět s Windows",
            "settings.sleepWhenMonitorsOff" => "Uspat ESP displej při vypnutém monitoru",
            "settings.autoReconnect" => "Automaticky připojit",
            "settings.lowPowerWhenIdle" => "Úsporný režim při neaktivitě",
            "settings.idleSeconds" => "Neaktivita (s):",
            "settings.lowPowerInterval" => "Interval úspory (ms):",
            "settings.cpuTempPref" => "Senzor CPU teploty:",
            "settings.gpuTempPref" => "Senzor GPU teploty:",
            "settings.save" => "Uložit nastavení",
            "group.preview" => "Aktuální telemetrie",
            "group.log" => "Log",
            "status.title" => "Stav:",
            "status.lastSend" => "Poslední odeslání:",
            "status.lastAck" => "Poslední ACK:",
            "status.sampleMs" => "Vzorek (ms):",
            "status.disconnected" => "Odpojeno",
            "status.connected" => "Připojeno k {0} @ {1}",
            "status.never" => "Nikdy",
            "status.na" => "N/A",
            "tray.open" => "Otevřít",
            "tray.exit" => "Ukončit",
            "tray.title" => "PC Monitor Host",
            "tray.running" => "Aplikace běží na pozadí. Dvojklik na ikonu ji obnoví.",
            "msg.selectCom" => "Vyberte COM port.",
            "msg.portRequiredTitle" => "Port je povinný",
            "msg.connectionFailed" => "Připojení selhalo",
            "msg.settingsSaved" => "Nastavení uloženo.",
            "msg.settingsTitle" => "Nastavení",
            "log.tipAdmin" => "Tip: Spusťte aplikaci jako správce pro přesnější CPU teploty z LibreHardwareMonitor.",
            "log.portsRefreshed" => "Porty obnoveny: {0}",
            "log.connected" => "Připojeno: {0} @ {1}",
            "log.connectError" => "Chyba připojení: {0}",
            "log.manualDisconnect" => "Ruční odpojení.",
            "log.autoReconnect" => "Automatické připojení: {0} @ {1}",
            "log.autoReconnectError" => "Chyba automatického připojení: {0}",
            "log.ackTimeout" => "ACK timeout, znovu připojuji.",
            "log.runtimeError" => "Chyba za běhu: {0}",
            "log.settingsSaved" => "Nastavení uloženo.",
            "log.startupSetError" => "Nepodařilo se nastavit spouštění s Windows: {0}",
            "preview.cpuUsage" => "CPU využití:",
            "preview.cpuTemp" => "CPU teplota:",
            "preview.cpuCt" => "CPU jádra/vlákna:",
            "preview.ramUsage" => "RAM využití:",
            "preview.ramUsed" => "RAM použito:",
            "preview.gpuUsage" => "GPU využití:",
            "preview.gpuTemp" => "GPU teplota:",
            "preview.netDown" => "Síť download:",
            "preview.netUp" => "Síť upload:",
            "preview.disks" => "Disky:",
            "preview.power" => "Spotřeba:",
            "preview.monitorState" => "Stav monitoru:",
            "preview.displaySleep" => "Požadavek uspání:",
            "preview.packet" => "Paket:",
            "lang.czech" => "Čeština",
            "lang.english" => "Angličtina",
            "pref.auto" => "Auto",
            "pref.cpu.package" => "Package",
            "pref.cpu.coreMax" => "Jádro max",
            "pref.cpu.coreAvg" => "Jádro průměr",
            "pref.cpu.core" => "Jádro",
            "pref.cpu.tdie" => "Tdie/Tctl",
            "pref.cpu.board" => "Deska",
            "pref.gpu.gpu" => "GPU",
            "pref.gpu.gpuCore" => "GPU Core",
            "pref.gpu.core" => "Jádro",
            "pref.gpu.edge" => "Edge",
            "pref.gpu.hotspot" => "Hot Spot",
            "misc.none" => "žádné",
            "misc.yes" => "ANO",
            "misc.no" => "NE",
            "misc.displayOff" => "VYPNUTO",
            "misc.displayOn" => "ZAPNUTO",
            "misc.displayDimmed" => "ZTLUMENO",
            "misc.displayUnknown" => "NEZNÁMÝ",
            _ => key
        };
    }

    private static string FormatPercent(float value)
    {
        return value < 0f ? "N/A" : $"{value:0.0}%";
    }

    private static string FormatTemp(float value)
    {
        return value < 0f ? "N/A" : $"{value:0.0} C";
    }

    private static string FormatGb(float value)
    {
        return value < 0f ? "N/A" : $"{value:0.0} GB";
    }

    private static string FormatMBps(float value)
    {
        return value < 0f ? "N/A" : $"{value:0.00} MB/s";
    }

    private static string FormatW(float value)
    {
        return value < 0f ? "N/A" : $"{value:0.0} W";
    }

    private static string FormatDisks(IReadOnlyList<DiskUsageSample> disks)
    {
        if (disks.Count == 0)
        {
            return "N/A";
        }

        var parts = new List<string>(disks.Count);
        for (int i = 0; i < disks.Count; i++)
        {
            DiskUsageSample disk = disks[i];
            parts.Add($"{disk.Label}:{disk.UsagePercent:0}%");
        }

        return string.Join("  ", parts);
    }

    private string DisplayStateLabel(DisplayState state)
    {
        return state switch
        {
            DisplayState.Off => T("misc.displayOff"),
            DisplayState.On => T("misc.displayOn"),
            DisplayState.Dimmed => T("misc.displayDimmed"),
            _ => T("misc.displayUnknown")
        };
    }

    private enum DisplayState
    {
        Unknown,
        Off,
        On,
        Dimmed
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public int DataLength;
        public byte Data;
    }

    private readonly record struct LanguageOption(UiLanguage Language, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly record struct CpuTempOption(CpuTempPreference Preference, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly record struct GpuTempOption(GpuTempPreference Preference, string Label)
    {
        public override string ToString() => Label;
    }
}
