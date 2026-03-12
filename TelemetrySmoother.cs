namespace PcMonitorHost;

internal sealed class TelemetrySmoother
{
    private const float UsageAlpha = 0.34f;
    private const float TempAlpha = 0.28f;
    private const float DataAlpha = 0.26f;
    private const float NetworkAlpha = 0.32f;
    private const float PowerAlpha = 0.30f;
    private const float ThreadAlpha = 0.22f;

    private HardwareSample _state = new();
    private bool _initialized;
    private float[] _threadState = Array.Empty<float>();

    public HardwareSample Apply(HardwareSample raw)
    {
        if (!_initialized)
        {
            IReadOnlyList<float> initialThreadLoads = SmoothThreadLoads(raw.CpuThreadLoads, raw.CpuLogicalThreads, raw.CpuUsagePercent);
            _state = new HardwareSample
            {
                CpuUsagePercent = raw.CpuUsagePercent,
                CpuTempC = raw.CpuTempC,
                CpuPhysicalCores = raw.CpuPhysicalCores,
                CpuLogicalThreads = raw.CpuLogicalThreads,
                CpuThreadLoads = initialThreadLoads,
                RamUsagePercent = raw.RamUsagePercent,
                RamUsedGb = raw.RamUsedGb,
                RamTotalGb = raw.RamTotalGb,
                GpuUsagePercent = raw.GpuUsagePercent,
                GpuTempC = raw.GpuTempC,
                NetDownloadMBps = raw.NetDownloadMBps,
                NetUploadMBps = raw.NetUploadMBps,
                DiskUsages = raw.DiskUsages,
                TotalPowerW = raw.TotalPowerW
            };
            _initialized = true;
            return _state;
        }

        float cpu = BlendPercent(_state.CpuUsagePercent, raw.CpuUsagePercent, UsageAlpha);
        float cpuTemp = BlendValue(_state.CpuTempC, raw.CpuTempC, TempAlpha);
        float ramPercent = BlendPercent(_state.RamUsagePercent, raw.RamUsagePercent, UsageAlpha);
        float ramUsed = BlendValue(_state.RamUsedGb, raw.RamUsedGb, DataAlpha);
        float ramTotal = BlendValue(_state.RamTotalGb, raw.RamTotalGb, DataAlpha);
        float gpu = BlendPercent(_state.GpuUsagePercent, raw.GpuUsagePercent, UsageAlpha);
        float gpuTemp = BlendValue(_state.GpuTempC, raw.GpuTempC, TempAlpha);
        float netDownload = BlendValue(_state.NetDownloadMBps, raw.NetDownloadMBps, NetworkAlpha);
        float netUpload = BlendValue(_state.NetUploadMBps, raw.NetUploadMBps, NetworkAlpha);
        float totalPower = BlendValue(_state.TotalPowerW, raw.TotalPowerW, PowerAlpha);
        IReadOnlyList<float> threadLoads = SmoothThreadLoads(raw.CpuThreadLoads, raw.CpuLogicalThreads, cpu);

        _state = new HardwareSample
        {
            CpuUsagePercent = cpu,
            CpuTempC = cpuTemp,
            CpuPhysicalCores = raw.CpuPhysicalCores,
            CpuLogicalThreads = raw.CpuLogicalThreads,
            CpuThreadLoads = threadLoads,
            RamUsagePercent = ramPercent,
            RamUsedGb = ramUsed,
            RamTotalGb = ramTotal,
            GpuUsagePercent = gpu,
            GpuTempC = gpuTemp,
            NetDownloadMBps = netDownload,
            NetUploadMBps = netUpload,
            DiskUsages = raw.DiskUsages,
            TotalPowerW = totalPower
        };

        return _state;
    }

    public void Reset()
    {
        _state = new HardwareSample();
        _initialized = false;
        _threadState = Array.Empty<float>();
    }

    private IReadOnlyList<float> SmoothThreadLoads(IReadOnlyList<float> rawLoads, int logicalThreads, float fallbackUsage)
    {
        int targetCount = Math.Max(rawLoads.Count, logicalThreads);
        if (targetCount <= 0)
        {
            _threadState = Array.Empty<float>();
            return _threadState;
        }

        if (_threadState.Length != targetCount)
        {
            var resized = new float[targetCount];
            for (int i = 0; i < targetCount; i++)
            {
                resized[i] = i < _threadState.Length ? _threadState[i] : -1f;
            }

            _threadState = resized;
        }

        for (int i = 0; i < targetCount; i++)
        {
            float current = i < rawLoads.Count ? rawLoads[i] : fallbackUsage;
            if (current < 0f && fallbackUsage >= 0f)
            {
                current = fallbackUsage;
            }

            _threadState[i] = BlendPercent(_threadState[i], current, ThreadAlpha);
        }

        return _threadState;
    }

    private static float BlendPercent(float previous, float current, float alpha)
    {
        return BlendCore(previous, current, alpha, 0f, 100f);
    }

    private static float BlendValue(float previous, float current, float alpha)
    {
        return BlendCore(previous, current, alpha, 0f, float.MaxValue);
    }

    private static float BlendCore(float previous, float current, float alpha, float min, float max)
    {
        if (current < 0f)
        {
            if (previous < 0f)
            {
                return -1f;
            }

            float decayed = previous * 0.55f;
            return decayed < 0.5f ? -1f : decayed;
        }

        if (previous < 0f)
        {
            return Clamp(current, min, max);
        }

        float smoothed = previous + (current - previous) * alpha;
        return Clamp(smoothed, min, max);
    }

    private static float Clamp(float value, float min, float max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

}
