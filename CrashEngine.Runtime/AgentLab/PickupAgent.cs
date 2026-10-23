using System.Numerics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-27
public sealed class PickupAgent
{
    public const int FirstId = 562;
    public const int StateCount = 10;
    public const int Inactive = 0;
    public const int Idle = 4;
    public const int SuckIn = 6;
    public const int Collect = 7;
    public const int FlyAway = 8;

    public readonly record struct Settings(float Radius, float RadiusSq, float Suck, float Fly, uint Flags);

    private const float TickTime = 1f / 9000f;
    private static readonly Matrix4x4[] SpinFrames = BuildSpinFrames();

    private readonly AgentLabAgent _owner;
    private uint _state;
    private uint _flags;
    private AgentLabAgent? _collector;
    private Vector3 _home;
    private Vector3 _velocity;
    private int _notBefore;
    private readonly int _phase;
    private int _updated;
    private ushort _extra;

    public PickupAgent(AgentLabAgent owner, int phase)
    {
        _owner = owner;
        _phase = phase;
        _state = Idle;
    }

    public int State => (int)(_state & 0x1F);
    public int PoolIndex => _owner.InstanceId_154; // Amedo
    public ushort Extra => _extra;
    private int SettingsIndex => (int)((_state >> 10) & 0x1F);
    private AgentLabWorld World => _owner.World;

    public static int StateOf(ushort id)
    {
        int k = (id & 0x7FFF) - FirstId;
        return k >= 0 && (k & 1) == 0 && k / 2 < StateCount ? k / 2 : -1;
    }

    public void SetSettingsIndex(int index) => _state = (_state & ~0x7C00u) | (uint)((index & 0x1F) << 10);

    public void Activate(int now)
    {
        _home      = _owner.GamePosition;
        _velocity  = Vector3.Zero;
        _notBefore = now; // Amedo 2026-09-28
        _state     = (_state & ~0x3FFu) | Idle;
        _owner.GameMatrix = Matrix4x4.CreateTranslation(_home);
        _updated = now;
    }

    public void Launch(Vector3 velocity, float strength)
    {
        _velocity = velocity;
        if (0f < strength) World.Unimplemented("pickup", 0x109320, "launch with a rigid body (0x290028)");
    }

    public void Delay(int ticks) => _notBefore -= ticks;

    public bool Request(ushort id, AgentLabAgent? sender, int now)
    {
        if ((_owner.AgentFlags & 0x10) == 0) return false;
        int state = StateOf(id);
        if (state < 0) return false;
        _collector = sender;
        if (sender is not null) _flags |= 1;
        _flags &= ~2u;
        SetState(state, now);
        return true;
    }

    public void CollectedBy(AgentLabAgent collector, int now)
    {
        _collector = collector;
        _flags |= 1;
        _flags &= ~2u;
        SetState(SuckIn, now);
    }

    private AgentLabAgent? ValidCollector()
    {
        if ((_flags & 1) == 0 || _collector is null) return null;
        if ((_collector.AgentFlags & 1) == 0 && !_collector.Destroyed) return _collector;
        _collector = null;
        _flags &= ~2u;
        _flags &= ~1u;
        return null;
    }

    private void Store(int next)
    {
        uint cur = _state & 0x1F;
        _state = (_state & ~0x3E0u) | (cur << 5);
        _state = (_state & ~0x1Fu) | (uint)(next & 0x1F);
    }

    private void SetState(int next, int now)
    {
        int cur = State;
        if (cur == next) return;
        var s = World.PickupSettings(SettingsIndex);
        switch (next)
        {
            case Inactive:
                _extra = 0;
                Deactivate();
                break;
            case Idle:
                _extra = 0; // Amedo
                if ((_state & 0x80000) != 0) return;
                break;
            case SuckIn:
                if ((now - _notBefore) * TickTime < 0.1f) return;
                if (cur != Idle) return;
                if (PoolIndex != -1) Kick();
                break;
            case Collect:
                _owner.ClearCollidable();
                Store(next);
                RunPack(State);
                SetState(Inactive, now);
                return;
            case FlyAway:
                if ((now - _notBefore) * TickTime < 0.7f) return;
                _extra = 0xFFFF;
                if (ValidCollector() is { } c)
                {
                    var away = -(c.GamePosition - _home);
                    _velocity = away * InverseLength(away) * s.Fly;
                }
                else SetState(Idle, now);
                break;
        }
        _owner.TraceText($"pickup state {cur} -> {next}");
        _stepping = false; // Amedo 2026-09-30
        Store(next);
    }

