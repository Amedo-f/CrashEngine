using System.Numerics;
using CrashEngine.Runtime.AgentLab;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character;

// Amedo
public sealed class CharacterGun
{
    public const int SlotDraw = 0x47, SlotHolster = 0x48, SlotFire = 0x49, SlotCharge = 0x6A, SlotEmpty = 0x6B;
    private const float TicksPerSecond = 9000f;

    private readonly CharacterController _c;

    public ulong Word;
    public int Duration;
    public int StateStart;
    public int LastShot;
    public float Charge = -1f;
    private AgentLabAgent? _weapon;
    public readonly TargetLock[] Locks = [new(), new(), new(), new()];

    public CharacterGun(CharacterController c)
    {
        _c = c;
        Construct_149FB8();
    }

    public int State => (int)(Word & 0xF);
    public int SecondCount => (int)((Word >> 9) & 0xF);
    public int Ammo => (int)((Word >> 13) & 0x7F);
    public AgentLabAgent? Weapon => _weapon is { Destroyed: true } ? _weapon = null : _weapon;

    private void Construct_149FB8()
    {
        _weapon = null;
        foreach (var l in Locks) l.Construct_15D128();
        Reset_15EF20();
        Locks[0].Offset = new Vector4(0f, 0.9f, 0f, 1f);
        Locks[0].SetFlatShape_161CF8(20f, 1.25f, 7.5f);
        Locks[0].AddKind_161DE8(0xD, 3);
        Locks[0].AddKind_161DE8(0x10, 2);
        Locks[0].AddKind_161DE8(0xF, 1);
        Locks[1].Offset = new Vector4(0f, 0.9f, 0f, 1f);
        Locks[1].SetShape_161CC8(50f, 1.25f, 24f);
        Locks[1].AddKind_161DE8(0x10, 2);
        Locks[1].AddKind_161DE8(0xF, 1);
        Locks[2].Offset = new Vector4(0f, 5f, 5f, 1f);
        Locks[2].SetShape_161CC8(40f, 2.5f, 15f);
        Locks[2].AddKind_161DE8(0x10, 2);
        Locks[2].AddKind_161DE8(0xF, 1);
        Locks[3].Offset = new Vector4(0f, 0.9f, 0f, 1f);
        Locks[3].SetFlatShape_161CF8(50f, 10f, 10f);
        Locks[3].AddKind_161DE8(0x10, 1);
    }

    public void Reset_15EF20()
    {
        Duration = 0;
        StateStart = 0;
        Word &= ~0xFFFFFFFFUL;
        Word = (Word & ~0xFUL & ~0xF0UL) | 0x70UL;
        Word = (Word & ~0x1E00UL) | 0xA00UL;
        Word = (Word & ~(0x7FUL << 13)) | (0xFUL << 13);
        LastShot = 0;
        Charge = -1f;
        foreach (var l in Locks) l.Reset_15D178();
    }

    public bool TakeAmmo_15EFE8(int n)
    {
        int ammo = Ammo;
        if (ammo < n)
        {
            Word &= ~(0x7FUL << 13);
            return false;
        }
        Word = (Word & ~(0x7FUL << 13)) | ((ulong)((ammo - n) & 0x7F) << 13);
        return true;
    }

    public bool AddAmmo_15F0A0(int n)
    {
        int ammo = Ammo + n;
        if ((uint)ammo < 100)
        {
            Word = (Word & ~(0x7FUL << 13)) | ((ulong)(ammo & 0x7F) << 13);
            return true;
        }
        Word = (Word & ~(0x7FUL << 13)) | (99UL << 13);
        return false;
    }

    public bool ShotWithin_15F108(int ticks) => !(ticks < _c.Ticks - LastShot);

    private int Seconds(int param) => GameMath.Trunc(_c.Param(param) * TicksPerSecond);

    public int LockKind_14A7C0()
    {
        if (_c.Kind == 5) return 2;
        if (((_c.Progression?.Invoke() ?? 0u) >> 16 & 0x1F) == 0x18 && _c.Kind == 1) return 3;
        return _c.Vehicle is not null ? 1 : 0;
    }

    public AgentLabAgent? Target_14A7C0() => Locks[LockKind_14A7C0()].Target;

    public bool AimPoint_14A860(out Vector3 aim)
    {
        if (Locks[LockKind_14A7C0()].TargetPoint_161DB8(out aim)) return true;
        if ((_c.Self as AgentLabAgent)?.AimSlot118_14A910() is not { } t) return false;
        var (mn, mx) = t.TreeBox;
        aim = (mx - mn) * 0.5f + mn;
        return true;
    }

    public int Shoot_149E08(float charge)
    {
        _c.GunHolder?.Invoke();
        Charge = charge;
        LastShot = _c.Ticks;
        int slot;
        if (0f < charge)
        {
            if (TakeAmmo_15EFE8(5)) { Duration = Seconds(0x34); slot = SlotFire; }
            else { Duration = Seconds(0x36); slot = SlotEmpty; }
        }
        else if (TakeAmmo_15EFE8(1)) { Duration = Seconds(0x35); slot = SlotFire; }
        else { Duration = Seconds(0x37); slot = SlotEmpty; }
        if (_c.TriggerOnChannel is { } onChannel) onChannel(slot, 1);
        else _c.Trigger(slot);
        return 6;
    }

    public void Frame_14A190(float trigger, float aim)
    {
        bool held = 0f < trigger;
        bool aiming = 0f < aim;
        int now = _c.Ticks;
        int lk = LockKind_14A7C0();
        var self = _c.Self as AgentLabAgent;
        if (State != 0 && Weapon is null && self is not null) _weapon = self.GunWeapon_14A2C8(_weapon);
        if (Weapon is { } weapon && State != 0 && aiming && self is not null) Locks[lk].Search_15D250(now, self, weapon);
        else Locks[lk].Drop_15D938();
        if ((Word & 0xF0) != 0x70)
        {
            Word = (Word & ~0xFUL) | ((Word >> 4) & 0xF);
            Word = (Word & ~0xF0UL) | 0x70UL;
            StateStart = now;
        }
        bool elapsed = !(now - StateStart < Duration);
        int next = -1;
        switch (State)
        {
            case 0:
                if (_c.IsActivePlayer?.Invoke() == true)
                {
                    _c.Trigger(SlotDraw);
                    next = 1;
                }
                break;
            case 1:
            case 6:
                if (elapsed) next = 2;
                break;
            case 2:
                if (_c.IsActivePlayer?.Invoke() == true)
                {
                    bool blocked = _c.MotionState is 8 or 0xC;
                    bool free = !blocked && (_c.MotionFlagsHigh & (1u << 9)) == 0;
                    if (held && free) next = 4;
                }
                else
                {
                    _c.Trigger(SlotHolster);
                    next = 3;
                }
                break;
            case 3:
                if (elapsed) next = 0;
                break;
            case 4:
                if (held)
                {
                    if (!(now - StateStart < Seconds(0x32)))
                    {
                        _c.Trigger(SlotCharge);
                        next = 5;
                        Duration = Seconds(0x33);
                    }
                }
                else next = Shoot_149E08(0f);
                break;
            case 5:
                if (elapsed) next = Shoot_149E08(1f);
                _c.SetOverride(CharacterController.ForwardOverride, ref _c.Forward, 0f);
                _c.SetOverride(CharacterController.SideOverride, ref _c.Side, 0f);
                break;
        }
        Word = (Word & ~0x100UL) | (held ? 0x100UL : 0UL);
        if (next != -1) Word = (Word & ~0xF0UL) | ((ulong)(next & 0xF) << 4);
    }
}
