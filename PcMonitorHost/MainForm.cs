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
    private static readonly Color WindowBack = Color.FromArgb(24, 24, 24);
    private static readonly Color PanelBack = Color.FromArgb(32, 32, 32);
    private static readonly Color InputBack = Color.FromArgb(44, 44, 44);
    private static readonly Color ForeText = Color.FromArgb(230, 230, 230);
    private static readonly Color AccentText = Color.FromArgb(120, 220, 140);
    private static readonly Color ErrorText = Color.FromArgb(255, 120, 120);

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
    private readonly Label _statusTitleLabel = new() { AutoSize = true, Padding = new Padding(0, 3, 0, 0) };
    private readonly Label _lastSendTitleLabel = new() { AutoSize = true, Padding = new Padding(12, 3, 0, 0) };
    private readonly Label _lastAckTitleLabel = new() { AutoSize = true, Padding = new Padding(12, 3, 0, 0) };
    private readonly Label _lastAckLabel = new() { AutoSize = true };
    private readonly Label _sampleMsTitleLabel = new() { AutoSize = true, Padding = new Padding(12, 3, 0, 0) };
    private readonly Label _sampleMsLabel = new() { AutoSize = true };

    private readonly TextBox _previewBox = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill
    };

    private readonly TextBox _logBox = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill
    };

    private readonly GroupBox _previewGroup = new() { Dock = DockStyle.Fill };
    private readonly GroupBox _logGroup = new() { Dock = DockStyle.Fill };
    private bool _logPanelVisible = true;

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
    private DateTime _lastPreviewRefreshUtc = DateTime.MinValue;
    private DisplayState _displayState = DisplayState.Unknown;
    private nint _powerSettingNotificationHandle = nint.Zero;
    private DateTime _lastAckUtc = DateTime.MinValue;
    private bool _ackSeen;
    private bool _manualDisconnect;
    private DateTime _lastAutoReconnectLogUtc = DateTime.MinValue;

    private const int WM_POWERBROADCAST = 0x0218;
    private const int PBT_POWERSETTINGCHANGE = 0x8013;
    private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;
    private static readonly TimeSpan PreviewRefreshInterval = TimeSpan.FromMilliseconds(220);
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
            Padding = new Padding(10)
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildTopControls(), 0, 0);
        root.Controls.Add(BuildSettingsControls(), 0, 1);
        root.Controls.Add(BuildContentArea(), 0, 2);
        root.Controls.Add(BuildStatusBar(), 0, 3);

        Controls.Add(root);
    }

    private Control BuildTopControls()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 9,
            RowCount = 1,
            Margin = new Padding(0)
        };

        // Set column widths: label (90px), combo (120px), label (90px), input (100px), etc.
        for (int i = 0; i < 9; i++)
        {
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        }

        // Columns: portLabel (0) | portCombo (1) | baudLabel (2) | baudInput (3) | 
        //         intervalLabel (4) | intervalInput (5) | refreshBtn (6) | connectBtn (7) | toggleLogBtn (8)
        _portLabel.Width = 90;
        _baudLabel.Width = 90;
        _intervalLabel.Width = 90;

        panel.Controls.Add(_portLabel, 0, 0);
        panel.Controls.Add(_portCombo, 1, 0);
        panel.Controls.Add(_baudLabel, 2, 0);
        panel.Controls.Add(_baudInput, 3, 0);
        panel.Controls.Add(_intervalLabel, 4, 0);
        panel.Controls.Add(_intervalInput, 5, 0);
        panel.Controls.Add(_refreshPortsButton, 6, 0);
        panel.Controls.Add(_connectButton, 7, 0);
        panel.Controls.Add(_toggleLogButton, 8, 0);

        return panel;
    }

    private Control BuildSettingsControls()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 6, 0, 6)
        };

        panel.Controls.Add(_settingsLanguageLabel);
        panel.Controls.Add(_languageCombo);

        panel.Controls.Add(_settingsDefaultComLabel);
        panel.Controls.Add(_defaultPortCombo);

        panel.Controls.Add(_startupCheck);
        panel.Controls.Add(_monitorSleepCheck);
        panel.Controls.Add(_autoReconnectCheck);
        panel.Controls.Add(_lowPowerCheck);
        panel.Controls.Add(_idleSecondsLabel);
        panel.Controls.Add(_idleSecondsInput);
        panel.Controls.Add(_lowPowerIntervalLabel);
        panel.Controls.Add(_lowPowerIntervalInput);

        panel.Controls.Add(_cpuTempPrefLabel);
        panel.Controls.Add(_cpuTempPrefCombo);
        panel.Controls.Add(_gpuTempPrefLabel);
        panel.Controls.Add(_gpuTempPrefCombo);
        panel.Controls.Add(_saveSettingsButton);

        return panel;
    }

    private Control BuildContentArea()
    {
        _previewGroup.Controls.Add(_previewBox);
        _logGroup.Controls.Add(_logBox);
        _logGroup.Visible = _logPanelVisible;

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 230,
            Panel2Collapsed = !_logPanelVisible
        };

        split.Panel1.Controls.Add(_previewGroup);
        split.Panel2.Controls.Add(_logGroup);

        // Store reference for toggling later
        Tag = split;

        return split;
    }

    private Control BuildStatusBar()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 8
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));

        table.Controls.Add(_statusTitleLabel, 0, 0);
        table.Controls.Add(_connectionLabel, 1, 0);
        table.Controls.Add(_lastSendTitleLabel, 2, 0);
        table.Controls.Add(_lastSendLabel, 3, 0);
        table.Controls.Add(_lastAckTitleLabel, 4, 0);
        table.Controls.Add(_lastAckLabel, 5, 0);
        table.Controls.Add(_sampleMsTitleLabel, 6, 0);
        table.Controls.Add(_sampleMsLabel, 7, 0);

        return table;
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
        _logGroup.Visible = _logPanelVisible;
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
            _lastSendLabel.Text = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            _sampleMsLabel.Text = $"{sw.ElapsedMilliseconds} ms";
            if (ShouldRefreshPreview())
            {
                _previewBox.Text = BuildPreview(sample, packet, displaySleepRequested);
            }
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

    private bool ShouldRefreshPreview()
    {
        DateTime now = DateTime.UtcNow;
        if (now - _lastPreviewRefreshUtc < PreviewRefreshInterval)
        {
            return false;
        }

        _lastPreviewRefreshUtc = now;
        return true;
    }

    private string BuildPreview(HardwareSample sample, string packet, bool displaySleepRequested)
    {
        var builder = new StringBuilder(320);
        builder.AppendLine($"{T("preview.cpuUsage")} {FormatPercent(sample.CpuUsagePercent)}");
        builder.AppendLine($"{T("preview.cpuTemp")}  {FormatTemp(sample.CpuTempC)}");
        builder.AppendLine($"{T("preview.cpuCt")}   {sample.CpuPhysicalCores}/{sample.CpuLogicalThreads}");
        builder.AppendLine($"{T("preview.ramUsage")} {FormatPercent(sample.RamUsagePercent)}");
        builder.AppendLine($"{T("preview.ramUsed")}  {FormatGb(sample.RamUsedGb)} / {FormatGb(sample.RamTotalGb)}");
        builder.AppendLine($"{T("preview.gpuUsage")} {FormatPercent(sample.GpuUsagePercent)}");
        builder.AppendLine($"{T("preview.gpuTemp")}  {FormatTemp(sample.GpuTempC)}");
        builder.AppendLine($"{T("preview.netDown")}  {FormatMBps(sample.NetDownloadMBps)}");
        builder.AppendLine($"{T("preview.netUp")}    {FormatMBps(sample.NetUploadMBps)}");
        builder.AppendLine($"{T("preview.disks")}    {FormatDisks(sample.DiskUsages)}");
        builder.AppendLine($"{T("preview.power")}     {FormatW(sample.TotalPowerW)}");
        builder.AppendLine($"{T("preview.monitorState")} {DisplayStateLabel(_displayState)}");
        builder.AppendLine($"{T("preview.displaySleep")} {(displaySleepRequested ? T("misc.yes") : T("misc.no"))}");
        builder.AppendLine();
        builder.AppendLine(T("preview.packet"));
        builder.Append(packet);
        return builder.ToString();
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

        _previewGroup.Text = T("group.preview");
        _logGroup.Text = T("group.log");

        _statusTitleLabel.Text = T("status.title");
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
        _connectionLabel.ForeColor = AccentText;
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
        _previewBox.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point);
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
                "label.baud" => "Baud:",
                "label.interval" => "Interval (ms):",
                "button.refresh" => "Refresh Ports",
                "button.connect" => "Connect",
                "button.disconnect" => "Disconnect",
                "button.toggleLog" => "📋",
                "settings.language" => "Language:",
                "settings.defaultCom" => "Default COM:",
                "settings.startWithWindows" => "Start with Windows",
                "settings.sleepWhenMonitorsOff" => "Sleep ESP display when monitor is OFF",
                "settings.autoReconnect" => "Auto reconnect",
                "settings.lowPowerWhenIdle" => "Low power when idle",
                "settings.idleSeconds" => "Idle (s):",
                "settings.lowPowerInterval" => "Low power interval (ms):",
                "settings.cpuTempPref" => "CPU temp sensor:",
                "settings.gpuTempPref" => "GPU temp sensor:",
                "settings.save" => "Save Settings",
                "group.preview" => "Current Telemetry",
                "group.log" => "Log",
                "status.title" => "Status:",
                "status.lastSend" => "Last send:",
                "status.lastAck" => "Last ack:",
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
                "log.connectError" => "Connect error: {0}",
                "log.manualDisconnect" => "Manual disconnect.",
                "log.autoReconnect" => "Auto reconnect: {0} @ {1}",
                "log.autoReconnectError" => "Auto reconnect error: {0}",
                "log.ackTimeout" => "ACK timeout, reconnecting.",
                "log.runtimeError" => "Runtime error: {0}",
                "log.settingsSaved" => "Settings saved.",
                "log.startupSetError" => "Unable to set startup with Windows: {0}",
                "preview.cpuUsage" => "CPU usage:",
                "preview.cpuTemp" => "CPU temp:",
                "preview.cpuCt" => "CPU C/T:",
                "preview.ramUsage" => "RAM usage:",
                "preview.ramUsed" => "RAM used:",
                "preview.gpuUsage" => "GPU usage:",
                "preview.gpuTemp" => "GPU temp:",
                "preview.netDown" => "Net down:",
                "preview.netUp" => "Net up:",
                "preview.disks" => "Disks:",
                "preview.power" => "Power:",
                "preview.monitorState" => "Monitor state:",
                "preview.displaySleep" => "Display sleep req:",
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
            "label.com" => "COM Port:",
            "label.baud" => "Rychlost:",
            "label.interval" => "Interval (ms):",
            "button.refresh" => "Obnovit porty",
            "button.connect" => "Pripojit",
            "button.disconnect" => "Odpojit",
            "button.toggleLog" => "📋",
            "settings.language" => "Jazyk:",
            "settings.defaultCom" => "Vychozi COM:",
            "settings.startWithWindows" => "Spoustet s Windows",
            "settings.sleepWhenMonitorsOff" => "Uspat ESP displej pri vypnutem monitoru",
            "settings.autoReconnect" => "Auto pripojeni",
            "settings.lowPowerWhenIdle" => "Low power pri neaktivite",
            "settings.idleSeconds" => "Neaktivita (s):",
            "settings.lowPowerInterval" => "Low power interval (ms):",
            "settings.cpuTempPref" => "CPU teplota senzor:",
            "settings.gpuTempPref" => "GPU teplota senzor:",
            "settings.save" => "Ulozit nastaveni",
            "group.preview" => "Aktualni telemetrie",
            "group.log" => "Log",
            "status.title" => "Stav:",
            "status.lastSend" => "Posledni odeslani:",
            "status.lastAck" => "Posledni ACK:",
            "status.sampleMs" => "Vzorek (ms):",
            "status.disconnected" => "Odpojeno",
            "status.connected" => "Pripojeno k {0} @ {1}",
            "status.never" => "Nikdy",
            "status.na" => "N/A",
            "tray.open" => "Otevrit",
            "tray.exit" => "Ukoncit",
            "tray.title" => "PC Monitor Host",
            "tray.running" => "Aplikace bezi na pozadi. Dvojklik na ikonu ji obnovi.",
            "msg.selectCom" => "Vyber COM port.",
            "msg.portRequiredTitle" => "Port je povinny",
            "msg.connectionFailed" => "Pripojeni selhalo",
            "msg.settingsSaved" => "Nastaveni ulozeno.",
            "msg.settingsTitle" => "Nastaveni",
            "log.tipAdmin" => "Tip: Spust aplikaci jako administrator pro presnejsi CPU teploty z LibreHardwareMonitor.",
            "log.portsRefreshed" => "Porty obnoveny: {0}",
            "log.connected" => "Pripojeno: {0} @ {1}",
            "log.connectError" => "Chyba pripojeni: {0}",
            "log.manualDisconnect" => "Rucni odpojeni.",
            "log.autoReconnect" => "Auto pripojeni: {0} @ {1}",
            "log.autoReconnectError" => "Chyba auto pripojeni: {0}",
            "log.ackTimeout" => "ACK timeout, znovu pripojuji.",
            "log.runtimeError" => "Chyba za behu: {0}",
            "log.settingsSaved" => "Nastaveni ulozeno.",
            "log.startupSetError" => "Nepodarilo se nastavit spousteni s Windows: {0}",
            "preview.cpuUsage" => "CPU vyuziti:",
            "preview.cpuTemp" => "CPU teplota:",
            "preview.cpuCt" => "CPU J/V:",
            "preview.ramUsage" => "RAM vyuziti:",
            "preview.ramUsed" => "RAM pouzito:",
            "preview.gpuUsage" => "GPU vyuziti:",
            "preview.gpuTemp" => "GPU teplota:",
            "preview.netDown" => "Sit download:",
            "preview.netUp" => "Sit upload:",
            "preview.disks" => "Disky:",
            "preview.power" => "Spotreba:",
            "preview.monitorState" => "Stav monitoru:",
            "preview.displaySleep" => "Pozadavek uspani:",
            "preview.packet" => "Paket:",
            "lang.czech" => "Cestina",
            "lang.english" => "Anglictina",
            "pref.auto" => "Auto",
            "pref.cpu.package" => "Package",
            "pref.cpu.coreMax" => "Jadro max",
            "pref.cpu.coreAvg" => "Jadro prumer",
            "pref.cpu.core" => "Jadro",
            "pref.cpu.tdie" => "Tdie/Tctl",
            "pref.cpu.board" => "Deska",
            "pref.gpu.gpu" => "GPU",
            "pref.gpu.gpuCore" => "GPU Core",
            "pref.gpu.core" => "Jadro",
            "pref.gpu.edge" => "Edge",
            "pref.gpu.hotspot" => "Hot Spot",
            "misc.none" => "zadne",
            "misc.yes" => "ANO",
            "misc.no" => "NE",
            "misc.displayOff" => "VYPNUTO",
            "misc.displayOn" => "ZAPNUTO",
            "misc.displayDimmed" => "ZTLUMENO",
            "misc.displayUnknown" => "NEZNAME",
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
