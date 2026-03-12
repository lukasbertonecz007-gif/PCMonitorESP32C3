namespace PcMonitorHost;

internal enum UiLanguage
{
    Czech,
    English
}

internal enum GpuTempPreference
{
    Auto,
    Gpu,
    GpuCore,
    Core,
    Edge,
    HotSpot
}

internal enum CpuTempPreference
{
    Auto,
    Package,
    CoreMax,
    CoreAverage,
    Core,
    TdieTctl,
    Board
}

internal sealed class AppSettings
{
    public UiLanguage Language { get; set; } = UiLanguage.Czech;
    public bool StartWithWindows { get; set; }
    public string DefaultComPort { get; set; } = string.Empty;
    public bool SleepWhenMonitorsOff { get; set; } = true;
    public bool AutoReconnect { get; set; } = true;
    public bool LowPowerWhenIdle { get; set; }
    public int IdleSeconds { get; set; } = 60;
    public int LowPowerIntervalMs { get; set; } = 250;
    public GpuTempPreference GpuTempPreference { get; set; } = GpuTempPreference.Auto;
    public CpuTempPreference CpuTempPreference { get; set; } = CpuTempPreference.Auto;
}
