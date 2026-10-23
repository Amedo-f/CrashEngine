using System.Numerics;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed partial class GameCamera
{
    public uint Handle(uint slot) => M.U(slot) is var h && h != 0 ? M.U(h) : 0u;

    public void TargetUpdate_2772B8(uint x, uint time)
    {
        uint inst = Handle(x + 0x74);
        if (inst == 0)
        {
            M.V(x + 0x10, new Vector4(0f, 0f, 0f, 1f));
            M.V(x + 0x20, M.V(0x3234D0) with { W = 1f });
            M.V(x + 0xB0, M.V(0x3234D0) with { W = 1f });
            M.F(x, 0f);
            return;
        }
        var prev = M.V(x + 0x20);
        float dt = M.I(time + 8) * M.F(SecondsPerTick);
        if ((M.Q(x + 0x70) & 0xA000) == 0xA000)
        {
            M.B(x + 0x61, 1);
            M.Q(x + 0x70, M.Q(x + 0x70) & ~0x2000UL);
        }
        if (M.U(x + 0x78) == 0)
        {
            uint node = M.U(inst + 8);
            NodeRefresh_18AFA8(node);
            M.V(x + 0x40, TargetPoint_276C60(x, node));
            M.V(x + 0x30, ToV(CamMath.MatrixToQuat_186900(M.M(node))));
        }
        else
        {
            M.V(x + 0x30, ToV(CamMath.MatrixToQuat_186900(M.M(M.U(x + 0x78))))); // Amedo 2026-09-29
            M.V(x + 0x40, M.V(M.U(x + 0x78) + 0x30));
            M.F(x, 0f);
        }
        M.Copy16(x + 0x10, x + 0x30);
        ulong fl = M.Q(x + 0x70);
        M.Copy16(x + 0x20, x + 0x40);
        if ((fl & 0x11006) == 2)
        {
            M.U(x + 0xD0, M.U(time + 4));
            M.Q(x + 0x70, fl | 4);
            var c0 = M.V(x + 0xC0);
            if ((fl & 0x20000) == 0 && (c0.X - prev.X) * (c0.X - prev.X) + (c0.Y - prev.Y) * (c0.Y - prev.Y) + (c0.Z - prev.Z) * (c0.Z - prev.Z) < 15f)
                M.Q(x + 0x70, fl & ~4UL);
            bool alt = (M.Q(x + 0x70) & 8) != 0;
            uint lo = alt ? x + 0x110 : x + 0xF0, hi = alt ? x + 0x120 : x + 0x100;
            bool same = Same3(x + 0x150, lo) && Same3(x + 0x160, hi);
            M.Q(x + 0x70, M.Q(x + 0x70) | (same ? 0x10000UL : 0x1000UL));
        }
        bool useAlt = (M.Q(x + 0x70) & 8) != 0;
        M.Copy16(x + 0x150, useAlt ? x + 0x110 : x + 0xF0);
        M.Copy16(x + 0x160, useAlt ? x + 0x120 : x + 0x100);
        if ((M.Q(x + 0x70) & 0x1000) != 0)
        {
            int el = M.I(time + 4) - M.I(x + 0xD0);
            if (el < M.I(x + 0xD4))
            {
                float t = el * M.F(SecondsPerTick) / (M.I(x + 0xD4) * M.F(SecondsPerTick));
                for (uint k = 0; k < 12; k += 4)
                {
                    M.F(x + 0x150 + k, (M.F(x + 0x150 + k) - M.F(x + 0x130 + k)) * t + M.F(x + 0x130 + k));
                    M.F(x + 0x160 + k, (M.F(x + 0x160 + k) - M.F(x + 0x140 + k)) * t + M.F(x + 0x140 + k));
                }
                M.F(x + 0x15C, 1f);
                M.F(x + 0x16C, 1f);
            }
            else M.Q(x + 0x70, (M.Q(x + 0x70) & ~0x1000UL) | 0x10000);
        }
        float kh = M.F(x + 0x90);
        for (uint k = 0; k < 12; k += 4)
            M.F(x + 0xA0 + k, M.F(x + 0x150 + k) + (M.F(x + 0x160 + k) - M.F(x + 0x150 + k)) * kh);
        M.F(x + 0xAC, 1f);
        if ((M.Q(x + 0x70) & 0x20) == 0) M.V(x + 0xB0, TargetHeight_276B40(dt, x, M.V(x + 0x20)));
        else M.Copy16(x + 0xB0, x + 0x20);
        fl = M.Q(x + 0x70);
        Vector4 off;
        if ((fl & 0x40000) != 0 || (fl & 0x90000) == 0x80000) off = M.V(x + 0xA0);
        else off = CamMath.RotateByQuat_186D98(ToQ(M.V(x + 0x10)), M.V(x + 0xA0), false);
        M.F(x + 0xB0, M.F(x + 0xB0) + off.X);
        M.F(x + 0xB4, M.F(x + 0xB4) + off.Y);
        M.F(x + 0xB8, M.F(x + 0xB8) + off.Z);
        fl = M.Q(x + 0x70);
        if ((fl & 0x200) != 0) M.Copy16(x + 0xC0, x + 0xB0);
        if ((fl & 4) == 0) M.Copy16(x + 0x20, (fl & 1) != 0 ? x + 0xC0 : x + 0xB0);
        else
        {
            int el = M.I(time + 4) - M.I(x + 0xD0);
            if (el < M.I(x + 0xD4))
            {
                float t = el * M.F(SecondsPerTick) / (M.I(x + 0xD4) * M.F(SecondsPerTick));
                if ((fl & 0x1C0) == 0x80) t = t * t * t;
                M.Copy16(x + 0x20, x + 0xB0);
                M.F(x + 0x20, prev.X + (M.F(x + 0xC0) - prev.X) * t);
                M.F(x + 0x24, prev.Y + (M.F(x + 0xC4) - prev.Y) * t);
                M.F(x + 0x2C, 1f);
                M.F(x + 0x28, prev.Z + (M.F(x + 0xC8) - prev.Z) * t);
            }
            else
            {
                M.Copy16(x + 0x20, x + 0xC0);
                if (M.U(x + 0x7C) == 0) M.Copy16(x + 0xB0, x + 0xC0);
            }
        }
        M.Q(x + 0x70, M.Q(x + 0x70) | 0x8000);
    }

    private bool Same3(uint a, uint b) => M.F(a) == M.F(b) && M.F(a + 4) == M.F(b + 4) && M.F(a + 8) == M.F(b + 8);

    public void NodeRefresh_18AFA8(uint node)
    {
        if ((M.Q(node + 0x60) & 2) != 0)
        {
            var q = M.V(node + 0x50);
            float inv = 1f / (q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
            q *= inv;
            M.V(node + 0x50, q);
            var m = CamMath.QuatToMatrix_18E620(ToQ(q));
            var keep = M.V(node + 0x30);
            M.M(node, m);
            M.V(node + 0x30, keep);
            M.Q(node + 0x60, M.Q(node + 0x60) & ~2UL);
        }
        if ((M.Q(node + 0x60) & 1) != 0)
        {
            M.Copy16(node + 0x30, node + 0x40);
            M.Q(node + 0x60, M.Q(node + 0x60) & ~1UL);
        }
    }

    public Vector4 TargetPoint_276C60(uint x, uint node)
    {
        uint zone = M.U(x + 0x7C);
        if (zone == 0 || (M.Q(x + 0x70) & 0x10) == 0) return M.V(node + 0x30);
        int n = M.B(zone + 0x19);
        if (n == 0) return M.V(node + 0x30);
        Vector3 sum = Vector3.Zero;
        for (int i = 0; i < n; i++)
        {
            uint other = M.U(M.U(zone + 0xE4 + (uint)i * 4) + 8);
            NodeRefresh_18AFA8(other);
            sum += new Vector3(M.F(other + 0x30), M.F(other + 0x34), M.F(other + 0x38));
        }
        var basePos = M.V(node + 0x30);
        float k = M.F(x + 0x80), maxLen = M.F(x + 0x84);
        var d = (sum * (1f / n) - new Vector3(basePos.X, basePos.Y, basePos.Z)) * k;
        float len = d.Length();
        M.F(x, len);
        if (maxLen < len)
        {
            d *= maxLen / len;
            M.F(x, 1f);
        }
        else M.F(x, len / maxLen);
        return new Vector4(basePos.X + d.X, basePos.Y + d.Y, basePos.Z + d.Z, basePos.W);
    }

    public Vector4 TargetHeight_276B40(float dt, uint x, Vector4 p)
    {
        float floor = M.F(x + 0x88), smooth = M.F(x + 0x8C), band = M.F(x + 0xA4);
        float y = p.Y;
        if (smooth < floor)
        {
            float lowered = y - band, rise = dt * 5f + smooth;
            if (lowered <= smooth || band <= 5e-5f) M.F(x + 0x8C, rise < floor ? rise : floor);
            else M.F(x + 0x8C, lowered);
        }
        else
        {
            float up = y + 0.1f;
            if (up < floor) M.F(x + 0x8C, floor);
            else
            {
                float top = up - band;
                if (smooth < top) M.F(x + 0x8C, band < 5e-5f ? y : top);
                else if (up < smooth) M.F(x + 0x8C, up);
            }
        }
        return new Vector4(p.X, M.F(x + 0x8C), p.Z, 1f);
    }

    private static bool Blend(ulong zoneFlags) => ((zoneFlags >> 5) & 1) == 0;

    public float TargetZone_277A88(uint x, uint zone)
    {
        float result = 0f;
        if (zone != 0 && ((M.Q(ZoneData(zone)) >> 34) & 1) != 0)
        {
            M.U(x + 0x180, 0);
            M.Q(x + 0x70, M.Q(x + 0x70) & 0xFFFFFFFFFFEFFFFFUL);
            M.Q(x + 0x180, (M.Q(x + 0x180) & ~0x10UL) | 0x10);
            TargetReset_27C128(x);
        }
        if ((M.Q(x + 0x70) & 0x100000) != 0)
        {
            TargetZoneApply_277D68(x, x + 0x180); // Amedo
        }
        else
        {
            uint old = M.U(x + 0x170);
            if (old != zone)
            {
                ulong f = (M.Q(x + 0x70) & 0xFFFFFFFFFFF9CDE0UL) | 0x18000;
                M.B(x + 0x61, 1);
                M.Q(x + 0x70, f);
                if (old != 0 && zone == 0)
                {
                    uint zd = ZoneData(old);
                    f |= 0x200;
                    bool blend = Blend(M.Q(zd));
                    M.Q(x + 0x70, f);
                    if (blend && (f & 2) == 0)
                    {
                        M.Copy16(x + 0xE0, x + 0x20);
                        M.I(x + 0xD4, CamMath.Trunc(M.F(zd + 0x10) * M.F(TicksPerSecond)));
                        M.Copy16(x + 0x130, x + 0x150);
                        M.Copy16(x + 0x140, x + 0x160);
                        f = (f & 0xFFFFFFFFFFFDFFFFUL) | (((M.Q(zd) >> 25) & 1) << 17);
                        M.Q(x + 0x70, f & 0xFFFFFFFFFFFEFFFFUL);
                    }
                    f = (M.Q(x + 0x70) | 1) & ~2UL | ((blend ? 1UL : 0UL) << 1);
                    M.Q(x + 0x70, f);
                    M.Q(x + 0x70, (f & 0xFFFFFFFFFFF7FFFFUL) | (((M.Q(zd) >> 28) & 1) << 19));
                }
                else
                {
                    ulong zf = M.Q(ZoneData(zone));
                    bool snap = !Blend(zf) || (f & 0x4000) != 0;
                    f = ((f & ~0x2000UL) | ((snap ? 1UL : 0UL) << 13)) & 0xFFFFFFFFFFFF7FFFUL;
                    M.Q(x + 0x70, f);
                    M.B(x + 0x61, (byte)(snap ? 0 : 1));
                    uint prev = M.U(x + 0x170);
                    f &= 0xFFFFFFFFFFF7FFFFUL;
                    if (prev != 0) f |= ((M.Q(ZoneData(prev)) >> 28) & 1) << 19;
                    M.Q(x + 0x70, f);
                }
            }
            if (zone != 0) result = TargetZoneApply_277D68(x, ZoneData(zone));
        }
        uint last = M.U(x + 0x7C);
        M.Q(x + 0x70, M.Q(x + 0x70) & ~0x4000UL);
        M.U(x + 0x7C, zone);
        M.U(x + 0x170, last);
        return result;
    }

    public float TargetZoneApply_277D68(uint x, uint zd)
    {
        ulong zf = M.Q(zd);
        ulong f = M.Q(x + 0x70);
        f = (f & ~8UL) | (((zf >> 8) & 1) << 3);
        f = (f & ~0x10UL) | (((zf >> 9) & 1) << 4);
        f = (f & 0xFFFFFFFFFFFBFFFFUL) | (((zf >> 28) & 1) << 18);
        M.Q(x + 0x70, f);
        bool blend = Blend(zf);
        float result = 0f;
        uint sub = M.U(zd + 8);
        if (sub != 0) result = Sub(sub)?.Compute_14(this, x + 0xB0, x, x + 0xC0) ?? 0f; // Amedo 2026-09-29
        else M.Q(x + 0x70, M.Q(x + 0x70) | 0x200);
        if ((M.Q(x + 0x70) & 8) != 0)
        {
            M.Copy16(x + 0x110, zd + 0x20);
            M.Copy16(x + 0x120, zd + 0x30);
        }
        if (blend && (M.Q(x + 0x70) & 2) == 0)
        {
            ulong g = M.Q(x + 0x70) & 0xFFFFFFFFFFFDFFFFUL;
            M.Copy16(x + 0xE0, x + 0x20);
            M.I(x + 0xD4, CamMath.Trunc(M.F(zd + 0x10) * M.F(TicksPerSecond)));
            M.Copy16(x + 0x130, x + 0x150);
            M.Copy16(x + 0x140, x + 0x160);
            g |= ((M.Q(zd) >> 25) & 1) << 17;
            M.Q(x + 0x70, g & 0xFFFFFFFFFFFEFFFFUL);
        }
        f = (M.Q(x + 0x70) | 1) & ~2UL | ((blend ? 1UL : 0UL) << 1);
        M.Q(x + 0x70, f);
        if ((f & 0x10) != 0)
        {
            M.U(x + 0x80, M.U(zd + 0x44));
            M.U(x + 0x84, M.U(zd + 0x40));
        }
        return result;
    }

    public void TargetReset_27C128(uint x)
    {
        M.B(x + 0x61, 1);
        M.Q(x + 0x70, (M.Q(x + 0x70) & 0xFFFFFFFFFFF9CDE0UL) | 0x18000);
    }
}
