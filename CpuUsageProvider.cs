using System.Runtime.InteropServices;

namespace PcMonitorHost;

internal sealed class CpuUsageProvider
{
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private bool _hasPrevious;

    public float ReadUsagePercent()
    {
        if (!GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime))
        {
            return -1f;
        }

        ulong idle = ToUInt64(idleTime);
        ulong kernel = ToUInt64(kernelTime);
        ulong user = ToUInt64(userTime);

        if (!_hasPrevious)
        {
            _lastIdle = idle;
            _lastKernel = kernel;
            _lastUser = user;
            _hasPrevious = true;
            return -1f;
        }

        ulong idleDelta = idle - _lastIdle;
        ulong kernelDelta = kernel - _lastKernel;
        ulong userDelta = user - _lastUser;
        ulong totalDelta = kernelDelta + userDelta;

        _lastIdle = idle;
        _lastKernel = kernel;
        _lastUser = user;

        if (totalDelta == 0)
        {
            return -1f;
        }

        double usage = (1.0d - (double)idleDelta / totalDelta) * 100.0d;
        if (usage < 0d)
        {
            usage = 0d;
        }
        else if (usage > 100d)
        {
            usage = 100d;
        }

        return (float)usage;
    }

    private static ulong ToUInt64(FileTime fileTime)
    {
        return ((ulong)fileTime.HighDateTime << 32) | fileTime.LowDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }
}
