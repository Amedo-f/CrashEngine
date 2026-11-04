using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-27
public sealed class CharacterJump
{
    public const uint SpinDoubleJumpBit    = 0x10000;
    public const uint SlideJumpBodySlamBit = 0x20000;

    private const float TickRate = 9000f;
    private const float TickTime = 1f / 9000f;

    private readonly CharacterController _c;

    public uint Flags = 0x290;
    public float Gravity;
    public float Speed;
    public float AirSpeed;
    public int AirTurnRate;
    public int LeftGround;
    public int CrossTime;
    public int CircleTime;
    public int PhaseTime;
    public int ApexTime;
    public int LandTime;

    public CharacterJump(CharacterController c)
    {
        _c = c;
        Gravity     = c.Param(1);
        AirSpeed    = c.Param(15);
        AirTurnRate = c.IntParam(6);
        c.Gravity   = Gravity;
    }

    public int Phase => (int)(Flags & 0x1F);

    public void Update(float cross, float circle, int now, int frameTicks, bool allowed)
    {
        bool crossOn = 0f < cross, circleOn = 0f < circle;
        int next = -1;
        if ((Flags & 0x400) == 0 && crossOn) CrossTime = now;
        if ((Flags & 0x800) == 0 && circleOn) CircleTime = now;
        if ((Flags & 0x3E0) != 0x280)
        {
            PhaseTime = now;
            Flags = (Flags & ~0x3FFu) | ((Flags >> 5) & 0x1F) | 0x280;
        }
        if (_c.MotionState is 6 or 10) Flags &= (_c.JumpRules & SpinDoubleJumpBit) != 0 ? ~0x2000u : ~0x3000u;
        else if (_c.Carrying) Flags &= ~0x3000u;
        if ((Flags & 0x4000) != 0) next = Launched(); // Amedo 2026-09-27
        else if ((_c.MotionFlagsHigh & (1u << 4)) != 0) next = 0;
        else
            switch (Phase)
            {
                case 0: Ground(now, allowed, crossOn, ref next); break;
                case 1: Rise(now, frameTicks, 2, ref next); break;
                case 2: Rise(now, frameTicks, 3, ref next); break;
                case 3: Rise(now, frameTicks, 4, ref next); break;
                case 4: Rise(now, frameTicks, 5, ref next); break;
                case 5: Rise(now, frameTicks, 6, ref next); break;
                case 6: Hang(now, 7, ref next); break;
                case 7: Slam(ref next); break;
                case 8: Rise(now, frameTicks, 8, ref next); break;
                case 9: Fall(now, frameTicks, 8, ref next); break;
                case 0xA: Hang(now, 9, ref next); break;
                case 0xB: Fall(now, frameTicks, 9, ref next); break;
                case 0xC: Hang(now, 10, ref next); break;
                case 0xD: Rise(now, frameTicks, 10, ref next); break;
                case 0xE: Fall(now, frameTicks, 10, ref next); break;
                case 0xF: Fall(now, frameTicks, 3, ref next); break;
                case 0x10: Fall(now, frameTicks, 2, ref next); break;
                case 0x11: Fall(now, frameTicks, 4, ref next); break;
                case 0x12: Fall(now, frameTicks, 5, ref next); break;
                case 0x13: Fall(now, frameTicks, 6, ref next); break;
            }
        Flags = (Flags & ~0xC00u) | (crossOn ? 0x400u : 0u) | (circleOn ? 0x800u : 0u);
        if (next != -1) Flags = (Flags & ~0x3E0u) | (uint)((next & 0x1F) << 5);
    }

    // Amedo 2026-09-27
    public void RequestLaunch(int slot, float speed, float gravity)
    {
        Flags   = ((Flags | 0x4000u) & 0xFF807FFFu) | (uint)((slot & 0xFF) << 15);
        Gravity = gravity;
        Speed   = speed;
    }

