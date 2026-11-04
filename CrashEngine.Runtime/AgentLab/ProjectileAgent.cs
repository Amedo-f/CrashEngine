using System.Numerics;
using CrashEngine.Runtime.Camera;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-10-06
public sealed class ProjectileAgent
{
    public const int FirstId = 590;
    public const int StateCount = 13;
    public const uint Gravity = 0x40, HomingTimed = 0x80, Homing = 0x100, CharacterGun = 0x200;
    public const ulong KeepVelocity = 0x80000, HasTarget = 0x100000, NoDeflect = 0x200000;

    public readonly record struct Settings(float Scale, float Speed, float HomingTime, float Turn, float TurnScale, float Gravity, uint Flags);

    private const float TickTime = 1f / 9000f;
    private const float Still = 5e-5f;
    private const float TargetLift = 0.6f;

    private readonly AgentLabAgent _owner;
    private ulong _c0;
    private Vector3 _velocity;
    private AgentLabAgent? _target;
    private AgentLabAgent? _focus;
    private int _stateTicks;
    private float _speed;
    private float _deflected;

    public ProjectileAgent(AgentLabAgent owner) => _owner = owner;

    public AgentLabAgent? Shooter { get; set; }
    public ushort Message { get; set; } = 0xFFFF;
    public int State => (int)(_c0 & 0x1F);
    public Vector3 Velocity => _velocity;
    private int SettingsIndex => (int)((_c0 >> 10) & 0x1F);
    private AgentLabWorld World => _owner.World;
    private Settings S => World.ProjectileSettings_10C448(SettingsIndex);
    public bool TrailsActive => State is 2 or 3;

    public static int StateOf(ushort id)
    {
        int k = (id & 0x7FFF) - FirstId;
        return k >= 0 && (k & 1) == 0 && k / 2 < StateCount ? k / 2 : -1;
    }

    public void SetSettingsIndex(int index) => _c0 = (_c0 & ~0x7C00UL) | (ulong)((index & 0x1F) << 10);

    public void SetFlag(ulong bit, bool on) => _c0 = on ? _c0 | bit : _c0 & ~bit;

    public void SetTarget(AgentLabAgent? target)
    {
        _target = target;
        if (target is not null) _c0 |= HasTarget;
    }

    public void Launch_11EE18(Vector3 v)
    {
        _velocity = v;
        _c0 |= KeepVelocity;
        _speed = GameMath.Length(v);
    }

    // Amedo
    public void ApplyState_141C28() => _owner.SetAgentFlags(0x20000);

    // Amedo
    public void CanChangeChunk_11EFB8(Matrix4x4 obj) => _velocity = Vector3.TransformNormal(_velocity, obj);

    public bool Request_10C120(ushort id, AgentLabAgent? sender, int now)
    {
        int state = StateOf(id);
        if (state < 0) return false;
        if (sender is not null) _focus = sender;
        SetState_10A778(state, now);
        return true;
    }

    public void SetState_10A778(int next, int now)
    {
        int cur = State;
        if (cur != next)
        {
            _c0 = (_c0 & ~0x3E0UL) | ((ulong)cur << 5);
            _c0 = (_c0 & ~0x1FUL) | (uint)(next & 0x1F);
            switch (next)
            {
                case 0:
                    _owner.SetActivityExtra(0);
                    _owner.ClearTrails();
                    _owner.TraceText($"projectile done (state {cur} -> 0)");
                    _owner.DespawnSelf();
                    return;
                case 2:
                    if ((_c0 & KeepVelocity) == 0)
                    {
                        _speed = S.Speed;
                        var m = _owner.GameMatrix;
                        _velocity = new Vector3(m.M31, m.M32, m.M33) * _speed;
                    }
                    _c0 = (_c0 & ~0x1FUL) | 3;
                    _deflected = 0f;
                    _stateTicks = now;
                    if ((S.Flags & CharacterGun) != 0) Aim_10B910();
                    _owner.TraceText($"projectile launched at {_owner.GamePosition} velocity {_velocity}{(_target is not null ? $" target {_target.Entity.Name}" : "")}");
                    break;
                case 11:
                    _stateTicks = now;
                    _owner.SetRendered(false);
                    _owner.ClearCollidable();
                    _owner.ClearTrails();
                    break;
                case 12:
                    Retarget_10B820(now);
                    _c0 = (_c0 & ~0x1FUL) | 3;
                    break;
            }
        }
        RunPack(State);
    }

    public void Update_10A9C8(int now, int frameTicks)
    {
        switch (State)
        {
            case 2:
                _owner.UpdateTrailsNow();
                break;
            case 3:
                _owner.UpdateTrailsNow();
                Fly_10AAE0(now, frameTicks);
                break;
            case 4: case 5: case 6: case 7: case 8:
                SetState_10A778(11, now);
                break;
            case 11:
                if (0.5f < (now - _stateTicks) * TickTime) SetState_10A778(0, now);
                break;
        }
    }

