using System.Globalization;
using CrashEngine.Core;
using CrashEngine.Renderer;
using CrashEngine.Runtime;

namespace CrashGame;

// Amedo 2026-09-30
sealed class PerfLog
{
    private const double WarmSeconds = 3.0;
    private readonly List<float> _frame = new(1 << 16), _work = new(1 << 16), _render = new(1 << 16), _update = new(1 << 16), _gc = new(1 << 16);
    private readonly List<int> _draws = new(1 << 16);
    private static readonly PerfSection[] Sections =
    {
        PerfSection.Sim, PerfSection.SimAgents, PerfSection.SimPlayer, PerfSection.SimPhysics, PerfSection.Camera,
        PerfSection.Particles, PerfSection.Sounds, PerfSection.Music, PerfSection.Streaming, PerfSection.Ui,
    };
    private readonly double[] _sectionSums = new double[Sections.Length];
    private readonly float[] _sectionMax = new float[Sections.Length];
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private bool _started;

    public static int LastDraws { get; private set; }
    public Func<double>? GameClock { get; set; } // Amedo
    private const float SlowMs = 20f; // Amedo
    private static float MissedMs => MathF.Min(SlowMs, PerfStats.FrameBudgetMs * 1.5f); // Amedo 2026-11-14
    private int _missed; // Amedo 2026-11-14
    private long _jitCount = System.Runtime.JitInfo.GetCompiledMethodCount(); // Amedo
    private readonly long[] _gpuLast = new long[7]; // Amedo 2026-11-20
    private readonly string[] _gpuFrames = { "-", "-", "-" }; // Amedo 2026-11-20

    // Amedo 2026-11-20
    private void GpuActivity()
    {
        long[] now = { GpuStats.MeshesMade, GpuStats.MeshesFreed, GpuStats.TexturesMade, GpuStats.TexturesFreed, GpuStats.PoolPagesMade, GpuStats.PoolPagesFreed, GpuStats.QueueBytes };
        _gpuFrames[2] = _gpuFrames[1];
        _gpuFrames[1] = _gpuFrames[0];
        _gpuFrames[0] = string.Create(CultureInfo.InvariantCulture,
            $"meshes +{now[0] - _gpuLast[0]}/-{now[1] - _gpuLast[1]} textures +{now[2] - _gpuLast[2]}/-{now[3] - _gpuLast[3]} pool pages +{now[4] - _gpuLast[4]}/-{now[5] - _gpuLast[5]} queued {(now[6] - _gpuLast[6]) / 1048576.0:0.0} MB");
        Array.Copy(now, _gpuLast, now.Length);
    }

