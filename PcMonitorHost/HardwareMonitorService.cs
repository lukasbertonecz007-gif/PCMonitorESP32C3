using System.Globalization;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using LibreHardwareMonitor.Hardware;

namespace PcMonitorHost;

internal sealed class HardwareMonitorService : IDisposable
{
    private static readonly Regex CpuLoadIndexRegex = new(
        @"/load/(\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex TemperatureIndexRegex = new(
        @"/temperature/(\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CoreThreadRegex = new(
        @"Core\s*#?\s*(\d+)(?:\s*Thread\s*#?\s*(\d+))?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly Computer _computer;
    private readonly CpuUsageProvider _cpuUsageProvider = new();
    private readonly Dictionary<string, NetworkSnapshot> _networkSnapshots = new(StringComparer.OrdinalIgnoreCase);

    private CpuTempPreference _cpuTempPreference = CpuTempPreference.Auto;
    private GpuTempPreference _gpuTempPreference = GpuTempPreference.Auto;

    private DateTime _lastNetworkCleanupUtc = DateTime.MinValue;
    private DateTime _lastDiskSnapshotUtc = DateTime.MinValue;
    private IReadOnlyList<DiskUsageSample> _cachedDiskUsages = Array.Empty<DiskUsageSample>();
    private Task<IReadOnlyList<DiskUsageSample>>? _diskQueryTask;
    private DateTime _diskQueryStartedUtc = DateTime.MinValue;
    private bool _disposed;

    private bool _fallbackCpuInfoLoaded;
    private int _fallbackPhysicalCores;
    private int _fallbackLogicalThreads;

    private const double BytesPerGiB = 1024d * 1024d * 1024d;
    private static readonly TimeSpan DiskSnapshotInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan DiskQueryTimeout = TimeSpan.FromMilliseconds(600);

    public HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsMemoryEnabled = true,
            IsGpuEnabled = true,
            IsStorageEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true
        };

