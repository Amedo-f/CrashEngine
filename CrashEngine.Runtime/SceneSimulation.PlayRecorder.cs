using System.Globalization;
using System.Numerics;
using CrashEngine.Core;
using Silk.NET.Input;

namespace CrashEngine.Runtime;

// Amedo 2026-10-01
public sealed partial class SceneSimulation
{
    private StreamWriter? _record;

    public string? RecordPath { get; private set; }

    private void PollRecordKey()
    {
        if (!Input.KeyDown(Key.F8)) return;
        if (_record is null) StartRecording();
        else StopRecording();
    }

    public void StartRecording(string? path = null)
    {
        StopRecording();
        path ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrashEngineRecords",
                              $"playrec_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _record = new StreamWriter(path) { AutoFlush = true };
        RecordPath = path;
        _log?.Invoke($"[rec] recording every logic step -> {path} (F8 stops)");
    }

    public void StopRecording()
    {
        if (_record is null) return;
        _record.Dispose();
        _record = null;
        _log?.Invoke($"[rec] stopped -> {RecordPath}");
    }

    private void RecordStep()
    {
        if (_record is null || World.PlayerCharacter is not { } c || World.Player is not { } p) return;
        string on = c.StandingOn is AgentLab.AgentLabAgent s
            ? string.Create(CultureInfo.InvariantCulture, $"{s.Entity.Name.Split("_act_").Last()}/{c.StandingPart} at {V(s.GamePosition)}")
            : "-";
        _record.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{World.Now:0.0000} f{World.Frame} steps {LastLogicSteps} pos {V(p.GamePosition)} vel {V(c.Velocity)} st {c.MotionState} mode 0x{c.State70 & 0xF00:X} " +
            $"grounded {c.IsGrounded} on {on} plat {V(c.LastPlatform)} mv {V(c.LastMove)} hit {c.LastHit} solids {c.Collision.Solids.Count} chunk {p.Chunk.Name}"));
    }

    private static string V(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"({v.X:0.####},{v.Y:0.####},{v.Z:0.####})");
}
