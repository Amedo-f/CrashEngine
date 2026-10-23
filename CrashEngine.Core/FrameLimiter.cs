using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CrashEngine.Core;

// Amedo 2026-09-29
public enum FrameMode { VSync, Fps60, Fps144, Unlimited, Fps240, Fps360 } // Amedo

// Amedo 2026-09-29
internal sealed class FrameLimiter : IDisposable
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr CreateWaitableTimerExW(IntPtr lpTimerAttributes, string? lpTimerName, uint dwFlags, uint dwDesiredAccess);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetWaitableTimer(IntPtr hTimer, ref long lpDueTime, int lPeriod, IntPtr pfnCompletionRoutine, IntPtr lpArgToCompletionRoutine, bool fResume);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr hObject);

    private const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x2;
    private const uint TIMER_ALL_ACCESS = 0x1F0003;

    private static readonly long SpinTicks = Stopwatch.Frequency / 1000;
    private IntPtr _timer;
    private long _next;

    public FrameLimiter()
    {
        if (!OperatingSystem.IsWindows()) return;
        try { _timer = CreateWaitableTimerExW(IntPtr.Zero, null, CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, TIMER_ALL_ACCESS); }
        catch { _timer = IntPtr.Zero; }
    }

    public void Wait(int fps)
    {
        long now = Stopwatch.GetTimestamp();
        if (fps <= 0)
        {
            _next = 0;
            return;
        }
        long period = Stopwatch.Frequency / fps;
        if (_next == 0 || now - _next > period) _next = now;
        SleepUntil(_next);
        _next += period;
    }

    private void SleepUntil(long target)
    {
        while (true)
        {
            long left = target - Stopwatch.GetTimestamp();
            if (left <= 0) return;
            if (left <= SpinTicks)
            {
                Thread.SpinWait(64);
                continue;
            }
            if (_timer != IntPtr.Zero)
            {
                long due = -(left - SpinTicks) * 10_000_000 / Stopwatch.Frequency;
                if (due < 0 && SetWaitableTimer(_timer, ref due, 0, IntPtr.Zero, IntPtr.Zero, false))
                {
                    WaitForSingleObject(_timer, 0xFFFFFFFF);
                    continue;
                }
            }
            Thread.Sleep(left > SpinTicks * 3 ? 1 : 0);
        }
    }

    public void Dispose()
    {
        if (_timer == IntPtr.Zero) return;
        CloseHandle(_timer);
        _timer = IntPtr.Zero;
    }
}
