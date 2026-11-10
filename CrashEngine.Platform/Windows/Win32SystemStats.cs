using System.Runtime.InteropServices;

namespace CrashEngine.Platform.Windows;

public static class Win32SystemStats
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
}
