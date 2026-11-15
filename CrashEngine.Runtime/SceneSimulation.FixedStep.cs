using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime;

// Amedo 2026-09-30
public static class SimulationClock
{
    public static bool Active { get; internal set; }
    public static float Alpha { get; internal set; } = 1f;
}

// Amedo 2026-09-30
public sealed partial class SceneSimulation
{
    public static bool FixedLogicStep { get; set; } = true;
    public const int NtscLogicTicks = 150, PalLogicTicks = 180;
    public static int LogicTicks { get; private set; } = NtscLogicTicks;
    private const int MaxLogicSteps = 8;
    private const float SnapDistanceSq = 2.5f * 2.5f;
    private const float SnapTurnCos = 0.7f;

    private long _realTicks, _simTicks;
    private bool _snapAll;
    private int _smoothFrame;
    public float RenderAlpha { get; private set; } = 1f;
    public int LastLogicSteps { get; private set; }

    private sealed class Smoothed
    {
        public Matrix4x4 Prev, Cur, Shown;
        public int Seen;
        public bool Fresh = true;
    }

    private double _tickCarry; // Amedo 2026-10-01
    private readonly Dictionary<Entity, Smoothed> _smooth = new(ReferenceEqualityComparer.Instance);
    private readonly List<Entity> _smoothGone = new();

    public void Tick(double dt)
    {
        PollRecordKey(); // Amedo 2026-10-01
        CrashEngine.Renderer.SunShadowPass.FocusEntity = World.Player?.Entity; // Amedo
        if (!FixedLogicStep)
        {
            SimulationClock.Active = false;
            SimulationClock.Alpha = 1f;
            CrashEngine.Importer.AnimatedObject.LogicAlpha = 1f; // Amedo 2026-10-01
            LastLogicSteps = 1;
            LogicStep(dt);
            foreach (var d in World.DynamicScenery) d.Show(1f); // Amedo 2026-10-06
            CrashEngine.Importer.AnimatedObject.LogicNowTicks = World.NowTicks; // Amedo 2026-10-02
            CrashEngine.Importer.AnimatedObject.LogicStepTicks = (int)Math.Round(dt * 9000.0);
            return;
        }
        SimulationClock.Active = true;
        SimulationClock.Alpha = 1f; // Amedo 2026-10-02
        RestoreLogical();
        _tickCarry += Math.Max(0.0, dt * 9000.0); // Amedo 2026-10-01
        int whole = (int)_tickCarry;
        _realTicks += whole;
        _tickCarry -= whole;
        int steps = 0;
        while (_simTicks < _realTicks && steps < MaxLogicSteps && InputTape.Current?.Done != true) // Amedo
        {
            CapturePrevious();
            LogicStep(LogicTicks / 9000.0);
            _simTicks += LogicTicks;
            steps++;
        }
        if (_simTicks < _realTicks) _realTicks = _simTicks;
        LastLogicSteps = steps;
        RenderAlpha = 1f - (_simTicks - _realTicks) / (float)LogicTicks;
        SimulationClock.Alpha = RenderAlpha;
        World.Particles?.SetRenderAlpha(RenderAlpha); // Amedo
        CrashEngine.Importer.AnimatedObject.LogicAlpha = RenderAlpha; // Amedo 2026-10-01
        CrashEngine.Importer.AnimatedObject.LogicNowTicks = World.NowTicks; // Amedo 2026-10-02
        CrashEngine.Importer.AnimatedObject.LogicStepTicks = LogicTicks;
        ShowInterpolated();
    }

    private void CapturePrevious()
    {
        foreach (var agent in World.Agents)
        {
            agent.ClearWarp(); // Amedo 2026-11-11
            if (agent.Entity.Transform.LocalMatrix is not { } m) continue;
            if (!_smooth.TryGetValue(agent.Entity, out var s))
            {
                s = new Smoothed { Prev = m, Cur = m, Shown = m };
                _smooth[agent.Entity] = s;
            }
            s.Prev = m;
            s.Fresh = false;
        }
    }

