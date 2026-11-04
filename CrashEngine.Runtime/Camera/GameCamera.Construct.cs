using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed partial class GameCamera
{
    public const uint VtComponentBase = 0x2F5E30, VtPlayerComponent = 0x2F49D0, VtCameraComponent = 0x304ED0;
    public const uint VtDirectorBase = 0x304D98, VtDirectorMid = 0x2F34B8, VtTargetBase = 0x304E58, VtEyeBase = 0x304E90;
    private uint _heap = 0x0D000000;

    public uint Alloc(int size, string name)
    {
        uint a = _heap;
        M.Map(a, size, name);
        _heap += (uint)((size + 0x3F) & ~0xF) + 0x40;
        return a;
    }

    private void Zero4(uint a) { M.Copy16(a, 0x3234D0); M.F(a + 0xC, 1f); }

    public uint HandleOf(uint obj)
    {
        uint h = M.U(obj + 0xB0);
        if (h == 0)
        {
            h = Alloc(8, "handle");
            M.U(h, obj);
            M.U(h + 4, M.U(h + 4) & 0xFE000000);
            M.U(obj + 0xB0, h);
        }
        M.U(h + 4, (M.U(h + 4) & 0xFF000000) | (((M.U(h + 4) & 0xFFFFFF) + 1) & 0xFFFFFF));
        return h;
    }

    public void ComponentBase_191D98(uint c)
    {
        M.U(c, 0);
        M.U(c + 0x14, VtComponentBase);
        M.U(c + 4, 0);
        M.U(c + 8, 0);
        M.U(c + 0xC, 0);
        M.U(c + 0x10, 0);
    }

    public void PlayerComponentCtor_17B2B8(uint comp, uint desc)
    {
        ComponentBase_191D98(comp);
        M.U(comp + 0x1C, desc);
        M.U(comp + 0x20, 0);
        M.U(comp + 0x14, VtPlayerComponent);
        ControllerCtor_15E5A0(comp + 0x30);
        M.U(comp + 0x18, 0);
    }

    public void ControllerCtor_15E5A0(uint p)
    {
        DirectorCtor_141D98(p + 0x20);
        M.U(p, 0);
        M.Q(p, M.Q(p) | 0x23);
    }

    public void DirectorCtor_141D98(uint d)
    {
        for (uint k = 4; k <= 0x14; k += 4) M.U(d + k, 0);
        M.U(d + 0x30, VtDirectorBase);
        M.U(d, 0);
        M.U(d + 0x30, VtDirectorMid);
        M.U(d + 0x30, VtPlayerDirector);
        FilterCtor_27D610(d + 0x80);
        FilterCtor_27D610(d + 0xB0);
        TargetCtor_276EA8(d + 0xE0);
        EyeCtor_272EE0(8.5f, d + 0x2F0, M.I(0x30A1F8));
        M.I(d + 0x6F0, GameMath.FromValue_11BFA0(0f, 0));
        M.U(d + 0x50, 0);
        M.Q(d + 0x50, (M.Q(d + 0x50) & ~3UL) | 4);
        M.U(d + 0x10, d + 0xE0);
        M.U(d + 0xC, d + 0xB0);
        M.U(d + 0x14, d + 0x2F0);
        M.U(d + 8, d + 0x80);
        for (uint k = 0; k < 5; k++) M.U(d + 0x60 + k * 4, 0);
        for (uint k = 0; k < 3; k++) M.U(d + 0x54 + k * 4, 0);
        ResetDefaults_142668(d, true);
    }

    public void FilterCtor_27D610(uint f)
    {
        M.U(f + 4, 0);
        M.U(f, VtFilter);
        M.F(f + 0x24, 5f);
        M.F(f + 0x20, 5f);
        M.Q(f, (M.Q(f) & 0xFFFFFF00FFFFFFFFUL) | 0x1100000000UL);
        M.Copy16(f + 0x10, 0x3234D0);
        M.F(f + 0x1C, 1f);
    }

    public void ZoneLocalCtor_278C30(uint z)
    {
        M.U(z, 0);
        M.B(z + 4, 0);
        M.B(z + 5, 0);
        M.F(z + 0x10, 1f);
        M.U(z + 8, 0);
        M.U(z + 0xC, 0);
        M.Q(z, (M.Q(z) & ~0x10UL) | 0x10);
        M.B(z + 0x80, 0);
    }

    public void TargetCtor_276EA8(uint x)
    {
        M.B(x + 0x61, 1);
        M.U(x + 0x64, VtTargetBase);
        M.U(x, 0);
        M.B(x + 0x60, 0);
        M.V(x + 0x10, new Vector4(0f, 0f, 0f, 1f));
        Zero4(x + 0x20);
        M.U(x + 0x74, 0);
        M.U(x + 0x64, VtTargetRig);
        ZoneLocalCtor_278C30(x + 0x180);
        Zero4(x + 0xF0);
        Zero4(x + 0x100);
        Zero4(x + 0x130);
        Zero4(x + 0x140);
        M.B(x + 0x60, 0);
        TargetInit_276FD0(x);
    }

    public void TargetInit_276FD0(uint x)
    {
        M.U(x + 0x70, 0);
        M.Copy16(x + 0xA0, x + 0xF0);
        M.U(x + 0x78, 0);
        M.U(x + 0x7C, 0);
        M.F(x + 0x90, 0.5f);
        for (uint k = 0; k < 12; k += 4) M.F(x + 0xA0 + k, (M.F(x + 0xA0 + k) + M.F(x + 0x100 + k)) * 0.5f);
        uint inst = Handle(x + 0x74);
        if (inst == 0)
        {
            M.V(x + 0x10, new Vector4(0f, 0f, 0f, 1f));
            Zero4(x + 0x20);
            Zero4(x + 0xB0);
            M.U(x, 0);
            M.U(x + 0x8C, 0);
        }
        else
        {
            uint node = M.U(inst + 8);
            NodeRefresh_18AFA8(node);
            M.V(x + 0x20, TargetPoint_276C60(x, node));
            M.V(x + 0x10, ToV(GameMath.MatrixToQuat_186900(M.M(node))));
            M.Copy16(x + 0x40, x + 0x20);
            M.Copy16(x + 0x30, x + 0x10);
            M.U(x + 0x8C, M.U(node + 0x34));
            M.V(x + 0xB0, TargetHeight_276B40(0f, x, M.V(x + 0x20)));
            ulong fl = M.Q(x + 0x70);
            Vector4 off;
            if ((fl & 0x40000) != 0 || (fl & 0x90000) == 0x80000) off = M.V(x + 0xA0);
            else off = GameMath.RotateByQuat_186D98(ToQ(M.V(x + 0x10)), M.V(x + 0xA0), false);
            M.F(x + 0xB0, M.F(x + 0xB0) + off.X);
            M.F(x + 0xB4, M.F(x + 0xB4) + off.Y);
            M.F(x + 0xB8, M.F(x + 0xB8) + off.Z);
        }
        M.U(x + 0x170, 0);
        M.U(x + 0x88, M.U(x + 0x8C));
        M.Q(x + 0x70, M.Q(x + 0x70) & ~0x200UL);
        Zero4(x + 0x150);
        Zero4(x + 0x160);
        M.Q(x + 0x70, M.Q(x + 0x70) | 0x4000);
        Zero4(x + 0xC0);
        TargetReset_27C128(x);
        M.U(x + 0x180, 0);
        M.Q(x + 0x70, M.Q(x + 0x70) & 0xFFFFFFFFFFEFFFFFUL);
        M.Q(x + 0x180, (M.Q(x + 0x180) & ~0x10UL) | 0x10);
        TargetReset_27C128(x);
    }

    public void ChannelCtor_2706D8(uint c, int value)
    {
        M.I(c + 0xC, value);
        M.I(c + 0x24, GameMath.FromValue_11BFA0(90f, 1));
        M.I(c + 0x28, GameMath.FromValue_11BFA0(90f, 1));
        M.I(c + 0x34, value);
        M.I(c + 0x38, value);
        M.I(c + 0x3C, value);
        M.U(c, M.U(c) & 0xFFFFFBFF);
        M.I(c + 0x40, value);
        ChannelReset_270770(c);
    }

    public void ChannelReset_270770(uint c)
    {
        M.I(c + 0x14, M.I(c + 0xC));
        int range = M.I(c + 0x38) - M.I(c + 0x34);
        M.I(c + 0x2C, M.I(c + 0x24));
        M.I(c + 0x18, 0);
        float ratio = range != 0 ? (M.I(c + 0xC) - M.I(c + 0x34)) / (float)range : 0.5f;
        M.F(c + 0x10, ratio);
        M.I(c + 0x20, 0);
        uint fl = M.U(c);
        M.U(c, (fl & 1) | (((fl >> 10) & 1) << 10));
        M.I(c + 0x44, M.I(c + 0x14));
        M.I(c + 4, 0);
        M.I(c + 8, 0);
    }

    public void DistanceChannelReset_27BA58(uint c)
    {
        float range = M.F(c + 0x38) - M.F(c + 0x34);
        M.I(c + 0x18, 0);
        M.U(c + 0x14, M.U(c + 0xC));
        if (5e-5f < MathF.Abs(range)) M.F(c + 0x10, (M.F(c + 0xC) - M.F(c + 0x34)) / range);
        else M.U(c + 0x10, 0x7149F2CA);
        M.U(c + 0x2C, M.U(c + 0x24));
        M.U(c, M.U(c) & 1);
        M.U(c + 0x44, M.U(c + 0x14));
        M.I(c + 8, 0);
        M.I(c + 0x48, 0);
        M.I(c + 4, 0);
    }

    public void EyeCtor_272EE0(float dist, uint y, int pitch)
    {
        M.B(y + 0x30, 1);
        M.U(y + 0x34, VtEyeBase);
        M.U(y, 0);
        M.U(y + 4, M.U(0x30A450));
        M.V(y + 0x10, new Vector4(0f, 0f, 0f, 1f));
        Zero4(y + 0x20);
        M.U(y + 0x4C, 0);
        M.U(y + 0x34, VtEyeRig);
        M.U(y + 0x50, 0);
        ChannelCtor_2706D8(y + 0x54, pitch);
        ChannelCtor_2706D8(y + 0xA4, M.I(0x30A450));
        ChannelCtor_2706D8(y + 0xF4, GameMath.FromValue_11BFA0(0f, 0));
        M.F(y + 0x150, dist);
        M.F(y + 0x16C, 5f);
        M.F(y + 0x168, 5f);
        M.F(y + 0x178, dist);
        M.F(y + 0x17C, dist);
        M.F(y + 0x180, dist);
        M.F(y + 0x184, dist);
        M.U(y + 0x18C, 0);
        DistanceChannelReset_27BA58(y + 0x144);
        M.F(y + 0x1C8, 1f);
        M.I(y + 0x1CC, GameMath.FromValue_11BFA0(180f, 1));
        M.I(y + 0x1D0, GameMath.FromValue_11BFA0(180f, 1));
        ZoneLocalCtor_278C30(y + 0x2F0);
        M.U(y + 0x388, 0);
        M.F(y + 0x394, 1f);
        M.F(y + 0x398, 1f);
        M.U(y + 0x380, 0);
        M.F(y + 0x390, 6f);
        M.B(y + 0x384, 0x3C);
        M.I(y + 0x3C8, GameMath.FromValue_11BFA0(M.F(y + 0x388), 0));
        M.B(y + 0x3CC, 0);
        M.U(y + 0xA4, M.U(y + 0xA4) | 1);
        M.Q(y + 0x40, (M.Q(y + 0x40) & 0xFFFFFFFDFFFFFFFDUL) | 0x400000000000UL);
        Zero4(y + 0x220);
        Zero4(y + 0x230);
        Zero4(y + 0x240);
        Zero4(y + 0x250);
        for (uint k = 0; k < 16; k++) M.F(y + 0x1D8 + k * 4, (k & 1) == 0 ? 1f : -1f);
        Zero4(y + 0x1A0);
        EyeSetDistance_27BD08(dist, y, pitch);
    }

    public void EyeSetDistance_27BD08(float dist, uint y, int pitch)
    {
        M.F(y + 0x17C, dist);
        M.F(y + 0x178, dist);
        M.F(y + 0x150, (dist + dist) * 0.5f);
        M.I(y + 0x60, pitch);
        EyeInit_273218(y);
    }

    public void EyeInit_273218(uint y)
    {
        ulong old = M.Q(y + 0x40);
        ulong tied = (old >> 1) & 1;
        EyeRestoreSpeeds_27BDB0(y);
        M.U(y + 0x60, M.U(0x30A658));
        ChannelReset_270770(y + 0x54);
        ChannelReset_270770(y + 0xA4);
        M.I(y + 0x100, DefaultYaw_2711B8(y));
        ChannelReset_270770(y + 0xF4);
        Zero4(y + 0x1A0);
        if (tied != 0) M.F(y + 0x150, M.F(y + 0x17C) * M.F(y + 0x64) + M.F(y + 0x178) * (1f - M.F(y + 0x64)));
        float range = M.F(y + 0x17C) - M.F(y + 0x178);
        M.F(y + 0x15C, 0f);
        M.F(y + 0x158, M.F(y + 0x150));
        if (5e-5f < MathF.Abs(range)) M.F(y + 0x154, (M.F(y + 0x150) - M.F(y + 0x178)) / range);
        else M.U(y + 0x154, 0x7149F2CA);
        M.U(y + 0x144, M.U(y + 0x144) & 1);
        M.U(y + 0x170, M.U(y + 0x168));
        M.U(y + 0x188, M.U(y + 0x158));
        M.U(y + 0x14C, 0);
        M.U(y + 0x18C, 0);
        M.U(y + 0x148, 0);
        M.U(y + 0x40, 0);
        M.B(y + 0x48, 0);
        M.B(y + 0x49, 0);
        M.B(y + 0x1D4, 0);
        M.Q(y + 0x40, (M.Q(y + 0x40) & 0xFFFF9FFBFFFFFEFDUL) | (((old >> 46) & 1) << 46) | (tied << 1) | (((old >> 8) & 1) << 8));
        uint xs = M.U(y + 0x50);
        var target = xs == 0 ? new Vector4(0f, 0f, 0f, 1f) : M.V(xs + 0x20);
        var eye = EyeFromAngles_2712D0(y, target, false);
        if (xs == 0) Zero4(y + 0x20);
        else M.V(y + 0x20, eye);
        M.U(y + 4, M.U(y + 0xB8));
        M.Q(y + 0x40, M.Q(y + 0x40) & 0xFFFFFFFFFFFFFF27UL);
        M.Q(y + 0x48, M.Q(y + 0x48) & ~0x100UL);
        M.U(y + 0x2EC, 0);
        EyeZoneReset_274170(y);
        M.I(y + 0x2E0, GameMath.FromValue_11BFA0(0f, 0));
        M.I(y + 0x2E4, GameMath.FromValue_11BFA0(0f, 0));
        M.U(y + 0x39C, 0);
        M.U(y + 0x3B8, 0);
        M.F(y + 0x3C0, 0.4f);
        M.Q(y + 0x40, (M.Q(y + 0x40) & 0xFFFFFFC7FFFFFFFFUL) | 0x4000008000UL);
        M.F(y + 0x3BC, 0.4f);
        M.Q(y + 0x40, M.Q(y + 0x40) & 0xFFFFE07FFFFFFFFFUL);
        M.U(y + 0x2F0, 0);
        M.Q(y + 0x40, M.Q(y + 0x40) & ~0x800UL);
        M.Q(y + 0x2F0, (M.Q(y + 0x2F0) & ~0x10UL) | 0x10);
        EyeZoneReset_274170(y);
        M.U(y + 0x3F0, 0);
        if (xs == 0) return;
        var above = M.V(xs + 0x40);
        above.Y += 1f;
        if (Obstructed_276780(y, M.V(y + 0x20), above) && Obstruction_2760B8(y, M.V(y + 0x20), above, out var moved, false))
        {
            M.V(y + 0x20, moved);
            EyeSnapToEye_273640(y);
        }
    }

    public void EyeSnapToEye_273640(uint y)
    {
        uint xs = M.U(y + 0x50);
        var v = new Vector4(M.F(xs + 0x20) - M.F(y + 0x20), M.F(xs + 0x24) - M.F(y + 0x24), M.F(xs + 0x28) - M.F(y + 0x28), 1f);
        M.F(y + 0x158, MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z));
        int yaw = GameMath.Yaw_18C830(v);
        var flat = GameMath.Rotate3_18E4B8(GameMath.RotY_18BBC0(-yaw), v);
        M.I(y + 0x68, GameMath.Wrap16(GameMath.Pitch_18C898(flat)));
        M.I(y + 0x108, GameMath.Wrap16(yaw));
        M.I(y + 0x6C, GameMath.FromValue_11BFA0(0f, 0));
        M.I(y + 0x10C, GameMath.FromValue_11BFA0(0f, 0));
        M.F(y + 0x15C, 0f);
        var target = M.V(xs + 0x20);
        M.V(y + 0x20, EyeFromAngles_2712D0(y, target, true));
        EyeFinish_2714A8(y, target, M.V(y + 0x20), false);
    }

    public void EyeAttach_27BC98(uint y, uint cameraObj, uint x)
    {
        M.U(y + 0x50, x);
        M.U(y + 0x4C, cameraObj);
        _eyeCaches.Remove(y);
        EyeInit_273218(y);
    }

    public void DirectorActivate_26EAD8(uint d, uint cameraObj)
    {
        uint x = M.U(d + 0x10);
        if (x != 0)
        {
            if (M.U(x + 0x64) == VtCutTargetRig) CutTargetInit_27C3D0(x); // Amedo 2026-09-29
            else TargetInit_276FD0(x);
            uint fa = M.U(d + 8);
            if (fa != 0) FilterSnap_27D6F8(fa, x + 0x20);
        }
        uint y = M.U(d + 0x14);
        if (y == 0) return;
        if (M.U(y + 0x34) == VtCutEyeRig) CutEyeInit_277FE8(y); // Amedo 2026-09-29
        else EyeAttach_27BC98(y, cameraObj, x);
        uint fb = M.U(d + 0xC);
        if (fb != 0) FilterSnap_27D6F8(fb, y + 0x20);
    }

    // Amedo 2026-09-29
    public void DirectorActivateVirtual(uint d, uint cameraObj)
    {
        uint vt = M.U(d + 0x30);
        if (vt == VtPlayerDirector || vt == VtCutDirector) DirectorActivate_26EAD8(d, cameraObj);
        else Missing($"director vt+0x14 for vtable 0x{vt:X}");
    }

    public uint SetDirector_27B888(uint c9, uint d, bool activate)
    {
        uint cur = M.U(c9 + 0x2C);
        if (cur != d)
        {
            uint fl = M.U(c9 + 0x18);
            if ((fl & 2) != 0)
            {
                if (cur != 0) Missing("0x27B888 release owned director (vt+0xC)");
                fl &= 0xFFFFFFFD;
                M.U(c9 + 0x18, fl);
            }
            M.U(c9 + 0x2C, d);
            M.U(c9 + 0x18, fl & 0xFFFFFFFD);
        }
        uint f2 = M.U(c9 + 0x18);
        if ((f2 & 4) != 0)
        {
            if (M.U(c9 + 0x30) != 0) Missing("0x27B888 release owned next director (vt+0xC)");
            f2 &= 0xFFFFFFFB;
        }
        M.U(c9 + 0x34, 0);
        M.U(c9 + 0x30, 0);
        M.U(c9 + 0x18, f2 & 0xFFFFFFFB);
        if (activate)
        {
            uint dd = M.U(c9 + 0x2C);
            if (dd == 0) return 0;
            DirectorActivateVirtual(dd, M.U(c9)); // Amedo 2026-09-29
        }
        return M.U(c9 + 0x2C);
    }

    public void CameraComponentCtor_27B5E0(uint c9)
    {
        ComponentBase_191D98(c9);
        M.U(c9 + 0x2C, 0);
        M.U(c9 + 0x30, 0);
        M.U(c9 + 0x14, VtCameraComponent);
        M.U(c9 + 0x18, 1);
        M.I(c9 + 0x1C, GameMath.FromValue_11BFA0(0.7853982f, 0));
        M.F(c9 + 0x24, 0.1f);
        M.F(c9 + 0x28, 1500f);
        M.F(c9 + 0x20, M.I(0x30A770) == 3 ? 1f : 0.96f);
        M.U(c9 + 0x34, 0);
        M.U(c9 + 0x38, 0);
    }

    public uint CameraObject_259488()
    {
        uint obj = Alloc(0x160, "camera object");
        uint node = Alloc(0x70, "camera node");
        M.M(node, Matrix4x4.Identity);
        M.U(obj + 8, node);
        uint c9 = Alloc(0x3C, "camera comp9");
        CameraComponentCtor_27B5E0(c9);
        M.U(c9 + 0x18, M.U(c9 + 0x18) | 1);
        M.U(c9 + 0x1C, M.U(0x30A450));
        M.U(c9, obj);
        M.U(obj + 0xD8 + 9 * 4, c9);
        return obj;
    }

    public void ControllerInit_15E6A0(uint p, uint cameraObj, uint owner, uint c)
    {
        uint c9 = CameraComponent(cameraObj);
        ulong old = M.Q(p);
        M.U(p, 0);
        M.U(p + 8, 0);
        M.Q(p, (M.Q(p) & 0xFFFFFFFFFFFFFFD8UL) | (old & 0x27));
        M.U(p + 4, 0);
        M.U(p + 0xC, 0);
        M.U(p + 0x10, 0);
        DirectorInit_141F68(p + 0x20, owner, c);
        M.U(p + 0x720, SetDirector_27B888(c9, p + 0x20, true));
    }

    public void DirectorInit_141F68(uint d, uint owner, uint c)
    {
        M.F(d + 0xA4, 6f);
        M.Q(d + 0x80, (M.Q(d + 0x80) & 0xFFFFFF0FFFFFFFFFUL) | 0x3000000000UL);
        uint have = Handle(d + 0x154);
        if (have != owner) M.U(d + 0x154, owner == 0 ? 0u : HandleOf(owner));
        M.Copy16(d + 0x1D0, 0x30B7B0);
        M.F(d + 0xD4, 6f);
        M.F(d + 0x4E8, 1f);
        M.Copy16(d + 0x1E0, 0x30B7C0);
        M.Q(d + 0xB0, (M.Q(d + 0xB0) & 0xFFFFFF0FFFFFFFFFUL) | 0x1000000000UL);
        M.V(d + 0x510, new Vector4(0f, 0f, -0.5f, 1f)); // Amedo 2026-10-30
        M.V(d + 0x520, new Vector4(0f, 0f, -0.5f, 1f));
        M.V(d + 0x530, new Vector4(0f, 0f, 0f, 1f));
        M.V(d + 0x540, new Vector4(0f, 0f, 0f, 1f));
        M.F(d + 0x4C8, 0.5f);
        M.F(d + 0x4CC, -0.5f);
        M.F(d + 0x504, -0.5f);
        M.F(d + 0x4FC, -0.5f);
        M.F(d + 0x4F0, 0.8f);
        M.F(d + 0x4F4, -0.8f);
        M.F(d + 0x4E0, 0.42f);
        M.F(d + 0x500, 0.5f);
        M.F(d + 0x4E4, -0.42f);
        M.F(d + 0x4EC, -0.8f);
        M.F(d + 0x4D0, 0.8f);
        M.F(d + 0x4D4, -0.8f);
        M.F(d + 0x4D8, 0.42f);
        M.F(d + 0x4F8, 0.5f);
        M.F(d + 0x4DC, -0.42f);
        M.F(d + 0x4B8, 10f);
        M.U(d + 0x4C0, M.U(0x30A1D0));
        M.U(d + 0x4BC, M.U(0x30A1C8));
        M.Q(d + 0x330, (M.Q(d + 0x330) & ~2UL) | 2);
        float tps = M.F(TicksPerSecond);
        int tenth = GameMath.Trunc(tps * 0.1f);
        M.U(d + 0x36C, M.U(0x30A1D8));
        M.U(d + 0x37C, M.U(0x30A1F0));
        M.U(d + 0x378, M.U(0x30A1E8));
        M.I(d + 0x360, tenth);
        M.U(d + 0x368, M.U(0x30A208));
        M.U(d + 0x40C, M.U(0x30A1E0));
        M.I(d + 0x400, GameMath.Trunc(tps));
        M.U(d + 0x408, M.U(0x30A200));
        M.F(d + 0x458, 10f);
        M.F(d + 0x468, 2f);
        M.F(d + 0x46C, 13f);
        M.I(d + 0x450, tenth);
        M.F(d + 0x45C, 10f);
        M.U(d + 0x3B0, 0);
        M.U(d + 0x3B8, M.U(0x30A200));
        PlayerMode_1428B0(d, c);
    }

    public void PlayerBind_1422F0(uint d, uint c)
    {
        if ((CharacterKind?.Invoke(c) ?? 0) == 5) { SetMechaView_142B18(d); return; } // Amedo
        M.F(d + 0x46C, 13f);
        M.F(d + 0x440, 7.5f);
        M.F(d + 0x468, 2f);
        ulong f50 = M.Q(d + 0x50);
        M.U(d + 0x434, (M.U(d + 0x434) & 0xFFFFFFFE) | (uint)((f50 >> 2) & 1));
        M.U(d + 0x350, M.U(0x30A1F8));
        M.U(d + 0x344, (M.U(d + 0x344) & 0xFFFFFFFE) | (uint)((f50 >> 1) & 1));
        M.U(d + 0x3E4, (M.U(d + 0x3E4) & 0xFFFFFFFE) | (uint)(f50 & 1));
        ResetDefaults_142668(d, ((f50 >> 3) & 1) != 0);
        M.Q(d + 0x50, M.Q(d + 0x50) & 0xFFFFFFFFFFFFFF8FUL);
    }

    public void ControllerBind_15E7C8(uint p, uint c)
    {
        PlayerBind_1422F0(p + 0x20, c);
        M.Q(p + 0x350, (M.Q(p + 0x350) & 0xFFFFBFFFFFFFFFFFUL) | (((M.Q(p) >> 1) & 1) << 46));
    }

    public uint PlayerComponentInit_17B310(uint comp, uint owner, uint c)
    {
        uint cam = CameraObject_259488();
        uint have = Handle(comp + 0x20);
        if (have != cam) M.U(comp + 0x20, HandleOf(cam));
        M.B(cam + 0x153, 0);
        ControllerInit_15E6A0(comp + 0x30, cam, owner, c);
        ControllerBind_15E7C8(comp + 0x30, c);
        M.U(comp + 0x760, 0);
        return cam;
    }

    public uint CreatePlayerCamera(uint owner, uint c)
    {
        uint comp = Alloc(0x780, "player camera comp22");
        PlayerComponentCtor_17B2B8(comp, 0);
        M.U(comp, owner);
        PlayerComponentInit_17B310(comp, owner, c);
        return comp;
    }
}
