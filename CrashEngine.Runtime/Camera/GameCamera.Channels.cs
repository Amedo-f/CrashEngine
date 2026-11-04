using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed partial class GameCamera
{
    private static uint W(uint c, int word) => c + (uint)word * 4;

    public void FilterSnap_27D6F8(uint filter, uint value) => M.Copy16(filter + 0x10, value);

    public void FilterUpdate_27A0C0(uint filter, uint time, uint target)
    {
        if ((M.B(time) & 1) == 0) return;
        ulong fl = M.Q(filter);
        if ((fl & 0x10000000000UL) != 0 && M.F(filter + 0x20) < 100f) M.F(filter + 0x20, M.F(filter + 0x20) + 1f);
        float t = M.I(time + 8) * M.F(SecondsPerTick) * M.F(filter + 0x20);
        if (1f <= t)
        {
            M.Copy16(filter + 0x10, target);
            M.Copy16(target, filter + 0x10);
            return;
        }
        uint mode = M.U(filter + 4) & 0xF;
        t = mode switch { 1 => t, 0 => 1f, 2 => t * t, 3 => MathF.Sqrt(t), _ => 0f };
        float x = M.F(filter + 0x10), y = M.F(filter + 0x14), z = M.F(filter + 0x18);
        M.F(filter + 0x10, x + (M.F(target) - x) * t);
        M.F(filter + 0x14, y + (M.F(target + 4) - y) * t);
        M.F(filter + 0x1C, 1f);
        M.F(filter + 0x18, z + (M.F(target + 8) - z) * t);
        M.Copy16(target, filter + 0x10);
    }

    public void FilterDefaults_27D858(uint filter)
    {
        ulong fl = M.Q(filter);
        M.Q(filter, (fl & 0xFFFFFFF0FFFFFFFFUL) | (((fl >> 36) & 0xF) << 32));
        if ((fl & 0x10000000000UL) == 0) M.F(filter + 0x20, M.F(filter + 0x24));
    }

    public void FilterFromZone_27D708(uint filter, uint p)
    {
        ulong fl = M.Q(filter);
        if (p == 0 || (M.U(p) & 3) == 1)
        {
            M.Q(filter, (fl & 0xFFFFFFF0FFFFFFFFUL) | (((fl >> 36) & 0xF) << 32));
            if ((fl & 0x10000000000UL) == 0) M.F(filter + 0x20, M.F(filter + 0x24));
            return;
        }
        if ((M.U(p) & 3) == 2)
        {
            M.Q(filter, (fl & 0xFFFFFFF0FFFFFFFFUL) | 0x100000000UL);
            if ((fl & 0x10000000000UL) == 0) M.U(filter + 0x20, M.U(p + 4));
            return;
        }
        M.Q(filter, fl & 0xFFFFFFF0FFFFFFFFUL);
    }

    public void FilterRate_27D7D8(float rate, uint filter)
    {
        ulong fl = M.Q(filter);
        if (0f <= rate)
        {
            M.Q(filter, (fl & 0xFFFFFFF0FFFFFFFFUL) | 0x100000000UL);
            if ((fl & 0x10000000000UL) == 0) M.F(filter + 0x20, rate);
        }
        else M.Q(filter, fl & 0xFFFFFFF0FFFFFFFFUL);
    }

    public void FilterAccelerate_27D5D0(uint filter, bool on) =>
        M.Q(filter, (M.Q(filter) & 0xFFFFFEFFFFFFFFFFUL) | ((on ? 1UL : 0UL) << 40));

    public void ChannelClamp_270360(uint c, int min, int max)
    {
        int cur = M.I(W(c, 5));
        int v = cur + M.I(W(c, 6));
        uint fl = M.U(c);
        uint atMin = min < v ? 0u : 1u;
        uint atMax = v < max ? 0u : 1u;
        M.U(c, (fl & 0xFFFFFF9F) | (atMin << 5) | (atMax << 6));
        if (atMin == 0)
        {
            if (atMax != 0 && cur <= max) M.I(W(c, 6), max - cur);
        }
        else if (min <= cur) M.I(W(c, 6), min - cur);
        if (min == max) M.F(W(c, 4), 0.5f);
        else if ((M.U(c) & 0x20) != 0) M.F(W(c, 4), 0f);
        else if ((M.U(c) & 0x40) != 0) M.F(W(c, 4), 1f);
        else M.F(W(c, 4), max - min != 0 ? (float)(v - min) / (max - min) : 0.5f);
    }

    public bool ChannelManual_270818(float dt, uint c, int input, bool clamp)
    {
        uint fl = M.U(c);
        if ((fl & 8) == 0) return ((fl >> 4) & 1) != 0;
        M.I(W(c, 6), GameMath.Trunc(input * dt));
        if (clamp)
        {
            bool alt = (M.U(c) & 0x80) != 0;
            ChannelClamp_270360(c, M.I(W(c, alt ? 15 : 13)), M.I(W(c, alt ? 16 : 14)));
        }
        M.U(c, M.U(c) | 0x10);
        return ((M.U(c) >> 4) & 1) != 0;
    }

    public bool ChannelApproach_2704B8(float speed, uint c, int target, int targetLow, bool snap)
    {
        int dead = M.I(0x30A628);
        bool moving = true;
        int diff = GameMath.Wrap16(target - M.I(W(c, 5)));
        bool clampTo;
        if (dead < diff)
        {
            if (snap) { M.I(W(c, 6), diff); goto Ratio; }
            M.I(W(c, 6), GameMath.Trunc(M.I(W(c, 9)) * speed));
            if ((M.U(c) & 0x404) == 0x400)
                M.I(W(c, 6), GameMath.Trunc(M.I(W(c, 6)) * MathF.Abs(GameMath.Sin_18B8C0(diff))));
            clampTo = diff < M.I(W(c, 6));
        }
        else
        {
            diff = GameMath.Wrap16(targetLow - M.I(W(c, 5)));
            if (-dead <= diff)
            {
                M.I(W(c, 6), 0);
                moving = false;
                goto Ratio;
            }
            if (snap) { M.I(W(c, 6), diff); goto Ratio; }
            M.I(W(c, 6), GameMath.Trunc(-M.I(W(c, 9)) * speed));
            if ((M.U(c) & 0x404) == 0x400)
                M.I(W(c, 6), GameMath.Trunc(M.I(W(c, 6)) * MathF.Abs(GameMath.Sin_18B8C0(diff))));
            clampTo = M.I(W(c, 6)) < diff;
        }
        if (clampTo) M.I(W(c, 6), diff);
    Ratio:
        int span = M.I(W(c, 14)) - M.I(W(c, 13));
        M.F(W(c, 4), span != 0 ? (float)(M.I(W(c, 5)) + M.I(W(c, 6)) - M.I(W(c, 13))) / span : 0.5f);
        return moving;
    }

    public bool ChannelStep_2708D0(uint c, uint time, int target, bool clamp, bool snap)
    {
        uint fl = M.U(c);
        float dt = M.I(time + 8) * M.F(SecondsPerTick);
        if ((fl & 8) == 0) return false;
        if ((fl & 0x10) != 0) { M.U(c, fl & 0xFFFFFFF9); return false; }
        if ((fl & 0x80) != 0)
        {
            bool mv = ChannelApproach_2704B8(dt, c, M.I(W(c, 15)), M.I(W(c, 16)), snap);
            uint f2 = (M.U(c) & 0xFFFFFFFD) | ((mv ? 1u : 0u) << 1);
            M.U(c, f2);
            if (!mv)
            {
                int v = GameMath.Scale_15DE90(M.F(W(c, 2)), M.I(W(c, 10)));
                v = GameMath.Scale_15DE90(dt, v);
                M.U(c, M.U(c) & 0xFFFFFFFB);
                M.I(W(c, 6), v);
            }
            else M.U(c, f2 | 4);
            return true;
        }
        if ((fl & 0x100) != 0)
        {
            float k = M.F(W(c, 12));
            int blend = GameMath.Trunc((M.I(W(c, 14)) * GameMath.UnitToRad * k + M.I(W(c, 13)) * GameMath.UnitToRad * (1f - k)) * GameMath.RadToUnit);
            bool mv = ChannelApproach_2704B8(dt, c, blend, blend, snap);
            M.U(c, (M.U(c) & 0xFFFFFFFD) | ((mv ? 1u : 0u) << 1) | 4);
            return true;
        }
        bool returning = 0f < M.F(W(c, 1));
        uint f3 = (fl & 0xFFFFFFFD) | ((returning ? 1u : 0u) << 1);
        M.U(c, f3);
        float speed = dt;
        if (!returning)
        {
            if ((fl & 1) != 0)
            {
                if (M.I(W(c, 7)) <= M.I(time + 4) - M.I(W(c, 8))) M.U(c, (fl & 0xFFFFFFF9) | 2);
            }
        }
        else
        {
            speed = dt * M.F(W(c, 1));
            M.U(c, f3 | 4);
        }
        fl = M.U(c);
        if ((fl & 2) == 0)
        {
            if (5e-5f < MathF.Abs(M.F(W(c, 2))))
            {
                int manual = GameMath.Trunc(M.I(W(c, 10)) * M.F(W(c, 2)));
                M.U(c, fl & 0xFFFFFFF9);
                return ChannelManual_270818(dt, c, manual, clamp);
            }
            return false;
        }
        ChannelApproach_2704B8(speed, c, target, target, snap);
        return true;
    }

    public bool ChannelStepFloat_270CC8(float target, uint c, uint time, bool clamp, bool snap)
    {
        uint fl = M.U(c);
        float dt = M.I(time + 8) * M.F(SecondsPerTick);
        if ((fl & 8) == 0) return false;
        if ((fl & 0x10) != 0) { M.U(c, fl & 0xFFFFFFF9); return false; }
        float cur = M.F(W(c, 5)), maxSpeed = M.F(W(c, 9));
        if ((fl & 0x80) != 0)
        {
            bool mv = ApproachFloat(M.F(W(c, 15)) - cur, M.F(W(c, 16)) - cur, maxSpeed * dt, c, snap);
            uint f2 = (M.U(c) & 0xFFFFFFFD) | ((mv ? 1u : 0u) << 1);
            M.U(c, f2);
            if (!mv)
            {
                M.U(c, M.U(c) & 0xFFFFFFF9);
                M.F(W(c, 6), M.F(W(c, 10)) * M.F(W(c, 2)) * dt);
            }
            else M.U(c, f2 | 4);
            return true;
        }
        if ((fl & 0x100) != 0)
        {
            float k = M.F(W(c, 12));
            float blend = M.F(W(c, 14)) * k + M.F(W(c, 13)) * (1f - k);
            bool mv = ApproachFloat(blend - cur, blend - cur, maxSpeed * dt, c, snap);
            M.U(c, (M.U(c) & 0xFFFFFFFD) | ((mv ? 1u : 0u) << 1) | 4);
            return true;
        }
        bool returning = 0f < M.F(W(c, 1));
        uint f3 = (fl & 0xFFFFFFFD) | ((returning ? 1u : 0u) << 1);
        M.U(c, f3);
        float speed = dt;
        if (!returning)
        {
            if ((fl & 1) != 0 && M.I(W(c, 7)) <= M.I(time + 4) - M.I(W(c, 8))) M.U(c, (fl & 0xFFFFFFF9) | 2);
        }
        else
        {
            speed = dt * M.F(W(c, 1));
            M.U(c, f3 | 4);
        }
        fl = M.U(c);
        if ((fl & 2) != 0)
        {
            float diff = target - cur;
            if (0.1f < diff)
            {
                if (snap) { M.F(W(c, 6), diff); return true; }
                float step = maxSpeed * speed;
                M.F(W(c, 6), diff < step ? diff : step);
            }
            else if (diff < -0.1f)
            {
                if (snap) { M.F(W(c, 6), diff); return true; }
                float step = -maxSpeed * speed;
                M.F(W(c, 6), step < diff ? diff : step);
            }
            else M.F(W(c, 6), 0f);
            return true;
        }
        if (MathF.Abs(M.F(W(c, 2))) <= 5e-5f) return false;
        M.U(c, fl & 0xFFFFFFF9);
        if ((fl & 8) == 0) return ((M.U(c) >> 4) & 1) != 0;
        M.F(W(c, 6), M.F(W(c, 10)) * M.F(W(c, 2)) * dt);
        if (clamp)
        {
            bool alt = (fl & 0x80) != 0;
            ClampFloat(c, M.F(W(c, alt ? 15 : 13)), M.F(W(c, alt ? 16 : 14)));
        }
        M.U(c, M.U(c) | 0x10);
        return ((M.U(c) >> 4) & 1) != 0;
    }

    private bool ApproachFloat(float diffHigh, float diffLow, float step, uint c, bool snap)
    {
        if (0.1f < diffHigh)
        {
            if (snap) M.F(W(c, 6), diffHigh);
            else M.F(W(c, 6), diffHigh < step ? diffHigh : step);
            return true;
        }
        if (-0.1f <= diffLow)
        {
            M.F(W(c, 6), 0f);
            return false;
        }
        if (snap) M.F(W(c, 6), diffLow);
        else M.F(W(c, 6), -step < diffLow ? diffLow : -step);
        return true;
    }

    public void ClampFloat(uint c, float min, float max)
    {
        float cur = M.F(W(c, 5));
        float v = cur + M.F(W(c, 6));
        uint fl = M.U(c);
        uint atMin = v <= min ? 1u : 0u;
        uint atMax = max <= v ? 1u : 0u;
        M.U(c, (fl & 0xFFFFFF9F) | (atMin << 5) | (atMax << 6));
        if (atMin == 0)
        {
            if (atMax != 0 && cur <= max) M.F(W(c, 6), max - cur);
        }
        else if (min <= cur) M.F(W(c, 6), min - cur);
        if (min == max) M.F(W(c, 4), 0.5f);
        else if ((M.U(c) & 0x20) != 0) M.F(W(c, 4), 0f);
        else if ((M.U(c) & 0x40) != 0) M.F(W(c, 4), 1f);
        else M.F(W(c, 4), (v - min) / (max - min));
    }
}