    // Amedo 2026-09-27
    private int Launched()
    {
        int next;
        if (0f < Speed)
        {
            Flags = (Flags & ~0x1000u) | (_c.Carrying ? 0u : 0x1000u);
            Flags &= ~0x2000u;
            int slot = (int)((Flags >> 15) & 0xFF);
            if (slot != 0x6F) _c.Trigger(slot);
            _c.MotionFlags &= ~CharacterController.Grounded;
            _c.Gravity = Gravity;
            _c.SetOverride(CharacterController.VerticalOverride, ref _c.Vertical, Speed);
            Flags |= 0x800000;
            _c.SetAirMode(1);
            ApexTime = 0;
            float apex = Speed / Gravity;
            float land = Landing(Gravity * -0.5f, Speed);
            ApexTime = GameMath.Trunc(apex * TickRate);
            LandTime = GameMath.Trunc(land * TickRate);
            _c.SetParam(14, land * 0.95f);
            next = 5;
        }
        else next = BeginFall(6, true);
        Flags &= ~0x4000u;
        return next;
    }

    private void Ground(int now, bool allowed, bool crossOn, ref int next)
    {
        bool slide = _c.MotionState is 8 or 12;
        bool canJump;
        if (!_c.IsGrounded)
        {
            if (LeftGround == 0) LeftGround = now;
            canJump = now - LeftGround < (int)(TickRate * 0.11f);
        }
        else
        {
            LeftGround = 0;
            canJump = _c.Carrying ? _c.Link?.Phase != 2 : !(_c.StandingOn is { } on && _c.MotionStateOf?.Invoke(on) >= 0); // Amedo
        }
        if (!allowed || !canJump || !crossOn || (Flags & 0x400) != 0) return;
        bool fits = _c.Fits(6);
        int type;
        if (slide && fits) type = 5;
        else
        {
            if ((_c.MotionFlagsHigh & 1u) != 0 && !fits) return;
            type = _c.Carrying ? 3 : 2;
        }
        next = StartJump(now, type, false, false);
    }

    private int StartJump(int now, int type, bool maxed, bool slowed)
    {
        int slot = JumpEvent(type, maxed);
        int turn, next;
        float v, air;
        bool reuse = false;
        switch (type)
        {
            case 2:
                next = 1; turn = _c.IntParam(6); v = _c.Param(16); air = _c.Param(15);
                break;
            case 3:
                v = 10f; turn = GameMath.Scale_15DE90(0.2f, _c.IntParam(6)); next = 2; air = _c.Param(15);
                break;
            case 4:
                turn = _c.IntParam(7); v = _c.Param(20); air = _c.Param(19);
                if (slowed)
                {
                    int e = now - PhaseTime;
                    float f = e < ApexTime ? e * TickTime / (ApexTime * TickTime) : 1f;
                    v *= 1f - f * 0.5f;
                }
                next = 3;
                break;
            case 5:
                v = _c.Param(24); next = 4; air = _c.Param(23); turn = _c.IntParam(8);
                break;
            case 6:
                turn = _c.IntParam(6);
                if (_c.Carrying) turn = GameMath.Scale_15DE90(0.2f, turn);
                v = Speed; next = 5; air = _c.Param(15); reuse = true;
                break;
            case 10:
                turn = 0; air = 0f; v = _c.Param(38); next = 0xD;
                break;
            default:
                return -1;
        }
        if (!(5e-5f < MathF.Abs(v))) return !slowed && reuse ? BeginFall(type, false) : -1;
        if (slot == 0x6F) Flags = (_c.JumpRules & SpinDoubleJumpBit) != 0 ? (Flags | 0x1000u) & ~0x2000u : Flags & ~0x3000u;
        else
        {
            Flags = (Flags & ~0x1000u) | (((_c.JumpRules & 0xFFFF) >> type & 1) != 0 ? 0x1000u : 0u);
            Flags = (Flags & ~0x2000u) | (type is 2 or 6 or 4 or 5 ? 0x2000u : 0u);
        }
        Speed       = v;
        AirTurnRate = turn;
        AirSpeed    = air;
        Gravity     = GravityFor(type, false);
        if (slot != 0x6F) _c.Trigger(slot);
        _c.MotionFlags &= ~CharacterController.Grounded;
        _c.Gravity = Gravity;
        _c.SetOverride(CharacterController.VerticalOverride, ref _c.Vertical, Speed);
        Flags |= 0x800000;
        _c.SetAirMode(1);
        ApexTime = 0;
        float apex = Speed / Gravity;
        float land = Landing(Gravity * -0.5f, Speed);
        ApexTime = GameMath.Trunc(apex * TickRate);
        LandTime = GameMath.Trunc(land * TickRate);
        _c.SetParam(14, land * 0.95f);
        return next;
    }

