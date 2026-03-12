namespace PcMonitorHost;

internal sealed class HardwareSample
{
    public float CpuUsagePercent { get; init; }
    public float CpuTempC { get; init; }
    public int CpuPhysicalCores { get; init; }
    public int CpuLogicalThreads { get; init; }
    public IReadOnlyList<float> CpuThreadLoads { get; init; } = Array.Empty<float>();
    public float RamUsagePercent { get; init; }
    public float RamUsedGb { get; init; }
    public float RamTotalGb { get; init; }
    public float GpuUsagePercent { get; init; }
    public float GpuTempC { get; init; }
    public float NetDownloadMBps { get; init; }
    public float NetUploadMBps { get; init; }
    public IReadOnlyList<DiskUsageSample> DiskUsages { get; init; } = Array.Empty<DiskUsageSample>();
    public float TotalPowerW { get; init; }
}
