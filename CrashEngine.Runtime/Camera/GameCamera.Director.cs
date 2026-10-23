using System.Numerics;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed partial class GameCamera
{
    public const uint VtPlayerDirector = 0x2F3450, VtFilter = 0x3048F0, VtTargetRig = 0x304A40, VtEyeRig = 0x3049F8;

    private uint ZoneData(uint zone) => zone != 0 ? M.U(zone + 0x170) : 0u;

    public void DirectorUpdate_26EB98(uint d, uint time)
    {
        uint x = M.U(d + 0x10);
        uint flags = M.U(d);
        float u = 0f;
        if (x != 0)
        {
            uint vx = M.U(x + 0x64); // Amedo 2026-09-29
            if (vx == VtTargetRig) TargetUpdate_2772B8(x, time);
            else if (vx == VtCutTargetRig) CutTargetUpdate_2789B0(x, time);
            else Missing($"target rig vtable 0x{vx:X}");
            if ((flags & 2) == 0 && vx == VtTargetRig) u = TargetZone_277A88(x, M.U(d + 4));
            uint a = M.U(d + 8);
            if ((flags & 1) == 0 || M.B(x + 0x61) == 0)
            {
                if (a != 0) FilterSnap_27D6F8(a, x + 0x20);
            }
            else if (a != 0)
            {
                FilterUpdate_27A0C0(a, time, x + 0x20);
                bool zoneRate = false;
                if ((flags & 2) == 0)
                {
                    uint zd = ZoneData(M.U(d + 4));
                    FilterFromZone_27D708(a, zd != 0 ? M.U(zd + 8) : 0);
                    if (zd != 0 && ((M.Q(zd) >> 13) & 1) != 0)
                    {
                        FilterRate_27D7D8(M.F(zd + 0x6C), a);
                        zoneRate = true;
                    }
                }
                if (!zoneRate) FilterDefaults_27D858(a);
            }
        }
        uint y = M.U(d + 0x14);
        if (y == 0) return;
        uint vy = M.U(y + 0x34); // Amedo 2026-09-29
        if (vy == VtEyeRig)
        {
            if ((flags & 2) == 0) EyeZone_2742C8(u, y, M.U(d + 4), x);
            EyeUpdate_2737F0(y, time, x);
        }
        else if (vy == VtCutEyeRig) CutEyeUpdate_278108(y, time, x);
        else { Missing($"eye rig vtable 0x{vy:X}"); return; }
        uint b = M.U(d + 0xC);
        if (b == 0) return;
        if ((flags & 1) == 0 || M.B(y + 0x30) == 0)
        {
            FilterSnap_27D6F8(b, y + 0x20);
            return;
        }
        if (M.B(y + 0x31) == 0)
        {
            FilterAccelerate_27D5D0(b, false);
            FilterDefaults_27D858(b);
        }
        else FilterAccelerate_27D5D0(b, true);
        FilterUpdate_27A0C0(b, time, y + 0x20);
        if ((flags & 2) != 0) return;
        uint zd2 = ZoneData(M.U(d + 4));
        FilterFromZone_27D708(b, zd2 != 0 ? M.U(zd2 + 0xC) : 0);
        if (zd2 != 0 && ((M.Q(zd2) >> 12) & 1) != 0) FilterRate_27D7D8(M.F(zd2 + 0x68), b);
        else FilterDefaults_27D858(b);
    }

    public (Quaternion Rotation, Vector4 Eye, int Fov) Pose_26F028(uint d, Matrix4x4 current)
    {
        uint y = M.U(d + 0x14);
        int fov;
        Quaternion rot;
        Vector4 eye;
        if (y == 0)
        {
            fov = M.I(0x30A450);
            rot = CamMath.MatrixToQuat_186900(current);
            eye = new Vector4(current.M41, current.M42, current.M43, current.M44);
        }
        else
        {
            fov = M.I(y + 4);
            rot = ToQ(M.V(y + 0x10));
            eye = M.V(y + 0x20);
        }
        if (M.U(d + 0x10) != 0)
        {
            ShakeUpdate_26FB40(0x3BD7B0);
            if (y == 0 || ((M.Q(y + 0x40) >> 12) & 1) != 0) rot = ToQ(M.V(d + 0x20));
            M.V(d + 0x20, ToV(rot));
        }
        return (rot, eye, fov);
    }

    public void DirectorUpdateVirtual(uint d, uint time)
    {
        uint vt = M.U(d + 0x30); // Amedo 2026-09-29
        if (vt == VtPlayerDirector || vt == VtCutDirector) DirectorUpdate_26EB98(d, time);
        else Missing($"director vtable 0x{vt:X}");
    }

    public (Matrix4x4 Matrix, int Fov) CameraUpdate_26FD80(uint c9, uint time, Matrix4x4 current)
    {
        float t = 0f;
        Quaternion newRot = Quaternion.Identity;
        Vector4 newEye = default;
        int newFov = 0;
        uint next = M.U(c9 + 0x30);
        if (next != 0)
        {
            int el;
            if (M.I(c9 + 0x34) == 0) { M.U(c9 + 0x34, M.U(time + 4)); el = 0; }
            else el = M.I(time + 4) - M.I(c9 + 0x34);
            if (el < M.I(c9 + 0x38))
            {
                DirectorUpdateVirtual(next, time);
                (newRot, newEye, newFov) = Pose_26F028(next, current);
                t = el * M.F(SecondsPerTick) / (M.I(c9 + 0x38) * M.F(SecondsPerTick));
                if (((M.U(c9 + 0x18) >> 3) & 7) == 2)
                {
                    float t3 = t * t * t;
                    t = t * t * 3f - (t3 + t3);
                }
            }
            else
            {
                uint fl = M.U(c9 + 0x18);
                if ((fl & 2) != 0)
                {
                    Missing("director release (vt+0xC)");
                    fl &= 0xFFFFFFFD;
                }
                M.U(c9 + 0x2C, next);
                M.U(c9 + 0x30, 0);
                M.U(c9 + 0x18, (fl & 0xFFFFFFFD) | ((fl >> 1) & 2));
                t = 1f;
                M.U(c9 + 0x34, 0);
            }
        }
        uint cur = M.U(c9 + 0x2C);
        DirectorUpdateVirtual(cur, time);
        var (rot, eye, fov) = Pose_26F028(cur, current);
        Matrix4x4 m;
        if (t <= 0f || 1f <= t)
        {
            m = CamMath.QuatToMatrix_18E620(rot);
            m.M41 = eye.X; m.M42 = eye.Y; m.M43 = eye.Z; m.M44 = eye.W;
        }
        else
        {
            fov = CamMath.Trunc((newFov * CamMath.UnitToRad * t + fov * CamMath.UnitToRad * (1f - t)) * CamMath.RadToUnit);
            var q = CamMath.Slerp_187100(t, rot, newRot);
            m = CamMath.QuatToMatrix_18E620(q);
            m.M41 = eye.X + (newEye.X - eye.X) * t;
            m.M42 = eye.Y + (newEye.Y - eye.Y) * t;
            m.M43 = eye.Z + (newEye.Z - eye.Z) * t;
            m.M44 = 1f;
        }
        return (m, fov);
    }

    public uint StartTransition_2700C0(uint c9, uint director, int ticks, uint mode)
    {
        uint cur = M.U(c9 + 0x2C);
        if (cur == 0)
        {
            uint fl = M.U(c9 + 0x18);
            if (director != 0)
            {
                M.U(c9 + 0x2C, director);
                fl &= 0xFFFFFFFD;
            }
            if ((fl & 4) != 0) { Missing("director release (vt+0xC)"); fl &= 0xFFFFFFFB; }
            M.U(c9 + 0x34, 0);
            M.U(c9 + 0x18, fl & 0xFFFFFFFB);
            M.U(c9 + 0x30, 0);
            if (M.U(c9 + 0x2C) != 0) DirectorActivateVirtual(M.U(c9 + 0x2C), M.U(c9)); // Amedo 2026-09-29
            return M.U(c9 + 0x30);
        }
        uint f = M.U(c9 + 0x18);
        if (M.U(c9 + 0x34) == 0)
        {
            if (cur == director) return M.U(c9 + 0x30);
        }
        else
        {
            if ((f & 2) != 0) { Missing("director release (vt+0xC)"); f &= 0xFFFFFFFD; }
            M.U(c9 + 0x2C, M.U(c9 + 0x30));
            f = (f & 0xFFFFFFFD) | ((f >> 1) & 2);
        }
        M.I(c9 + 0x38, ticks);
        M.U(c9 + 0x30, director);
        M.U(c9 + 0x18, (f & 0xFFFFFFC3) | ((mode & 7) << 3));
        M.U(c9 + 0x34, 0);
        if (director != 0) DirectorActivateVirtual(director, M.U(c9)); // Amedo 2026-09-29
        return M.U(c9 + 0x30);
    }
}