    private static float Landing(float a, float b)
    {
        const float c = 0f;
        float disc = b * b - a * (c * 4f);
        if (disc < 0f) return 0f;
        float sign = b < 0f ? -1f : 1f;
        float q = (b + sign * MathF.Sqrt(disc)) * -0.5f;
        int n = 0;
        float r0 = 0f, r1 = 0f;
        if (a != 0f) { r0 = q / a; n = 1; }
        if (q != 0f)
        {
            if (n == 0) r0 = c / q;
            else r1 = c / q;
            n++;
        }
        return n == 1 ? r0 : n == 2 ? MathF.Max(r1, r0) : 0f;
    }

    private float GravityFor(int type, bool falling) => falling
        ? type switch
        {
            2 or 3 => _c.Param(18),
            4      => _c.Param(22),
            5 or 8 => _c.Param(26),
            6      => _c.Param(1),
            9      => _c.Param(36),
            10     => _c.Param(39),
            _      => 0f,
        }
        : type switch
        {
            2 or 3 => _c.Param(17),
            4      => _c.Param(21),
            5 or 8 => _c.Param(25),
            6      => Gravity,
            10     => _c.Param(39),
            _      => 0f,
        };

    private int JumpEvent(int type, bool maxed)
    {
        int state = _c.MotionState;
        if (state is 8 or 12) return 0x16;
        if (state is 6 or 10) return 0x6F;
        uint high = _c.MotionFlagsHigh;
        bool carried = (high & (1u << 21)) != 0;
        if ((high & (1u << 11)) == 0)
        {
            if (type == 4) return maxed ? 0x1A : 0x19;
            if (type == 10) return 0x20;
            if (!carried) return 0x17;
            _c.Link?.Follower_15FE98()?.Trigger(100); // Amedo 2026-10-05
            return 100;
        }
        if (!carried) return 0x18;
        _c.Link?.Follower_15FE98()?.Trigger(0x65); // Amedo 2026-10-05
        return 0x65;
    }

    private int BeginFall(int type, bool trigger)
    {
        Gravity = GravityFor(type, true);
        Speed = 0f;
        _c.Gravity = Gravity;
        _c.StartFall(trigger);
        ApexTime = GameMath.Trunc((LandTime * TickTime - ApexTime * TickTime) * TickRate);
        return type switch
        {
            2 => 0x10, 3 => 0xF, 4 => 0x11, 5 => 0x12, 6 => 0x13, 8 => 9, 9 => 0xB, 10 => 0xE, _ => -1,
        };
    }

    private int PressWindow(int frameTicks, int press, int window)
    {
        int t = frameTicks / 8;
        if (press < PhaseTime - t) return 0;
        return press < PhaseTime + ApexTime - window - t ? 1 : 2;
    }

    private static bool Reached(int frameTicks, int now, int deadline) => !(now < deadline - frameTicks / 8);

    private void Rise(int now, int frameTicks, int type, ref int next)
    {
        int start = PhaseTime;
        int doubleJump = 0;
        if ((Flags & 0x1000) != 0 && 5e-5f < MathF.Abs(_c.Param(20)))
            doubleJump = PressWindow(frameTicks, CrossTime, GameMath.Trunc(TickRate * 0.15f));
        int attack = (Flags & 0x2000) == 0 ? 0 : PressWindow(frameTicks, CircleTime, GameMath.Trunc(_c.Param(29) * TickRate));
        if (attack == 0) Flags &= ~0x2000000u;
        if (doubleJump != 0) next = StartJump(now, 4, doubleJump == 2, false);
        else if (attack != 0 || (Flags & 0x2000000) != 0) next = AerialAttack(type);
        else if (Reached(frameTicks, now, start + ApexTime)) next = BeginFall(type, false);
        else _c.Gravity = Gravity;
    }

