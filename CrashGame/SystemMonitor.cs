using System.Diagnostics;
using System.Runtime.InteropServices;
using CrashEngine.Platform.Windows;

namespace CrashGame;

// Amedo 2026-09-30
sealed class SystemMonitor : IDisposable
{
    public volatile float CpuGame = -1, CpuTotal = -1, GpuGame = -1, GpuTotal = -1;
    public long RamGame, RamUsed, RamTotal, VramGame = -1, VramUsed = -1;
    public string CpuName { get; } = ReadCpuName();

    private readonly Thread _thread;
    private volatile bool _stop;
    private readonly int _pid = Environment.ProcessId;
    private readonly List<(float Cpu, float Gpu, long Ram)> _history = new();

    public SystemMonitor()
    {
        _thread = new Thread(Loop) { IsBackground = true, Name = "SystemMonitor", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    private void Loop()
    {
        IntPtr query = IntPtr.Zero, gpuEngine = IntPtr.Zero, gpuProcMem = IntPtr.Zero, gpuAdapterMem = IntPtr.Zero;
        bool pdh = OperatingSystem.IsWindows() && Pdh.PdhOpenQueryW(null, IntPtr.Zero, out query) == 0;
        if (pdh)
        {
            Pdh.PdhAddEnglishCounterW(query, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out gpuEngine);
            Pdh.PdhAddEnglishCounterW(query, @"\GPU Process Memory(*)\Dedicated Usage", IntPtr.Zero, out gpuProcMem);
            Pdh.PdhAddEnglishCounterW(query, @"\GPU Adapter Memory(*)\Dedicated Usage", IntPtr.Zero, out gpuAdapterMem);
            Pdh.PdhCollectQueryData(query);
        }
        using var self = Process.GetCurrentProcess();
        var lastCpu = self.TotalProcessorTime;
        var lastWall = Stopwatch.GetTimestamp();
        Win32SystemStats.GetSystemTimes(out long lastIdle, out long lastKernel, out long lastUser);
        string ownTag = $"pid_{_pid}_";
        while (!_stop)
        {
            Thread.Sleep(1000);
            if (_stop) break;
            self.Refresh();
            var cpu = self.TotalProcessorTime;
            long wall = Stopwatch.GetTimestamp();
            double wallMs = (wall - lastWall) * 1000.0 / Stopwatch.Frequency;
            CpuGame = (float)((cpu - lastCpu).TotalMilliseconds / Math.Max(1.0, wallMs * Environment.ProcessorCount) * 100.0);
            lastCpu = cpu;
            lastWall = wall;
            if (Win32SystemStats.GetSystemTimes(out long idle, out long kernel, out long user))
            {
                long busy = (kernel - lastKernel) + (user - lastUser);
                CpuTotal = busy <= 0 ? 0 : (float)(100.0 * (busy - (idle - lastIdle)) / busy);
                (lastIdle, lastKernel, lastUser) = (idle, kernel, user);
            }
            RamGame = self.WorkingSet64;
            var mem = new Win32SystemStats.MemoryStatusEx { Length = (uint)Marshal.SizeOf<Win32SystemStats.MemoryStatusEx>() };
            if (Win32SystemStats.GlobalMemoryStatusEx(ref mem))
            {
                RamTotal = (long)mem.TotalPhys;
                RamUsed = (long)(mem.TotalPhys - mem.AvailPhys);
            }
            if (pdh && Pdh.PdhCollectQueryData(query) == 0)
            {
                double game = 0, total = 0;
                foreach (var (name, value) in Pdh.Read(gpuEngine, Pdh.FmtDouble))
                {
                    if (!name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase)) continue;
                    total += value;
                    if (name.StartsWith(ownTag, StringComparison.Ordinal)) game += value;
                }
                GpuGame = (float)Math.Min(100.0, game);
                GpuTotal = (float)Math.Min(100.0, total);
                long vGame = 0, vUsed = 0;
                foreach (var (name, value) in Pdh.Read(gpuProcMem, Pdh.FmtLarge))
                    if (name.StartsWith(ownTag, StringComparison.Ordinal)) vGame += (long)value;
                foreach (var (_, value) in Pdh.Read(gpuAdapterMem, Pdh.FmtLarge)) vUsed += (long)value;
                VramGame = vGame;
                VramUsed = vUsed;
            }
            lock (_history) _history.Add((CpuGame, GpuGame, RamGame));
        }
        if (pdh) Pdh.PdhCloseQuery(query);
    }

    public (float Cpu, float Gpu, long Ram, int Samples) Averages()
    {
        lock (_history)
        {
            var s = _history.Skip(3).ToList();
            if (s.Count == 0) return (-1, -1, 0, 0);
            return (s.Average(x => x.Cpu), s.Where(x => x.Gpu >= 0).Select(x => x.Gpu).DefaultIfEmpty(-1).Average(), (long)s.Average(x => x.Ram), s.Count);
        }
    }

    public void Dispose() => _stop = true;

    private static string ReadCpuName()
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return (Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", null) as string)?.Trim() ?? "CPU";
        }
        catch { }
        return "CPU";
    }
}
