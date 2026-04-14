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
    // Theme colors
    private static readonly Color WindowBack = Color.FromArgb(18, 18, 24);
    private static readonly Color PanelBack = Color.FromArgb(28, 28, 36);
    private static readonly Color CardBack = Color.FromArgb(34, 34, 44);
    private static readonly Color InputBack = Color.FromArgb(44, 44, 56);
    private static readonly Color ForeText = Color.FromArgb(230, 230, 235);
    private static readonly Color AccentGreen = Color.FromArgb(0, 200, 140);
    private static readonly Color AccentRed = Color.FromArgb(220, 60, 80);
    private static readonly Color ErrorText = Color.FromArgb(255, 120, 120);

    // Connection bar controls
    private readonly Label _portLabel = new() { AutoSize = true, ForeColor = Color.FromArgb(180, 180, 190), Font = new Font("Segoe UI", 9.5F) };
    private readonly ComboBox _portCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly Button _connectButton = new() { AutoSize = false, MinimumSize = new Size(120, 34), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
    private readonly Button _refreshPortsButton = new() { AutoSize = false, MinimumSize = new Size(100, 34) };

    // Settings controls
    private readonly Label _settingsLanguageLabel = new() { AutoSize = true };
    private readonly ComboBox _languageCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly Label _settingsDefaultComLabel = new() { AutoSize = true };
    private readonly ComboBox _defaultPortCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly CheckBox _startupCheck = new() { AutoSize = true };
    private readonly CheckBox _monitorSleepCheck = new() { AutoSize = true };
    private readonly CheckBox _autoReconnectCheck = new() { AutoSize = true };
    private readonly CheckBox _lowPowerCheck = new() { AutoSize = true };
    private readonly Label _idleSecondsLabel = new() { AutoSize = true };
    private readonly NumericUpDown _idleSecondsInput = new() { Minimum = 10, Maximum = 3600, Increment = 10, Value = 60, Width = 70 };
    private readonly Label _lowPowerIntervalLabel = new() { AutoSize = true };
    private readonly NumericUpDown _lowPowerIntervalInput = new() { Minimum = 80, Maximum = 5000, Increment = 20, Value = 250, Width = 80 };
    private readonly Label _gpuTempPrefLabel = new() { AutoSize = true };
    private readonly ComboBox _gpuTempPrefCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly Label _cpuTempPrefLabel = new() { AutoSize = true };
    private readonly ComboBox _cpuTempPrefCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly Button _debugWakeButton = new() { AutoSize = false, MinimumSize = new Size(110, 28), Enabled = false };
    private readonly Button _debugAlertButton = new() { AutoSize = false, MinimumSize = new Size(110, 28), Enabled = false };
    private readonly Button _saveSettingsButton = new() { AutoSize = false, MinimumSize = new Size(140, 30) };

    // Status bar
    private readonly StatusIndicator _statusIndicator = new() { Width = 300 };
    private readonly Label _lastSendLabel = new() { AutoSize = true };
    private readonly Label _lastAckLabel = new() { AutoSize = true };
    private readonly Label _sampleMsLabel = new() { AutoSize = true };

    // Dashboard cards
    private readonly TelemetryCard _cpuCard = new() { Title = "CPU", Unit = "%", AccentColor = AccentGreen, Maximum = 100f };
    private readonly TelemetryCard _gpuCard = new() { Title = "GPU", Unit = "%", AccentColor = Color.FromArgb(80, 140, 255), Maximum = 100f };
    private readonly TelemetryCard _cpuTempCard = new() { Title = "CPU Teplota", Unit = "°C", AccentColor = Color.FromArgb(255, 180, 0), Maximum = 110f };
    private readonly TelemetryCard _gpuTempCard = new() { Title = "GPU Teplota", Unit = "°C", AccentColor = Color.FromArgb(255, 120, 60), Maximum = 110f };
    private readonly TelemetryCard _ramCard = new() { Title = "RAM", Unit = "%", AccentColor = Color.FromArgb(180, 120, 255), Maximum = 100f };
    private readonly TelemetryCard _powerCard = new() { Title = "Spotřeba", Unit = "W", AccentColor = Color.FromArgb(255, 200, 0), Maximum = 600f };
    private readonly SparklineGraph _netSparkline = new() { LineColor = AccentGreen, FillColor = Color.FromArgb(30, 0, 200, 140), Height = 50, Maximum = 100f };
    private readonly SparklineGraph _diskSparkline = new() { LineColor = Color.FromArgb(200, 140, 255), FillColor = Color.FromArgb(30, 200, 140, 255), Height = 50, Maximum = 100f };
    private readonly Label _netLabel = new() { Text = " SÍŤ", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(180, 180, 190), AutoSize = false, Height = 20 };
    private readonly Label _diskLabel = new() { Text = "💾 DISKY", Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(180, 180, 190), AutoSize = false, Height = 20 };
    private readonly Label _netValueLabel = new() { Text = "↓ 0  ↑ 0 MB/s", Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(160, 160, 170), AutoSize = true };
    private readonly Label _diskValueLabel = new() { Text = "0%", Font = new Font("Segoe UI", 8.5F), ForeColor = Color.FromArgb(160, 160, 170), AutoSize = true };

    // Log
    private readonly RichTextBox _logBox = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = RichTextBoxScrollBars.Vertical,
        Dock = DockStyle.Fill,
        WordWrap = true,
        BorderStyle = BorderStyle.None,
        BackColor = Color.FromArgb(16, 16, 22),
        ForeColor = Color.FromArgb(200, 200, 210),
        Font = new Font("Consolas", 8.5F)
    };
    private readonly Button _toggleLogButton = new() { Text = "📋 Log", AutoSize = false, MinimumSize = new Size(80, 28) };
    private readonly Panel _logPanel = new() { Dock = DockStyle.Fill, Visible = false };

    // Toast
    private readonly ToastNotification _toast = new();

    // Services
    private readonly Timer _sendTimer = new();
    private readonly Timer _reconnectTimer = new();
    private readonly SerialService _serialService = new();
    private readonly HardwareMonitorService _monitorService = new();
    private readonly TelemetrySmoother _smoother = new();

    // Tray
    private readonly NotifyIcon _trayIcon = new();
    private readonly ContextMenuStrip _trayMenu = new();
    private readonly ToolStripMenuItem _trayOpenItem = new();
    private readonly ToolStripMenuItem _trayExitItem = new();

    // State
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
    private DateTime _debugSleepUntilUtc = DateTime.MinValue;
    private bool _debugAlertPending;

    private const int WM_POWERBROADCAST = 0x0218;
    private const int PBT_POWERSETTINGCHANGE = 0x8013;
    private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;
    private static readonly Guid GuidSessionDisplayStatus = new("2B84C20E-AD23-4DDF-93DB-05FFBD7EFCA5");
    private static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan AutoReconnectLogInterval = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan DebugSleepDuration = TimeSpan.FromMilliseconds(1400);

    public MainForm()
    {
        _settings = AppSettingsStore.Load();
        _language = _settings.Language;
        (_appIcon, _ownsAppIcon) = CreateAppIcon();

        Text = T("app.title");
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(960, 700);
        Size = new Size(1050, 740);
        Icon = _appIcon;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUi();
        ApplyDarkTheme();
        WireEvents();
        InitializeTray();
        ApplyLocalization();

        _sendTimer.Interval = (int)_idleSecondsInput.Value;
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
        if (_powerSettingNotificationHandle != nint.Zero || !IsHandleCreated) return;
        _powerSettingNotificationHandle = RegisterPowerSettingNotification(Handle, GuidSessionDisplayStatus, DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    private void UnregisterDisplayPowerNotification()
    {
        if (_powerSettingNotificationHandle == nint.Zero) return;
        UnregisterPowerSettingNotification(_powerSettingNotificationHandle);
        _powerSettingNotificationHandle = nint.Zero;
    }

    private void ProcessPowerBroadcast(nint lParam)
    {
        if (lParam == nint.Zero) return;
        var setting = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(lParam);
        if (setting.PowerSetting != GuidSessionDisplayStatus) return;

        var dataPtr = nint.Add(lParam, Marshal.OffsetOf<POWERBROADCAST_SETTING>(nameof(POWERBROADCAST_SETTING.Data)).ToInt32());
        int stateValue = setting.DataLength >= sizeof(int) ? Marshal.ReadInt32(dataPtr) : Marshal.ReadByte(dataPtr);

        _displayState = stateValue switch { 0 => DisplayState.Off, 1 => DisplayState.On, 2 => DisplayState.Dimmed, _ => DisplayState.Unknown };
    }

    private void WireEvents()
    {
        _refreshPortsButton.Click += (_, _) => RefreshPorts();
        _connectButton.Click += (_, _) => ToggleConnection();
        _sendTimer.Tick += (_, _) => OnSendTimerTick();
        _reconnectTimer.Tick += (_, _) => TryAutoReconnect();
        _serialService.LineReceived += OnSerialLineReceived;
        _languageCombo.SelectedIndexChanged += (_, _) => OnLanguageSelectionChanged();
        _debugWakeButton.Click += (_, _) => StartDebugWakeTest();
        _debugAlertButton.Click += (_, _) => StartDebugAlertTest();
        _saveSettingsButton.Click += (_, _) => SaveSettings();
        _toggleLogButton.Click += (_, _) => _logPanel.Visible = !_logPanel.Visible;
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
        catch { }

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
            Padding = new Padding(10),
            BackColor = WindowBack
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildTopBar(), 0, 0);
        root.Controls.Add(BuildSettingsPanel(), 0, 1);
        root.Controls.Add(BuildMainContent(), 0, 2);
        root.Controls.Add(BuildStatusBar(), 0, 3);

        Controls.Add(root);
        Controls.Add(_toast);
    }

    private Control BuildTopBar()
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, AutoSize = true, CornerRadius = 10, Padding = new Padding(14, 10, 14, 10) };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent };

        _portLabel.Text = T("label.com");
        _connectButton.Text = T("button.connect");
        _connectButton.BackColor = AccentGreen;
        _connectButton.ForeColor = Color.White;
        _connectButton.FlatStyle = FlatStyle.Flat;
        _connectButton.FlatAppearance.BorderSize = 0;
        _connectButton.Margin = new Padding(8, 0, 4, 0);

        _refreshPortsButton.Text = T("button.refresh");
        _refreshPortsButton.BackColor = InputBack;
        _refreshPortsButton.ForeColor = ForeText;
        _refreshPortsButton.FlatStyle = FlatStyle.Flat;
        _refreshPortsButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);

        panel.Controls.Add(_portLabel);
        panel.Controls.Add(_portCombo);
        panel.Controls.Add(_connectButton);
        panel.Controls.Add(_refreshPortsButton);

        card.Controls.Add(panel);
        return card;
    }

    private Control BuildSettingsPanel()
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, AutoSize = true, CornerRadius = 10, Padding = new Padding(12, 10, 12, 10), Margin = new Padding(0, 8, 0, 8) };
        var mainPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent };

        mainPanel.Controls.Add(BuildSettingsGroup(T("settings.group.general"), BuildGeneralControls()));
        mainPanel.Controls.Add(BuildSettingsGroup(T("settings.group.connection"), BuildConnectionControls()));
        mainPanel.Controls.Add(BuildSettingsGroup(T("settings.group.power"), BuildPowerControls()));
        mainPanel.Controls.Add(BuildSettingsGroup(T("settings.group.sensors"), BuildSensorControls()));
        mainPanel.Controls.Add(BuildSettingsGroup(T("settings.group.debug"), BuildDebugControls()));

        card.Controls.Add(mainPanel);
        return card;
    }

    private Control[] BuildGeneralControls() => new Control[] {
        _settingsLanguageLabel, _languageCombo, _startupCheck
    };

    private Control[] BuildConnectionControls() => new Control[] {
        _settingsDefaultComLabel, _defaultPortCombo, _autoReconnectCheck
    };

    private Control[] BuildPowerControls() => new Control[] {
        _monitorSleepCheck, _lowPowerCheck, _idleSecondsLabel, _idleSecondsInput,
        _lowPowerIntervalLabel, _lowPowerIntervalInput
    };

    private Control[] BuildSensorControls() => new Control[] {
        _cpuTempPrefLabel, _cpuTempPrefCombo, _gpuTempPrefLabel, _gpuTempPrefCombo
    };

    private Control[] BuildDebugControls() => new Control[] {
        _debugWakeButton, _debugAlertButton, _saveSettingsButton
    };

    private Control BuildSettingsGroup(string title, Control[] controls)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.TopDown, MinimumSize = new Size(200, 0), Margin = new Padding(8, 4, 8, 4), BackColor = Color.Transparent };

        var titleLabel = new Label { Text = title, Font = new Font("Segoe UI", 10F, FontStyle.SemiBold), ForeColor = AccentGreen, AutoSize = true, Margin = new Padding(0, 2, 0, 8) };
        panel.Controls.Add(titleLabel);

        foreach (var ctrl in controls)
        {
            ctrl.Margin = new Padding(0, 3, 0, 3);
            ctrl.ForeColor = ForeText;
            panel.Controls.Add(ctrl);
        }

        return panel;
    }

    private Control BuildMainContent()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 620,
            SplitterWidth = 3,
            BackColor = Color.FromArgb(50, 50, 60)
        };

        BuildDashboardPanel(split.Panel1);
        BuildLogPanel(split.Panel2);

        return split;
    }

    private void BuildDashboardPanel(Panel parent)
    {
        parent.Padding = new Padding(6);
        parent.BackColor = WindowBack;

        // Row 1: CPU + GPU (large cards)
        var row1 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 115, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, Padding = new Padding(2, 4, 2, 4) };
        _cpuCard.Size = new Size(300, 106);
        _gpuCard.Size = new Size(300, 106);
        row1.Controls.Add(_cpuCard);
        row1.Controls.Add(_gpuCard);

        // Row 2: Temps + RAM + Power
        var row2 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 115, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, Padding = new Padding(2, 4, 2, 4) };
        _cpuTempCard.Size = new Size(150, 106);
        _gpuTempCard.Size = new Size(150, 106);
        _ramCard.Size = new Size(150, 106);
        _powerCard.Size = new Size(150, 106);
        row2.Controls.Add(_cpuTempCard);
        row2.Controls.Add(_gpuTempCard);
        row2.Controls.Add(_ramCard);
        row2.Controls.Add(_powerCard);

        // Row 3: Network + Disk sparklines
        var row3 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 110, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, Padding = new Padding(2, 4, 2, 4) };

        var netPanel = new RoundedPanel { Size = new Size(295, 100), CornerRadius = 8, Padding = new Padding(8, 6, 8, 6) };
        _netLabel.Dock = DockStyle.Top;
        _netValueLabel.Dock = DockStyle.Top;
        _netSparkline.Dock = DockStyle.Fill;
        netPanel.Controls.Add(_netSparkline);
        netPanel.Controls.Add(_netValueLabel);
        netPanel.Controls.Add(_netLabel);

        var diskPanel = new RoundedPanel { Size = new Size(295, 100), CornerRadius = 8, Padding = new Padding(8, 6, 8, 6) };
        _diskLabel.Dock = DockStyle.Top;
        _diskValueLabel.Dock = DockStyle.Top;
        _diskSparkline.Dock = DockStyle.Fill;
        diskPanel.Controls.Add(_diskSparkline);
        diskPanel.Controls.Add(_diskValueLabel);
        diskPanel.Controls.Add(_diskLabel);

        row3.Controls.Add(netPanel);
        row3.Controls.Add(diskPanel);

        parent.Controls.Add(row3);
        parent.Controls.Add(row2);
        parent.Controls.Add(row1);
    }

    private void BuildLogPanel(Panel parent)
    {
        parent.BackColor = WindowBack;

        var header = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, Padding = new Padding(6, 4, 6, 4) };

        _toggleLogButton.Text = T("button.logToggle");
        _toggleLogButton.BackColor = InputBack;
        _toggleLogButton.ForeColor = ForeText;
        _toggleLogButton.FlatStyle = FlatStyle.Flat;
        _toggleLogButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 80);

        var logTitle = new Label { Text = T("group.log"), Font = new Font("Segoe UI", 10F, FontStyle.SemiBold), ForeColor = ForeText, AutoSize = true, Padding = new Padding(6, 4, 0, 0) };

        var clearBtn = new Button { Text = T("button.clearLog"), AutoSize = true, FlatStyle = FlatStyle.Flat, BackColor = InputBack, ForeColor = ForeText, Margin = new Padding(6, 0, 0, 0) };
        clearBtn.Click += (_, _) => _logBox.Clear();

        header.Controls.Add(_toggleLogButton);
        header.Controls.Add(logTitle);
        header.Controls.Add(clearBtn);

        _logPanel.Controls.Add(_logBox);
        _logPanel.Controls.Add(header);
        parent.Controls.Add(_logPanel);
    }

    private Control BuildStatusBar()
    {
        var card = new RoundedPanel { Dock = DockStyle.Fill, AutoSize = true, CornerRadius = 8, Padding = new Padding(14, 8, 14, 8) };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent };

        _statusIndicator.Text = T("status.disconnected");
        _statusIndicator.DotColor = ErrorText;
        _statusIndicator.ForeColor = ForeText;

        _lastSendLabel.ForeColor = Color.FromArgb(150, 150, 160);
        _lastAckLabel.ForeColor = Color.FromArgb(150, 150, 160);
        _sampleMsLabel.ForeColor = Color.FromArgb(150, 150, 160);

        _lastSendLabel.Padding = new Padding(20, 0, 0, 0);
        _lastAckLabel.Padding = new Padding(20, 0, 0, 0);
        _sampleMsLabel.Padding = new Padding(20, 0, 0, 0);

        panel.Controls.Add(_statusIndicator);
        panel.Controls.Add(_lastSendLabel);
        panel.Controls.Add(_lastAckLabel);
        panel.Controls.Add(_sampleMsLabel);

        card.Controls.Add(panel);
        return card;
    }

    private void RefreshPorts()
    {
        IReadOnlyList<string> ports = SerialService.GetPortNames();
        string? preferredCurrent = _portCombo.SelectedItem as string ?? _settings.DefaultComPort;
        string? preferredDefault = _defaultPortCombo.SelectedItem as string ?? _settings.DefaultComPort;

        PopulatePortCombo(_portCombo, ports, preferredCurrent, includePreferredIfMissing: false);
        PopulatePortCombo(_defaultPortCombo, ports, preferredDefault, includePreferredIfMissing: true);

        if (!_serialService.IsConnected && !string.IsNullOrWhiteSpace(_settings.DefaultComPort) && _portCombo.Items.Contains(_settings.DefaultComPort))
            _portCombo.SelectedItem = _settings.DefaultComPort;

        string listed = ports.Count == 0 ? T("misc.none") : string.Join(", ", ports);
        Log(F("log.portsRefreshed", listed));
    }

    private static void PopulatePortCombo(ComboBox combo, IReadOnlyList<string> ports, string? preferred, bool includePreferredIfMissing)
    {
        combo.BeginUpdate();
        try
        {
            combo.Items.Clear();
            foreach (string port in ports) combo.Items.Add(port);
            if (includePreferredIfMissing && !string.IsNullOrWhiteSpace(preferred) && !combo.Items.Contains(preferred))
                combo.Items.Add(preferred);
        }
        finally { combo.EndUpdate(); }

        if (!string.IsNullOrWhiteSpace(preferred) && combo.Items.Contains(preferred))
            combo.SelectedItem = preferred;
        else if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
        else
            combo.SelectedItem = null;
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
            _serialService.Connect(selectedPort, 115200);
            _smoother.Reset();
            _manualDisconnect = false;
            _ackSeen = false;
            _lastAckUtc = DateTime.MinValue;
            _debugSleepUntilUtc = DateTime.MinValue;
            UpdateAckLabel();
            _sendTimer.Start();
            _reconnectTimer.Stop();
            SetConnectedStatus(selectedPort);
            Log(F("log.connected", selectedPort));
            SendSample();
        }
        catch (Exception ex)
        {
            SetDisconnectedStatus();
            Log(F("log.connectError", ex.Message));
            MessageBox.Show(this, ex.Message, T("msg.connectionFailed"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void SendSample()
    {
        if (!_serialService.IsConnected || _sendInProgress) return;

        _sendInProgress = true;
        try
        {
            var sw = Stopwatch.StartNew();
            HardwareSample rawSample = await Task.Run(_monitorService.ReadSample);
            sw.Stop();
            if (!_serialService.IsConnected || _isExiting) return;

            HardwareSample sample = _smoother.Apply(rawSample);
            bool displaySleepRequested = GetDisplaySleepRequested();
            bool debugAlertRequested = _debugAlertPending;
            string packet = SerialPacketFormatter.BuildDataPacket(sample, displaySleepRequested, debugAlertRequested);

            _serialService.SendLine(packet);
            _debugAlertPending = false;
            _lastSendLabel.Text = $"⏱ {DateTime.Now:HH:mm:ss}";
            _sampleMsLabel.Text = $"📊 {sw.ElapsedMilliseconds} ms";
            UpdateDashboard(sample);
        }
        catch (Exception ex)
        {
            if (!_isExiting) Disconnect(F("log.runtimeError", ex.Message));
        }
        finally { _sendInProgress = false; }
    }

    private void UpdateDashboard(HardwareSample sample)
    {
        _cpuCard.Value = sample.CpuUsagePercent;
        _cpuTempCard.Value = sample.CpuTempC;
        _gpuCard.Value = sample.GpuUsagePercent;
        _gpuTempCard.Value = sample.GpuTempC;
        _ramCard.Value = sample.RamUsagePercent;
        _powerCard.Value = sample.TotalPowerW;

        _cpuCard.AddSparklineValue(sample.CpuUsagePercent);
        _gpuCard.AddSparklineValue(sample.GpuUsagePercent);
        _ramCard.AddSparklineValue(sample.RamUsagePercent);
        _powerCard.AddSparklineValue(sample.TotalPowerW);

        _netSparkline.AddValue(sample.NetDownloadMBps + sample.NetUploadMBps);
        _netValueLabel.Text = $"↓ {sample.NetDownloadMBps:0.0}  ↑ {sample.NetUploadMBps:0.0} MB/s";

        float diskMax = sample.DiskUsages.Count > 0 ? sample.DiskUsages.Max(d => d.UsagePercent) : 0f;
        _diskSparkline.AddValue(diskMax);
        _diskValueLabel.Text = FormatDisks(sample.DiskUsages);
    }

    private void OnSendTimerTick() { CheckAckTimeout(); SendSample(); }

    private void OnSerialLineReceived(string line)
    {
        if (!string.Equals(line, "ACK", StringComparison.OrdinalIgnoreCase)) return;
        if (!IsHandleCreated) return;
        BeginInvoke(() => { _ackSeen = true; _lastAckUtc = DateTime.UtcNow; UpdateAckLabel(); });
    }

    private void UpdateAckLabel()
    {
        _lastAckLabel.Text = _ackSeen ? $"✅ {_lastAckUtc.ToLocalTime():HH:mm:ss}" : T("status.never");
    }

    private bool GetDisplaySleepRequested()
    {
        if (DateTime.UtcNow < _debugSleepUntilUtc) return true;
        return _settings.SleepWhenMonitorsOff && _displayState == DisplayState.Off;
    }

    private void StartDebugWakeTest()
    {
        if (!_serialService.IsConnected) { MessageBox.Show(this, T("msg.debugRequiresConnection"), T("msg.debugTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        _debugSleepUntilUtc = DateTime.UtcNow + DebugSleepDuration;
        Log(T("log.debugWakeStarted"));
        SendSample();
    }

    private void StartDebugAlertTest()
    {
        if (!_serialService.IsConnected) { MessageBox.Show(this, T("msg.debugRequiresConnection"), T("msg.debugTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        _debugAlertPending = true;
        Log(T("log.debugAlertStarted"));
        SendSample();
    }

    private void CheckAckTimeout()
    {
        if (!_serialService.IsConnected || !_ackSeen) return;
        if (DateTime.UtcNow - _lastAckUtc > AckTimeout) Disconnect(T("log.ackTimeout"), allowAutoReconnect: _settings.AutoReconnect);
    }

    private void TryAutoReconnect()
    {
        if (_isExiting || _manualDisconnect || _serialService.IsConnected || !_settings.AutoReconnect) return;
        string? port = _portCombo.SelectedItem as string ?? _settings.DefaultComPort;
        if (string.IsNullOrWhiteSpace(port)) return;

        try
        {
            _serialService.Connect(port, 115200);
            _smoother.Reset();
            _ackSeen = false;
            _lastAckUtc = DateTime.MinValue;
            UpdateAckLabel();
            _sendTimer.Start();
            SetConnectedStatus(port);