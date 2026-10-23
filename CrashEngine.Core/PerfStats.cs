using System.Diagnostics;

namespace CrashEngine.Core;

// Amedo 2026-09-28
public enum PerfSection
{
    Update, Render, Ui,
    Sim, SimAgents, SimPlayer, SimTriggers, SimPhysics, SimGate,
    Camera, Particles, Sounds, Music,
    Streaming, // Amedo 2026-09-29
    FrameWait, WindowEvents, Present, // Amedo
    Count,
}

// Amedo 2026-09-28
public static class PerfStats
{
    public readonly struct Scope : IDisposable
    {
        private readonly PerfSection _section;
        private readonly long _start;
        private readonly long _bytes; // Amedo 2026-09-29

        public Scope(PerfSection section)
        {
            _section = section;
            _start = Stopwatch.GetTimestamp();
            _bytes = GC.GetAllocatedBytesForCurrentThread();
        }

        public void Dispose()
        {
            _frameTicks[(int)_section] += Stopwatch.GetTimestamp() - _start;
            _frameBytes[(int)_section] += GC.GetAllocatedBytesForCurrentThread() - _bytes; // Amedo 2026-09-29
        }
    }

    private const int N = (int)PerfSection.Count;
    private const float Smooth = 0.1f;
    private static readonly double MsPerTick = 1000.0 / Stopwatch.Frequency;

    private static readonly long[] _frameTicks = new long[N];
    private static readonly long[] _frameBytes = new long[N]; // Amedo 2026-09-29
    private static readonly float[] _allocSecond = new float[N];
    private static readonly float[] _allocKb = new float[N];
    private static readonly float[] _avg = new float[N];
    private static readonly float[] _peak = new float[N];
    private static readonly float[] _peakRun = new float[N];
    private static long _lastFrame = Stopwatch.GetTimestamp();
    private static long _lastAlloc = GC.GetAllocatedBytesForCurrentThread();
    private static long _secondStart = Stopwatch.GetTimestamp();
    private static int _gc0, _gc1, _gc2, _framesRun;
    private static float _frameRunPeak, _allocRun;

    public static float GpuMs { get; private set; }
    public static bool VSync { get; set; } = true;
    public static string FrameMode { get; set; } = "VSync"; // Amedo 2026-09-29
    public static float FrameBudgetMs { get; set; } = 1000f / 60f;

    public static void ReportGpu(float ms)
    {
        LastGpuMs = ms; // Amedo 2026-09-30
        GpuMs = GpuMs <= 0f ? ms : GpuMs + (ms - GpuMs) * Smooth;
    }

    public static float LastGpuMs { get; private set; } // Amedo 2026-09-30

    public static float FrameMs { get; private set; }
    public static float FramePeakMs { get; private set; }
    public static float Fps { get; private set; }
    public static float AllocKbPerFrame { get; private set; }
    public static int Gen0PerSecond { get; private set; }
    public static int Gen1PerSecond { get; private set; }
    public static int Gen2PerSecond { get; private set; }
    public static float GcPausePeakMs { get; private set; } // Amedo 2026-09-29
    public static float LastGcPauseMs { get; private set; }
    private static TimeSpan _lastPause = GC.GetTotalPauseDuration();
    private static float _pauseRunPeak;

    public static Scope Measure(PerfSection section) => new(section);

    public static float Ms(PerfSection section) => _avg[(int)section];
    public static float PeakMs(PerfSection section) => _peak[(int)section];
    public static float AllocKb(PerfSection section) => _allocKb[(int)section]; // Amedo 2026-09-29
    public static float LastMs(PerfSection section) => _last[(int)section]; // Amedo 2026-09-29
    public static float LastFrameMs { get; private set; }
    private static readonly float[] _last = new float[N];

    public static long FrameNumber { get; private set; } // Amedo

    public static void AddSinceFrameEnd(PerfSection section) => _frameTicks[(int)section] += Stopwatch.GetTimestamp() - _lastFrame; // Amedo

    public static void EndFrame()
    {
        FrameNumber++; // Amedo
        long now = Stopwatch.GetTimestamp();
        float frame = (float)((now - _lastFrame) * MsPerTick);
        _lastFrame = now;
        FrameMs = FrameMs <= 0f ? frame : FrameMs + (frame - FrameMs) * Smooth;
        _frameRunPeak = MathF.Max(_frameRunPeak, frame);
        LastFrameMs = frame; // Amedo 2026-09-29
        for (int i = 0; i < N; i++)
        {
            float ms = (float)(_frameTicks[i] * MsPerTick);
            _last[i] = ms; // Amedo 2026-09-29
            _avg[i] += (ms - _avg[i]) * Smooth;
            _peakRun[i] = MathF.Max(_peakRun[i], ms);
            _frameTicks[i] = 0;
            _allocSecond[i] += _frameBytes[i] / 1024f; // Amedo 2026-09-29
            _frameBytes[i] = 0;
        }
        long alloc = GC.GetAllocatedBytesForCurrentThread();
        _allocRun += (alloc - _lastAlloc) / 1024f;
        _lastAlloc = alloc;
        var pause = GC.GetTotalPauseDuration(); // Amedo 2026-09-29
        LastGcPauseMs = (float)(pause - _lastPause).TotalMilliseconds;
        _lastPause = pause;
        _pauseRunPeak = MathF.Max(_pauseRunPeak, LastGcPauseMs);
        _framesRun++;
        if ((now - _secondStart) * MsPerTick < 1000.0) return;
        Fps = _framesRun * 1000f / (float)((now - _secondStart) * MsPerTick);
        FramePeakMs = _frameRunPeak;
        AllocKbPerFrame = _allocRun / _framesRun;
        for (int i = 0; i < N; i++) // Amedo 2026-09-29
        {
            _allocKb[i] = _allocSecond[i] / _framesRun;
            _allocSecond[i] = 0f;
        }
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        Gen0PerSecond = g0 - _gc0;
        Gen1PerSecond = g1 - _gc1;
        Gen2PerSecond = g2 - _gc2;
        (_gc0, _gc1, _gc2) = (g0, g1, g2);
        GcPausePeakMs = _pauseRunPeak; // Amedo 2026-09-29
        _pauseRunPeak = 0f;
        Array.Copy(_peakRun, _peak, N);
        Array.Clear(_peakRun);
        _frameRunPeak = 0f;
        _allocRun = 0f;
        _framesRun = 0;
        _secondStart = now;
    }
}