    private void Fall(int now, int frameTicks, int type, ref int next)
    {
        if (_c.IsGrounded)
        {
            next = 0;
            return;
        }
        int doubleJump = 0;
        if ((Flags & 0x1000) != 0)
        {
            int t = frameTicks / 8;
            int w = GameMath.Trunc(TickRate * 0.4f);
            doubleJump = CrossTime < PhaseTime - t ? 0 : CrossTime < PhaseTime + w - t ? 1 : 2;
        }
        int attack = 0;
        if ((Flags & 0x2000) != 0)
        {
            int w = GameMath.Trunc(_c.Param(30) * TickRate);
            int t = frameTicks / 8;
            attack = CircleTime < PhaseTime - t ? 0 : CircleTime < PhaseTime + w - t ? 1 : 2;
        }
        if (doubleJump == 1) next = StartJump(now, 4, false, true);
        else if (attack == 1) next = AerialAttack(type);
        else Gravity = _c.Param(1);
        _c.Gravity = Gravity;
    }

    private int AerialAttack(int type)
    {
        Flags &= ~0x3000u;
        if (type == 5 && (_c.JumpRules & SlideJumpBodySlamBit) == 0) return -1;
        ApexTime = GameMath.Trunc(_c.Param(37) * TickRate); // Amedo
        if (type == 2 && _c.Gun is { } gun && ApexTime != 0)
        {
            if (gun.TakeAmmo_15EFE8(5))
            {
                _c.Trigger(0x1F);
                return 0xC;
            }
            _c.Trigger(0x6C);
            return -1;
        }
        ApexTime = GameMath.Trunc(_c.Param(34) * TickRate);
        if (type == 2 && ApexTime != 0)
        {
            _c.Trigger(0x1D);
            return 10;
        }
        ApexTime = GameMath.Trunc(_c.Param(31) * TickRate);
        if (ApexTime == 0) return -1;
        if (_c.Fits(7, push: true)) // Amedo 2026-10-06
        {
            _c.Trigger(0x1B);
            return 6;
        }
        Flags |= 0x2000000;
        return -1;
    }

    private void Hang(int now, int type, ref int next)
    {
        float air = 0f;
        Gravity = 0f;
        Speed = 0f;
        if (now - PhaseTime < ApexTime) air = type == 9 ? _c.Param(35) : 0f;
        else if (type == 9)
        {
            _c.Trigger(0x25);
            next = BeginFall(9, false);
        }
        else if (type == 7)
        {
            Speed   = _c.Param(32);
            Gravity = _c.Param(33);
            _c.Trigger(0x1C);
            _c.Gravity = Gravity;
            next = 7;
        }
        else if (type == 10) next = StartJump(now, 10, false, false);
        _c.Locomotion.Force(air, air, 0);
        _c.Gravity = Gravity;
        _c.SetOverride(CharacterController.VerticalOverride, ref _c.Vertical, Speed);
        _c.SetOverride(CharacterController.ForwardOverride, ref _c.Forward, 0f);
        _c.SetOverride(CharacterController.SideOverride, ref _c.Side, 0f);
        _c.SetOverride(CharacterController.AccelOverride, ref _c.AccelScale, 3f);
    }

    private void Slam(ref int next)
    {
        if (_c.IsGrounded)
        {
            _c.Trigger(0x2A);
            Gravity = _c.Param(1);
            _c.Gravity = Gravity;
            next = 0;
        }
        _c.SetOverride(CharacterController.ForwardOverride, ref _c.Forward, 0f);
        _c.SetOverride(CharacterController.SideOverride, ref _c.Side, 0f);
        _c.SetOverride(CharacterController.AccelOverride, ref _c.AccelScale, 3f);
    }
}
