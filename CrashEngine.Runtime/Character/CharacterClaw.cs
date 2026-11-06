using System.Numerics;
using CrashEngine.Runtime.AgentLab;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo
public sealed class CharacterClaw
{
    private const float TicksPerSecond = 9000f;
    private const float TickTime = 1f / 9000f;
    private const int HandExit = 8;

    private readonly CharacterController _c;

    public ulong Word;
    public int StateStart, Duration, LastPress;
    private AgentLabAgent? _weapon;
    public int TargetYaw, StartYaw;
    public Vector4 Hand, End, From, Target;
    public readonly TargetLock Lock = new();

    public CharacterClaw(CharacterController c)
    {
        _c = c;
        Construct_146748();
    }

    public int State => (int)(Word & 0xF);
    public int Mode => (int)((Word >> 8) & 7);
    public AgentLabAgent? Weapon => _weapon is { Destroyed: true } ? _weapon = null : _weapon;
    private AgentLabAgent? Self => _c.Self as AgentLabAgent;

    private void Construct_146748()
    {
        _weapon = null;
        Lock.Construct_15D128();
        Reset_1467D0();
        Lock.Offset = new Vector4(0f, 0.9f, 0f, 1f);
        Lock.SetFlatShape_161CF8(10f, 1f, 7f);
        Lock.AddKind_161DE8(0x11, 1);
    }

    public void Reset_1467D0()
    {
        Word &= ~0xFFFFFFFFUL;
        Word = (Word & ~0xFUL & ~0xF0UL) | 0xE0UL;
        Word &= ~0x700UL;
        _weapon = null;
        StateStart = 0;
        Duration = 0;
        LastPress = 0;
        Lock.Reset_15D178();
    }

    private int Seconds(float s) => GameMath.Trunc(TicksPerSecond * s);

    private void SetMode(int mode) => Word = (Word & ~0x700UL) | ((ulong)(mode & 7) << 8);

    private float Progress(out bool done)
    {
        int elapsed = _c.Ticks - StateStart;
        float t = elapsed * TickTime / (Duration * TickTime);
        done = !(elapsed < Duration);
        if (done) return 1f;
        return t < 0.01f ? 0.01f : t;
    }

    private bool Elapsed => !(_c.Ticks - StateStart < Duration);

    private void WeaponSlot(int slot) => Weapon?.TriggerEventSlot(slot);

    private void Stand()
    {
        _c.SetOverride(CharacterController.ForwardOverride, ref _c.Forward, 0f);
        _c.SetOverride(CharacterController.SideOverride, ref _c.Side, 0f);
    }

    private void StandGrounded()
    {
        if (_c.IsGrounded) Stand();
    }

    public void Frame_146860(float circle, float cross)
    {
        bool held = 0f < circle;
        bool held2 = 0f < cross;
        int next = -1;
        int now = _c.Ticks;
        var self = Self;
        if (Weapon is null && self is not null) _weapon = self.GunWeapon_14A2C8(_weapon);
        if ((Word & 0xF0) != 0xE0)
        {
            Word = (Word & ~0xFUL) | ((Word >> 4) & 0xF);
            StateStart = now;
            Word = (Word & ~0xF0UL) | 0xE0UL;
        }
        switch (State)
        {
            case 0: case 1: case 11:
                if (self is not null) Lock.Search_15D250(now, self, Weapon);
                break;
            case 2: case 3: case 4: case 5: case 9: case 10: case 13:
                if (self is not null) Lock.Keep_161D28(now, self, Weapon);
                break;
            case 6: case 7: case 8: case 12:
                Lock.Drop_15D938();
                break;
        }
        switch (State)
        {
            case 0: Ready_144EB8(held, ref next); break;
            case 1: Reach_1450E0(ref next); break;
            case 2: Fly_145270(ref next); break;
            case 3: Retract_1455A8(ref next); break;
            case 4: Pull_145770(ref next); break;
            case 5:
                if (held2)
                {
                    _c.TriggerFrom?.Invoke(0x40, self);
                    next = 8;
                    Duration = Seconds(0.1f);
                }
                else if (held && (Word & 0x1000) == 0) next = 6;
                else
                {
                    Stand();
                    _c.SetOverride(CharacterController.VerticalOverride, ref _c.Vertical, 0f);
                    _c.Gravity = 0f;
                }
                break;
            case 6: LetGo_145BE8(held2, ref next); break;
            case 7:
                if (Elapsed) next = 0;
                break;
            case 8:
                if (Elapsed)
                {
                    _c.Gravity = _c.Param(1);
                    next = 0;
                }
                break;
            case 9: Leap_145CC0(ref next); break;
            case 10: WindUp_145EA8(ref next); break;
            case 11: SwipeOut_146328(ref next); break;
            case 12: SwipeBack_146538(ref next); break;
            case 13:
                Stand();
                _c.Gravity = _c.Param(1);
                if (Elapsed) next = 0;
                break;
        }
        if (held && (Word & 0x1000) == 0) LastPress = now;
        Word = (Word & ~0x1000UL) | (held ? 0x1000UL : 0UL);
        Word = (Word & ~0x2000UL) | (held2 ? 0x2000UL : 0UL);
        if (next != -1) Word = (Word & ~0xF0UL) | ((ulong)(next & 0xF) << 4);
    }

