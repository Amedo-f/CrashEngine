using System.Numerics;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-27
public sealed class CharacterCrouch
{
    private const float TickRate = 9000f;
    private const float TickTime = 1f / 9000f;

    private readonly CharacterController _c;

    public int Phase;
    public uint Flags;
    public float Cooldown;
    public int PhaseTime;
    public int Duration;
    public float Friction;
    public Vector3 Direction;
    public float SlideScale = 1f;

    public CharacterCrouch(CharacterController c)
    {
        _c = c;
        Flags = 0x400u | (MathF.Abs(c.Param(41)) <= 5e-5f ? 0u : 0x800u);
    }

    public bool JumpAllowed => (Flags & 0x400) != 0;
    public bool LowShape => (Flags & 0x800) != 0;
    public bool Sloped => (Flags & 0x200) != 0;

    public void Update(float circle, float len, float turn, int now, int frameTicks)
    {
        bool on = 0f < circle;
        int next = -1;
        uint high = _c.MotionFlagsHigh;
        int state = _c.MotionState;
        if ((high & (1u << 12)) != 0)
        {
            if (_c.Fits(0))
            {
                next = 0;
                Cooldown = 0.2f;
                Flags |= 0x400;
            }
        }
        else if ((high & (1u << 1)) != 0)
        {
            next = 0;
            Cooldown = 0.2f;
        }
        else if (state != 3 && state is not (8 or 12))
        {
            if (!_c.Fits(0)) next = 3;
            else
            {
                next = 0;
                Cooldown = 0.2f;
            }
        }
        else if (!_c.IsGrounded && Phase is not (5 or 6))
        {
            if (_c.Fits(0))
            {
                next = 0;
                Cooldown = 0.2f;
            }
        }
        else
            switch (Phase)
            {
                case 0:
                    Start(on, ref next);
                    break;
                case 1:
                    if (Ends(now)) next = 2;
                    break;
                case 2:
                    if (!on && _c.Fits(0, touchUp: true)) // Amedo 2026-10-06
                    {
                        _c.Trigger(0x31);
                        Duration = CharacterController.Cvt(_c.Param(42) * TickRate);
                        next = 4;
                        Cooldown = 0.2f;
                    }
                    else if (0.6f < len && _c.Fits(4, push: true)) // Amedo 2026-10-06
                    {
                        _c.Trigger(0x2F);
                        next = 3;
                    }
                    break;
                case 3:
                    if (!on && _c.Fits(0, touchUp: true)) // Amedo 2026-10-06
                    {
                        _c.Trigger(0x32);
                        Duration = CharacterController.Cvt(_c.Param(43) * TickRate);
                        next = 4;
                    }
                    else if (len <= 0.6f)
                    {
                        _c.Trigger(0x30);
                        next = 2;
                    }
                    break;
                case 4:
                    if (on && Cooldown == 0f)
                    {
                        _c.Trigger(0x2E);
                        Duration = CharacterController.Cvt(_c.Param(41) * TickRate);
                        next = 1;
                    }
                    else if (Ends(now))
                    {
                        if (!_c.Fits(0, push: true)) next = 3; // Amedo 2026-10-06
                        else
                        {
                            next = 0;
                            Cooldown = 0.2f;
                        }
                    }
                    break;
                case 5:
                    Slide(turn, now, ref next);
                    break;
                case 6:
                    SlideEnd(turn, now, on, ref next);
                    break;
            }
        if ((_c.MotionFlagsHigh & 1u) != 0)
        {
            if (0.6f < len)
            {
                _c.SetOverride(CharacterController.ForwardOverride, ref _c.Forward, _c.Param(40));
                _c.SetOverride(CharacterController.SideOverride, ref _c.Side, 0f);
                _c.SetTurn(CharacterController.Mul(_c.IntParam(4), turn));
            }
            else
            {
                _c.SetOverride(CharacterController.ForwardOverride, ref _c.Forward, 0f);
                _c.SetOverride(CharacterController.SideOverride, ref _c.Side, 0f);
                _c.SetTurn(0);
            }
        }
        Cooldown -= frameTicks * TickTime;
        if (Cooldown < 0f) Cooldown = 0f;
        if (next != -1)
        {
            PhaseTime = now;
            Phase = next;
        }
    }

