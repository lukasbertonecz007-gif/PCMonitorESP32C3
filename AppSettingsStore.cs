using System.Text.Json;

namespace PcMonitorHost;

internal static class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PcMonitorHost");

    private static string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(SettingsFilePath);
            AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return Sanitize(loaded ?? new AppSettings());
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        AppSettings sanitized = Sanitize(settings);
        Directory.CreateDirectory(SettingsDirectory);
        string json = JsonSerializer.Serialize(sanitized, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }

    private static AppSettings Sanitize(AppSettings settings)
    {
        string defaultPort = settings.DefaultComPort?.Trim() ?? string.Empty;
        if (defaultPort.Length > 32)
        {
            defaultPort = defaultPort[..32];
        }

        int idleSeconds = settings.IdleSeconds;
        if (idleSeconds < 10)
        {
            idleSeconds = 10;
        }
        else if (idleSeconds > 3600)
        {
            idleSeconds = 3600;
        }

        int lowPowerInterval = settings.LowPowerIntervalMs;
        if (lowPowerInterval < 80)
        {
            lowPowerInterval = 80;
        }
        else if (lowPowerInterval > 5000)
        {
            lowPowerInterval = 5000;
        }

        return new AppSettings
        {
            Language = settings.Language,
            StartWithWindows = settings.StartWithWindows,
            DefaultComPort = defaultPort,
            SleepWhenMonitorsOff = settings.SleepWhenMonitorsOff,
            AutoReconnect = settings.AutoReconnect,
            LowPowerWhenIdle = settings.LowPowerWhenIdle,
            IdleSeconds = idleSeconds,
            LowPowerIntervalMs = lowPowerInterval,
            GpuTempPreference = settings.GpuTempPreference,
            CpuTempPreference = settings.CpuTempPreference
        };
    }
}