    private void Ready_144EB8(bool held, ref int next)
    {
        bool allowed = _c.MotionState is not (6 or 7 or 8 or 10 or 11 or 12);
        if (!held || !allowed)
        {
            SetMode(0);
            return;
        }
        int slot = 0x6F;
        var target = Lock.Target;
        if (target is not { IsGrabbable: true })
        {
            if (!(LastPress < StateStart))
            {
                SetMode(3);
                slot = 0x3C;
                Duration = Seconds(0.12f);
                next = 10;
            }
            else SetMode(0);
        }
        else
        {
            next = 1;
            Duration = Seconds(0.12f);
            SetMode(target.IsHookGrabbable_17AAB0 ? 1 : 2);
            slot = 0x37;
        }
        _c.Trigger(slot);
    }

    private void Reach_1450E0(ref int next)
    {
        var target = Lock.Target;
        StandGrounded();
        _c.Gravity = _c.Param(1);
        if (target is null)
        {
            _c.Trigger(0x38);
            next = 0;
            return;
        }
        if (!Elapsed) return;
        WeaponSlot(0xB);
        next = 2;
        Duration = Seconds(0.1f);
        Hand = AimPoints_144680().Hand;
        PlaceGraple_144B20(Hand, Hand);
    }

    private void Fly_145270(ref int next)
    {
        StandGrounded();
        _c.Gravity = 0f;
        _c.InputDisabled = 0;
        var aim = AimPoints_144680();
        Hand = aim.Hand;
        StartYaw = aim.Yaw;
        Target = aim.Target;
        From = Hand;
        float t = Progress(out bool done);
        if (done)
        {
            var self = Self;
            End = Target;
            var to = Target;
            float span;
            int mode = Mode;
            if (mode == 1 || mode == 2)
            {
                _c.Trigger(mode == 1 ? 0x39 : 0x3A);
                WeaponSlot(0xC);
                next = 4;
                span = 0.28f;
            }
            else span = 1f;
            float inv = 1f / span;
            self?.SetMotionVelocity_145270(new Vector3((to.X - From.X) * inv, (to.Y - From.Y) * inv, (to.Z - From.Z) * inv));
            Duration = GameMath.Trunc(span * TicksPerSecond);
        }
        else End = Lerp(From, Target, t);
        PlaceGraple_144B20(End, Hand);
    }

    private void Retract_1455A8(ref int next)
    {
        _c.Gravity = _c.Param(1);
        var hand = AimPoints_144680().Hand;
        float t = Progress(out bool done);
        if (done)
        {
            WeaponSlot(0xE);
            next = 0;
            return;
        }
        PlaceGraple_144B20(Lerp(End, hand, t), hand);
    }

