using System.Diagnostics;
using CrashEngine.Platform.Windows;

namespace CrashEngine.Core;

// Amedo 2026-09-29
public enum FrameMode { VSync, Fps60, Fps144, Unlimited, Fps240, Fps360 } // Amedo

// Amedo 2026-09-29
internal sealed class FrameLimiter : IDisposable
{
    private static readonly long SpinTicks = Stopwatch.Frequency / 1000;
    private HighResolutionTimer? _timer;
    private long _next;

    public FrameLimiter()
    {
        if (!OperatingSystem.IsWindows()) return;
        _timer = HighResolutionTimer.TryCreate();
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
            if (_timer is not null)
            {
                long due = -(left - SpinTicks) * 10_000_000 / Stopwatch.Frequency;
                if (due < 0 && _timer.Wait(due)) continue;
            }
            if (_timer is null) // Amedo 2026-10-27
            {
                Thread.Sleep(Math.Max(1, (int)((left - SpinTicks) * 1000 / Stopwatch.Frequency)));
                continue;
            }
            Thread.Sleep(left > SpinTicks * 3 ? 1 : 0);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