    public void Frame()
    {
        LastDraws = GpuStats.Draws;
        long jit = System.Runtime.JitInfo.GetCompiledMethodCount(); // Amedo
        long jitNew = jit - _jitCount;
        _jitCount = jit;
        GpuActivity(); // Amedo 2026-11-20
        if (_started || _clock.Elapsed.TotalSeconds >= WarmSeconds)
        {
            if (_started)
            {
                if (PerfStats.LastFrameMs > MissedMs) _missed++; // Amedo 2026-11-14
                if (PerfStats.LastFrameMs > MissedMs) // Amedo 2026-11-14
                {
                    float other = PerfStats.LastFrameMs - PerfStats.LastMs(PerfSection.Update) - PerfStats.LastMs(PerfSection.Render) - PerfStats.LastMs(PerfSection.Ui) // Amedo
                        - PerfStats.LastMs(PerfSection.Present) - PerfStats.LastMs(PerfSection.FrameWait) - PerfStats.LastMs(PerfSection.WindowEvents);
                    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                        $"[slow t={GameClock?.Invoke() ?? 0:0.00}s] frame {PerfStats.LastFrameMs:0.0} ms: update {PerfStats.LastMs(PerfSection.Update):0.0} " +
                        $"(sim {PerfStats.LastMs(PerfSection.Sim):0.0}, agents {PerfStats.LastMs(PerfSection.SimAgents):0.0}, player {PerfStats.LastMs(PerfSection.SimPlayer):0.0}, " +
                        $"phys {PerfStats.LastMs(PerfSection.SimPhysics):0.0}, stream {PerfStats.LastMs(PerfSection.Streaming):0.0}, cam {PerfStats.LastMs(PerfSection.Camera):0.0}, " +
                        $"fx {PerfStats.LastMs(PerfSection.Particles):0.0}, snd {PerfStats.LastMs(PerfSection.Sounds):0.0}, mus {PerfStats.LastMs(PerfSection.Music):0.0}) " +
                        $"render {PerfStats.LastMs(PerfSection.Render):0.0} ui {PerfStats.LastMs(PerfSection.Ui):0.0} gc {PerfStats.LastGcPauseMs:0.0} present {PerfStats.LastMs(PerfSection.Present):0.0} wait {PerfStats.LastMs(PerfSection.FrameWait):0.0} events {PerfStats.LastMs(PerfSection.WindowEvents):0.0} other {other:0.0} jit {jitNew} draws {LastDraws} | gpu (this, 1 and 2 frames before) {_gpuFrames[0]} ; {_gpuFrames[1]} ; {_gpuFrames[2]}")); // Amedo 2026-11-20
                }
                _frame.Add(PerfStats.LastFrameMs);
                _update.Add(PerfStats.LastMs(PerfSection.Update));
                _render.Add(PerfStats.LastMs(PerfSection.Render));
                _work.Add(PerfStats.LastMs(PerfSection.Update) + PerfStats.LastMs(PerfSection.Render) + PerfStats.LastMs(PerfSection.Ui));
                _gc.Add(PerfStats.LastGcPauseMs);
                _draws.Add(LastDraws);
                for (int i = 0; i < Sections.Length; i++)
                {
                    float ms = PerfStats.LastMs(Sections[i]);
                    _sectionSums[i] += ms;
                    if (ms > _sectionMax[i]) _sectionMax[i] = ms;
                }
            }
            _started = true;
        }
        GpuStats.BeginFrame();
    }

    private static string Stat(string name, List<float> v)
    {
        if (v.Count == 0) return $"{name}: -";
        var s = v.OrderBy(x => x).ToList();
        float P(double p) => s[Math.Min(s.Count - 1, (int)Math.Ceiling(p * s.Count) - 1)];
        return string.Create(CultureInfo.InvariantCulture, $"{name}: avg {s.Average():0.00}  p95 {P(0.95):0.00}  p99 {P(0.99):0.00}  max {s[^1]:0.00} ms");
    }

    public void Write(string level, int width, int height, SystemMonitor? monitor, string gpuName)
    {
        if (_frame.Count == 0) return;
        double seconds = _frame.Sum(x => (double)x) / 1000.0;
        var lines = new List<string>
        {
            $"CrashGame performance — {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"level {level}, {width}x{height}, frame mode {Engine.FrameModeName}, power throttling {(Engine.PowerThrottlingOff ? "off" : "Windows default")}",
            string.Create(CultureInfo.InvariantCulture, $"frames {_frame.Count} over {seconds:0.0} s (after {WarmSeconds:0} s warm-up) = {_frame.Count / Math.Max(seconds, 1e-6):0.0} fps"),
            Stat("frame (wall)", _frame),
            Stat("work (update + render + ui)", _work),
            Stat("update", _update),
            Stat("render", _render),
            $"draws per frame: avg {_draws.Average():0}  max {_draws.Max()}",
            $"frames over 6.94 ms (144): {_frame.Count(x => x > 1000f / 144f)}, over 16.7 ms (60): {_frame.Count(x => x > 1000f / 60f)}, over 33.3 ms (30): {_frame.Count(x => x > 1000f / 30f)}",
            string.Create(CultureInfo.InvariantCulture, $"frames that missed the screen (over {MissedMs:0.0} ms = 1.5x the frame): {_missed}, each one is a [slow] line in CrashGame.log"), // Amedo 2026-11-14
            string.Create(CultureInfo.InvariantCulture, $"longest GC pause {(_gc.Count == 0 ? 0 : _gc.Max()):0.00} ms"),
            $"memory peak: RAM {MemoryReport.PeakPrivateBytes >> 20} MB, C# {MemoryReport.PeakManagedBytes >> 20} MB | {MemoryReport.LastCheck}",
            "sections (avg / max ms): " + string.Join("  ", Sections.Select((sec, i) =>
                string.Create(CultureInfo.InvariantCulture, $"{sec} {_sectionSums[i] / _frame.Count:0.00}/{_sectionMax[i]:0.0}"))),
        };
        if (monitor is not null && monitor.Averages() is { Samples: > 0 } avg)
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"PC usage by the game (avg of {avg.Samples} s): CPU {avg.Cpu:0.0}% ({monitor.CpuName}), GPU {(avg.Gpu < 0 ? "n/a" : $"{avg.Gpu:0.0}%")} ({gpuName}), RAM {avg.Ram / 1048576.0:0} MB"));
        var name = $"perf_{GamePaths.FileName(level)}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, name), lines);
        foreach (var l in lines) Console.WriteLine("[perf] " + l);
    }
}