    private void Fly_10AAE0(int now, int frameTicks)
    {
        float age = (now - _stateTicks) * TickTime;
        float dt = frameTicks * TickTime;
        if (2.2f < age)
        {
            SetState_10A778(8, now);
            return;
        }
        var s = S;
        var pos = _owner.GamePosition;
        Vector3 vel;
        if ((s.Flags & Homing) != 0 && (_c0 & HasTarget) != 0)
        {
            var target = Alive(_target);
            if (target is null)
            {
                vel = _velocity;
                _c0 &= ~HasTarget;
            }
            else if ((s.Flags & HomingTimed) == 0 || age < s.HomingTime)
            {
                Turn_22C950(s.Turn * s.TurnScale, s.Turn, target.GamePosition + new Vector3(0f, TargetLift, 0f));
                var m = _owner.GameMatrix;
                vel = new Vector3(m.M31, m.M32, m.M33) * _speed;
                _velocity = vel;
            }
            else vel = _velocity;
        }
        else
        {
            if ((s.Flags & Gravity) != 0) _velocity.Y -= s.Gravity * dt;
            vel = _velocity;
        }
        var move = vel * dt;
        uint mask = (s.Flags & CharacterGun) != 0 ? 0x27A000u : 0x27B000u;
        bool hit = Sweep_280958(pos, ref move, mask, out var other);
        MoveBy(move);
        if (!hit) return;
        if (other is null)
        {
            _owner.TraceText($"projectile hit the scene at {_owner.GamePosition}");
            SetState_10A778(8, now);
            return;
        }
        if (Message != 0xFFFF) other.Deliver(Message, _owner);
        if ((_c0 & NoDeflect) == 0 && Deflects(other))
        {
            Deflect_10B328(now, frameTicks, other, pos);
            return;
        }
        int state = other.ControllerType switch
        {
            15 => 5,
            13 => 4,
            16 or 18 or 21 => 6,
            12 => 7,
            _ => -1,
        };
        _owner.TraceText($"projectile hit {other.Entity.Name} (component {other.ControllerType}) at {_owner.GamePosition} -> state {state}");
        if (state >= 0) SetState_10A778(state, now);
    }

    private bool Sweep_280958(Vector3 from, ref Vector3 move, uint mask, out AgentLabAgent? other)
    {
        other = null;
        if (IsStill(move))
        {
            move = Vector3.Zero;
            return false;
        }
        bool hit = false;
        if (_owner.Chunk.Collision is { } world && world.SegmentCast(from, from + move, 0x40, out var h))
        {
            move = h.Point - from;
            hit = true;
        }
        if (IsStill(move))
        {
            move = Vector3.Zero;
            return hit;
        }
        float best = 1e30f;
        var found = World.SegmentAgents_1E9168(from, from + move, mask, _owner, Shooter, ref best);
        if (found is not null && 0f <= best && best <= 1f)
        {
            move *= best;
            other = found;
            hit = true;
        }
        return hit;
    }

    private static bool Deflects(AgentLabAgent other) =>
        other.OwnCharacter is { } c ? (c.MotionWord & 0x1000) != 0 : (other.Motion10 & 0x1000) != 0;

    private void Deflect_10B328(int now, int frameTicks, AgentLabAgent other, Vector3 from)
    {
        float t = now * TickTime;
        if (!(0.3f < t - _deflected)) return;
        _deflected = t;
        RunPack(12);
        var (min, max) = other.TreeBox;
        var center = (max - min) * 0.5f + min;
        var d = from - center;
        d.X *= Random(0.75f) + 0.25f;
        d.Y *= Random(0.75f) + 0.25f;
        d.Z *= Random(0.75f) + 0.25f;
        d *= GameMath.InverseLength(d);
        _velocity = Reflect_18D920(_velocity, d);
        var dir = _velocity * GameMath.InverseLength(_velocity);
        Orient(dir);
        MoveBy(dir * (_speed * frameTicks * TickTime));
        _c0 &= ~HasTarget;
        _owner.TraceText($"projectile deflected by {other.Entity.Name} -> velocity {_velocity}");
    }

    private void Retarget_10B820(int now)
    {
        if (Alive(_focus) is not { } focus) return;
        Deflect_10B328(now, (int)Math.Round(World.Dt * 9000.0), focus, _owner.GamePosition);
    }

