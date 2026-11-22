using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed class RuntimeParticles
{
    // Amedo 2026-09-29
    public const int MaxSystems = 300;
    public const int Banks = 16;

    private readonly ParticleEffectsRenderer _fx;
    private readonly List<(TwinParticleSystem? System, int Bank)> _slots = new(); // Amedo 2026-09-29
    private readonly bool[] _banks = new bool[Banks];
    private readonly Dictionary<int, List<ParticleEmitterSim>> _bankEmitters = new();
    private readonly Dictionary<int, (ParticleEmitterSim Sim, int Index)> _live = new();
    private readonly List<(ParticleEmitterSim Sim, int Index)> _dying = new(); // Amedo 2026-09-29
    private int _next;
    private int _count;

    public Action<string, int, string>? Unimplemented { get; set; }
    public Func<Matrix4x4>? RootToAnchor { get; set; } // Amedo 2026-09-29
    public int LiveCount => _live.Count;
    public int SystemCount => _count;

    public RuntimeParticles(ParticleEffectsRenderer fx, PS2AnyTwinsanityRM2? global, PS2AnyTwinsanityRM2 level)
    {
        _fx = fx;
        foreach (var sys in SystemsOf(global)) Place(sys, 0); // Amedo 2026-09-29
        FirstBank = AddBank(level);
    }

    public int FirstBank { get; } // Amedo 2026-09-29

    // Amedo
    private static readonly Func<bool> AlwaysDrawn = static () => true;
    public Func<bool>? AnchorDrawn { get => _fx.World.AnchorDrawn; set => _fx.World.AnchorDrawn = value; }
    public Func<Vector3, Vector3, bool>? BoxInView { get => _fx.World.BoxInView; set => _fx.World.BoxInView = value; } // Amedo
    public Func<bool>? ClockStopped { get => _fx.ClockStopped; set => _fx.ClockStopped = value; } // Amedo

    private static List<TwinParticleSystem> SystemsOf(PS2AnyTwinsanityRM2? rm) =>
        rm?.GetItem<PS2AnyParticleData>((uint)Constants.LEVEL_PARTICLES_ITEM)?.ParticleSystems ?? new();

    // Amedo 2026-09-29
    private void Place(TwinParticleSystem sys, int bank)
    {
        int i = _slots.FindIndex(s => s.System is null);
        if (i < 0)
        {
            if (_slots.Count >= MaxSystems - 1) return;
            _slots.Add((sys, bank));
        }
        else _slots[i] = (sys, bank);
        _count++;
    }

    // Amedo 2026-09-29
    public int AddBank_1B8910(PS2AnyTwinsanityRM2? rm, Matrix4x4? layoutToAnchor, Func<bool>? sceneDrawn = null)
    {
        int bank = -1;
        if (!_banks[2]) bank = 2;
        else
            for (int b = 3; b < Banks; b++)
                if (!_banks[b]) { bank = b; break; }
        if (bank < 0)
        {
            Unimplemented?.Invoke("particle", Banks, "no free particle bank (0x1B8910)");
            return -1;
        }
        var systems = SystemsOf(rm);
        int n = Math.Min(systems.Count, MaxSystems - _count);
        for (int i = 0; i < n; i++) Place(systems[i], bank);
        _banks[bank] = true;
        if (layoutToAnchor is { } toAnchor) StartEmitters_1B8648(rm, bank, toAnchor, sceneDrawn);
        return bank;
    }

    public int AddBank(PS2AnyTwinsanityRM2? rm) => AddBank_1B8910(rm, null);

    // Amedo
    public void MoveBank(int bank, Matrix4x4 oldToAnchor, Matrix4x4 newToAnchor)
    {
        if (!_bankEmitters.TryGetValue(bank, out var list) || !Matrix4x4.Invert(oldToAnchor, out var inv)) return;
        var delta = inv * newToAnchor;
        var rot = delta;
        rot.Translation = Vector3.Zero;
        foreach (var sim in list)
        {
            sim.Origin = Vector3.Transform(sim.Origin, delta);
            sim.EmitRotation *= rot;
            sim.RenderRotation *= rot;
        }
    }

    // Amedo 2026-09-29
    private void StartEmitters_1B8648(PS2AnyTwinsanityRM2? rm, int bank, Matrix4x4 toAnchor, Func<bool>? sceneDrawn)
    {
        var data = rm?.GetItem<PS2AnyParticleData>((uint)Constants.LEVEL_PARTICLES_ITEM);
        if (data is null) return;
        var rot = toAnchor;
        rot.Translation = Vector3.Zero;
        var list = new List<ParticleEmitterSim>();
        foreach (var em in data.ParticleEmitters)
        {
            int index = Find_1B7620(ParticleNames.Clean(em.Name), bank);
            if (index < 0 || System(index) is not { } sys) continue;
            var sim = new ParticleEmitterSim(ParticleFx.DefFor(sys), _fx.World, em.UnkInt1);
            sim.ApplyEmitter(em);
            sim.EmitRotation *= rot;
            sim.RenderRotation *= rot;
            sim.Origin = Vector3.Transform(new Vector3(em.Position.X, em.Position.Y, em.Position.Z), toAnchor);
            sim.SceneDrawn = sceneDrawn; // Amedo
            _fx.World.Emitters.Add(sim);
            list.Add(sim);
        }
        _bankEmitters[bank] = list;
    }

    // Amedo 2026-09-29
    public int Find_1B7620(string name, int bank)
    {
        for (int pass = 0; pass < 3; pass++)
            for (int i = 0; i < _slots.Count; i++)
            {
                var (sys, b) = _slots[i];
                if (sys is null || (pass == 0 && b != bank) || (pass == 1 && b != 0)) continue;
                if (ParticleNames.Clean(sys.Name) == name) return i;
            }
        return -1;
    }

    // Amedo 2026-09-29
    public void RemoveBank_1B7AE0(int bank)
    {
        if (bank < 0 || bank >= Banks) return;
        if (_bankEmitters.Remove(bank, out var emitters))
            foreach (var sim in emitters) _fx.World.Emitters.Remove(sim);
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].System is null || _slots[i].Bank != bank) continue;
            foreach (var (h, live) in _live.ToList())
                if (live.Index == i) Kill(h); // Amedo 2026-09-29
            foreach (var dying in _dying.Where(d => d.Index == i).ToList()) // Amedo 2026-09-29
            {
                _fx.World.Emitters.Remove(dying.Sim);
                _dying.Remove(dying);
            }
            _slots[i] = (null, 0);
            _count--;
        }
        _banks[bank] = false;
    }

    // Amedo 2026-09-29
    public void PauseMarkers(Entity root, bool paused)
    {
        if (paused) _fx.PausedRoots.Add(root);
        else _fx.PausedRoots.Remove(root);
    }

    public TwinParticleSystem? System(int index) => index >= 0 && index < _slots.Count ? _slots[index].System : null; // Amedo 2026-09-29

    // Amedo 2026-09-29
    private Vector3 AnchorPoint(Vector3 p) => RootToAnchor is { } f ? Vector3.Transform(p, f()) : p;
    private Matrix4x4 AnchorMatrix(Matrix4x4 m) => RootToAnchor is { } f ? m * f() : m;

    // Amedo 2026-09-30
    public void AddDecal_1ADC18(in Vector3 positionIn, in Vector3 normalIn, in Vector3 directionIn, int type, int group, int flags, int key)
    {
        var position = positionIn; // Amedo 2026-10-27
        var normal = normalIn;
        var direction = directionIn;
        if (RootToAnchor is { } f)
        {
            var m = f();
            position = Vector3.Transform(position, m);
            normal = Vector3.TransformNormal(normal, m);
            direction = Vector3.TransformNormal(direction, m);
        }
        _fx.World.Decals.Add(position, normal, direction, type, group, flags, key);
    }

    // Amedo
    public void SkidMarks_13FEB8(SkidMarkRing ring, bool on)
    {
        var list = _fx.World.SkidMarks;
        if (!on) list.Remove(ring);
        else if (!list.Contains(ring)) list.Add(ring);
        _fx.World.SkidToAnchor = RootToAnchor?.Invoke() ?? Matrix4x4.Identity;
    }

    public int Spawn(int index, int count, bool worldSpace, Vector3 position)
    {
        if (System(index) is not { } sys)
        {
            Unimplemented?.Invoke("particle", index, "particle system index out of the loaded table");
            return -1;
        }
        var def = ParticleFx.DefFor(sys);
        var sim = new ParticleEmitterSim(def, _fx.World, def.Phase, count) { Origin = AnchorPoint(position), WorldSpace = worldSpace, IgnoreDistance = true, SceneDrawn = AlwaysDrawn }; // Amedo 2026-09-29
        _fx.World.Emitters.Add(sim);
        int h = _next++;
        _live[h] = (sim, index); // Amedo 2026-09-29
        return h;
    }

    // Amedo
    public void SetScale(int h, float scale)
    {
        if (_live.TryGetValue(h, out var live)) live.Sim.Scale = scale;
    }

    public void SetEmitMatrix(int h, Matrix4x4 m)
    {
        if (!_live.TryGetValue(h, out var live)) return;
        var sim = live.Sim; // Amedo 2026-09-29
        m = AnchorMatrix(m);
        var rot = m;
        rot.Translation = Vector3.Zero;
        sim.EmitRotation = rot;
        sim.RenderOffset = m.Translation; // Amedo 2026-11-21
        if (!sim.WorldSpace) return;
        sim.Origin = m.Translation;
    }

    // Amedo
    public bool TestCollisionSpheres_1B7738(Vector3 point, float radius, float yScale) =>
        _fx.World.TestCollisionSpheres_1B7738(AnchorPoint(point), radius, yScale) is not null;

    public void SetRenderMatrix(int h, Matrix4x4 m)
    {
        if (!_live.TryGetValue(h, out var live)) return;
        var sim = live.Sim; // Amedo 2026-09-29
        m = AnchorMatrix(m);
        var rot = m;
        rot.Translation = Vector3.Zero;
        sim.RenderRotation = rot;
        sim.Origin = m.Translation;
        sim.RenderOffset = m.Translation; // Amedo
    }

    public void Stop(int h)
    {
        if (!_live.Remove(h, out var live)) return;
        live.Sim.Release_1B47E0(); // Amedo 2026-09-29
        _dying.Add(live);
    }

    // Amedo 2026-09-29
    private void Kill(int h)
    {
        if (_live.Remove(h, out var live)) _fx.World.Emitters.Remove(live.Sim);
    }

    public bool LogicDriven { get => _fx.World.LogicDriven; set => _fx.World.LogicDriven = value; } // Amedo

    // Amedo
    public void StepWithLogic(Vector3? cameraWorld) => _fx.StepWithLogic(cameraWorld);

    // Amedo
    public void SetRenderAlpha(float alpha) => _fx.World.RenderCarry = -(1f - alpha) * ParticleWorld.Dt;

    public void Update()
    {
        if (_fx.World.SkidMarks.Count > 0) _fx.World.SkidToAnchor = RootToAnchor?.Invoke() ?? Matrix4x4.Identity; // Amedo
        float now = _fx.World.LogicDriven ? _fx.World.Clock : _fx.World.RenderTime; // Amedo
        foreach (var (h, live) in _live.ToList())
            if (live.Sim.Finished && !live.Sim.AnyAlive(now)) Kill(h); // Amedo 2026-09-29
        for (int i = _dying.Count - 1; i >= 0; i--) // Amedo 2026-09-29
            if (!_dying[i].Sim.AnyAlive(now))
            {
                _fx.World.Emitters.Remove(_dying[i].Sim);
                _dying.RemoveAt(i);
            }
    }

    public void Clear()
    {
        foreach (var live in _live.Values) _fx.World.Emitters.Remove(live.Sim); // Amedo 2026-09-29
        _live.Clear();
        foreach (var dying in _dying) _fx.World.Emitters.Remove(dying.Sim); // Amedo 2026-09-29
        _dying.Clear();
        _fx.World.Decals.Clear(); // Amedo 2026-09-30
        _fx.World.SkidMarks.Clear(); // Amedo
    }

    // Amedo
    public void KillParticles_1B9AB0()
    {
        Clear();
        _fx.World.Clock += 10f;
    }
}
