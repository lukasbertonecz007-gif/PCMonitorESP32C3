using System.Runtime.InteropServices;

namespace PcMonitorHost;

internal static class UserIdleDetector
{
    public static bool IsIdleForSeconds(int seconds)
    {
        if (seconds <= 0)
        {
            return false;
        }

        uint idleMs = GetIdleMilliseconds();
        return idleMs >= (uint)seconds * 1000u;
    }

    private static uint GetIdleMilliseconds()
    {
        var info = new LastInputInfo
        {
            Size = (uint)Marshal.SizeOf<LastInputInfo>()
        };

        if (!GetLastInputInfo(ref info))
        {
            return 0u;
        }

        return unchecked((uint)Environment.TickCount - info.Time);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);
}