    private void ShowInterpolated()
    {
        _smoothFrame++;
        float a = RenderAlpha;
        foreach (var agent in World.Agents)
        {
            var e = agent.Entity;
            if (e.Transform.LocalMatrix is not { } m) continue;
            if (!_smooth.TryGetValue(e, out var s))
            {
                s = new Smoothed { Prev = m };
                _smooth[e] = s;
            }
            s.Cur = m;
            s.Seen = _smoothFrame;
            if (s.Fresh || _snapAll || agent.WarpedThisStep || Jumped(s.Prev, m)) s.Prev = m; // Amedo 2026-11-11
            s.Fresh = false;
            var shown = a >= 1f ? m : Matrix4x4.Lerp(s.Prev, m, a);
            s.Shown = shown;
            if (shown != m) e.Transform.LocalMatrix = shown;
        }
        _snapAll = false;
        foreach (var d in World.DynamicScenery) d.Show(a); // Amedo 2026-10-06
        foreach (var (e, s) in _smooth)
            if (s.Seen != _smoothFrame) _smoothGone.Add(e);
        foreach (var e in _smoothGone) _smooth.Remove(e);
        _smoothGone.Clear();
    }

    private void RestoreLogical()
    {
        foreach (var (e, s) in _smooth)
        {
            if (e.Transform.LocalMatrix is not { } m) continue;
            if (m == s.Shown) e.Transform.LocalMatrix = s.Cur;
            else
            {
                s.Cur = m;
                s.Prev = m;
            }
        }
    }

    private static bool Jumped(Matrix4x4 prev, Matrix4x4 cur)
    {
        if (GameMath.DistanceSq(prev.Translation, cur.Translation) > SnapDistanceSq) return true;
        var f0 = new Vector3(prev.M31, prev.M32, prev.M33);
        var f1 = new Vector3(cur.M31, cur.M32, cur.M33);
        float l = GameMath.Length(f0) * GameMath.Length(f1);
        return l > 1e-6f && GameMath.Dot(f0, f1) / l < SnapTurnCos;
    }

    private bool _hostShowsGameCamera;

    private void NoteHostCamera(Vector3 forward)
    {
        _hostShowsGameCamera = false;
        if (GameCamera is null || !GameCameraView(out _, out var shown, out _, out _)) return;
        float l = GameMath.Length(forward);
        _hostShowsGameCamera = l > 1e-6f && GameMath.Dot(forward / l, shown) > 0.99999f;
    }

    private void UseLogicalCamera()
    {
        if (!FixedLogicStep || !_hostShowsGameCamera || GameCamera is not { } cam) return;
        var gameToWorld = RootToWorld;
        var pose = cam.Pose;
        var eye = Vector3.Transform(new Vector3(pose.M41, pose.M42, pose.M43), gameToWorld);
        var forward = Vector3.TransformNormal(new Vector3(pose.M31, pose.M32, pose.M33), gameToWorld);
        var up = Vector3.TransformNormal(new Vector3(pose.M21, pose.M22, pose.M23), gameToWorld);
        if (GameMath.LengthSquared(forward) <= 0f) return;
        forward = GameMath.Normalize(forward);
        up = GameMath.LengthSquared(up) > 0f ? GameMath.Normalize(up) : Vector3.UnitY;
        var f = GameMath.Normalize(eye + forward - eye);
        var s = GameMath.Normalize(Vector3.Cross(f, up));
        var u = Vector3.Cross(s, f);
        ApplyCameraBasis(s, u, f);
    }

    internal void SnapDisplay() => _snapAll = true;

    internal void SnapDisplay(Entity e)
    {
        if (_smooth.TryGetValue(e, out var s)) s.Fresh = true;
    }

    // Amedo 2026-11-14
    internal void CarryDisplay(Entity e, Matrix4x4 obj)
    {
        if (!_smooth.TryGetValue(e, out var s)) return;
        s.Prev *= obj;
        s.Cur *= obj;
        s.Shown *= obj;
    }
}