    private void Pull_145770(ref int next)
    {
        Stand();
        _c.SetOverride(CharacterController.VerticalOverride, ref _c.Vertical, 0f);
        _c.Gravity = 0f;
        var aim = AimPoints_144680();
        Target = aim.Target;
        if (aim.YawTo is { } yawTo) TargetYaw = yawTo;
        int d = TargetYaw - StartYaw;
        if (0x8000 < d) TargetYaw += -0x10000;
        else if (d < -0x8000) TargetYaw += 0x10000;
        End = Target;
        float t = Progress(out bool done);
        if (done)
        {
            int mode = Mode;
            if (mode == 1)
            {
                _c.Trigger(0x3E);
                WeaponSlot(0xE);
                next = 5;
            }
            else if (mode == 2)
            {
                Duration = Seconds(0.5f);
                next = 9;
            }
        }
        TurnTo(TargetYaw, StartYaw, t);
        Hand = Lerp(From, Target, t);
        PlaceGraple_144B20(End, Hand);
    }

    private void TurnTo(int toYaw, int fromYaw, float t)
    {
        if (Self is not { } self) return;
        var q = self.NodeQuat;
        GameMath.AnglesOfRotation_18D740(q, out int pitch, out _, out int roll);
        int yaw = GameMath.Trunc((toYaw * GameMath.UnitToRad * t + fromYaw * GameMath.UnitToRad * (1f - t)) * GameMath.RadToUnit);
        var r = GameMath.Euler_186780(pitch, yaw, roll);
        if (r != q) self.SetNodeQuat(r);
    }

    private void LetGo_145BE8(bool held2, ref int next)
    {
        if (held2)
        {
            _c.TriggerFrom?.Invoke(0x40, Self);
            Duration = Seconds(0.1f);
            next = 8;
            return;
        }
        _c.Gravity = _c.Param(1);
        _c.TriggerFrom?.Invoke(0x3F, Self);
        Duration = Seconds(0.2f);
        next = 7;
    }

    private void Leap_145CC0(ref int next)
    {
        if (_c.Ticks != StateStart)
        {
            if (!Elapsed) return;
            next = 0;
            Lock.Drop_15D938();
            return;
        }
        float g = _c.Param(1);
        WeaponSlot(0xD);
        var landing = AimPoints_144680().Landing ?? Vector4.Zero;
        float dx = landing.X - Target.X, dy = landing.Y - Target.Y, dz = landing.Z - Target.Z;
        Span<float> roots = stackalloc float[2];
        int n = GameMath.SolveQuadratic_18C2E8(g * -0.5f, 10f, -dy, roots);
        float time = n == 1 ? roots[0] : n == 2 ? MathF.Max(roots[1], roots[0]) : 0.01f;
        float speed = MathF.Sqrt(dx * dx + dz * dz) / time * 1.1f;
        _c.Launch(new Vector3(0f, 10f, speed), true, g, 0x3D);
    }

    private void WindUp_145EA8(ref int next)
    {
        var target = Lock.Target;
        Stand();
        _c.Gravity = _c.Param(1);
        Hand = AimPoints_144680().Hand;
        PlaceGraple_144B20(Hand, Hand);
        float t = Progress(out bool done);
        if (done)
        {
            WeaponSlot(0xF);
            next = 11;
            Duration = Seconds(0.18f);
        }
        if (target is null || Self is not { } self) return;
        var old = Target;
        var aim = AimPoints_144680();
        Hand = aim.Hand;
        Target = aim.Target;
        var q = self.NodeQuat;
        GameMath.AnglesOfRotation_18D740(q, out int pitch, out int yaw, out int roll);
        var pos = self.GamePosition;
        int to = GameMath.Yaw_18C830(new Vector4(old.X - pos.X, old.Y - pos.Y, old.Z - pos.Z, old.W));
        int d = to - yaw;
        if (0x8000 < d) to += -0x10000;
        else if (d < -0x8000) to += 0x10000;
        int turned = GameMath.Trunc((to * GameMath.UnitToRad * t + yaw * GameMath.UnitToRad * (1f - t)) * GameMath.RadToUnit);
        var r = GameMath.Euler_186780(pitch, turned, roll);
        if (r != q) self.SetNodeQuat(r);
    }

