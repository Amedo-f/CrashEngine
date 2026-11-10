using System.Runtime.InteropServices;

namespace CrashEngine.Platform.Windows;

public sealed class HighResolutionTimer : IDisposable
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr CreateWaitableTimerExW(IntPtr lpTimerAttributes, string? lpTimerName, uint dwFlags, uint dwDesiredAccess);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetWaitableTimer(IntPtr hTimer, ref long lpDueTime, int lPeriod, IntPtr pfnCompletionRoutine, IntPtr lpArgToCompletionRoutine, bool fResume);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr hObject);

    private const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x2;
    private const uint TIMER_ALL_ACCESS = 0x1F0003;

    private IntPtr _timer;

    private HighResolutionTimer(IntPtr timer) => _timer = timer;

    public static HighResolutionTimer? TryCreate()
    {
        IntPtr timer;
        try { timer = CreateWaitableTimerExW(IntPtr.Zero, null, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS); }
        catch { timer = IntPtr.Zero; }
        return timer == IntPtr.Zero ? null : new HighResolutionTimer(timer);
    }

    public bool Wait(long dueTime100ns)
    {
        if (!SetWaitableTimer(_timer, ref dueTime100ns, 0, IntPtr.Zero, IntPtr.Zero, false)) return false;
        WaitForSingleObject(_timer, 0xFFFFFFFF);
        return true;
    }

    public void Dispose()
    {
        if (_timer == IntPtr.Zero) return;
        CloseHandle(_timer);
        _timer = IntPtr.Zero;
    }
}
