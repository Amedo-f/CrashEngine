using System.Diagnostics;
using CrashEngine.Core;

namespace CrashEngine.Renderer;

// Amedo 2026-09-29
internal interface IGpuResource
{
    bool Uploaded { get; }
    void Upload();
    void Warm() { }
    void Dispose();
    string Describe() => GetType().Name; // Amedo
    long PendingBytes => 0; // Amedo
}

// Amedo 2026-09-29
public sealed class GpuUploadQueue
{
    [ThreadStatic] private static GpuUploadQueue? _current;

    internal static GpuUploadQueue? Current => _current;
    public static bool Recording => _current is not null;

    private readonly List<IGpuResource> _resources = new();
    private int _next;

    public int Count => _resources.Count;
    public int Done => _next;
    public double UploadMs { get; private set; }
    public int Slices { get; private set; }

    public readonly struct Scope : IDisposable
    {
        private readonly GpuUploadQueue? _previous;
        internal Scope(GpuUploadQueue? previous) => _previous = previous;
        public void Dispose() => _current = _previous;
    }

    public Scope Begin()
    {
        var previous = _current;
        _current = this;
        return new Scope(previous);
    }

    internal void Add(IGpuResource r) => _resources.Add(r);

    public static double SlowItemMs { get; set; } // Amedo
    internal static long LastFrame { get; private set; } = -1; // Amedo
    public static Action<string>? SlowItem { get; set; } // Amedo
    public static long MaxBytesPerFrame { get; set; } = 2L << 20; // Amedo
    private static long _bytesFrame = -1, _frameBytes; // Amedo
    public static long MaxBytesPerSecond { get; set; } = (2L << 20) * 144; // Amedo 2026-11-20
    private static double _secondBytes; // Amedo 2026-11-20
    private static long _secondStamp; // Amedo 2026-11-20
    public static int MaxTexturesPerFrame { get; set; } = 8; // Amedo 2026-11-20
    private static int _frameTextures; // Amedo 2026-11-20

    public void Warm()
    {
        foreach (var r in _resources) r.Warm();
    }

    public bool Upload(double budgetMs)
    {
        if (_next < _resources.Count) // Amedo
        {
            Slices++;
            LastFrame = PerfStats.FrameNumber;
        }
        long start = Stopwatch.GetTimestamp();
        long budget = (long)(budgetMs * Stopwatch.Frequency / 1000.0);
        int first = _next; // Amedo
        bool capped = MaxBytesPerFrame > 0 && !double.IsPositiveInfinity(budgetMs); // Amedo
        if (_bytesFrame != PerfStats.FrameNumber)
        {
            _bytesFrame = PerfStats.FrameNumber;
            _frameBytes = 0;
            _frameTextures = 0; // Amedo 2026-11-20
            long stamp = Stopwatch.GetTimestamp(); // Amedo 2026-11-20
            double elapsed = _secondStamp == 0 ? 1.0 : (stamp - _secondStamp) / (double)Stopwatch.Frequency;
            _secondStamp = stamp;
            _secondBytes = Math.Min(MaxBytesPerFrame, _secondBytes + MaxBytesPerSecond * elapsed);
        }
        if (capped && MaxBytesPerSecond > 0 && _secondBytes <= 0) return _next >= _resources.Count; // Amedo 2026-11-20
        while (_next < _resources.Count)
        {
            long bytes = _resources[_next].Uploaded ? 0 : _resources[_next].PendingBytes; // Amedo
            if (capped && _frameBytes > 0 && _frameBytes + bytes > MaxBytesPerFrame) break;
            bool texture = _resources[_next] is Texture2D { Uploaded: false }; // Amedo 2026-11-20
            if (capped && texture && MaxTexturesPerFrame > 0 && _frameTextures >= MaxTexturesPerFrame) break; // Amedo 2026-11-20
            if (texture) _frameTextures++; // Amedo 2026-11-20
            if (capped && MaxBytesPerSecond > 0 && _frameBytes > 0 && bytes > _secondBytes) break; // Amedo 2026-11-20
            _frameBytes += bytes;
            if (capped) _secondBytes -= bytes; // Amedo 2026-11-20
            GpuStats.QueueBytes += bytes; // Amedo 2026-11-20
            var r = _resources[_next++];
            if (SlowItem is not { } slow) // Amedo
            {
                if (!r.Uploaded) r.Upload();
            }
            else
            {
                long item = Stopwatch.GetTimestamp();
                int gc = GC.CollectionCount(0);
                if (!r.Uploaded) r.Upload();
                double ms = (Stopwatch.GetTimestamp() - item) * 1000.0 / Stopwatch.Frequency;
                if (ms > SlowItemMs) slow($"{r.Describe()} {ms:0.0} ms (item {_next - 1 - first} of this step, gc0 {GC.CollectionCount(0) - gc})");
            }
            if (budget < Stopwatch.GetTimestamp() - start) break;
        }
        UploadMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        return _next >= _resources.Count;
    }

    public void DisposeAll()
    {
        foreach (var r in _resources) r.Dispose();
        _resources.Clear();
        _next = 0;
    }

    // Amedo 2026-11-14
    public int MoveUnused(HashSet<object> used, GpuUploadQueue keep, GpuUploadQueue unused)
    {
        int moved = 0;
        foreach (var r in _resources)
        {
            if (used.Contains(r)) keep._resources.Add(r);
            else
            {
                unused._resources.Add(r);
                moved++;
            }
        }
        _resources.Clear();
        _next = 0;
        return moved;
    }

    // Amedo 2026-11-14
    public bool DisposeSome(double budgetMs)
    {
        long start = Stopwatch.GetTimestamp();
        long budget = (long)(budgetMs * Stopwatch.Frequency / 1000.0);
        while (_resources.Count > 0)
        {
            int last = _resources.Count - 1;
            _resources[last].Dispose();
            _resources.RemoveAt(last);
            if (budget < Stopwatch.GetTimestamp() - start) break;
        }
        return _resources.Count == 0;
    }

    internal static void RequireMainThread(string what)
    {
        if (Engine.MainThreadId >= 0 && Environment.CurrentManagedThreadId != Engine.MainThreadId)
            throw new InvalidOperationException($"{what}: GL call off the main thread (no GpuUploadQueue active)");
    }
}
