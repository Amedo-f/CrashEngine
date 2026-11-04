using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-27
public sealed class CharacterLocomotion
{
    private const float DegreesToAngle = 182.04444f;
    private const float RadiansToAngle = 10430.378f;

    private readonly CharacterController _c;

    public int Phase = 1;
    public int Pending;
    public bool RightMinusLeftHeld;
    public int PhaseTime;
    public int StandTime;
    public float AirSpeed;
    public int AirTurnRate;
    public Vector3 Move;
    public Vector3 Facing;
    public float Speed;
    public int Turn;

    public CharacterLocomotion(CharacterController c) => _c = c;

    public int[] TurnRates { get; set; } = [0x10000, 0x20000, 0x20000];

    // Amedo 2026-10-05
    public void Stand_160470()
    {
        Phase = 2;
        Pending = 2;
    }

    // Amedo 2026-10-05
    public void Reset_160600()
    {
        Phase = 1;
        Pending = 0;
        RightMinusLeftHeld = false;
        ForcedVector = false;
        ForcedTimed = false;
        Speed = 0f;
        AirSpeed = 0f;
        AirTurnRate = 0;
        Move = Vector3.Zero;
        Facing = Vector3.Zero;
        Turn = 0;
        ForcedCurrent = Vector3.Zero;
    }

    public bool Update(int ticks, int frameTicks)
    {
        var move = new Vector3(_c.MoveZ, 0f, _c.MoveX);
        float rl = SideStepAllowed() && _c.Crouch.Phase == 0 ? _c.RightMinusLeft : 0f, turnIn = _c.MoveTurn;
        if ((_c.State70 & 0x4000) != 0)
        {
            move = Vector3.Zero;
            rl = turnIn = 0f;
        }
        float len = GameMath.Length(move);
        Move = _c.Move;
        Facing = Vector3.Zero;
        if (Pending != 0)
        {
            Phase = Pending;
            Pending = 0;
            PhaseTime = ticks;
        }
        int next = 0;
        switch (Phase)
        {
            case 1:
                next = Select(len, rl, ticks);
                break;
            case 2:
                Speed = AnalogSpeed_13FD18() ? len * Speed_1602E0() : 0f; // Amedo
                TurnStep(turnIn, frameTicks, TurnRates[0]);
                WriteOverrides(true);
                next = Select(len, rl, ticks);
                break;
            case 3:
                Speed = AnalogSpeed_13FD18() ? len * Speed_1602E0() : 0f; // Amedo
                TurnStep(turnIn, frameTicks, TurnRates[0]);
                WriteOverrides(true);
                next = Select(len, rl, ticks);
                break;
            case 4:
                Speed = AnalogSpeed_13FD18() ? len * Speed_1602E0() : _c.WalkSpeed; // Amedo 2026-10-05
                TurnStep(turnIn, frameTicks, TurnRates[1]);
                WriteOverrides(!(ticks - StandTime < 9000));
                next = Select(len, rl, ticks);
                break;
            case 5:
                Speed = AnalogSpeed_13FD18() ? len * Speed_1602E0() : _c.RunSpeed; // Amedo 2026-10-05
                TurnStep(turnIn, frameTicks, TurnRates[2]);
                WriteOverrides(!(ticks - StandTime < 9000));
                next = Select(len, rl, ticks);
                break;
            case 7:
            {
                bool slideJump = (_c.MotionFlagsHigh & ((1u << 3) | (1u << 7))) != 0;
                Speed = AnalogSpeed_13FD18() ? len * Speed_1602E0() : slideJump ? AirSpeed : AirSpeed * len; // Amedo
                TurnStep(turnIn, frameTicks, AirTurnRate);
                WriteOverrides(slideJump);
                next = Select(len, rl, ticks);
                break;
            }
            case 8: // Amedo 2026-10-05
                Speed = AnalogSpeed_13FD18() ? len * Speed_1602E0() : len * 4f;
                TurnStep(turnIn, frameTicks, (int)(8f * RadiansToAngle));
                WriteOverrides(false);
                next = Select(len, rl, ticks);
                break;
            case 6:
                next = Strafe_14F808(rl, ticks, frameTicks); // Amedo
                break;
            case 9:
                next = Forced(len, rl, ticks);
                break;
            default:
                next = Select(len, rl, ticks);
                break;
        }
        RightMinusLeftHeld = !(MathF.Abs(rl) <= 5e-5f);
        if (next != 0) Pending = next & 0xF;
        return Phase is 4 or 5;
    }