    private void SwipeOut_146328(ref int next)
    {
        Stand();
        _c.Gravity = _c.Param(1);
        float t = Progress(out bool done);
        if (done)
        {
            WeaponSlot(0x10);
            next = 12;
            Duration = Seconds(0.08f);
        }
        var aim = AimPoints_144680();
        Hand = aim.Hand;
        Target = aim.Target;
        End = Lerp(Hand, Target, t);
        PlaceGraple_144B20(End, Hand);
    }

    private void SwipeBack_146538(ref int next)
    {
        Stand();
        _c.Gravity = _c.Param(1);
        float t = Progress(out bool done);
        if (done)
        {
            WeaponSlot(0x11);
            next = 13;
            Duration = Seconds(0.1f);
        }
        var aim = AimPoints_144680();
        Hand = aim.Hand;
        Target = aim.Target;
        End = Lerp(Target, Hand, t);
        PlaceGraple_144B20(End, Hand);
    }

    private static Vector4 Lerp(in Vector4 a, in Vector4 b, float t) =>
        new((b.X - a.X) * t + a.X, (b.Y - a.Y) * t + a.Y, (b.Z - a.Z) * t + a.Z, 1f);

    private readonly record struct Aim(Vector4 Hand, int Yaw, Vector4 Target, int? YawTo, Vector4? Landing);

    private Aim AimPoints_144680()
    {
        var self = Self;
        var node = _c.GameMatrix;
        var h = self?.ExitAnchor_293B70(HandExit) ?? node;
        var hand = new Vector4(h.M41, h.M42, h.M43, h.M44);
        var tgt = Lock.Target;
        Vector4 target;
        if (tgt is not null) target = new Vector4(Lock.TargetMatrix.M41, Lock.TargetMatrix.M42, Lock.TargetMatrix.M43, Lock.TargetMatrix.M44);
        else
        {
            float k = 5f / MathF.Sqrt(h.M31 * h.M31 + h.M32 * h.M32 + h.M33 * h.M33);
            target = new Vector4(h.M31 * k + h.M41, h.M32 * k + h.M42, h.M33 * k + h.M43, h.M34);
        }
        int yaw = GameMath.Yaw_18C830(new Vector4(node.M31, node.M32, node.M33, node.M34));
        Vector4 landing;
        if (tgt is null) landing = new Vector4(0f, 0f, 0f, 1f);
        else if (tgt.IsGrabbable && tgt.GrabbableLandingPoint_1724E8(node) is { } lp) landing = lp;
        else
        {
            var off = GameMath.Rotate3_18E4B8(node, new Vector4(0f, -4.8f, 14.4f, 1f));
            var tp = tgt.GamePosition;
            landing = new Vector4(tp.X + off.X, tp.Y + off.Y, tp.Z + off.Z, 1f);
        }
        bool zero = MathF.Abs(landing.X) <= 5e-05f && MathF.Abs(landing.Y) <= 5e-05f && MathF.Abs(landing.Z) <= 5e-05f;
        int? yawTo;
        if (Mode == 1)
        {
            yawTo = tgt is not null
                ? GameMath.Yaw_18C830(new Vector4(-Lock.TargetMatrix.M31, -Lock.TargetMatrix.M32, -Lock.TargetMatrix.M33, Lock.TargetMatrix.M34))
                : null;
        }
        else
        {
            var to = zero ? target : landing;
            yawTo = GameMath.Yaw_18C830(new Vector4(to.X - Hand.X, to.Y - Hand.Y, to.Z - Hand.Z, to.W));
        }
        return new Aim(hand, yaw, target, yawTo, zero ? null : landing);
    }

    private void PlaceGraple_144B20(Vector4 end, Vector4 start)
    {
        if (Weapon is not { } weapon || Self is not { } self) return;
        var h = self.ExitAnchor_293B70(HandExit);
        weapon.SetGrapleEnds_140FA8(start, end);
        int state = State;
        if (state is >= 4 and <= 6 && !_c.Carrying)
        {
            var pos = self.GamePosition;
            self.SetNodePosition(new Vector3(pos.X - h.M41 + start.X, pos.Y - h.M42 + start.Y, pos.Z - h.M43 + start.Z));
            return;
        }
        var q = GameMath.MatrixToQuat_186900(h);
        if (q != weapon.NodeQuat) weapon.SetNodeQuat(q);
    }
}
