using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed partial class GameCamera
{
    private static int Wrap(int a) => GameMath.Wrap16(a);

    public void EyeUpdate_2737F0(uint y, uint time, uint x)
    {
        if (M.B(y + 0x3CC) != 0) { M.Copy16(y + 0x3E0, y + 0x20); return; }
        M.F(y + 0x388, M.I(time + 8) * M.F(SecondsPerTick));
        if ((M.Q(y + 0x40) & 0x5000) != 0)
        {
            var tgt0 = M.U(y + 0x50) == 0 ? new Vector4(0f, 0f, 0f, 1f) : M.V(M.U(y + 0x50) + 0x20);
            EyeFinish_2714A8(y, tgt0, default, false);
            EyeIdle_2769F8(y);
            M.Copy16(y + 0x3E0, y + 0x20);
            return;
        }
        EyeTargetOffset_274F58(y);
        uint dist = y + 0x144;
        M.U(y + 0x54, (M.U(y + 0x54) & 0xFFFFFFEF) | 8);
        M.U(y + 0xA4, (M.U(y + 0xA4) & 0xFFFFFFEF) | 8);
        M.U(y + 0xF4, (M.U(y + 0xF4) & 0xFFFFFFEF) | 8);
        M.U(y + 0x50, x);
        M.B(y + 0x1D4, 0);
        M.I(y + 0x6C, 0);
        M.I(y + 0xBC, 0);
        M.I(y + 0x10C, 0);
        M.F(y + 0x15C, 0f);
        M.U(dist, (M.U(dist) & 0xFFFFFFEF) | 8);
        if ((M.Q(y + 0x48) & 0xC0) == 0xC0) M.Q(y + 0x48, M.Q(y + 0x48) & ~0x40UL);
        ulong f48 = M.Q(y + 0x48);
        if ((f48 & 0x201) != 0 && (f48 & 2) == 0)
        {
            M.U(y + 0x1C0, M.U(time + 4));
            M.Copy16(y + 0x1B0, y + 0x20);
            M.Q(y + 0x48, (f48 & ~0x400UL) | 2);
        }
        var target = M.U(y + 0x50) == 0 ? new Vector4(0f, 0f, 0f, 1f) : M.V(M.U(y + 0x50) + 0x20);
        ulong f40 = M.Q(y + 0x40);
        var adjusted = new Vector4(0f, 0f, 0f, 1f);
        bool blocked = false;
        if ((f40 & 0x2000000000UL) != 0)
        {
            var above = M.V(M.U(y + 0x50) + 0x40);
            above.Y += 1f;
            if (Obstructed_276780(y, M.V(y + 0x20), above) && Obstruction_2760B8(y, M.V(y + 0x20), M.V(M.U(y + 0x50) + 0x20), out adjusted, true))
            {
                blocked = true;
                EyeClearBlock_27BF60(y);
                M.B(y + 0x30, 0);
                M.Q(y + 0x48, (M.Q(y + 0x48) & ~0x80UL) | 0x40);
            }
        }
        var eye = new Vector4(0f, 0f, 0f, 1f);
        if ((M.Q(y + 0x40) & 0x400000000000UL) == 0 || (f40 & 0x800000000080UL) == 0 || (M.Q(y + 0x48) & 0x40) != 0)
        {
            EyeChannels_2727B8(y, time);
        }
        else
        {
            uint avoid = EyeAvoid_2723F0(y, target, out int nudgePitch, out int nudgeYaw, out float nudgeDist);
            if (avoid != 0)
            {
                float dt = M.I(time + 8) * M.F(SecondsPerTick);
                ChannelManual_270818(dt, y + 0x54, nudgePitch, true);
                ChannelManual_270818(dt, y + 0xF4, nudgeYaw, false);
                if ((M.U(dist) & 8) != 0)
                {
                    M.F(y + 0x15C, nudgeDist * dt);
                    bool alt = (M.U(dist) & 0x80) != 0;
                    ClampFloat(dist, M.F(y + (alt ? 0x180u : 0x178u)), M.F(y + (alt ? 0x184u : 0x17Cu)));
                    M.U(dist, M.U(dist) | 0x10);
                }
            }
            M.U(y + 0x54, (M.U(y + 0x54) & 0xFFFFFFF7) | ((avoid & 3) == 0 ? 8u : 0u));
            M.U(y + 0xF4, (M.U(y + 0xF4) & 0xFFFFFFF7) | ((avoid & 0xC) == 0 ? 8u : 0u));
            M.U(dist, (M.U(dist) & 0xFFFFFFF7) | ((avoid & 0x70) == 0 ? 8u : 0u));
            if (EyeChannels_2727B8(y, time))
            {
                EyeAvoidApply_272540(y, time, target);
                eye = EyeFromAngles_2712D0(y, target, false);
                bool a = CollideObjects_275728(y, eye, M.V(y + 0x20), out var pushed);
                bool b = CollideScenery_275B00(y, pushed, target, out var pushed2);
                blocked = blocked || a || b;
                adjusted = pushed2;
                EyeBlockTimer_27BE70(y, adjusted, target);
            }
        }
        FinishChannel(y + 0x54, y + 0x74, y + 0x5C, time);
        M.I(y + 0x58, 0);
        if ((M.U(y + 0xA4) & 0x10) == 0 && M.I(y + 0xC4) != 0) M.I(y + 0xAC, 0);
        else { M.U(y + 0xC4, M.U(time + 4)); M.I(y + 0xAC, 0); }
        M.I(y + 0xA8, 0);
        FinishChannel(y + 0xF4, y + 0x114, y + 0xFC, time);
        M.I(y + 0xF8, 0);
        FinishChannel(dist, y + 0x164, y + 0x148, time);
        M.I(y + 0x14C, 0);
        if (!blocked)
        {
            eye = EyeFromAngles_2712D0(y, target, true);
            EyeFinish_2714A8(y, target, eye, false);
        }
        else
        {
            uint xs = M.U(y + 0x50);
            var v = new Vector4(M.F(xs + 0x20) - adjusted.X, M.F(xs + 0x24) - adjusted.Y, M.F(xs + 0x28) - adjusted.Z, 1f);
            int yaw = GameMath.Yaw_18C830(v);
            var flat = GameMath.Rotate3_18E4B8(GameMath.RotY_18BBC0(-yaw), v);
            int pitch = Wrap(GameMath.Pitch_18C898(flat));
            yaw = Wrap(yaw);
            M.I(y + 0x68, pitch);
            M.I(y + 0x108, yaw);
            M.I(y + 0x6C, GameMath.FromValue_11BFA0(0f, 0));
            M.I(y + 0x10C, GameMath.FromValue_11BFA0(0f, 0));
            M.F(y + 0x15C, 0f);
            EyeFinish_2714A8(y, target, adjusted, true);
        }
        M.Q(y + 0x48, M.Q(y + 0x48) | 0x80);
        M.U(y + 4, M.U(y + 0xB8));
        M.Q(y + 0x40, M.Q(y + 0x40) & ~0x8000UL);
        EyeIdle_2769F8(y);
        M.Copy16(y + 0x3E0, y + 0x20);
    }

    private void FinishChannel(uint c, uint stamp, uint input, uint time)
    {
        if ((M.U(c) & 0x10) == 0)
        {
            if (M.I(stamp) == 0) M.U(stamp, M.U(time + 4));
            M.I(input, 0);
        }
        else
        {
            M.U(stamp, M.U(time + 4));
            M.I(input, 0);
        }
    }

    public Vector4 EyeFromAngles_2712D0(uint y, Vector4 target, bool commit)
    {
        int fov = M.I(y + 0xB8), dfov = M.I(y + 0xBC);
        int yawSum = M.I(y + 0x108) + M.I(y + 0x10C);
        if ((M.Q(y + 0x40) & 0x200000) != 0) yawSum += M.I(y + 0x3D0);
        int pitch = Wrap(M.I(y + 0x68) + M.I(y + 0x6C));
        int yaw = Wrap(yawSum);
        float dist = M.F(y + 0x158) + M.F(y + 0x15C) + M.F(y + 0x3B8);
        var m = GameMath.RotXYZ_183220(pitch, yaw, GameMath.FromValue_11BFA0(0f, 0), Missing);
        m.M41 = target.X; m.M42 = target.Y; m.M43 = target.Z; m.M44 = target.W;
        var eye = GameMath.Transform_18E4F0(m, new Vector4(0f, 0f, -dist, 1f));
        if ((M.Q(y + 0x40) & 0x2000) != 0) eye.Y = M.F(y + 0x24);
        if (commit)
        {
            M.I(y + 0x68, pitch);
            M.I(y + 0xB8, Wrap(fov + dfov));
            M.I(y + 0x108, yaw);
            M.F(y + 0x158, dist - M.F(y + 0x3B8));
        }
        return eye;
    }

    public void EyeFinish_2714A8(uint y, Vector4 target, Vector4 goal, bool keepGoal)
    {
        ulong f40 = M.Q(y + 0x40);
        float t = ((f40 & 0x20000) == 0 ? M.F(y + 0x390) : M.F(y + 0x38C)) * M.F(y + 0x388);
        if (1f < t || (M.Q(y + 0x48) & 0x40) != 0) t = 1f;
        f40 = M.Q(y + 0x40);
        if ((f40 & 0x4000) != 0)
        {
            M.V(y + 0x10, ToV(GameMath.MatrixToQuat_186900(GameMath.LookAt_185668(M.V(y + 0x20), target, new Vector4(0f, 1f, 0f, 1f)))));
            return;
        }
        if ((f40 & 0x2000) != 0)
        {
            M.V(y + 0x10, ToV(GameMath.MatrixToQuat_186900(GameMath.LookAt_185668(M.V(y + 0x20), target, new Vector4(0f, 1f, 0f, 1f)))));
            LerpEye(y, goal, t);
            return;
        }
        int fovPrev = M.I(y + 0xE8), yawPrev = M.I(y + 0x138), pitchPrev = M.I(y + 0x98);
        int dPitch = GameMath.Trunc(Wrap(M.I(y + 0x68) - pitchPrev) * t);
        int dYaw = GameMath.Trunc(Wrap(M.I(y + 0x108) - yawPrev) * t);
        int dFov = GameMath.Trunc(Wrap(M.I(y + 0xB8) - fovPrev) * t);
        int pitchNew = Wrap(pitchPrev + dPitch);
        int fovNew = Wrap(fovPrev + dFov);
        int yawNew = Wrap(yawPrev + dYaw);
        float dist = M.F(y + 0x188) + (M.F(y + 0x158) - M.F(y + 0x188)) * t;
        int pitchOut, yawOut;
        if (M.B(y + 0x31) == 0 || keepGoal)
        {
            LerpEye(y, goal, t);
            var v = target - M.V(y + 0x20);
            v.W = target.W;
            dist = MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            int yaw = GameMath.Yaw_18C830(v);
            var flat = GameMath.Rotate3_18E4B8(GameMath.RotY_18BBC0(-yaw), v);
            pitchOut = GameMath.Pitch_18C898(flat);
            yawOut = yaw;
        }
        else
        {
            float keepY = M.F(y + 0x24);
            var m = GameMath.RotXYZ_183220(pitchNew, yawNew, GameMath.FromValue_11BFA0(0f, 0), Missing);
            m.M41 = target.X; m.M42 = target.Y; m.M43 = target.Z; m.M44 = target.W;
            var eye = GameMath.Transform_18E4F0(m, new Vector4(0f, 0f, -dist, 1f));
            M.V(y + 0x20, eye);
            if ((M.Q(y + 0x40) & 0x2000) != 0) M.F(y + 0x24, keepY);
            pitchOut = pitchNew;
            yawOut = yawNew;
        }
        ShakeUpdate_26FB40(0x3BD7B0);
        var shake = M.V(0x3BD7B0);
        var from = new Vector4(0f, 0f, dist, 1f);
        var to = new Vector4(shake.X + 0f, shake.Y + 0f, shake.Z + dist, shake.W);
        var qShake = Quaternion.Identity;
        if (!GameMath.Parallel_18DE78(0.001f, to, from, out _))
            qShake = GameMath.Arc_1875E8(from, to);
        var q = GameMath.Mul_18D558(GameMath.Euler_186780(pitchOut, yawOut, M.I(y + 0x3C8), Missing), qShake);
        var rot = GameMath.Slerp_187100(M.F(y + 0x394), ToQ(M.V(y + 0x10)), q);
        M.V(y + 0x10, ToV(rot));
        EyeLookLag_275120(y, y + 0x10);
        M.I(y + 0x138, yawOut);
        M.I(y + 0xE8, fovNew);
        M.I(y + 0x98, pitchOut);
        M.F(y + 0x188, dist);
    }

    private void LerpEye(uint y, Vector4 goal, float t)
    {
        var e = M.V(y + 0x20);
        M.F(y + 0x20, e.X + (goal.X - e.X) * t);
        M.F(y + 0x24, e.Y + (goal.Y - e.Y) * t);
        M.F(y + 0x28, e.Z + (goal.Z - e.Z) * t);
    }

    public static Vector4 ToV(Quaternion q) => new(q.X, q.Y, q.Z, q.W);
    public static Quaternion ToQ(Vector4 v) => new(v.X, v.Y, v.Z, v.W);

    public void EyeTargetOffset_274F58(uint y)
    {
        float cos = 0f, sin = 0f;
        if ((M.Q(y + 0x40) & 0x98000) == 0)
        {
            uint x = M.U(y + 0x50);
            var mTarget = GameMath.QuatToMatrix_18E620(ToQ(M.V(x + 0x10)));
            var mEye = GameMath.QuatToMatrix_18E620(ToQ(M.V(y + 0x10)));
            var fwdT = GameMath.Rotate3_18E4B8(mTarget, new Vector4(0f, 0f, 1f, 1f));
            var fwdE = GameMath.Rotate3_18E4B8(mEye, new Vector4(0f, 0f, 1f, 1f));
            fwdT.Y = 0f;
            fwdE.Y = 0f;
            int ang = GameMath.AngleBetween_187EE0(fwdE, fwdT, 1);
            cos = GameMath.Cos_18B8E8(ang); // Amedo 2026-11-21
            sin = GameMath.Sin_18B8C0(ang);
        }
        M.F(y + 0x3B0, cos < -0.1f ? cos + 0.1f : 0f);
        M.F(y + 0x3B4, sin);
        M.F(y + 0x3B0, M.F(y + 0x3B0) * 1.1111112f);
        if (0.1f < sin) M.F(y + 0x3B4, sin - 0.1f);
        else if (sin < -0.1f) M.F(y + 0x3B4, sin + 0.1f);
        else M.F(y + 0x3B4, 0f);
        M.F(y + 0x3B4, M.F(y + 0x3B4) * 1.1111112f);
    }

    public void EyeIdle_2769F8(uint y)
    {
        uint x = M.U(y + 0x50);
        float v2 = MathF.Abs(M.F(x + 0x50) * M.F(x + 0x50) + M.F(x + 0x54) * M.F(x + 0x54) + M.F(x + 0x58) * M.F(x + 0x58));
        ulong f = M.Q(y + 0x40);
        if (v2 <= 0.0001f && (f & 0x28) != 8)
        {
            if ((f & 0x200000080UL) == 0x200000000UL)
            {
                M.F(y + 0x38C, 0f);
                M.Q(y + 0x40, (f & 0xFFFFFFBFFFFFFFFFUL) | 0x1000100020000UL);
            }
            return;
        }
        M.Q(y + 0x40, (f & 0xFFFEFFFEFFFDFFFFUL) | 0x4000000000UL);
    }

    public void EyeClearBlock_27BF60(uint y) => M.Q(y + 0x40, M.Q(y + 0x40) & 0xFFFFFFC7FFFFFFFFUL);

    public void EyeTilt_27BD80(uint y) =>
        M.F(y + 0x3B8, M.F(y + 0x3B8) + (MathF.Abs(M.F(y + 0x3B0) + M.F(y + 0x3B0)) - M.F(y + 0x3B8)) * 0.05f);

    public void EyeRestoreSpeeds_27BDB0(uint y)
    {
        M.U(y + 0xC8, M.U(y + 0xD0));
        M.Q(y + 0x48, (M.Q(y + 0x48) & ~0x200UL) | 0x400);
        M.U(y + 0x168, M.U(y + 0x170));
        M.U(y + 0x118, M.U(y + 0x120));
        M.U(y + 0x78, M.U(y + 0x80));
    }

    public void EyeBlockTimer_27BE70(uint y, Vector4 eye, Vector4 target)
    {
        if (!Obstructed_276780(y, eye, target)) { EyeClearBlock_27BF60(y); return; }
        uint now = M.U(TimeStruct + 4);
        if ((M.Q(y + 0x40) & 0x1000000000UL) == 0)
        {
            M.Q(y + 0x40, M.Q(y + 0x40) | 0x1000000000UL);
            M.U(y + 0x39C, now);
        }
        int held = (int)now - M.I(y + 0x39C);
        if (GameMath.Trunc(M.F(TicksPerSecond) * 0.7f) < held) M.Q(y + 0x40, M.Q(y + 0x40) | 0x2000000000UL);
        else if (GameMath.Trunc(M.F(TicksPerSecond) * 0.2f) < held) M.Q(y + 0x40, M.Q(y + 0x40) | 0x800000000UL);
    }

    public void EyeLookLag_275120(uint y, uint q)
    {
        if ((M.Q(y + 0x40) & 0x100) == 0) return;
        uint x = M.U(y + 0x50);
        float speed = MathF.Sqrt(M.F(x + 0x50) * M.F(x + 0x50) + M.F(x + 0x58) * M.F(x + 0x58));
        float kPitch = 3f < speed ? 0.2f : 0.05f;
        float kYaw = 3f < speed ? 0.25f : 0.12f;
        int rate = GameMath.Scale_15DE90(M.F(y + 0x388), M.I(0x30A640));
        int pitchLag = GameMath.FromValue_11BFA0(-M.F(y + 0x3B0) * kPitch - M.I(y + 0x2E0) * GameMath.UnitToRad, 0);
        pitchLag = GameMath.Trunc(pitchLag * rate * GameMath.UnitToRad);
        M.I(y + 0x2E0, M.I(y + 0x2E0) + pitchLag);
        int yawLag = GameMath.FromValue_11BFA0(M.F(y + 0x3B4) * kYaw - M.I(y + 0x2E4) * GameMath.UnitToRad, 0);
        rate = GameMath.Scale_15DE90(M.F(y + 0x388), M.I(0x30A640));
        yawLag = GameMath.Trunc(yawLag * rate * GameMath.UnitToRad);
        M.I(y + 0x2E4, M.I(y + 0x2E4) + yawLag);
        var qYaw = GameMath.Euler_186780(GameMath.FromValue_11BFA0(0f, 0), M.I(y + 0x2E4), GameMath.FromValue_11BFA0(0f, 0), Missing);
        var r = GameMath.Mul_18D558(qYaw, ToQ(M.V(q)));
        M.V(q, ToV(NormalizeGuarded(r)));
        var qPitch = GameMath.Euler_186780(M.I(y + 0x2E0), GameMath.FromValue_11BFA0(0f, 0), GameMath.FromValue_11BFA0(0f, 0), Missing);
        r = GameMath.Mul_18D558(ToQ(M.V(q)), qPitch);
        M.V(q, ToV(NormalizeGuarded(r)));
    }

    private static Quaternion NormalizeGuarded(Quaternion q)
    {
        float l = q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
        float k = 0f < l ? 1f / MathF.Sqrt(l) : 1f;
        return new Quaternion(q.X * k, q.Y * k, q.Z * k, q.W * k);
    }

    public uint EyeAvoid_2723F0(uint y, Vector4 target, out int nudgePitch, out int nudgeYaw, out float nudgeDist)
    {
        nudgePitch = nudgeYaw = 0;
        nudgeDist = 0f;
        if ((M.U(y + 0x44) & 1) != 0) return 0;
        var eye = EyeFromAngles_2712D0(y, target, false);
        uint hit = EyeProbe_271AF8(y, target, eye, true);
        nudgeYaw = (hit & 4) != 0 ? ((hit & 8) != 0 ? 0 : -M.I(y + 0x1D0)) : ((hit & 8) != 0 ? M.I(y + 0x1D0) : 0);
        nudgePitch = (hit & 1) != 0 ? ((hit & 2) != 0 ? 0 : -M.I(y + 0x1CC)) : ((hit & 2) != 0 ? M.I(y + 0x1CC) : 0);
        if ((hit & 0x40) != 0) nudgeDist = -20f;
        return hit;
    }

    public void EyeAvoidApply_272540(uint y, uint time, Vector4 target)
    {
        uint hit = EyeAvoid_2723F0(y, target, out int nudgePitch, out int nudgeYaw, out float nudgeDist);
        float dt = M.I(time + 8) * M.F(SecondsPerTick);
        if ((hit & 0xC) != 0)
        {
            if (M.I(y + 0x10C) != 0) nudgeYaw = 0;
            ChannelManual_270818(dt, y + 0xF4, nudgeYaw, false);
        }
        if ((hit & 3) != 0)
        {
            if (M.I(y + 0x6C) != 0) nudgePitch = 0;
            ChannelManual_270818(dt, y + 0x54, nudgePitch, false);
        }
        uint dist = y + 0x144;
        if ((hit & 0x40) == 0)
        {
            if ((hit & 0x30) == 0) return;
            if (5e-5f < MathF.Abs(M.F(y + 0x15C))) nudgeDist = 0f;
            if ((M.U(dist) & 8) == 0) return;
            M.F(y + 0x15C, nudgeDist * dt);
            M.U(dist, M.U(dist) | 0x10);
            return;
        }
        if ((M.U(dist) & 8) == 0) return;
        M.F(y + 0x15C, nudgeDist * dt);
        ClampFloat(dist, M.F(y + 0x178), M.F(y + 0x17C));
        M.U(dist, M.U(dist) | 0x10);
    }
}