    private int Select(float len, float rl, int ticks)
    {
        if (_c.MotionState == 9) return Phase != 8 ? 8 : 0;
        if (!_c.IsGrounded)
        {
            if (Phase == 7)
            {
                if ((_c.JumpRules & 0x20) != 0) AirTurnRate |= _c.Jump.AirTurnRate;
                return 0;
            }
            if (Phase == 9) return 7;
            AirSpeed = _c.Jump.AirSpeed;
            AirTurnRate = _c.Jump.AirTurnRate;
            return 7;
        }
        if ((_c.MotionFlagsHigh & 1u) != 0) return Phase != 1 ? 1 : 0;
        if (!RightMinusLeftHeld && 5e-5f < MathF.Abs(rl) && SideStepAllowed())
        {
            if (Phase == 6) return 0;
            _c.Trigger(rl < 0f ? 0x10 : 0x11);
            return 6;
        }
        if (0.96f < len && 0f < _c.RunSpeed)
        {
            _c.Trigger(0xF);
            return 5;
        }
        if ((AnalogSpeed_13FD18() ? 1e-4f : 0.3f) < len) // Amedo
        {
            _c.Trigger(0xE);
            return 4;
        }
        int angle = Move.X * Move.X + Move.Z * Move.Z < 5e-5f ? 0 : SignedAngle(Move, _c.ForwardAxis);
        int abs = angle < 0 ? -angle : angle;
        StandTime = ticks;
        int threshold = (int)(5f * DegreesToAngle);
        if (threshold < abs)
        {
            _c.Trigger(0xD);
            return 3;
        }
        if (Phase != 2)
        {
            _c.Trigger(0xC);
            return 2;
        }
        return 0;
    }

    private bool SideStepAllowed() => 5e-5f < MathF.Abs(_c.Param(8));

    public float LastStrafe; // Amedo

    // Amedo
    private int Strafe_14F808(float rl, int ticks, int frameTicks)
    {
        bool held = !(MathF.Abs(rl) <= 5e-5f);
        var m = _c.GameMatrix;
        int elapsed = ticks - PhaseTime;
        if (held) LastStrafe = rl;
        if (_c.StrafeTarget_124?.Invoke() is { } target)
        {
            var d = new Vector3(target.X - m.M41, 0f, target.Z - m.M43);
            float inv = GameMath.InverseLength(d);
            Facing = new Vector3(d.X * inv, d.Y * inv, d.Z * inv);
        }
        else Facing = new Vector3(m.M31, m.M32, m.M33);
        var up = new Vector3(m.M21, m.M22, m.M23);
        float k = -LastStrafe;
        Move = new Vector3((Facing.Y * up.Z - Facing.Z * up.Y) * k, (Facing.Z * up.X - Facing.X * up.Z) * k, (Facing.X * up.Y - Facing.Y * up.X) * k);
        Speed = AnalogSpeed_13FD18() ? Speed_1602E0() : _c.Param(8);
        int kind = _c.IntParam(0);
        TurnStep(LastStrafe, frameTicks, kind == 5 ? (int)(180f * DegreesToAngle) : 0);
        WriteOverrides(false);
        int hold = (int)((kind == 5 ? 0.25f : 0f) * 9000f);
        if (held || elapsed < hold)
        {
            if (kind != 5 || elapsed < hold) return 0;
        }
        if (Phase == 2) return 0;
        _c.Trigger(0xC);
        return 2;
    }


    // Amedo 2026-10-05
    private bool AnalogSpeed_13FD18() => _c.Controlled_13FD18(); // Amedo

    // Amedo 2026-10-05
    private float Speed_1602E0() => 0f < _c.Param(7) ? _c.Param(7) : _c.Param(6);

    public bool ForcedTimed;
    public int ForcedDuration;
    public float ForcedFrom, ForcedTo;

    public void Force(float from, float to, int duration)
    {
        Pending = 9;
        ForcedVector = false; // Amedo 2026-09-28
        ForcedTimed = duration != 0;
        ForcedFrom = from;
        ForcedTo = to;
        ForcedDuration = duration;
    }

    // Amedo 2026-09-28
    public bool ForcedVector;
    public Vector3 ForcedFromVector, ForcedToVector, ForcedCurrent;

    // Amedo 2026-09-28
    public void ForceVector(Vector3 from, Vector3 to, int duration)
    {
        Pending = 9;
        ForcedVector = true;
        ForcedTimed = duration != 0;
        ForcedFromVector = from;
        ForcedToVector = to;
        ForcedDuration = duration;
    }