        _computer.Open();
        LoadFallbackCpuInfo();
    }

    public void SetTempPreferences(CpuTempPreference cpuPreference, GpuTempPreference gpuPreference)
    {
        _cpuTempPreference = cpuPreference;
        _gpuTempPreference = gpuPreference;
    }

    public HardwareSample ReadSample()
    {
        ThrowIfDisposed();

        float cpuUsageFromSensor = float.NaN;
        float cpuTemp = float.NaN;
        float cpuTempAny = float.NaN;
        int cpuTempPriority = -1;
        float cpuTempPreferred = float.NaN;
        int cpuTempPreferredPriority = -1;

        float ramUsage = float.NaN;
        float ramUsedGb = float.NaN;
        float ramAvailableGb = float.NaN;

        float gpuUsage = float.NaN;
        float gpuTemp = float.NaN;
        int gpuTempScore = int.MinValue;

        float totalPower = 0f;
        bool hasPower = false;

        int logicalThreadCount = 0;
        var physicalCores = new HashSet<int>();
        var cpuLogicalLoads = new List<CpuLoadSample>(32);

        foreach (IHardware hardware in TraverseHardware())
        {
            switch (hardware.HardwareType)
            {
                case HardwareType.Cpu:
                    ProcessCpuSensors(
                        hardware,
                        cpuLogicalLoads,
                        physicalCores,
                        ref logicalThreadCount,
                        ref cpuUsageFromSensor,
                        ref cpuTemp,
                        ref cpuTempAny,
                        ref cpuTempPriority,
                        ref cpuTempPreferred,
                        ref cpuTempPreferredPriority,
                        _cpuTempPreference);
                    break;
                case HardwareType.Memory:
                    ProcessMemorySensors(hardware, ref ramUsage, ref ramUsedGb, ref ramAvailableGb);
                    break;
                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    ProcessGpuSensors(hardware, ref gpuUsage, ref gpuTemp, ref gpuTempScore, _gpuTempPreference);
                    break;
                case HardwareType.Motherboard:
                case HardwareType.SuperIO:
                case HardwareType.EmbeddedController:
                    ProcessCpuTempFallbackSensors(
                        hardware,
                        ref cpuTemp,
                        ref cpuTempPriority,
                        ref cpuTempPreferred,
                        ref cpuTempPreferredPriority,
                        _cpuTempPreference);
                    break;
            }

            ProcessPowerSensors(hardware, ref totalPower, ref hasPower);
        }

        IReadOnlyList<float> logicalLoads = BuildCpuLoadList(cpuLogicalLoads);
        if (logicalThreadCount <= 0)
        {
            logicalThreadCount = logicalLoads.Count;
        }

        if (logicalThreadCount <= 0)
        {
            logicalThreadCount = _fallbackLogicalThreads > 0 ? _fallbackLogicalThreads : Environment.ProcessorCount;
        }

        int physicalCoreCount = physicalCores.Count;
        if (physicalCoreCount <= 0)
        {
            if (_fallbackPhysicalCores > 0)
            {
                physicalCoreCount = _fallbackPhysicalCores;
            }
            else
            {
                physicalCoreCount = Math.Max(1, logicalThreadCount / 2);
            }
        }

        if (_cpuTempPreference != CpuTempPreference.Auto && !float.IsNaN(cpuTempPreferred))
        {
            cpuTemp = cpuTempPreferred;
        }
        else if (float.IsNaN(cpuTemp))
        {
            cpuTemp = cpuTempAny;
        }

        float cpuUsage = _cpuUsageProvider.ReadUsagePercent();
        if (cpuUsage < 0f)
        {
            if (!float.IsNaN(cpuUsageFromSensor))
            {
                cpuUsage = cpuUsageFromSensor;
            }
            else if (logicalLoads.Count > 0)
            {
                cpuUsage = logicalLoads.Average();
            }
        }

        IReadOnlyList<float> threadLoads = logicalLoads;
        if (threadLoads.Count == 0 && cpuUsage >= 0f && logicalThreadCount > 0)
        {
            threadLoads = BuildUniformLoads(logicalThreadCount, cpuUsage);
        }

        float ramTotalGb = float.NaN;
        if (!float.IsNaN(ramUsedGb) && !float.IsNaN(ramAvailableGb))
        {
            ramTotalGb = ramUsedGb + ramAvailableGb;
            if (float.IsNaN(ramUsage) && ramTotalGb > 0.01f)
            {
                ramUsage = ramUsedGb / ramTotalGb * 100f;
            }
        }

        // Prefer OS-reported physical RAM values to avoid virtual/pagefile sensor mixups.
        if (TryReadPhysicalMemory(out float osRamUsedGb, out float osRamTotalGb, out float osRamUsage))
        {
            ramUsedGb = osRamUsedGb;
            ramTotalGb = osRamTotalGb;
            ramUsage = osRamUsage;
        }

        ReadNetworkRates(out float netDownloadMBps, out float netUploadMBps);
        IReadOnlyList<DiskUsageSample> diskUsages = ReadDiskUsages();

        return new HardwareSample
        {
            CpuUsagePercent = SanitizePercent(cpuUsage),
            CpuTempC = SanitizeTemp(cpuTemp),
            CpuPhysicalCores = Math.Max(1, physicalCoreCount),
            CpuLogicalThreads = Math.Max(1, logicalThreadCount),
            CpuThreadLoads = threadLoads,
            RamUsagePercent = SanitizePercent(ramUsage),
            RamUsedGb = SanitizeValue(ramUsedGb),
            RamTotalGb = SanitizeValue(ramTotalGb),
            GpuUsagePercent = SanitizePercent(gpuUsage),
            GpuTempC = SanitizeTemp(gpuTemp),
            NetDownloadMBps = SanitizeValue(netDownloadMBps),
            NetUploadMBps = SanitizeValue(netUploadMBps),
            DiskUsages = diskUsages,
            TotalPowerW = hasPower ? SanitizeValue(totalPower) : -1f
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _computer.Close();
        _disposed = true;
    }

    private IEnumerable<IHardware> TraverseHardware()
    {
        foreach (IHardware root in _computer.Hardware)
        {
            foreach (IHardware item in TraverseAndUpdate(root))
            {
                yield return item;
            }
        }
    }

    private static IEnumerable<IHardware> TraverseAndUpdate(IHardware hardware)
    {
        hardware.Update();
        yield return hardware;

        foreach (IHardware subHardware in hardware.SubHardware)
        {
            foreach (IHardware subItem in TraverseAndUpdate(subHardware))
            {
                yield return subItem;
            }
        }
    }

    private static void ProcessCpuSensors(
        IHardware hardware,
        ICollection<CpuLoadSample> logicalLoads,
        ISet<int> physicalCores,
        ref int logicalThreadCount,
        ref float cpuUsageFromSensor,
        ref float cpuTemp,
        ref float cpuTempAny,
        ref int cpuTempPriority,
        ref float cpuTempPreferred,
        ref int cpuTempPreferredPriority,
        CpuTempPreference preference)
    {
        int fallbackSortOrder = logicalLoads.Count;

        foreach (ISensor sensor in hardware.Sensors)
        {
            if (sensor.Value is not float value)
            {
                continue;
            }

            if (sensor.SensorType == SensorType.Load)
            {
                if (IsCpuTotalLoad(sensor.Name))
                {
                    cpuUsageFromSensor = value;
                    continue;
                }

                if (!IsLogicalCpuLoad(sensor.Name))
                {
                    continue;
                }

                int sortKey = GetLoadSortKey(sensor, fallbackSortOrder++);
                logicalLoads.Add(new CpuLoadSample(sortKey, value));
                logicalThreadCount++;

                if (TryParseCoreIndex(sensor.Name, out int coreIndex))
                {
                    physicalCores.Add(coreIndex);
                }

                continue;
            }

            if (sensor.SensorType != SensorType.Temperature)
            {
                continue;
            }

            float candidateTemp = value;
            if (IsDistanceToTjMaxSensor(sensor.Name))
            {
                if (!TryConvertDistanceToTjMax(value, out candidateTemp))
                {
                    continue;
                }
            }

            if (candidateTemp > 5f && candidateTemp < 130f)
            {
                cpuTempAny = float.IsNaN(cpuTempAny) ? candidateTemp : Math.Max(cpuTempAny, candidateTemp);
            }

            TrySetCpuTemp(
                ref cpuTemp,
                ref cpuTempPriority,
                sensor.Name,
                sensor.Identifier.ToString(),
                candidateTemp,
                allowGenericFallback: true,
                fromCpuHardware: true,
                preference: CpuTempPreference.Auto);

            if (preference != CpuTempPreference.Auto)
            {
                TrySetCpuTemp(
                    ref cpuTempPreferred,
                    ref cpuTempPreferredPriority,
                    sensor.Name,
                    sensor.Identifier.ToString(),
                    candidateTemp,
                    allowGenericFallback: true,
                    fromCpuHardware: true,
                    preference: preference);
            }
        }
    }

    private static bool TryParseCoreIndex(string sensorName, out int coreIndex)
    {
        Match match = CoreThreadRegex.Match(sensorName);
        if (match.Success &&
            int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int core))
        {
            coreIndex = Math.Max(0, core - 1);
            return true;
        }

        coreIndex = -1;
        return false;
    }

    private static bool IsCpuTotalLoad(string sensorName)
    {
        return sensorName.Equals("CPU Total", StringComparison.OrdinalIgnoreCase) ||
               (sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase) &&
                sensorName.Contains("Total", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLogicalCpuLoad(string sensorName)
    {
        if (sensorName.Contains("Core Max", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Total", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Utility", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return sensorName.Contains("Thread", StringComparison.OrdinalIgnoreCase) ||
               sensorName.Contains("CPU Core #", StringComparison.OrdinalIgnoreCase) ||
               sensorName.Contains("Core #", StringComparison.OrdinalIgnoreCase) ||
               (sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase) &&
                sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase));
    }

    private static int GetLoadSortKey(ISensor sensor, int fallback)
    {
        string identifier = sensor.Identifier.ToString();
        Match idMatch = CpuLoadIndexRegex.Match(identifier);
        if (idMatch.Success &&
            int.TryParse(idMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int loadIndex))
        {
            return loadIndex;
        }

        Match coreThread = CoreThreadRegex.Match(sensor.Name);
        if (coreThread.Success &&
            int.TryParse(coreThread.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int core))
        {
            int thread = 0;
            if (coreThread.Groups[2].Success)
            {
                int.TryParse(coreThread.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out thread);
            }

            return (Math.Max(1, core) * 100) + Math.Max(0, thread);
        }

        return 10_000 + fallback;
    }

    private static void ProcessCpuTempFallbackSensors(
        IHardware hardware,
        ref float cpuTemp,
        ref int cpuTempPriority,
        ref float cpuTempPreferred,
        ref int cpuTempPreferredPriority,
        CpuTempPreference preference)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Temperature || sensor.Value is not float value)
            {
                continue;
            }

            float candidateTemp = value;
            if (IsDistanceToTjMaxSensor(sensor.Name))
            {
                if (!TryConvertDistanceToTjMax(value, out candidateTemp))
                {
                    continue;
                }
            }

            TrySetCpuTemp(
                ref cpuTemp,
                ref cpuTempPriority,
                sensor.Name,
                sensor.Identifier.ToString(),
                candidateTemp,
                allowGenericFallback: false,
                fromCpuHardware: false,
                preference: CpuTempPreference.Auto);

            if (preference != CpuTempPreference.Auto)
            {
                TrySetCpuTemp(
                    ref cpuTempPreferred,
                    ref cpuTempPreferredPriority,
                    sensor.Name,
                    sensor.Identifier.ToString(),
                    candidateTemp,
                    allowGenericFallback: false,
                    fromCpuHardware: false,
                    preference: preference);
            }
        }
    }

    private static void ProcessMemorySensors(
        IHardware hardware,
        ref float ramUsage,
        ref float ramUsedGb,
        ref float ramAvailableGb)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (sensor.Value is not float value)
            {
                continue;
            }

            if (IsVirtualMemorySensor(sensor))
            {
                continue;
            }

            if (sensor.SensorType == SensorType.Load &&
                sensor.Name.Contains("Memory", StringComparison.OrdinalIgnoreCase))
            {
                ramUsage = value;
                continue;
            }

            if (sensor.SensorType != SensorType.Data)
            {
                continue;
            }

            if (IsPhysicalMemoryUsedSensor(sensor.Name))
            {
                ramUsedGb = value;
                continue;
            }

            if (IsPhysicalMemoryAvailableSensor(sensor.Name))
            {
                ramAvailableGb = value;
            }
        }
    }

    private static bool IsVirtualMemorySensor(ISensor sensor)
    {
        string name = sensor.Name;
        string id = sensor.Identifier.ToString();

        if (name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Page", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Commit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return id.Contains("/vram/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPhysicalMemoryUsedSensor(string sensorName)
    {
        if (!sensorName.Contains("Used", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return sensorName.Contains("Memory", StringComparison.OrdinalIgnoreCase) ||
               sensorName.Equals("Used", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPhysicalMemoryAvailableSensor(string sensorName)
    {
        if (!sensorName.Contains("Available", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return sensorName.Contains("Memory", StringComparison.OrdinalIgnoreCase) ||
               sensorName.Equals("Available", StringComparison.OrdinalIgnoreCase);
    }

    private static void ProcessGpuSensors(
        IHardware hardware,
        ref float gpuUsage,
        ref float gpuTemp,
        ref int gpuTempScore,
        GpuTempPreference preference)
    {
        foreach (ISensor sensor in hardware.Sensors)
        {
            if (sensor.Value is not float value)
            {
                continue;
            }

            if (sensor.SensorType == SensorType.Load &&
                (sensor.Name.Contains("GPU Core", StringComparison.OrdinalIgnoreCase) ||
                 sensor.Name.Contains("D3D", StringComparison.OrdinalIgnoreCase) ||
                 sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)))
            {
                gpuUsage = float.IsNaN(gpuUsage) ? value : Math.Max(gpuUsage, value);
                continue;
            }

            if (sensor.SensorType == SensorType.Temperature)
            {
                TrySetGpuTemp(
                    ref gpuTemp,
                    ref gpuTempScore,
                    sensor.Name,
                    sensor.Identifier.ToString(),
                    value,
                    preference);
            }
        }
    }

    private static void TrySetGpuTemp(
        ref float gpuTemp,
        ref int gpuTempScore,
        string sensorName,
        string sensorIdentifier,
        float value,
        GpuTempPreference preference)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 5f || value > 130f)
        {
            return;
        }

        int score = GetGpuTempScore(sensorName, sensorIdentifier, preference);
        if (score == int.MinValue)
        {
            return;
        }

        if (score > gpuTempScore)
        {
            gpuTempScore = score;
            gpuTemp = value;
        }
    }

    private static int GetGpuTempScore(string sensorName, string sensorIdentifier, GpuTempPreference preference)
    {
        int priority = GetGpuTempPriority(sensorName, sensorIdentifier, preference);
        if (priority < 0)
        {
            return int.MinValue;
        }

        int tempIndex = GetTemperatureSensorIndex(sensorIdentifier);
        int indexPenalty = Math.Clamp(tempIndex, 0, 999);
        return (priority * 1000) - indexPenalty;
    }

    private static int GetGpuTempPriority(string sensorName, string sensorIdentifier, GpuTempPreference preference)
    {
        bool isHotspot = sensorName.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase) ||
                         sensorName.Contains("Hotspot", StringComparison.OrdinalIgnoreCase) ||
                         sensorName.Contains("Junction", StringComparison.OrdinalIgnoreCase) ||
                         sensorIdentifier.Contains("hotspot", StringComparison.OrdinalIgnoreCase) ||
                         sensorIdentifier.Contains("junction", StringComparison.OrdinalIgnoreCase);

        if (preference != GpuTempPreference.Auto)
        {
            if (!MatchesGpuTempPreference(sensorName, sensorIdentifier, preference))
            {
                return -1;
            }
        }

        if (preference != GpuTempPreference.HotSpot &&
            (isHotspot ||
             sensorName.Contains("Memory", StringComparison.OrdinalIgnoreCase) ||
             sensorName.Contains("VRAM", StringComparison.OrdinalIgnoreCase) ||
             sensorName.Contains("HBM", StringComparison.OrdinalIgnoreCase) ||
             sensorName.Contains("VRM", StringComparison.OrdinalIgnoreCase) ||
             sensorName.Contains("MOS", StringComparison.OrdinalIgnoreCase)))
        {
            return -1;
        }

        if (sensorName.Equals("GPU", StringComparison.OrdinalIgnoreCase))
        {
            return 40;
        }

        if (sensorName.Contains("GPU Core", StringComparison.OrdinalIgnoreCase))
        {
            return 35;
        }

        if (sensorName.Equals("Core", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Core Temp", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Core Temperature", StringComparison.OrdinalIgnoreCase))
        {
            return 34;
        }

        if (sensorName.Contains("Edge", StringComparison.OrdinalIgnoreCase))
        {
            return 33;
        }

        if (sensorName.Equals("Temperature", StringComparison.OrdinalIgnoreCase))
        {
            return 32;
        }

        if (sensorName.Contains("GPU", StringComparison.OrdinalIgnoreCase) &&
            (sensorName.Contains("Temp", StringComparison.OrdinalIgnoreCase) ||
             sensorName.Contains("Temperature", StringComparison.OrdinalIgnoreCase)))
        {
            return 30;
        }

        if (sensorIdentifier.Contains("/temperature/0", StringComparison.OrdinalIgnoreCase))
        {
            return 28;
        }

        if (sensorIdentifier.Contains("/temperature/", StringComparison.OrdinalIgnoreCase))
        {
            return 20;
        }

        return -1;
    }

    private static bool MatchesGpuTempPreference(string sensorName, string sensorIdentifier, GpuTempPreference preference)
    {
        switch (preference)
        {
            case GpuTempPreference.Gpu:
                return sensorName.Equals("GPU", StringComparison.OrdinalIgnoreCase);
            case GpuTempPreference.GpuCore:
                return sensorName.Contains("GPU Core", StringComparison.OrdinalIgnoreCase);
            case GpuTempPreference.Core:
                return sensorName.Equals("Core", StringComparison.OrdinalIgnoreCase) ||
                       sensorName.Contains("Core Temp", StringComparison.OrdinalIgnoreCase) ||
                       sensorName.Contains("Core Temperature", StringComparison.OrdinalIgnoreCase);
            case GpuTempPreference.Edge:
                return sensorName.Contains("Edge", StringComparison.OrdinalIgnoreCase);
            case GpuTempPreference.HotSpot:
                return sensorName.Contains("Hot Spot", StringComparison.OrdinalIgnoreCase) ||
                       sensorName.Contains("Hotspot", StringComparison.OrdinalIgnoreCase) ||
                       sensorName.Contains("Junction", StringComparison.OrdinalIgnoreCase) ||
                       sensorIdentifier.Contains("hotspot", StringComparison.OrdinalIgnoreCase) ||
                       sensorIdentifier.Contains("junction", StringComparison.OrdinalIgnoreCase);
            default:
                return true;
        }
    }

    private static int GetTemperatureSensorIndex(string sensorIdentifier)
    {
        Match match = TemperatureIndexRegex.Match(sensorIdentifier);
        if (match.Success &&
            int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
        {
            return index;
        }

        return 999;
    }

    private static void ProcessPowerSensors(IHardware hardware, ref float totalPower, ref bool hasPower)
    {
        var values = new List<(string Name, float Value)>(8);

        foreach (ISensor sensor in hardware.Sensors)
        {
            if (sensor.SensorType != SensorType.Power || sensor.Value is not float value)
            {
                continue;
            }

            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || value > 2000f)
            {
                continue;
            }

            values.Add((sensor.Name, value));
        }

        if (values.Count == 0)
        {
            return;
        }

        float selected = SelectPowerSensorValue(hardware.HardwareType, values);
        if (selected < 0f)
        {
            return;
        }

        totalPower += selected;
        hasPower = true;
    }

    private static float SelectPowerSensorValue(HardwareType hardwareType, IReadOnlyList<(string Name, float Value)> values)
    {
        if (values.Count == 1)
        {
            return values[0].Value;
        }

        if (hardwareType == HardwareType.Cpu)
        {
            if (TryPickByName(values, out float value, "Package", "PPT", "CPU Package", "Core", "IA Cores"))
            {
                return value;
            }

            return values.Max(item => item.Value);
        }

        if (hardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
        {
            if (TryPickByName(values, out float value, "Board", "Total", "GPU"))
            {
                return value;
            }

            return values.Max(item => item.Value);
        }

        return values.Max(item => item.Value);
    }

    private static bool TryPickByName(
        IReadOnlyList<(string Name, float Value)> values,
        out float value,
        params string[] keys)
    {
        foreach (string key in keys)
        {
            foreach ((string name, float sensorValue) in values)
            {
                if (name.Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    value = sensorValue;
                    return true;
                }
            }
        }

        value = -1f;
        return false;
    }

    private void ReadNetworkRates(out float downloadMBps, out float uploadMBps)
    {
        DateTime now = DateTime.UtcNow;
        double selectedDownBytesPerSecond = 0d;
        double selectedUpBytesPerSecond = 0d;
        double selectedTrafficBytesPerSecond = -1d;
        int selectedPriority = int.MinValue;

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!IsTrackedInterface(nic))
            {
                continue;
            }

            int priority = GetNetworkInterfacePriority(nic);
            IPv4InterfaceStatistics stats;
            try
            {
                stats = nic.GetIPv4Statistics();
            }
            catch
            {
                continue;
            }

            string key = nic.Id;
            long rx = stats.BytesReceived;
            long tx = stats.BytesSent;

            if (_networkSnapshots.TryGetValue(key, out NetworkSnapshot previous))
            {
                double seconds = (now - previous.TimestampUtc).TotalSeconds;
                if (seconds > 0.08d && seconds < 8d)
                {
                    long deltaRx = rx - previous.BytesReceived;
                    long deltaTx = tx - previous.BytesSent;
                    double downBytesPerSecond = 0d;
                    double upBytesPerSecond = 0d;
                    if (deltaRx > 0)
                    {
                        downBytesPerSecond = deltaRx / seconds;
                    }
                    if (deltaTx > 0)
                    {
                        upBytesPerSecond = deltaTx / seconds;
                    }

                    double totalTraffic = downBytesPerSecond + upBytesPerSecond;
                    if (priority > selectedPriority ||
                        (priority == selectedPriority && totalTraffic > selectedTrafficBytesPerSecond))
                    {
                        selectedPriority = priority;
                        selectedTrafficBytesPerSecond = totalTraffic;
                        selectedDownBytesPerSecond = downBytesPerSecond;
                        selectedUpBytesPerSecond = upBytesPerSecond;
                    }
                }
            }

            _networkSnapshots[key] = new NetworkSnapshot(rx, tx, now);
        }

        if (now - _lastNetworkCleanupUtc > TimeSpan.FromSeconds(30))
        {
            CleanupNetworkSnapshots(now);
        }

        // Use binary MB here to better match what download clients typically show.
        downloadMBps = (float)(selectedDownBytesPerSecond / 1_048_576d);
        uploadMBps = (float)(selectedUpBytesPerSecond / 1_048_576d);
    }

    private void CleanupNetworkSnapshots(DateTime now)
    {
        _lastNetworkCleanupUtc = now;
        string[] keys = _networkSnapshots.Keys.ToArray();
        foreach (string key in keys)
        {
            if (now - _networkSnapshots[key].TimestampUtc > TimeSpan.FromSeconds(45))
            {
                _networkSnapshots.Remove(key);
            }
        }
    }

    private static bool IsTrackedInterface(NetworkInterface nic)
    {
        if (nic.OperationalStatus != OperationalStatus.Up)
        {
            return false;
        }

        if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or
            NetworkInterfaceType.Tunnel or
            NetworkInterfaceType.Unknown)
        {
            return false;
        }

        return HasIpv4Address(nic);
    }

    private static int GetNetworkInterfacePriority(NetworkInterface nic)
    {
        bool hasGateway = HasIpv4Gateway(nic);
        bool likelyVirtual = IsLikelyVirtualInterface(nic);
        bool commonPrimaryType = nic.NetworkInterfaceType is NetworkInterfaceType.Ethernet or
            NetworkInterfaceType.Wireless80211 or
            NetworkInterfaceType.GigabitEthernet or
            NetworkInterfaceType.FastEthernetFx or
            NetworkInterfaceType.FastEthernetT or
            NetworkInterfaceType.Ppp;

        if (hasGateway && commonPrimaryType && !likelyVirtual)
        {
            return 4;
        }

        if (hasGateway && !likelyVirtual)
        {
            return 3;
        }

        if (commonPrimaryType && !likelyVirtual)
        {
            return 2;
        }

        return 1;
    }

    private static bool HasIpv4Address(NetworkInterface nic)
    {
        try
        {
            return nic.GetIPProperties()
                .UnicastAddresses
                .Any(address => address.Address.AddressFamily == AddressFamily.InterNetwork);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasIpv4Gateway(NetworkInterface nic)
    {
        try
        {
            return nic.GetIPProperties()
                .GatewayAddresses
                .Any(address =>
                    address.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !address.Address.Equals(System.Net.IPAddress.Any));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsLikelyVirtualInterface(NetworkInterface nic)
    {
        string name = nic.Name;
        string description = nic.Description;

        return name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("VMware", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("Npcap", StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<DiskUsageSample> ReadDiskUsages()
    {
        DateTime now = DateTime.UtcNow;
        if (_diskQueryTask is { IsCompleted: true })
        {
            try
            {
                _cachedDiskUsages = _diskQueryTask.Result;
            }
            catch
            {
                // Keep previous data on background task errors.
            }
            finally
            {
                _diskQueryTask = null;
            }
        }

        if (_diskQueryTask is not null)
        {
            if (now - _diskQueryStartedUtc > DiskQueryTimeout)
            {
                _diskQueryTask = null;
            }

            return _cachedDiskUsages;
        }

        if (now - _lastDiskSnapshotUtc < DiskSnapshotInterval)
        {
            return _cachedDiskUsages;
        }

        _lastDiskSnapshotUtc = now;
        _diskQueryTask = Task.Run(QueryDiskUsages);
        _diskQueryStartedUtc = now;
        return _cachedDiskUsages;
    }

    private IReadOnlyList<DiskUsageSample> QueryDiskUsages()
    {
        var list = new List<DiskUsageSample>(8);
        HashSet<string> allowedLabels = GetAllowedDiskLabels();
        bool filterByAllowedLabels = allowedLabels.Count > 0;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PercentDiskTime FROM Win32_PerfFormattedData_PerfDisk_LogicalDisk WHERE Name <> '_Total'");

            foreach (ManagementObject item in searcher.Get())
            {
                string? name = item["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string label = NormalizeDiskLabel(name, list.Count);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                if (filterByAllowedLabels && !allowedLabels.Contains(label))
                {
                    continue;
                }

                object? rawPercent = item["PercentDiskTime"];
                if (rawPercent is null)
                {
                    continue;
                }

                float usage = SanitizePercent(Convert.ToSingle(rawPercent, CultureInfo.InvariantCulture));
                if (usage < 0f)
                {
                    continue;
                }

                list.Add(new DiskUsageSample(label, usage));
            }
        }
        catch
        {
            // Keep previous data on WMI errors.
            return _cachedDiskUsages;
        }

        if (list.Count == 0)
        {
            return Array.Empty<DiskUsageSample>();
        }

        return list
            .OrderBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static HashSet<string> GetAllowedDiskLabels()
    {
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Network or DriveType.Ram))
                {
                    continue;
                }

                char letter = char.ToUpperInvariant(drive.Name[0]);
                if (char.IsLetter(letter))
                {
                    labels.Add(letter.ToString(CultureInfo.InvariantCulture));
                }
            }
        }
        catch
        {
            return labels;
        }

        return labels;
    }

    private static string NormalizeDiskLabel(string raw, int index)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            int colonIndex = raw.IndexOf(':');
            if (colonIndex > 0)
            {
                char letter = raw[colonIndex - 1];
                if (char.IsLetter(letter))
                {
                    return char.ToUpperInvariant(letter).ToString(CultureInfo.InvariantCulture);
                }
            }

            foreach (char ch in raw)
            {
                if (char.IsLetter(ch))
                {
                    return char.ToUpperInvariant(ch).ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        return $"D{index + 1}";
    }

    private static IReadOnlyList<float> BuildCpuLoadList(IReadOnlyCollection<CpuLoadSample> loadSamples)
    {
        if (loadSamples.Count == 0)
        {
            return Array.Empty<float>();
        }

        return loadSamples
            .OrderBy(sample => sample.SortKey)
            .Select(sample => SanitizePercent(sample.Value))
            .Where(value => value >= 0f)
            .ToArray();
    }

    private static IReadOnlyList<float> BuildUniformLoads(int count, float cpuUsage)
    {
        if (count <= 0)
        {
            return Array.Empty<float>();
        }

        float clamped = SanitizePercent(cpuUsage);
        if (clamped < 0f)
        {
            return Array.Empty<float>();
        }

        var values = new float[count];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = clamped;
        }

        return values;
    }

    private static void TrySetCpuTemp(
        ref float cpuTemp,
        ref int cpuTempPriority,
        string sensorName,
        string sensorIdentifier,
        float value,
        bool allowGenericFallback,
        bool fromCpuHardware,
        CpuTempPreference preference)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 5f || value > 130f)
        {
            return;
        }

        if (preference != CpuTempPreference.Auto &&
            !MatchesCpuTempPreference(sensorName, sensorIdentifier, fromCpuHardware, preference))
        {
            return;
        }

        int priority = GetCpuTempPriority(sensorName, sensorIdentifier, allowGenericFallback, fromCpuHardware);
        if (priority < 0)
        {
            return;
        }

        if (preference != CpuTempPreference.Auto)
        {
            priority += 100;
        }

        if (priority > cpuTempPriority)
        {
            cpuTempPriority = priority;
            cpuTemp = value;
            return;
        }

        if (priority == cpuTempPriority)
        {
            cpuTemp = float.IsNaN(cpuTemp) ? value : Math.Max(cpuTemp, value);
        }
    }

    private static bool MatchesCpuTempPreference(
        string sensorName,
        string sensorIdentifier,
        bool fromCpuHardware,
        CpuTempPreference preference)
    {
        if (preference == CpuTempPreference.Auto)
        {
            return true;
        }

        bool fromIntelCpuPath = sensorIdentifier.Contains("/intelcpu/", StringComparison.OrdinalIgnoreCase);
        bool fromBoardPath = sensorIdentifier.Contains("/lpc/", StringComparison.OrdinalIgnoreCase) ||
                             sensorIdentifier.Contains("/superio/", StringComparison.OrdinalIgnoreCase);

        switch (preference)
        {
            case CpuTempPreference.Package:
                return sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                       sensorName.Contains("Package id", StringComparison.OrdinalIgnoreCase);
            case CpuTempPreference.CoreMax:
                return sensorName.Contains("Core Max", StringComparison.OrdinalIgnoreCase);
            case CpuTempPreference.CoreAverage:
                return sensorName.Contains("Core Average", StringComparison.OrdinalIgnoreCase);
            case CpuTempPreference.Core:
                return sensorName.Contains("CPU Core", StringComparison.OrdinalIgnoreCase) ||
                       sensorName.Contains("Core #", StringComparison.OrdinalIgnoreCase) ||
                       sensorName.Contains("P-Core", StringComparison.OrdinalIgnoreCase) ||
                       sensorName.Contains("E-Core", StringComparison.OrdinalIgnoreCase);
            case CpuTempPreference.TdieTctl:
                return sensorName.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ||
                       sensorName.Contains("Tctl", StringComparison.OrdinalIgnoreCase);
            case CpuTempPreference.Board:
                return fromBoardPath ||
                       sensorName.Contains("Socket", StringComparison.OrdinalIgnoreCase) ||
                       sensorName.Equals("CPU Temp", StringComparison.OrdinalIgnoreCase) ||
                       sensorName.Equals("CPU Temperature", StringComparison.OrdinalIgnoreCase);
            default:
                return fromCpuHardware || fromIntelCpuPath;
        }
    }

    private static int GetCpuTempPriority(
        string sensorName,
        string sensorIdentifier,
        bool allowGenericFallback,
        bool fromCpuHardware)
    {
        bool fromIntelCpuPath = sensorIdentifier.Contains("/intelcpu/", StringComparison.OrdinalIgnoreCase);
        bool fromBoardPath = sensorIdentifier.Contains("/lpc/", StringComparison.OrdinalIgnoreCase) ||
                             sensorIdentifier.Contains("/superio/", StringComparison.OrdinalIgnoreCase);

        if (IsDistanceToTjMaxSensor(sensorName))
        {
            return fromCpuHardware || fromIntelCpuPath ? 6 : 2;
        }

        if (sensorName.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Package id", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Junction", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Tdie", StringComparison.OrdinalIgnoreCase))
        {
            return fromIntelCpuPath ? 12 : 10;
        }

        if (sensorName.Contains("Core Max", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Core Average", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("CPU Die", StringComparison.OrdinalIgnoreCase))
        {
            return fromIntelCpuPath ? 11 : 9;
        }

        if (sensorName.Contains("CPU Core", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("P-Core", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("E-Core", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("Core #", StringComparison.OrdinalIgnoreCase) ||
            sensorName.Contains("CCD", StringComparison.OrdinalIgnoreCase))
        {
            return fromIntelCpuPath ? 10 : 8;
        }

        if (fromCpuHardware && sensorName.Contains("Core", StringComparison.OrdinalIgnoreCase))
        {
            return 7;
        }

        if (sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase) &&
            sensorName.Contains("Socket", StringComparison.OrdinalIgnoreCase))
        {
            return 6;
        }

        if (sensorName.Contains("PECI", StringComparison.OrdinalIgnoreCase))
        {
            return fromBoardPath ? 8 : 5;
        }

        if (fromBoardPath &&
            (sensorName.Equals("CPU", StringComparison.OrdinalIgnoreCase) ||
             sensorName.Equals("CPU Temp", StringComparison.OrdinalIgnoreCase) ||
             sensorName.Equals("CPU Temperature", StringComparison.OrdinalIgnoreCase)))
        {
            return 4;
        }

        if (fromCpuHardware && sensorName.Contains("CPU", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (allowGenericFallback && (fromCpuHardware || fromIntelCpuPath))
        {
            return 2;
        }

        return -1;
    }

    private static bool IsDistanceToTjMaxSensor(string sensorName)
    {
        return sensorName.Contains("Distance", StringComparison.OrdinalIgnoreCase) &&
               sensorName.Contains("TjMax", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryConvertDistanceToTjMax(float distance, out float convertedTemp)
    {
        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f || distance > 120f)
        {
            convertedTemp = -1f;
            return false;
        }

        // Comet Lake (i7-10700K) uses TjMax around 100 C; this yields usable package-equivalent temp.
        convertedTemp = 100f - distance;
        if (convertedTemp < 5f || convertedTemp > 130f)
        {
            return false;
        }

        return true;
    }

    private void LoadFallbackCpuInfo()
    {
        if (_fallbackCpuInfoLoaded)
        {
            return;
        }

        _fallbackCpuInfoLoaded = true;
        _fallbackLogicalThreads = Environment.ProcessorCount;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");

            int cores = 0;
            int threads = 0;
            foreach (ManagementObject cpu in searcher.Get())
            {
                if (cpu["NumberOfCores"] is not null)
                {
                    cores += Convert.ToInt32(cpu["NumberOfCores"], CultureInfo.InvariantCulture);
                }

                if (cpu["NumberOfLogicalProcessors"] is not null)
                {
                    threads += Convert.ToInt32(cpu["NumberOfLogicalProcessors"], CultureInfo.InvariantCulture);
                }
            }

            if (cores > 0)
            {
                _fallbackPhysicalCores = cores;
            }

            if (threads > 0)
            {
                _fallbackLogicalThreads = threads;
            }
        }
        catch
        {
            // Ignore WMI failures and keep environment fallback.
        }
    }

    private static float SanitizePercent(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return -1f;
        }

        return Math.Clamp(value, 0f, 100f);
    }

    private static float SanitizeTemp(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return -1f;
        }

        return Math.Clamp(value, -50f, 150f);
    }

    private static float SanitizeValue(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return -1f;
        }

        return Math.Max(value, 0f);
    }

    private static bool TryReadPhysicalMemory(out float usedGb, out float totalGb, out float usagePercent)
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
        {
            usedGb = float.NaN;
            totalGb = float.NaN;
            usagePercent = float.NaN;
            return false;
        }

        totalGb = (float)(status.TotalPhys / BytesPerGiB);
        float availableGb = (float)(status.AvailPhys / BytesPerGiB);
        usedGb = Math.Max(0f, totalGb - availableGb);
        usagePercent = totalGb > 0.01f ? (usedGb / totalGb) * 100f : float.NaN;
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(HardwareMonitorService));
        }
    }

    private readonly record struct CpuLoadSample(int SortKey, float Value);

    private readonly record struct NetworkSnapshot(long BytesReceived, long BytesSent, DateTime TimestampUtc);
}