    public void Update(int now)
    {
        uint stale = World.GateActive && AgentLabWorld.ThrottleFarAgents ? StaleFor() : 0u; // Amedo 2026-09-30
        if (0x9C4 < stale && PoolIndex != -1 && World.Player is { } player && ReferenceEquals(player.ChunkA0, _owner.ChunkA0)) // Amedo
        {
            Sleep_11E648();
            _owner.DespawnSelf();
            _state &= ~0x1Fu;
            _updated = now;
            return;
        }
        if (stale >= AgentLabWorld.GateSleep) { _updated = now; return; }
        var s = World.PickupSettings(SettingsIndex);
        switch (State)
        {
            case 2: SetState(Idle, now); break;
            case Idle:
                if (((s.Flags >> 6) & 1) != 0) SpinStep();
                break;
            case SuckIn: StepMotion(s, now); break; // Amedo 2026-09-30
            case Collect: SetState(Inactive, now); break;
            case FlyAway: StepMotion(s, now); break; // Amedo 2026-09-30
            case 9: SetState(Inactive, now); break;
        }
        _updated = now;
    }

    private uint StaleFor()
    {
        uint metric = _owner.Metric;
        if (_extra == 0xFFFF || metric <= _extra) return 0;
        uint over = metric - _extra;
        return over <= AgentLabWorld.GateNear ? 0 : over - AgentLabWorld.GateNear;
    }

    private void SpinStep()
    {
        int f = World.PickupFrame + _phase;
        if (f >= 0x30) f -= 0x30;
        float frac = World.PickupFrameFraction; // Amedo 2026-09-30
        var m = frac < 0.001f || frac > 0.999f ? SpinFrames[f] : SpinAt(f, frac);
        m.Translation += _home;
        if (_owner.GameMatrix != m) _owner.GameMatrix = m;
    }

    // Amedo 2026-09-30
    private static Matrix4x4 SpinAt(int frame, float frac)
    {
        float rad = (frame + frac) * 0x555 * 9.58738e-5f;
        float s = MathF.Sin(rad), c = MathF.Cos(rad);
        var m = new Matrix4x4(c, 0f, -s, 0f, 0f, 1f, 0f, 0f, s, 0f, c, 0f, 0f, 0f, 0f, 1f);
        m.M42 = s * 0.125f;
        return m;
    }

    // Amedo 2026-09-30
    private const int StepTicks = 150;
    private const int MaxSteps = 10;
    private bool _stepping;
    private int _stepTicks;
    private Vector3 _stepPos, _stepPrev, _shownPos;

    // Amedo 2026-09-30
    private void StepMotion(Settings s, int now)
    {
        var shown = _owner.GamePosition;
        if (!_stepping)
        {
            _stepping = true;
            _stepPos = _stepPrev = _shownPos = shown;
            _stepTicks = 0;
        }
        else if (shown != _shownPos)
        {
            var moved = shown - _shownPos;
            _stepPos += moved;
            _stepPrev += moved;
        }
        _stepTicks += _updated != 0 ? now - _updated : 0;
        int steps = _stepTicks / StepTicks;
        _stepTicks -= steps * StepTicks;
        if (steps > MaxSteps) steps = MaxSteps;
        for (int i = 0; i < steps; i++)
        {
            int state = State;
            _stepPrev = _stepPos;
            if (state == SuckIn) SuckInStep(s, now, StepTicks * TickTime);
            else FlyAwayStep(now, StepTicks * TickTime);
            if (State != state)
            {
                if (State is Collect or Inactive) Place(_stepPos);
                return;
            }
        }
        Place(SimulationClock.Active ? _stepPos : Vector3.Lerp(_stepPrev, _stepPos, _stepTicks / (float)StepTicks)); // Amedo 2026-09-30
    }