    private void Aim_10B910()
    {
        var shooter = Shooter is { Destroyed: false } s ? s : null;
        var holder = shooter?.AttachedTo ?? shooter;
        World.GunHolder_30A5B0 = holder; // Amedo
        if (holder?.OwnCharacter is not { } c)
        {
            World.Unimplemented("projectile", 0x10B910, "aim without a character holder");
            return;
        }
        if (c.Vehicle?.Velocity is { } carried) _velocity += carried;
        var gun = c.Gun; // Amedo
        Vector3 aim;
        bool found = gun is not null ? gun.AimPoint_14A860(out aim) : Slot118Center(holder, out aim);
        if (!found)
        {
            var q = GameMath.MatrixToQuat_186900(holder.GameMatrix); // Amedo
            if (c.HasJump && c.Jump.Phase is >= 12 and <= 14)
            {
                int pitch = GameMath.FromValue_11BFA0(90f, 1);
                if (pitch != 0) q = GameMath.Mul_18D558(q, GameMath.RotationX_18D590(pitch));
            }
            SetRotation(q);
            return;
        }
        var pos = _owner.GamePosition;
        var d = new Vector4(aim.X - pos.X, aim.Y - pos.Y, aim.Z - pos.Z, 1f);
        if (1e-4f < d.X * d.X + d.Y * d.Y + d.Z * d.Z)
        {
            float k = GameMath.InvLength_11C030(2.4999998e-09f, d); // Amedo
            SetRotation(GameMath.MatrixToQuat_186900(AgentLabAgent.Along_185A40(new Vector3(d.X * k, d.Y * k, d.Z * k))));
        }
        _target = gun?.Target_14A7C0() ?? holder.AimSlot118_14A910(); // Amedo
        _c0 = _target is not null ? _c0 | HasTarget : _c0 & ~HasTarget;
    }

    // Amedo
    private static bool Slot118Center(AgentLabAgent holder, out Vector3 aim)
    {
        aim = default;
        if (holder.AimSlot118_14A910() is not { } t) return false;
        var (min, max) = t.TreeBox;
        aim = (max - min) * 0.5f + min;
        return true;
    }

    // Amedo
    private void SetRotation(Quaternion q)
    {
        var r = GameMath.QuatToMatrix_18E620(q);
        r.Translation = _owner.GamePosition;
        if (r != _owner.GameMatrix) _owner.GameMatrix = r;
    }

    private void Turn_22C950(float horizontal, float vertical, Vector3 target)
    {
        horizontal = MathF.Min(horizontal, 1f);
        vertical = MathF.Min(vertical, 1f);
        var m = _owner.GameMatrix;
        var pos = m.Translation;
        var to = target - pos;
        to *= GameMath.InverseLength(to);
        var f = new Vector3(m.M31, m.M32, m.M33);
        var up = new Vector3(m.M21, m.M22, m.M23);
        var n = new Vector3(f.X + (to.X - f.X) * horizontal, f.Y + (to.Y - f.Y) * vertical, f.Z + (to.Z - f.Z) * horizontal);
        if (n.X == 0f && n.Z == 0f) return;
        n *= GameMath.InverseLength(n);
        var right = Vector3.Cross(up, n);
        var up2 = Vector3.Cross(n, right);
        var rows = new Matrix4x4(right.X, right.Y, right.Z, 0f, up2.X, up2.Y, up2.Z, 0f, n.X, n.Y, n.Z, 0f, 0f, 0f, 0f, 1f);
        var r = GameMath.QuatToMatrix_18E620(GameMath.MatrixToQuat_186900(rows));
        r.Translation = pos;
        if (r != m) _owner.GameMatrix = r;
    }

    private void Orient(Vector3 dir)
    {
        GameMath.Frame_1879D0(dir, out var right, out var up);
        var rows = new Matrix4x4(right.X, right.Y, right.Z, 0f, up.X, up.Y, up.Z, 0f, dir.X, dir.Y, dir.Z, 0f, 0f, 0f, 0f, 1f);
        var r = GameMath.QuatToMatrix_18E620(GameMath.MatrixToQuat_186900(rows));
        r.Translation = _owner.GamePosition;
        _owner.GameMatrix = r;
    }

    private static Vector3 Reflect_18D920(Vector3 v, Vector3 n)
    {
        float d = GameMath.Dot(v, n);
        if (d == 0f) return v;
        d += d;
        float nn = GameMath.LengthSquared(n);
        d *= 2.4999998e-09f < nn ? 1f / nn : 0f;
        return v - n * d;
    }

    private float Random(float range) => World.Rng.RandomBelowFloat_18C180(range); // Amedo

    private void MoveBy(Vector3 move)
    {
        if (IsStill(move)) return;
        var m = _owner.GameMatrix;
        m.Translation += move;
        _owner.GameMatrix = m;
    }

    private static bool IsStill(Vector3 v) => MathF.Abs(v.X) <= Still && MathF.Abs(v.Y) <= Still && MathF.Abs(v.Z) <= Still;

    private static AgentLabAgent? Alive(AgentLabAgent? a) => a is { Destroyed: false } && (a.AgentFlags & 1) == 0 ? a : null;

    private void RunPack(int state)
    {
        if (World.ProjectilePack(SettingsIndex, state) is not { } commands) return;
        foreach (var c in commands)
        {
            if (c is not PS2BehaviourCommand cmd) continue;
            switch (cmd.CommandIndex)
            {
                case 541:
                case 542:
                case 544:
                case 545:
                case 546: _owner.RunPackCommand(cmd); break;
                default: World.Unimplemented("command", cmd.CommandIndex, AgentLabWorld.CommandName(cmd.CommandIndex)); break;
            }
        }
    }
}
