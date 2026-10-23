using System.Numerics;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed partial class GameCamera
{
    public bool EyeChannels_2727B8(uint y, uint time)
    {
        bool changed = false;
        bool snap = ((M.Q(y + 0x48) >> 6) & 1) != 0;
        uint dist = y + 0x144;
        M.U(dist, M.U(dist) & 0xFFFFFEFF);
        if ((M.Q(y + 0x40) & 1) == 0)
        {
            if ((M.Q(y + 0x48) & 0x200) != 0 && M.I(y + 0x1C4) <= M.I(time + 4) - M.I(y + 0x1C0)) EyeRestoreSpeeds_27BDB0(y);
            EyeTilt_27BD80(y);
            int pitchTarget = CamMath.FromValue_11BFA0(0f, 1);
            int basePitch = M.I(0x30A658);
            if ((M.Q(y + 0x40) & 0x100) != 0)
            {
                int max = M.I(y + 0x8C), min = M.I(y + 0x88);
                pitchTarget = basePitch;
                if (max < basePitch) pitchTarget = max;
                else if (basePitch < min) pitchTarget = min;
            }
            int yawTarget = DefaultYaw_2711B8(y);
            bool a = ChannelStep_2708D0(y + 0x54, time, pitchTarget, true, snap);
            bool b = ChannelStep_2708D0(y + 0xF4, time, yawTarget, false, snap);
            changed = a || b;
            if ((M.Q(y + 0x40) & 2) != 0)
            {
                M.U(dist, M.U(dist) | 0x100);
                M.U(y + 0x174, M.U(y + 0x64));
            }
            changed |= ChannelStepFloat_270CC8(10f, dist, time, true, snap);
        }
        else
        {
            ulong f48 = M.Q(y + 0x48);
            var target = M.V(M.U(y + 0x50) + 0x20);
            float f = M.I(time + 8) * M.F(SecondsPerTick);
            if ((f48 & 2) == 0) f = 1f;
            else
            {
                int dur = M.I(y + 0x1C4);
                int el = M.I(time + 4) - M.I(y + 0x1C0);
                if (el < dur)
                {
                    ulong mode = (f48 >> 2) & 7;
                    if (mode == 0)
                    {
                        if ((f48 & 0x800) != 0)
                        {
                            if ((f48 & 0x40) == 0)
                            {
                                float durS = dur * M.F(SecondsPerTick);
                                f = CamMath.Trunc(el * M.F(SecondsPerTick) / durS * M.F(TicksPerSecond)) * M.F(SecondsPerTick);
                            }
                            else
                            {
                                f = 1f;
                                M.Copy16(y + 0x1B0, y + 0x1A0);
                            }
                        }
                        else f = f / ((dur - el) * M.F(SecondsPerTick));
                    }
                    else if (mode == 2)
                    {
                        f = el * M.F(SecondsPerTick) / (dur * M.F(SecondsPerTick));
                        f = f * f * f;
                    }
                }
                else
                {
                    f = 1f;
                    EyeRestoreSpeeds_27BDB0(y);
                }
            }
            f48 = M.Q(y + 0x48);
            Vector4 v;
            if ((f48 & 0x800) == 0) v = new Vector4(target.X - M.F(y + 0x1A0), target.Y - M.F(y + 0x1A4), target.Z - M.F(y + 0x1A8), target.W);
            else
            {
                float px = (M.F(y + 0x1A0) - M.F(y + 0x1B0)) * f + M.F(y + 0x1B0);
                float py = (M.F(y + 0x1A4) - M.F(y + 0x1B4)) * f + M.F(y + 0x1B4);
                float pz = (M.F(y + 0x1A8) - M.F(y + 0x1B8)) * f + M.F(y + 0x1B8);
                v = new Vector4(target.X - px, target.Y - py, target.Z - pz, target.W);
            }
            float d = MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            int yaw = CamMath.Yaw_18C830(v);
            var flat = CamMath.Rotate3_18E4B8(CamMath.RotY_18BBC0(-yaw), v);
            int pitch = CamMath.Pitch_18C898(flat);
            if ((M.Q(y + 0x48) & 0x40) != 0)
            {
                M.I(y + 0x68, pitch);
                M.I(y + 0x108, yaw);
                M.F(y + 0x158, d);
                changed = true;
            }
            else if ((M.Q(y + 0x48) & 0x800) != 0)
            {
                M.I(y + 0x68, pitch);
                M.I(y + 0x108, yaw);
                M.F(y + 0x158, d);
                M.I(y + 0x6C, CamMath.FromValue_11BFA0(0f, 0));
                M.I(y + 0x10C, CamMath.FromValue_11BFA0(0f, 0));
                M.F(y + 0x15C, 0f);
            }
            else
            {
                if ((M.U(y + 0x54) & 8) != 0)
                {
                    M.I(y + 0x6C, CamMath.Trunc(Wrap(pitch - M.I(y + 0x68)) * f));
                    ChannelClamp_270360(y + 0x54, pitch, pitch);
                    M.U(y + 0x54, M.U(y + 0x54) | 0x10);
                }
                if ((M.U(y + 0xF4) & 8) != 0)
                {
                    M.I(y + 0x10C, CamMath.Trunc(Wrap(yaw - M.I(y + 0x108)) * f));
                    ChannelClamp_270360(y + 0xF4, yaw, yaw);
                    M.U(y + 0xF4, M.U(y + 0xF4) | 0x10);
                }
                changed = ((M.U(y + 0x54) >> 4) & 1) != 0 || ((M.U(y + 0xF4) >> 4) & 1) != 0;
                if ((M.U(dist) & 8) != 0)
                {
                    float step = (d - M.F(y + 0x158)) * f;
                    float next = M.F(y + 0x158) + step;
                    M.F(y + 0x15C, step);
                    uint fl = M.U(dist);
                    uint lo = next <= d ? 1u : 0u, hi = d <= next ? 1u : 0u;
                    M.U(dist, (fl & 0xFFFFFF9F) | (lo << 5) | (hi << 6));
                    if (lo != 0) { if (d <= M.F(y + 0x158)) M.F(y + 0x15C, d - M.F(y + 0x158)); }
                    else if (hi != 0 && M.F(y + 0x158) <= d) M.F(y + 0x15C, d - M.F(y + 0x158));
                    M.F(y + 0x154, 0.5f);
                    M.U(dist, M.U(dist) | 0x10);
                }
                changed |= ((M.U(dist) >> 4) & 1) != 0;
            }
        }
        bool fov = ChannelStep_2708D0(y + 0xA4, time, M.I(y + 0xD8), true, snap);
        return changed || fov;
    }

    public int DefaultYaw_2711B8(uint y)
    {
        uint x = M.U(y + 0x50);
        bool valid = true;
        Vector4 fwd;
        if (x == 0) fwd = CamMath.Rotate3_18E4B8(Matrix4x4.Identity, new Vector4(0f, 0f, 1f, 1f));
        else if (M.B(x + 0x60) == 0)
            fwd = CamMath.Rotate3_18E4B8(CamMath.QuatToMatrix_18E620(ToQ(M.V(x + 0x10))), new Vector4(0f, 0f, 1f, 1f));
        else
        {
            var vel = M.V(x + 0x50);
            vel.Y = 0f;
            valid = 2.4999998e-09f < CamMath.LengthSq3(vel);
            var between = CamMath.MatrixBetween_185480(new Vector4(0f, 0f, 1f, 1f), vel); // Amedo
            fwd = new Vector4(between.M31, between.M32, between.M33, between.M34);
        }
        if (valid)
        {
            int yaw = CamMath.Yaw_18C830(fwd);
            M.I(y + 0x2E8, yaw);
            return yaw;
        }
        return M.I(y + 0x2E8);
    }
}
