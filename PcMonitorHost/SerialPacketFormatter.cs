using System.Globalization;
using System.Text;

namespace PcMonitorHost;

internal static class SerialPacketFormatter
{
    private const int MaxThreadLoadsInPacket = 32;
    private const int MaxDisksInPacket = 8;

    public static string BuildDataPacket(HardwareSample sample, bool displaySleepRequested, bool debugAlertRequested)
    {
        var builder = new StringBuilder(780);
        builder.Append("DATA");
        Append(builder, "cpu", sample.CpuUsagePercent);
        Append(builder, "ct", sample.CpuTempC);
        Append(builder, "co", sample.CpuPhysicalCores);
        Append(builder, "th", sample.CpuLogicalThreads);
        AppendThreadLoads(builder, sample.CpuThreadLoads);
        Append(builder, "rp", sample.RamUsagePercent);
        Append(builder, "ru", sample.RamUsedGb);
        Append(builder, "rt", sample.RamTotalGb);
        Append(builder, "gu", sample.GpuUsagePercent);
        Append(builder, "gt", sample.GpuTempC);
        Append(builder, "nd", sample.NetDownloadMBps);
        Append(builder, "nu", sample.NetUploadMBps);
        AppendDiskUsages(builder, sample.DiskUsages);
        Append(builder, "pw", sample.TotalPowerW);
        Append(builder, "id", displaySleepRequested ? 1 : 0);
        Append(builder, "oa", debugAlertRequested ? 1 : 0);
        string payload = builder.ToString();
        ushort crc = Crc16Ccitt(payload);
        builder.Append(";crc=").Append(crc.ToString("X4", CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static ushort Crc16Ccitt(string text)
    {
        Span<byte> bytes = stackalloc byte[text.Length];
        int count = Encoding.ASCII.GetBytes(text, bytes);
        ushort crc = 0xFFFF;
        for (int i = 0; i < count; i++)
        {
            crc ^= (ushort)(bytes[i] << 8);
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
        }
        return crc;
    }

    private static void AppendThreadLoads(StringBuilder builder, IReadOnlyList<float> threadLoads)
    {
        builder.Append(";tl=");

        if (threadLoads.Count == 0)
        {
            builder.Append("na");
            return;
        }

        int count = Math.Min(threadLoads.Count, MaxThreadLoadsInPacket);
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            float value = threadLoads[i];
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                builder.Append("-1");
                continue;
            }

            int rounded = (int)MathF.Round(Math.Clamp(value, 0f, 100f), MidpointRounding.AwayFromZero);
            builder.Append(rounded.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static void Append(StringBuilder builder, string key, int value)
    {
        builder.Append(';')
            .Append(key)
            .Append('=')
            .Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void Append(StringBuilder builder, string key, float value)
    {
        builder.Append(';')
            .Append(key)
            .Append('=')
            .Append(value.ToString("0.0", CultureInfo.InvariantCulture));
    }

    private static void AppendDiskUsages(StringBuilder builder, IReadOnlyList<DiskUsageSample> disks)
    {
        builder.Append(";ds=");
        if (disks.Count == 0)
        {
            builder.Append("na");
            return;
        }

        int count = Math.Min(disks.Count, MaxDisksInPacket);
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            DiskUsageSample disk = disks[i];
            string label = NormalizeDiskLabel(disk.Label, i);
            int usage = (int)MathF.Round(Math.Clamp(disk.UsagePercent, 0f, 100f), MidpointRounding.AwayFromZero);

            builder
                .Append(label)
                .Append(':')
                .Append(usage.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static string NormalizeDiskLabel(string? source, int index)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return $"D{index + 1}";
        }

        var chars = new StringBuilder(4);
        foreach (char ch in source)
        {
            if (char.IsLetterOrDigit(ch))
            {
                chars.Append(char.ToUpperInvariant(ch));
            }

            if (chars.Length >= 4)
            {
                break;
            }
        }

        return chars.Length > 0 ? chars.ToString() : $"D{index + 1}";
    }
}
