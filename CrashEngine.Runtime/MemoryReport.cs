using System.Diagnostics;
using CrashEngine.Core;
using CrashEngine.Renderer;

namespace CrashEngine.Runtime;

public static class MemoryReport
{
    private static long _sampledAt;
    private static string _line = "";

    public static long PeakPrivateBytes { get; private set; }
    public static long PeakManagedBytes { get; private set; }
    public static string LastCheck { get; private set; } = "F9 = memory check (full collection, what stays alive)";

    public static string Line(SceneSimulation? sim)
    {
        long now = Environment.TickCount64;
        if (_line.Length > 0 && now - _sampledAt < 1000) return _line;
        _sampledAt = now;
        long privateBytes, workingSet;
        using (var self = Process.GetCurrentProcess())
        {
            privateBytes = self.PrivateMemorySize64;
            workingSet = self.WorkingSet64;
        }
        long managed = GC.GetGCMemoryInfo().HeapSizeBytes;
        PeakPrivateBytes = Math.Max(PeakPrivateBytes, privateBytes);
        PeakManagedBytes = Math.Max(PeakManagedBytes, managed);
        _line = $"RAM {Mb(privateBytes)} MB (in use {Mb(workingSet)}, peak {Mb(PeakPrivateBytes)}) | C# {Mb(managed)} MB | GPU textures {Mb(GpuStats.TextureBytes)} + meshes {Mb(GpuStats.MeshBytes)} + pool {Mb(GpuStats.MeshPoolBytes)} MB" +
                (sim is null ? "" : $" | scenes: {sim.StreamCounts}");
        return _line;
    }

    public static string Check()
    {
        long before = GC.GetTotalMemory(false);
        var watch = Stopwatch.StartNew();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        LastCheck = $"check: C# kept {Mb(GC.GetTotalMemory(false))} MB (was {Mb(before)}, {watch.ElapsedMilliseconds} ms) | alive: {LiveProbe.Report()}";
        Console.WriteLine("[mem] " + LastCheck);
        _line = "";
        return LastCheck;
    }

    private static long Mb(long bytes) => bytes >> 20;
}