    private int Forced(float len, float rl, int ticks)
    {
        int next = 0;
        int e = ticks - PhaseTime;
        if (!ForcedTimed)
        {
            Speed = ForcedTo;
            ForcedCurrent = ForcedToVector; // Amedo 2026-09-28
            if ((_c.MotionFlagsHigh & (1u << 15)) == 0 && (0.3f < len || _c.IsGrounded)) next = Select(len, rl, ticks);
        }
        else if (ForcedDuration <= e)
        {
            Speed = ForcedTo;
            ForcedCurrent = ForcedToVector;
            next = Select(len, rl, ticks);
        }
        else
        {
            float f = e * (1f / 9000f) / (ForcedDuration * (1f / 9000f));
            f *= f;
            Speed = ForcedTo * f + ForcedFrom * (1f - f);
            ForcedCurrent = ForcedFromVector + (ForcedToVector - ForcedFromVector) * f;
        }
        int angle = 0; // Amedo 2026-09-28
        if (ForcedVector)
        {
            angle = SignedAngle(ForcedCurrent, _c.ForwardAxis);
            Turn = 0;
            Speed = GameMath.Length(ForcedCurrent);
        }
        if (!_c.IsGrounded) AirSpeed = Speed;
        float radians = angle * (MathF.PI * 2f / 65536f);
        _c.SetOverride(CharacterController.ForwardOverride, ref _c.Forward, Speed * MathF.Cos(radians));
        _c.SetOverride(CharacterController.SideOverride, ref _c.Side, -(Speed * MathF.Sin(radians)));
        _c.SetTurn(Turn);
        return next;
    }

    private void TurnStep(float turnIn, int frameTicks, int rate)
    {
        float dt = frameTicks * (1f / 9000f);
        if (!(MathF.Abs(Facing.X) <= 5e-5f && MathF.Abs(Facing.Y) <= 5e-5f && MathF.Abs(Facing.Z) <= 5e-5f))
        {
            Turn = SignedAngle(_c.ForwardAxis, Facing);
        }
        else
        {
            int a45 = (int)(45f * DegreesToAngle);
            int a135 = (int)(135f * DegreesToAngle);
            int maxStep = (int)(rate * dt);
            int minStep = (int)(rate * -dt);
            int want = (int)(turnIn * 180f * DegreesToAngle);
            if (-a45 < want && want < a45)
            {
                float r = (float)want / a45;
                float k = r * r;
                want = (int)(want * (k * 0.5f + (1f - k) * 0.05f));
            }
            else if (-a135 < want && want < a135)
            {
                float f = MathF.Abs((float)(want - a45) / (a135 - a45));
                want = (int)(want * (f + (1f - f) * 0.5f));
            }
            Turn = maxStep < want ? maxStep : want < minStep ? minStep : want;
        }
        Turn = dt != 0f ? (int)(Turn / dt) : 0;
    }

    private void WriteOverrides(bool forwardOnly)
    {
        float forward, side;
        if (forwardOnly)
        {
            forward = Speed;
            side = 0f;
        }
        else
        {
            int angle = Move.X * Move.X + Move.Z * Move.Z < 5e-5f ? 0 : SignedAngle(Move, _c.ForwardAxis);
            float radians = angle * (MathF.PI * 2f / 65536f);
            forward = Speed * MathF.Cos(radians);
            side = Speed * -MathF.Sin(radians);
        }
        _c.SetOverride(CharacterController.ForwardOverride, ref _c.Forward, forward);
        _c.SetOverride(CharacterController.SideOverride, ref _c.Side, side);
        _c.SetTurn(Turn);
    }

    public static int SignedAngle(Vector3 u, Vector3 v)
    {
        float uu = u.X * u.X + u.Y * u.Y + u.Z * u.Z;
        float vv = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
        float n = MathF.Sqrt(uu * vv);
        float inv = n == 0f ? n : 1f / n;
        float c = (u.X * v.X + u.Y * v.Y + u.Z * v.Z) * inv;
        float a = MathF.Abs(c);
        float s = 1f - a;
        if (s < 0f) s = 0f;
        float acos = (((-0.0187293f * a + 0.074261f) * a - 0.2121144f) * a + 1.5707288f) * MathF.Sqrt(s);
        if (c < 0f) acos = MathF.PI - acos;
        int angle = (int)(acos * RadiansToAngle);
        if (angle == 0 || angle == 0x8000) return angle;
        var cross = new Vector3(u.Y * v.Z - u.Z * v.Y, u.Z * v.X - u.X * v.Z, u.X * v.Y - u.Y * v.X);
        if (MathF.Abs(cross.X) <= 5e-5f && MathF.Abs(cross.Y) <= 5e-5f && MathF.Abs(cross.Z) <= 5e-5f)
            return angle < 0x4000 ? 0 : (int)(MathF.PI * RadiansToAngle);
        if (MathF.Abs(cross.Y) <= 5e-5f) return 0;
        return (int)(angle * (cross.Y < 0f ? -1f : 1f));
    }
}
