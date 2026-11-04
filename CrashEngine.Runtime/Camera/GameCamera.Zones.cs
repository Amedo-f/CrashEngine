using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed partial class GameCamera
{
    public void ZoneRequestReset_15E9D8(uint p)
    {
        M.U(p + 4, M.U(p + 8));
        M.U(p + 0x14, 0);
        M.U(p + 8, 0);
    }

    public void ZoneRequest_15E9F0(uint p, uint zone, uint priority, uint c)
    {
        bool take = M.U(p + 8) == 0 || M.U(p + 0x14) < priority || (priority == M.U(p + 0x14) && M.U(p + 4) == zone);
        if (!take || !ZoneAllowed_143530(p, zone, c)) return;
        M.U(p + 0x14, priority);
        M.U(p + 8, zone);
    }

    public bool ZoneAllowed_143530(uint p, uint zone, uint c)
    {
        uint zd = ZoneData(zone);
        ulong zf = M.Q(zd);
        bool ok = true;
        if (((zf >> 26) & 1) != 0 && M.U(p + 0xC) == 0) ok = false;
        if (((zf >> 32) & 1) != 0 && (M.Q(p) & 0x40) == 0) ok = false;
        byte group = M.B(zd + 0x80);
        if (group != 0 && M.U(p + 0xC) != 0)
        {
            byte other = M.B(ZoneData(M.U(p + 0xC)) + 0x80);
            if (other != 0 && other != group) ok = false;
        }
        if (!ok) { M.U(p + 0x10, 0); return false; }
        bool grounded = ((M.I(M.U(c + 0x10) + 0x14) >> 2) & 1) != 0;
        if (M.U(c + 0xB8) == 0 && zone != M.U(p + 0xC) && ((zf >> 27) & 1) == 0 && !grounded && ((M.Q(p + 0x350) >> 15) & 1) == 0) ok = false;
        if (!ok) { M.U(p + 0x10, 0); return false; }
        if (Sub(M.U(zd + 0xC)) is { Kind: SubCamera.KindSpecial }) Missing("0x143530 sub-camera 0x1C0E (+0x25)"); // Amedo 2026-09-29
        M.U(p + 0x10, ((zf >> 32) & 1) != 0 ? zone : 0u);
        return true;
    }

    public void EyeZone_2742C8(float u, uint y, uint zone, uint x)
    {
        if (zone != 0 && ((M.Q(ZoneData(zone)) >> 34) & 1) != 0)
        {
            M.U(y + 0x2F0, 0);
            M.Q(y + 0x40, M.Q(y + 0x40) & ~0x800UL);
            M.Q(y + 0x2F0, (M.Q(y + 0x2F0) & ~0x10UL) | 0x10);
            EyeZoneReset_274170(y);
        }
        ulong f40 = M.Q(y + 0x40);
        if ((f40 & 0x800) != 0)
        {
            EyeZoneApply_2745B0(u, y, y + 0x2F0, x);
        }
        else
        {
            if (zone != 0)
            {
                ulong zf = M.Q(ZoneData(zone));
                ulong a1 = (f40 & ~0x20UL) | (((zf >> 11) & 1) << 5);
                M.Q(y + 0x40, a1);
                bool reset = false;
                if ((a1 & 0x20) != 0) M.Q(y + 0x40, a1 & ~0x80UL);
                else if ((a1 & 0x80) != 0)
                {
                    if ((a1 & 0x48) == 0x40)
                    {
                        M.Q(y + 0x48, M.Q(y + 0x48) | 0x100);
                        M.Q(y + 0x40, a1 & ~0x80UL);
                        reset = true;
                    }
                }
                else if ((a1 & 0x88) == 8)
                {
                    M.Q(y + 0x48, M.Q(y + 0x48) & ~0x100UL);
                    M.Q(y + 0x40, a1 | 0x80);
                    reset = true;
                }
                if (reset) EyeZoneReset_274170(y);
            }
            uint old = M.U(y + 0x2EC);
            if (zone != old)
            {
                EyeZoneReset_274170(y);
                if (zone == 0) EyeZoneLeave_275498(y, old, true);
                else
                {
                    uint zd = ZoneData(zone);
                    ulong snap = !Blend(M.Q(zd)) || (M.Q(y + 0x40) & 0x8000) != 0 ? 1UL : 0UL;
                    ulong f48 = M.Q(y + 0x48);
                    M.Q(y + 0x48, (f48 & ~0x40UL) | (snap << 6));
                    if (M.U(y + 0x2EC) != 0)
                    {
                        ulong keep = 0;
                        if (((M.Q(ZoneData(M.U(y + 0x2EC))) >> 24) & 1) != 0) keep = (M.Q(zd) >> 24) & 1;
                        M.Q(y + 0x48, (f48 & ~0x40UL) | ((snap | keep) << 6));
                    }
                    M.Q(y + 0x48, M.Q(y + 0x48) & ~0x80UL);
                    if (Sub(M.U(zd + 0xC)) is { } sub) // Amedo 2026-09-29
                    {
                        sub.Enter_24(this, y + 0x20, M.U(y + 0x50) + 0x20);
                        if (sub.Kind == SubCamera.KindSpecial) M.B(y + 0x3CC, 1);
                    }
                }
            }
            if (zone != 0) EyeZoneApply_2745B0(u, y, ZoneData(zone), x);
        }
        f40 = M.Q(y + 0x40);
        M.U(y + 0x2EC, zone);
        M.Q(y + 0x40, (f40 & ~0x10UL) | (((f40 >> 3) & 1) << 4));
    }

    public void EyeZoneReset_274170(uint y)
    {
        EyeRestoreSpeeds_27BDB0(y);
        M.Q(y + 0x40, (M.Q(y + 0x40) & 0xFFFFFFFEFFCC89DAUL) | 0x800000000000UL);
        M.Q(y + 0x48, (M.Q(y + 0x48) & 0xFFFFFFFFFFFFF0BCUL) | 0x80);
        M.U(y + 0x54, M.U(y + 0x54) & 0xFFFFFF7F);
        M.U(y + 0xA4, M.U(y + 0xA4) & 0xFFFFFF7F);
        M.U(y + 0x144, M.U(y + 0x144) & 0xFFFFFF7F);
        M.U(y + 0xF4, M.U(y + 0xF4) & 0xFFFFFB7F);
        M.F(y + 0x38C, 6f);
        M.F(y + 0x398, 1f);
        M.B(y + 0x31, 0);
        M.B(y + 0x30, 0);
    }

    private void ZoneDuration(uint y, uint zd)
    {
        if ((M.Q(y + 0x48) & 1) != 0) return;
        float seconds = (M.Q(y + 0x48) & 0x100) != 0 ? 2f : M.F(zd + 0x10);
        M.I(y + 0x1C4, GameMath.Trunc(seconds * M.F(TicksPerSecond)));
    }

    private int Mix(int a, int b, float k) =>
        GameMath.Trunc((b * GameMath.UnitToRad * k + a * GameMath.UnitToRad * (1f - k)) * GameMath.RadToUnit);

    public void EyeZoneApply_2745B0(float u, uint y, uint zd, uint x)
    {
        uint sub2 = M.U(zd + 0xC);
        uint sub1 = M.U(zd + 8);
        if (M.B(y + 0x3CC) != 0) { Missing("0x2768B0 eye sub-camera follow"); return; }
        ulong zf = M.Q(zd);
        bool direct = ((zf >> 10) & 1) == 0 || x == 0;
        ulong f = M.Q(y + 0x40);
        ulong b12 = ((((f >> 12) & 1) | ((zf >> 21) & 1)) << 12);
        ulong b14 = ((((f >> 14) & 1) | ((zf >> 19) & 1)) << 14);
        ulong b13 = ((((f >> 13) & 1) | ((zf >> 20) & 1)) << 13);
        M.Q(y + 0x48, (M.Q(y + 0x48) & ~0x800UL) | (((zf >> 29) & 1) << 11));
        f = (f & 0xFFFFFFFFFFCF8FFFUL) | b12 | b13 | b14 | (((zf >> 30) & 1) << 20) | (((zf >> 31) & 1) << 21);
        M.Q(y + 0x40, f);
        bool blend = Blend(zf);
        uint prevZone = M.U(y + 0x2EC);
        if (prevZone != 0 && ((zf >> 24) & 1) != 0 && ((M.Q(ZoneData(prevZone)) >> 24) & 1) != 0) blend = false;
        bool active = (M.Q(y + 0x48) & 0x100) != 0 || blend;
        if (active) ZoneDuration(y, zd);
        bool idle = (M.Q(y + 0x48) & 0x600) == 0;
        bool alt = ZoneAltYaw_278F30(zd, M.I(y + 0x108));
        float k = x != 0 ? M.F(x) : 0f;

        if (((zf >> 7) & 1) == 0) M.U(y + 0xA4, M.U(y + 0xA4) & 0xFFFFFF7F);
        else
        {
            int a = M.I(zd + 0x48), b = M.I(zd + 0x4C);
            if (direct)
            {
                if (active && idle) ZoneRate_270BD8(y + 0xA4, y + 0x1C4, a, b);
                M.U(y + 0xA4, M.U(y + 0xA4) | 0x80);
                M.I(y + 0xE0, a);
                M.I(y + 0xE4, b);
            }
            else
            {
                M.U(y + 0xA4, M.U(y + 0xA4) | 0x80);
                int v = Mix(a, b, k);
                M.I(y + 0xE4, v);
                M.I(y + 0xE0, v);
            }
        }

        if (((zf >> 3) & 1) == 0) M.U(y + 0x144, M.U(y + 0x144) & 0xFFFFFF7F);
        else
        {
            float lo, hi;
            if (alt && ((zf >> 18) & 1) != 0) lo = hi = M.F(zd + 0x7C);
            else { lo = M.F(zd + 0x60); hi = M.F(zd + 0x64); }
            if (direct)
            {
                if (active && idle)
                {
                    float d1 = lo - M.F(y + 0x158), d2 = hi - M.F(y + 0x158);
                    if (d2 <= d1) d1 = d2;
                    M.U(y + 0x170, M.U(y + 0x168));
                    M.F(y + 0x168, MathF.Abs(d1 / (M.I(y + 0x1C4) * M.F(SecondsPerTick))));
                }
                M.U(y + 0x144, M.U(y + 0x144) | 0x80);
                M.F(y + 0x184, hi);
                M.F(y + 0x180, lo);
            }
            else
            {
                M.U(y + 0x144, M.U(y + 0x144) | 0x80);
                float v = hi * k + lo * (1f - k);
                M.F(y + 0x184, v);
                M.F(y + 0x180, v);
            }
        }

        ulong f10 = M.Q(y + 0x40);
        ulong g = (f10 & 0xFFFFFFFFFFFEFFFFUL) | ((((zf >> 22) & 1) ^ 1) << 16);
        ulong b32 = ((zf >> 23) & 1) << 32;
        g = (g & 0xFFFFFFFEFFFFFFFFUL) | b32;
        if (((f10 & 0x80) | b32) == 0x100000080UL) g = (g & 0xFFFFFFFEFFFFFFFFUL) | ((((zf >> 33) & 1) ^ 1) << 32);
        M.Q(y + 0x40, g);
        if ((M.Q(y + 0x40) & 0x80) == 0)
        {
            M.Q(y + 0x40, (M.Q(y + 0x40) & 0xFFFF7FFFFFFFFFFBUL) | (((zf >> 4) & 1) << 47) | ((sub1 != 0 ? 1UL : 0UL) << 2));
            if (sub2 != 0)
            {
                var sub = Sub(sub2); // Amedo 2026-09-29
                if ((M.B(zd) & 1) != 0) sub?.At_1C(this, u, y + 0x1A0);
                else if (x != 0) sub?.Compute_14(this, sub1 == 0 ? x + 0x20 : x + 0x40, x, y + 0x1A0);
                M.Q(y + 0x48, M.Q(y + 0x48) & ~0x100UL);
            }

            if (((zf >> 2) & 1) == 0) M.U(y + 0x54, M.U(y + 0x54) & 0xFFFFFF7F);
            else
            {
                int a, b;
                if (alt && ((zf >> 17) & 1) != 0) a = b = M.I(zd + 0x78);
                else { a = M.I(zd + 0x50); b = M.I(zd + 0x54); }
                if (direct)
                {
                    if (active && idle) ZoneRate_270BD8(y + 0x54, y + 0x1C4, a, b);
                    M.U(y + 0x54, M.U(y + 0x54) | 0x80);
                    M.I(y + 0x90, a);
                    M.I(y + 0x94, b);
                }
                else
                {
                    M.U(y + 0x54, M.U(y + 0x54) | 0x80);
                    int v = Mix(a, b, k);
                    M.I(y + 0x94, v);
                    M.I(y + 0x90, v);
                }
            }

            if (((zf >> 6) & 1) == 0) M.U(y + 0xF4, M.U(y + 0xF4) & 0xFFFFFF7F);
            else
            {
                int a, b;
                if (alt && ((zf >> 16) & 1) != 0) a = b = M.I(zd + 0x74);
                else { a = M.I(zd + 0x58); b = M.I(zd + 0x5C); }
                if (direct)
                {
                    if (active && idle) ZoneRate_270BD8(y + 0xF4, y + 0x1C4, a, b);
                    M.U(y + 0xF4, M.U(y + 0xF4) | 0x80);
                    M.I(y + 0x130, a);
                    M.I(y + 0x134, b);
                    if (a == b) M.B(y + 0x31, 1);
                }
                else
                {
                    M.U(y + 0xF4, M.U(y + 0xF4) | 0x80);
                    int v = Mix(a, b, k);
                    M.I(y + 0x134, v);
                    M.I(y + 0x130, v);
                }
            }

            if (((zf >> 15) & 1) != 0)
            {
                M.U(y + 0xF4, M.U(y + 0xF4) | 0x400);
                M.I(y + 0x118, M.I(zd + 0x70));
                M.Q(y + 0x40, M.Q(y + 0x40) | 0x400);
            }
            if (((zf >> 12) & 1) != 0)
            {
                M.I(y + 0x38C, M.I(zd + 0x68));
                M.Q(y + 0x40, M.Q(y + 0x40) | 0x20000);
            }
            if (sub2 != 0 || M.B(y + 0x31) != 0) M.Q(y + 0x40, M.Q(y + 0x40) | 0x100000);
        }
        if (sub2 != 0 && (M.Q(y + 0x40) & 0x80) == 0)
        {
            M.Q(y + 0x40, M.Q(y + 0x40) | 1);
            M.Q(y + 0x48, (M.Q(y + 0x48) & ~1UL) | (active ? 1UL : 0UL));
        }
        if (active && (M.Q(y + 0x48) & 0x600) == 0) M.Q(y + 0x48, M.Q(y + 0x48) | 0x200);
    }

    public void EyeZoneLeave_275498(uint y, uint zone, bool mark)
    {
        uint zd = ZoneData(zone);
        uint sub2 = M.U(zd + 0xC);
        bool active = (M.Q(y + 0x48) & 0x100) != 0 || Blend(M.Q(zd));
        if (active) ZoneDuration(y, zd);
        ulong zf = M.Q(zd);
        bool idle() => (M.Q(y + 0x48) & 0x600) == 0;
        if (((zf >> 7) & 1) != 0 && active && idle())
        {
            int fov = M.I(0x30A450);
            ZoneRate_270BD8(y + 0xA4, y + 0x1C4, fov, fov);
        }
        if (((zf >> 2) & 1) != 0 || sub2 != 0)
        {
            int pitch = GameMath.FromValue_11BFA0(0f, 1);
            if ((M.Q(y + 0x40) & 0x100) != 0) pitch = M.I(0x30A658);
            if (active && idle()) ZoneRate_270BD8(y + 0x54, y + 0x1C4, pitch, pitch);
        }
        if ((((zf >> 3) & 1) != 0 || sub2 != 0) && active && idle())
        {
            M.U(y + 0x170, M.U(y + 0x168));
            M.F(y + 0x168, MathF.Abs((10f - M.F(y + 0x158)) / (M.I(y + 0x1C4) * M.F(SecondsPerTick))));
        }
        if ((((zf >> 6) & 1) != 0 || sub2 != 0) && active && idle())
        {
            int yaw = GameMath.FromValue_11BFA0(10f, 0);
            ZoneRate_270BD8(y + 0xF4, y + 0x1C4, yaw, yaw);
        }
        if (mark && active && idle()) M.Q(y + 0x48, M.Q(y + 0x48) | 0x200);
    }

    public void ZoneRate_270BD8(uint c, uint ticks, int a, int b)
    {
        int cur = M.I(c + 0x14);
        int db = GameMath.Wrap16(b - cur);
        int da = GameMath.Wrap16(a - cur);
        if (da < db) db = da;
        M.U(c + 0x2C, M.U(c + 0x24));
        int t = M.I(ticks);
        int rate = t == 0 ? db : GameMath.Divide_11C008(t * M.F(SecondsPerTick), db);
        M.I(c + 0x24, Math.Abs(rate));
    }

    public static bool ZoneAltYaw_278F30(uint zd, int yaw, CamMemory m)
    {
        int w = GameMath.Wrap16(yaw);
        int toMain = Math.Abs(GameMath.Wrap16(w - m.I(zd + 0x58)));
        int toAlt = Math.Abs(GameMath.Wrap16(w - m.I(zd + 0x74)));
        return toAlt <= toMain;
    }

    private bool ZoneAltYaw_278F30(uint zd, int yaw) => ZoneAltYaw_278F30(zd, yaw, M);
}