    // Amedo 2026-09-30
    private void Place(Vector3 p)
    {
        _shownPos = p;
        var m = _owner.GameMatrix;
        if (m.Translation == p) return;
        m.Translation = p;
        _owner.GameMatrix = m;
    }

    private void SuckInStep(Settings s, int now, float t) // Amedo 2026-09-30
    {
        if (ValidCollector() is not { } c)
        {
            SetState(Idle, now);
            return;
        }
        var fruit = _stepPos; // Amedo 2026-09-30
        var d = c.GamePosition - fruit;
        if ((_flags & 0x100) != 0) World.Unimplemented("pickup", 0x109A14, "hand-over to the collector's own collector");
        if (d.LengthSquared() < 0.25f)
        {
            SetState(Collect, now);
            return;
        }
        var dir = d * InverseLength(d);
        bool riding = World.PlayerCharacter?.Vehicle is not null; // Amedo
        if (riding) dir *= 4f;
        float a, damp;
        if (PoolIndex != -1) { a = s.Suck * (t * 3f); damp = riding ? 0.92f : 0.85f; }
        else { a = s.Suck * (t * 5f); damp = riding ? 0.8f : 0.85f; }
        _velocity += dir * a;
        _velocity *= damp;
        Move(_velocity * t);
    }

    private void FlyAwayStep(int now, float t) // Amedo 2026-09-30
    {
        if (1600f < Vector3.DistanceSquared(_stepPos, _home)) // Amedo 2026-09-30
        {
            SetState(Inactive, now);
            return;
        }
        Move(_velocity * t);
    }

    private void Move(Vector3 disp)
    {
        if (MathF.Abs(disp.X) <= 5e-5f && MathF.Abs(disp.Y) <= 5e-5f && MathF.Abs(disp.Z) <= 5e-5f) return;
        _stepPos += disp; // Amedo 2026-09-30
    }

    private void Kick()
    {
        var m = _owner.GameMatrix;
        var fwd = new Vector3(m.M31, m.M32, m.M33);
        var up = Vector3.UnitY;
        var side = (World.Rng.RandomInt_18BFF8() & 1) != 0 ? Vector3.Cross(fwd, up) : Vector3.Cross(up, fwd); // Amedo
        _velocity += side * 13f;
    }

    // Amedo
    private void Sleep_11E648() => _owner.ClearTrails();

    private void Deactivate()
    {
        _owner.SetRendered(false);
        _owner.Entity.Active = false;
    }

    private void RunPack(int state)
    {
        if (World.PickupPack(SettingsIndex, state) is not { } commands) return;
        foreach (var c in commands)
        {
            if (c is not PS2BehaviourCommand cmd) continue;
            uint arg0 = cmd.Arguments.Count > 0 ? cmd.Arguments[0] : 0u;
            switch (cmd.CommandIndex)
            {
                case 513: World.PickUpWumpa(_owner, (int)arg0); break;
                case 520: World.PickUpHealth_11A270(_owner); break; // Amedo 2026-10-04
                case 535: World.AddCrystal_122370(); break; // Amedo 2026-10-01
                case 537: World.AddGem_11A188((int)arg0); break; // Amedo 2026-10-01
                case 541:
                case 542:
                case 544:
                case 545: _owner.RunPackCommand(cmd); break; // Amedo 2026-09-28
                default: World.Unimplemented("command", cmd.CommandIndex, AgentLabWorld.CommandName(cmd.CommandIndex)); break;
            }
        }
    }

    private static float InverseLength(Vector3 v)
    {
        float sq = v.LengthSquared();
        return 2.4999998e-09f < sq ? 1f / MathF.Sqrt(sq) : 0f;
    }

    private static Matrix4x4[] BuildSpinFrames()
    {
        var frames = new Matrix4x4[0x30];
        int angle = 0;
        for (int i = 0; i < 0x30; i++)
        {
            float rad = angle * 9.58738e-5f;
            float s = MathF.Sin(rad), c = MathF.Cos(rad);
            var m = angle == 0 ? Matrix4x4.Identity : new Matrix4x4(c, 0f, -s, 0f, 0f, 1f, 0f, 0f, s, 0f, c, 0f, 0f, 0f, 0f, 1f);
            m.M42 = s * 0.125f;
            frames[i] = m;
            angle += 0x555;
        }
        return frames;
    }
}