    private bool Ends(int now) => CharacterController.Cvt((PhaseTime * TickTime + Duration * TickTime) * TickRate) <= now;

    private void Start(bool on, ref int next)
    {
        if (!on || Cooldown != 0f || !_c.Fits(3, push: true)) return; // Amedo 2026-10-06
        bool slide = false;
        if ((_c.MotionFlagsHigh & (1u << 11)) != 0 && 1f < _c.Velocity.LengthSquared())
        {
            Duration = CharacterController.Cvt((_c.Param(45) + _c.Param(46) - _c.Param(47)) * TickRate);
            slide = Duration != 0;
        }
        if (slide)
        {
            next = 5;
            Friction = 1f;
            Flags &= ~(0x200u | 0x400u);
            SlideScale = 1f;
            Direction = _c.Move * CharacterController.InverseLength(_c.Move);
            _c.Trigger(0x33);
            return;
        }
        Duration = CharacterController.Cvt(_c.Param(41) * TickRate);
        if (Duration == 0) return;
        _c.Trigger(0x2E);
        next = 1;
    }

    private void Slide(float turn, int now, ref int next)
    {
        int elapsed = now - PhaseTime;
        int early = CharacterController.Cvt(_c.Param(45) * TickRate);
        bool late = !(elapsed < early);
        Flags = (Flags & ~0x400u) | (late ? 0x400u : 0u);
        int rate;
        float speed;
        if (Friction < 0.2f)
        {
            rate = 0;
            _c.Trigger(0x36);
            speed = 0f;
            Duration = CharacterController.Cvt(_c.Param(49) * TickRate);
            next = 6;
            Cooldown = 0f;
            Flags |= 0x400;
        }
        else
        {
            if (Duration <= elapsed)
            {
                Duration = CharacterController.Cvt(_c.Param(47) * TickRate);
                next = 6;
            }
            if (!late && !(MathF.Abs(Direction.X) <= 5e-5f && MathF.Abs(Direction.Y) <= 5e-5f && MathF.Abs(Direction.Z) <= 5e-5f))
            {
                float remain = early * TickTime - elapsed * TickTime;
                int angle = CharacterLocomotion.SignedAngle(Direction, _c.ForwardAxis);
                rate = Math.Abs(CharacterController.Cvt(angle / remain));
            }
            else rate = _c.IntParam(5);
            speed = _c.Param(44);
        }
        Drive(speed, CharacterController.Mul(rate, turn));
    }

    private void SlideEnd(float turn, int now, bool on, ref int next)
    {
        int elapsed = now - PhaseTime;
        if (elapsed < Duration)
        {
            SlideScale = 1f - elapsed * TickTime / (Duration * TickTime);
            Drive(_c.Param(44), CharacterController.Mul(_c.IntParam(5), turn));
            return;
        }
        int d = CharacterController.Cvt(_c.Param(48) * TickRate);
        Flags &= ~0x200u;
        Duration = d;
        if ((!on || d == 0) && _c.Fits(0, touchUp: true)) // Amedo 2026-10-06
        {
            _c.Trigger(0x36);
            Duration = CharacterController.Cvt(_c.Param(49) * TickRate);
            next = 4;
            Cooldown = 0.2f;
        }
        else
        {
            _c.Trigger(0x35);
            next = 1;
        }
        SlideScale = 0f;
        Drive(0f, 0);
    }

    private void Drive(float speed, int turnRate)
    {
        Friction *= _c.Efficiency;
        _c.SetOverride(CharacterController.ForwardOverride, ref _c.Forward, speed * SlideScale);
        _c.SetOverride(CharacterController.SideOverride, ref _c.Side, 0f);
        _c.SetTurn(CharacterController.Mul(turnRate, SlideScale));
    }
}
