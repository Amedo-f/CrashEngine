using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-28
public sealed partial class CharacterController
{
    public const int DeathSlot = 0x43;
    public const ulong DeadFlag = 0x4000;
    private const uint Knocked = 1u << 15;

    public struct DamageRecord
    {
        public Vector4 Source;
        public uint Kind;
        public int Amount;
    }

    public DamageRecord LastDamage;
    public int Timer74;
    public int Duration78;
    public Action<int, object?>? TriggerFrom;
    public Action<bool>? SetVisible;
    public Action<string>? Log;
    private bool? _visible;

    public bool Invincible => (MotionWord & 0x400) != 0;
    public bool Dead => (State70 & DeadFlag) != 0;

    public void Damage(Vector3 source, uint kind, int amount, object? sender)
    {
        if (CutsceneMode) return; // Amedo 2026-09-28
        var primary = this; // Amedo 2026-09-29
        CharacterController? second = null;
        if (Vehicle is { } vehicle)
        {
            if (vehicle.Driver) second = vehicle.Other;
            else if (vehicle.Other is { } driver) { primary = driver; second = this; }
        }
        int hp = primary.HitPoints;
        if (hp == 0) return;
        if (primary.Invincible && (uint)amount < 50) return;
        int now = hp > amount ? hp - amount : 0;
        var record = new DamageRecord { Source = new Vector4(source, 1f), Kind = kind, Amount = amount };
        primary.LastDamage = record;
        primary.HitPoints = now;
        if (second is not null)
        {
            second.LastDamage = record;
            second.HitPoints = now;
        }
        if (amount != 0) Log?.Invoke($"[player] damage {amount} (kind 0x{kind:X}) hp {hp} -> {now}{(now == 0 ? " DEAD" : "")}{(second is not null ? " (vehicle pair)" : "")} from {(sender as CrashEngine.Core.Component)?.Entity?.Name ?? "-"}"); // Amedo 2026-09-30
        if (now == 0)
        {
            primary.Die(sender);
            second?.Die(sender);
            return;
        }
        if (now == hp) return;
        primary.HurtAndGuard(source);
        second?.HurtAndGuard(source);
    }

    // Amedo 2026-09-29
    private void Die(object? sender)
    {
        InputDisabled = 0xFFFFFFFF;
        State70 |= DeadFlag;
        if (TriggerFrom is not null) TriggerFrom(DeathSlot, sender);
        else _trigger(DeathSlot);
    }

    private void HurtAndGuard(Vector3 source)
    {
        Hurt(source);
        if (Invincible) return;
        MotionWord |= 0x400;
        Duration78 = (int)(9000f + 9000f);
        State70 = (State70 & ~0xF0ul) | 0x10;
    }

    public void SetInvincible(int mode)
    {
        if (Invincible) return;
        MotionWord |= 0x400;
        State70 = (State70 & ~0xF0ul) | 0x10;
        Duration78 = mode == 0 ? (int)(9000f * 2f) : (int)Math.Min(9000.0 * GoldenSeconds, int.MaxValue); // Amedo 2026-10-04
    }

    private void Hurt(Vector3 source)
    {
        float gravity = Param(1);
        var m = _getMatrix();
        var from = m.Translation - Velocity * 0.25f;
        var d = new Vector3(from.X - source.X, 0f, from.Z - source.Z);
        int angle = 0;
        if (d.X * d.X + d.Z * d.Z < 1e-6f) d = Vector3.Zero;
        else
        {
            d *= 5f / MathF.Sqrt(d.X * d.X + d.Z * d.Z);
            angle = SignedAngle(new Vector3(m.M31, m.M32, m.M33), d, new Vector3(m.M21, m.M22, m.M23));
        }
        int slot = angle < -0x6000 || 0x6000 < angle ? 0x51 : angle < -0x2000 ? 0x52 : 0x2000 < angle ? 0x53 : 0x50;
        MotionFlagsHigh |= Knocked;
        Log?.Invoke($"[player] hurt: knockback ({d.X:0.##}, 12.5, {d.Z:0.##}) angle 0x{angle & 0xFFFF:X4} slot 0x{slot:X}");
        Launch(new Vector3(d.X, 12.5f, d.Z), false, gravity, slot);
    }

    private static int SignedAngle(in Vector3 a, in Vector3 b, in Vector3 up)
    {
        float la = GameMath.Length(a), lb = GameMath.Length(b);
        if (la <= 0f || lb <= 0f) return 0;
        float c = Math.Clamp(GameMath.Dot(a, b) / (la * lb), -1f, 1f);
        float s = GameMath.Dot(Vector3.Cross(a, b), up);
        float rad = MathF.Acos(c);
        return (int)((s < 0f ? -rad : rad) * 10430.378f);
    }

