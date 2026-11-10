using System.Runtime.InteropServices;

namespace CrashEngine.Platform.Windows;

public static class PowerThrottling
{
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerThrottlingState { public uint Version, ControlMask, StateMask; }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr process, int infoClass, ref PowerThrottlingState info, uint size);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    public static bool Disable()
    {
        var state = new PowerThrottlingState { Version = 1, ControlMask = 0x1 | 0x4, StateMask = 0 };
        try
        {
            return SetProcessInformation(GetCurrentProcess(), 4, ref state, (uint)Marshal.SizeOf<PowerThrottlingState>());
        }
        catch { return false; }
    }
}