    private void InvincibilityTimer()
    {
        if ((State70 & 0xF0) != 0x30)
        {
            Timer74 = Ticks;
            State70 = (State70 & ~0xFul) | ((State70 >> 4) & 0xF);
            State70 = (State70 & ~0xF0ul) | 0x30;
        }
        if ((State70 & 0xF) != 1)
        {
            if (GoldenCheck_2C8260?.Invoke(this) == true) InvincibilityEnd_131760(); // Amedo
            return;
        }
        int elapsed = Ticks - Timer74;
        if ((MotionFlagsHigh & Knocked) != 0 && (int)(9000f * 0.3f) <= elapsed) MotionFlagsHigh &= ~Knocked;
        if (Duration78 <= elapsed)
        {
            InvincibilityEnd_131760(); // Amedo
            return;
        }
        if (HitPoints < 3)
        {
            int period = (int)(9000f * 0.2f);
            int e = elapsed;
            while (period <= e) e -= period;
            Visible(e * (1f / 9000f) * 5f <= 0.8f);
        }
        else Visible(true);
    }

    // Amedo
    private void InvincibilityEnd_131760()
    {
        InvincibilityEnded?.Invoke(); // Amedo 2026-10-04
        Visible(true);
        MotionWord &= ~0x400;
        State70 &= ~0xF0ul;
    }

    public Func<int, int>? InstanceInt_2619D8; // Amedo
    public Action? ClearTriggers_110; // Amedo
    public Action? IdleSlot_211D30; // Amedo

    // Amedo
    public void Reset_131888()
    {
        BaseVertical = 0f;
        State70 &= ~0xFFul;
        State70 &= ~0x3000ul;
        GroundPoint = Position;
        ClearTriggers_110?.Invoke();
        Jump = new CharacterJump(this);
        Crouch = new CharacterCrouch(this);
        Spin = new CharacterSpin(this);
        ClearSharedTail();
        Pushed = null;
        if (InstanceInt_2619D8?.Invoke(0) != 4 && StandHull is not null) LiftOntoGround_136890(2f, 20f);
        if (Vehicle is null) IdleSlot_211D30?.Invoke();
    }

    // Amedo
    public void ApplyState_131B58()
    {
        uint sf = Self is { } self ? StateFlagsOf?.Invoke(self) ?? 0u : 0u;
        MotionPartReset_140230();
        MotionWord = (MotionWord & ~0x100) | ((sf & 0x200) != 0 ? 0x100 : 0);
        MotionWord = (MotionWord & ~0x200) | ((sf & 0x8000) != 0 ? 0x200 : 0);
        MotionWord = (MotionWord & ~0x400) | ((sf & 0x10000) != 0 ? 0x400 : 0);
        MotionWord = (MotionWord & ~0x1000) | ((sf & 0x20000) != 0 ? 0x1000 : 0);
        MotionFlags = (MotionFlags & ~1) | ((sf & 0x40000) != 0 ? 1 : 0);
        HitPoints = InstanceInt_2619D8?.Invoke(2) ?? HitPoints;
        if ((MotionFlags & 1) != 0) SnapToGround_13D428();
        State70 = ((State70 & ~0xFul) | 2) & ~0xF1ul;
        BaseVertical = 0f;
        State70 &= ~0x3000ul;
        HomeChunk_90 = null;
        GroundPoint = Position;
        DestroyComponents_1351A0();
        CreateComponents_134F18();
        ClearTriggers_110?.Invoke();
        ClearSharedTail();
        AltShape_1F0168(null);
        if (Self is { } owner) SetInstanceFlags?.Invoke(owner, 0x20000);
        Pushed = null;
    }

    // Amedo
    internal void MotionPartReset_140230()
    {
        MotionWord = 3 | 0x1E000 | 0x20000 | 0x40000 | 0x80000;
        MotionFlags = GravityOn;
        MotionFlagsHigh = 0;
        TurnRate = 0;
        Limbs.Accumulated = Vector3.Zero;
        Limbs.Smoothed = Vector3.Zero;
        Forward = Side = Vertical = AccelScale = 0f;
    }

    // Amedo
    private void ClearSharedTail()
    {
        MoveTurn = MoveZ = MoveX = 0f;
        Cross = Square = Circle = RightMinusLeft = 0f;
        InputDisabled = 0;
        Move = Vector3.Zero;
        SpinKickCooldown = 0f;
        SlideKickCooldown = 0f;
        SetSurface_13F658(_world.SurfaceAt(0));
        State70 &= ~0xF00ul;
        StandingOn = null;
        SupportModel = null;
        StandingPart = -1;
        GroundNormal = Vector3.UnitY;
        Velocity = Vector3.Zero;
        Mode270 = 0;
        CenterOffset = 0f;
        Offset27C = 0f;
        PrevCenter_280 = GroundPoint;
        Override1A0 = Vector3.Zero;
        CrushCounter_CC = 0;
    }

    private void Visible(bool on)
    {
        if (_visible == on) return;
        _visible = on;
        SetVisible?.Invoke(on);
    }
}
