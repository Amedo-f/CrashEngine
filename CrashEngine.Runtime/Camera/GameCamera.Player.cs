using System.Numerics;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed partial class GameCamera
{
    public Func<uint, Vector4>? CharacterVelocity { get; set; }
    public Func<uint, int>? CharacterKind { get; set; }
    public Func<uint, Vector4>? VehicleVelocity { get; set; } // Amedo 2026-09-29
    public Func<uint, int>? VehicleKind { get; set; }

    private Vector4 Velocity_13FE50(uint c)
    {
        uint v = M.U(c + 0xB8); // Amedo 2026-09-29
        if (v != 0) return VehicleVelocity?.Invoke(v) ?? Vector4.Zero;
        return CharacterVelocity?.Invoke(c) ?? Vector4.Zero;
    }

    public void PlayerCameraUpdate_172A48(uint comp, uint time, uint activeInst, uint c)
    {
        uint owner = M.U(comp);
        if (activeInst == owner || activeInst == 0)
        {
            if ((M.B(time) & 1) == 0) M.Q(comp + 0x18, M.Q(comp + 0x18) | 1);
            else
            {
                float left = CamMath.Trunc(M.I(comp + 0x760) - M.I(time + 8) * M.F(SecondsPerTick));
                M.I(comp + 0x760, (int)left);
                if (left < 0f) M.I(comp + 0x760, 0);
                ControllerUpdate_1436A8(comp + 0x30, time, c, Handle(comp + 0x20));
                M.Q(comp + 0x18, M.Q(comp + 0x18) & ~1UL);
            }
        }
        else Missing("0x17B460 camera of an inactive character");
    }

    public void ControllerUpdate_1436A8(uint p, uint time, uint c, uint camObj)
    {
        uint zd = ZoneData(M.U(p + 8));
        ulong allowed = zd == 0 ? 1UL : (M.Q(zd) >> 1) & 1;
        if (M.U(p + 8) != M.U(p + 0xC)) M.Q(p + 0x70, M.Q(p + 0x70) & 0xFFFFFFFFFFFFFF8FUL);
        if (((M.Q(c + 0x70) >> 14) & 1) != 0 && M.U(p + 0x10) != 0)
        {
            M.U(p + 8, M.U(p + 0x10));
            M.Q(p, M.Q(p) | 0x80);
        }
        ulong fl = M.Q(p);
        if (allowed != 0 && (fl & 8) != 0)
        {
            fl = (fl & 0x10) == 0 ? fl | 0x10 : fl & ~0x10UL;
            M.Q(p, fl);
            uint c9 = CameraComponent(camObj);
            M.U(p + 0x720, StartTransition_2700C0(c9, p + 0x20, CamMath.Trunc(M.F(TicksPerSecond) * 0.5f), 0));
            M.Q(p, M.Q(p) & ~8UL);
        }
        fl = M.Q(p);
        uint director = M.U(p + 0x720);
        if ((fl & 0x10) == 0) M.U(director + 4, M.U(p + 8));
        else M.U(p + 8, 0);
        M.Q(director, (M.Q(director) & ~1UL) | (M.Q(p) & 1));
        M.Q(director, (M.Q(director) & ~2UL) | (((M.Q(p) >> 2) & 1) << 1));
        if ((M.Q(p) & 0x20) != 0)
        {
            if (M.U(director + 0x30) == VtPlayerDirector) PlayerDirectorUpdate_1423F0(director, time, c);
            else Missing($"director vt+0x54 for vtable 0x{M.U(director + 0x30):X}");
        }
        if ((M.Q(p) & 0x40) == 0) M.Q(p, (M.Q(p) & ~0x40UL) | (((M.Q(c + 0x70) >> 14) & 1) << 6));
        M.U(p + 0xC, M.U(p + 8));
    }

    public uint CameraComponent(uint camObj) => camObj != 0 ? M.U(camObj + 0xD8 + 9 * 4) : 0u;

    public void PlayerDirectorUpdate_1423F0(uint d, uint time, uint c)
    {
        uint partner = M.U(c + 0xB8);
        M.U(d + 0x158, partner != 0 ? partner + 0x10 : 0u);
        if (((M.Q(c + 0x70) >> 14) & 1) != 0) M.Q(d + 0x330, (M.Q(d + 0x330) & ~0x100000UL) | 0x100000);
        M.U(d + 0x168, M.U(c + 0xD4));
        M.U(d + 0x170, M.U(d + 0x354));
        M.U(d + 0x6B4, M.U(c + 0xD4));
        if (((M.Q(d + 0x330) >> 5) & 1) == 0)
        {
            M.U(d + 0x34C, M.U(d + 0x64));
            M.F(d + 0x3EC, -M.F(d + 0x60));
            M.F(d + 0x43C, -M.F(d + 0x68));
        }
        else
        {
            M.U(d + 0x34C, 0);
            M.U(d + 0x3EC, 0);
            M.U(d + 0x43C, 0);
        }
        bool manual = 5e-5f < MathF.Abs(M.F(d + 0x64)) || 5e-5f < MathF.Abs(M.F(d + 0x60)) || 5e-5f < MathF.Abs(M.F(d + 0x68));
        ulong mask;
        if (manual)
        {
            M.Q(d + 0x330, (M.Q(d + 0x330) & ~8UL) | 8);
            mask = 0;
            M.Q(d + 0x150, (M.Q(d + 0x150) & ~0x400UL) | 0x400);
            M.Q(d + 0x50, M.Q(d + 0x50) | 0x100);
        }
        else
        {
            M.Q(d + 0x330, M.Q(d + 0x330) & ~8UL);
            M.Q(d + 0x150, M.Q(d + 0x150) & ~0x400UL);
            M.Q(d + 0x50, M.Q(d + 0x50) & ~0x100UL);
            mask = 1;
        }
        Moving_15E310(d, c, (mask & (((M.Q(d + 0x330) >> 10) & 0xFF) ^ 1) & 1) != 0);
        M.U(d + 0x348, M.U(d + 0x58));
        if ((M.Q(d + 0x50) & 0x400) == 0) M.U(d + 0x3E8, M.U(d + 0x54));
        M.U(d + 0x438, M.U(d + 0x5C));
        PlayerMode_1428B0(d, c);
        M.I(d + 0x6C0, CamMath.FromValue_11BFA0(M.F(c + 0x300), 0));
        PartnerAngle_15E4F8(d, c);
    }

    public void Moving_15E310(uint d, uint c, bool unused)
    {
        var v = Velocity_13FE50(c);
        if (v.X * v.X + v.Z * v.Z < 0.5f)
        {
            M.Q(d + 0x330, M.Q(d + 0x330) & ~0x40UL);
            M.Q(d + 0x50, M.Q(d + 0x50) & ~0x400UL);
        }
        else
        {
            M.Q(d + 0x330, M.Q(d + 0x330) | 0x40);
            M.Q(d + 0x50, M.Q(d + 0x50) | 0x400);
        }
    }

    public void PartnerAngle_15E4F8(uint d, uint c)
    {
        if (M.U(c + 0xB0) == 0) M.I(d + 0x6E0, 0);
        else M.U(d + 0x6E0, LinkedSecond_15FE98?.Invoke() ?? 0u); // Amedo
    }

    public Func<uint>? LinkedSecond_15FE98 { get; set; } // Amedo

    // Amedo
    public void SetHoverboardView_15E4B8(uint d)
    {
        M.B(d + 0x140, 1);
        M.Q(d + 0x330, M.Q(d + 0x330) | 0x10000);
    }

    // Amedo
    public void PlayerDirectorRestore_15E1B0(uint d)
    {
        M.U(d + 0x10, d + 0xE0);
        M.U(d + 0xC, d + 0xB0);
        M.U(d + 0x14, d + 0x2F0);
        M.U(d + 8, d + 0x80);
    }

    // Amedo
    public void PlayerCameraReset_15E538(uint d)
    {
        PlayerDirectorRestore_15E1B0(d);
        TargetInit_276FD0(d + 0xE0);
        EyeInit_273218(d + 0x2F0);
        FilterSnap_27D6F8(d + 0x80, d + 0x100);
        FilterSnap_27D6F8(d + 0xB0, d + 0x310);
    }

    // Amedo
    public void SetMechaView_142B18(uint d)
    {
        uint yaw = M.U(0x30A250);
        M.U(d + 0x37C, yaw);
        M.U(d + 0x378, yaw);
        M.F(d + 0x46C, 12f);
        M.F(d + 0x468, 12f);
        uint pitch = M.U(0x30A248);
        M.U(d + 0x3CC, pitch);
        M.U(d + 0x3C8, pitch);
        M.V(d + 0x1D0, M.V(0x30B7E0));
        M.V(d + 0x1E0, M.V(0x30B7E0));
    }

    public void PlayerMode_1428B0(uint d, uint c)
    {
        var vel = Velocity_13FE50(c);
        if (M.U(c + 0xB8) == 0)
        {
            ulong f50 = M.Q(d + 0x50);
            if ((f50 & 8) != 0 && (f50 & 0x70) != 0)
            {
                if ((CharacterKind?.Invoke(c) ?? 0) == 5) SetMechaView_142B18(d); // Amedo
                else
                {
                    ResetDefaults_142668(d, true);
                    M.B(d + 0x140, 0);
                }
            }
            Modes_1432D0(d);
            YawReturn_142EC8(d, c);
            M.Q(d + 0x50, M.Q(d + 0x50) & 0xFFFFFFFFFFFFFF8FUL);
        }
        else // Amedo 2026-09-29
        {
            uint v = M.U(c + 0xB8);
            int kind = VehicleKind?.Invoke(v) ?? 0;
            switch (kind)
            {
                case 1:
                    if ((M.Q(d + 0x50) & 0x70) != 0x10) VehicleCamera_142C40(d);
                    Modes_1432D0(d);
                    VehicleRate_15E3C0(MathF.Sqrt(vel.X * vel.X + vel.Y * vel.Y + vel.Z * vel.Z), d, v);
                    break;
                case 3: // Amedo
                    HumiliskateView_142D28(d);
                    Modes_1432D0(d);
                    HumiliskateFrame_15E4E0(d);
                    break;
                case 5: // Amedo
                    if ((M.Q(d + 0x50) & 0x70) != 0x50) SetHoverboardView_15E4B8(d);
                    Modes_1432D0(d);
                    YawReturn_142EC8(d, c);
                    break;
                default:
                    if ((int)((M.Q(d + 0x50) >> 4) & 7) != kind && (M.Q(d + 0x50) & 8) != 0) ResetDefaults_142668(d, true);
                    YawReturn_142EC8(d, c);
                    M.B(d + 0x140, 0);
                    break;
            }
            M.Q(d + 0x50, (M.Q(d + 0x50) & 0xFFFFFFFFFFFFFF8FUL) | ((ulong)(kind & 7) << 4));
        }
        M.V(d + 0x130, vel); // Amedo
    }

    // Amedo 2026-09-29
    public void VehicleCamera_142C40(uint d)
    {
        M.B(d + 0x140, 1);
        M.Q(d + 0x50, M.Q(d + 0x50) | 8);
        EyeQuick_27BD60(d + 0x2F0, true);
        M.U(d + 0x3E4, M.U(d + 0x3E4) | 0x401);
        M.I(d + 0x400, CamMath.Trunc(M.F(TicksPerSecond) * 0.2f));
        M.U(d + 0x408, M.U(0x30A210));
        M.U(d + 0x344, M.U(d + 0x344) | 1);
        M.U(d + 0x360, 0);
        M.U(d + 0x368, M.U(0x30A218));
        M.Q(d + 0x330, M.Q(d + 0x330) | 0x10000);
        M.U(d + 0x434, M.U(d + 0x434) | 0x80);
        M.F(d + 0x474, 8f);
        M.F(d + 0x470, 8f);
        M.F(d + 0x2F0 + 0x394, 1f);
    }

    // Amedo
    public void HumiliskateView_142D28(uint d)
    {
        M.B(d + 0x140, 0);
        M.Q(d + 0x50, M.Q(d + 0x50) | 8);
        EyeQuick_27BD60(d + 0x2F0, true);
        M.U(d + 0x3E4, M.U(d + 0x3E4) | 0x401);
        M.U(d + 0x344, M.U(d + 0x344) | 1);
        M.U(d + 0x408, M.U(0x30A238));
        M.U(d + 0x400, 0);
        M.U(d + 0x360, 0);
        M.U(d + 0x368, M.U(0x30A218));
        M.Q(d + 0x330, M.Q(d + 0x330) | 0x10000);
        M.F(d + 0x2F0 + 0x394, 0.3f);
        M.F(d + 0x2F0 + 0x390, 25f);
        int wide = CamMath.FromValue_11BFA0(80f, 1), narrow = CamMath.FromValue_11BFA0(20f, 1);
        M.V(d + 0x1D0, new Vector4(0f, 1f, 0f, 1f));
        M.V(d + 0x1E0, new Vector4(0f, 1f, 0f, 1f));
        M.F(d + 0x434 + 0x38, 2.3f);
        M.F(d + 0x434 + 0x34, 2.3f);
        M.I(d + 0x37C, narrow);
        M.I(d + 0x3C8, wide);
        M.I(d + 0x3CC, wide);
        M.I(d + 0x378, narrow);
    }

    // Amedo
    public void HumiliskateFrame_15E4E0(uint d) => M.Q(d + 0x330, M.Q(d + 0x330) | 0x10000);

    public void VehicleRate_15E3C0(float speed, uint d, uint vehicle)
    {
        Rate_142B88(speed, d);
        float k = M.F(vehicle + 0xEC) - 0.8f;
        if (0f < k)
        {
            M.U(d + 0x434, M.U(d + 0x434) | 0x80);
            M.F(d + 0x474, k + 8f);
            M.F(d + 0x470, k + 8f);
            var offset = new Vector4(0f, k + 2f, 0f, 1f);
            TargetOffsets_27C1C0(d + 0xE0, offset, offset);
        }
        else M.U(d + 0x434, M.U(d + 0x434) & 0xFFFFFF7F);
        M.Q(d + 0x330, M.Q(d + 0x330) | 0x10000);
    }

    public void Rate_142B88(float speed, uint d)
    {
        if (50f < speed) speed = M.I(0x30A228) * CamMath.UnitToRad;
        int rate = CamMath.Scale_15DE90(speed * 0.02f, M.I(0x30A230)) + M.I(0x30A220);
        M.I(d + 0x400, CamMath.Trunc(M.F(TicksPerSecond) * 0.2f));
        M.I(d + 0x408, rate);
    }

    public void EyeQuick_27BD60(uint y, bool on) => M.Q(y + 0x40, (M.Q(y + 0x40) & ~0x100UL) | ((on ? 1UL : 0UL) << 8));

    public void TargetOffsets_27C1C0(uint x, Vector4 a, Vector4 b)
    {
        M.V(x + 0x110, a);
        M.Q(x + 0x70, M.Q(x + 0x70) | 8);
        M.V(x + 0x120, b);
    }

    public void Modes_1432D0(uint d)
    {
        ulong f50 = M.Q(d + 0x50);
        ulong f330;
        if ((f50 & 0x100) != 0)
        {
            M.U(d + 0x3E4, M.U(d + 0x3E4) & 0xFFFFFFFE);
            M.U(d + 0x344, M.U(d + 0x344) & 0xFFFFFFFE);
            M.Q(d + 0x330, (M.Q(d + 0x330) & ~0x80000UL) | 0x80000);
            M.Q(d + 0x50, M.Q(d + 0x50) | 0x200);
            f330 = M.Q(d + 0x330);
        }
        else if ((f50 & 0x400) != 0)
        {
            M.U(d + 0x3E4, M.U(d + 0x3E4) | 1);
            M.U(d + 0x344, M.U(d + 0x344) | 1);
            M.Q(d + 0x330, M.Q(d + 0x330) & ~0x80000UL);
            M.Q(d + 0x50, M.Q(d + 0x50) & ~0x200UL);
            f330 = M.Q(d + 0x330);
        }
        else if ((f50 & 0x200) != 0) f330 = M.Q(d + 0x330);
        else
        {
            M.U(d + 0x3E4, M.U(d + 0x3E4) & 0xFFFFFFFE);
            M.U(d + 0x344, (M.U(d + 0x344) & 0xFFFFFFFE) | 1);
            M.Q(d + 0x330, M.Q(d + 0x330) & ~0x80000UL);
            f330 = M.Q(d + 0x330);
        }
        if (((f330 >> 38) & 1) == 0)
        {
            M.U(d + 0x344, M.U(d + 0x344) & 0xFFFFFFFE);
            M.U(d + 0x3E4, M.U(d + 0x3E4) & 0xFFFFFFFE);
        }
    }

    public void YawReturn_142EC8(uint d, uint c)
    {
        if (((((M.Q(d + 0x330) >> 10) & 0xFF) ^ 1) & 1) == 0) return;
        if (5e-5f < MathF.Abs(M.F(d + 0x3E8)))
        {
            M.I(d + 0x408, M.I(0x30A200) << 1);
            return;
        }
        var vel = Velocity_13FE50(c);
        float speed = MathF.Sqrt(vel.X * vel.X + vel.Y * vel.Y + vel.Z * vel.Z);
        int wanted = CamMath.FromValue_11BFA0(0f, 0);
        if ((sbyte)M.B(d + 0x3E4) >= 0)
        {
            float grounded = ((M.I(M.U(c + 0x10) + 0x14) >> 2) & 1) != 0 ? 1f : 0.5f;
            int prev = M.I(d + 0x6F0);
            if (M.F(d + 0x6C) != 0f || M.F(d + 0x70) != 0f)
            {
                int a = CamMath.Yaw_18C830(new Vector4(M.F(d + 0x70), 0f, M.F(d + 0x6C), 1f));
                a = CamMath.Wrap16(a);
                a = CamMath.FromValue_11BFA0(MathF.Abs(a * CamMath.UnitToRad), 0);
                int r = M.I(0x30A210);
                if (0xB5 < a && a <= 0x3FFF)
                    wanted = CamMath.Trunc(r * (float)CamMath.Divide_11C008(1.5533430f, a - 0xB6) * CamMath.UnitToRad);
                else if (0x3FFF < a && a <= 0x6000)
                {
                    int k = CamMath.Divide_11C008(0.7853982f, a - 0x4000);
                    wanted = CamMath.Trunc(CamMath.Scale_15DE90(0.5f, r) * (k * CamMath.UnitToRad)) + r;
                }
                else if (0x6000 < a && a < 0x7F49)
                {
                    int k = CamMath.Divide_11C008(0.7679449f, a - 0x6000);
                    wanted = CamMath.Trunc(CamMath.Scale_15DE90(1.3f, r) * (1f - k * CamMath.UnitToRad));
                }
            }
            wanted = CamMath.Trunc((wanted - prev) * 0.1f) + prev;
            int scaled = CamMath.Scale_15DE90(speed, wanted);
            M.I(d + 0x408, CamMath.Scale_15DE90(grounded, scaled));
        }
        M.I(d + 0x6F0, wanted);
    }

    public void ResetDefaults_142668(uint d, bool quick)
    {
        uint y = d + 0x2F0;
        M.Q(d + 0x50, (M.Q(d + 0x50) & ~8UL) | ((quick ? 1UL : 0UL) << 3));
        M.Q(y + 0x40, (M.Q(y + 0x40) & ~0x100UL) | ((quick ? 1UL : 0UL) << 8));
        M.U(d + 0x394, M.U(d + 0x394) & 0xFFFFFF7F);
        M.U(d + 0x3E4, M.U(d + 0x3E4) & 0xFFFFFF7F);
        M.U(d + 0x434, M.U(d + 0x434) & 0xFFFFFF7F);
        M.U(d + 0x344, M.U(d + 0x344) & 0xFFFFFF7F);
        M.Q(d + 0xE0 + 0x70, M.Q(d + 0xE0 + 0x70) & ~8UL);
        M.U(d + 0x3E4, (M.U(d + 0x3E4) & 0xFFFFFBFE) | (uint)(M.B(d + 0x50) & 1));
        float tps = M.F(TicksPerSecond);
        if (!quick)
        {
            M.U(d + 0x408, M.U(0x30A200));
            M.I(d + 0x400, CamMath.Trunc(tps));
        }
        else
        {
            M.U(d + 0x408, M.U(0x30A210));
            M.I(d + 0x400, CamMath.Trunc(tps * 0.2f));
        }
        M.U(d + 0x344, (M.U(d + 0x344) & 0xFFFFFFFE) | (quick ? 1u : 0u));
        if (!quick)
        {
            M.U(d + 0x368, M.U(0x30A208));
            M.I(d + 0x360, CamMath.Trunc(tps * 0.1f));
        }
        else
        {
            M.I(d + 0x360, 0);
            M.U(d + 0x368, M.U(0x30A218));
        }
        if (quick)
        {
            M.F(y + 0x394, 1f);
            M.F(y + 0x390, 6f);
            M.Q(d + 0x330, M.Q(d + 0x330) & ~0x10000UL);
            M.U(d + 0x3CC, M.U(0x30A450));
            M.U(d + 0x3C8, M.U(0x30A450));
            M.F(d + 0x46C, 13f);
            M.F(d + 0x468, 2f);
            M.U(d + 0x37C, M.U(0x30A1F0));
            M.U(d + 0x378, M.U(0x30A1E8));
            M.Copy16(d + 0x1D0, 0x30B7B0);
            M.Copy16(d + 0x1E0, 0x30B7C0);
        }
    }

    public void SetInput(uint d, float rightX, float rightY, float axis2, float leftA, float leftB, float button0, float button1, float button2)
    {
        M.F(d + 0x60, rightX);
        M.F(d + 0x64, rightY);
        M.F(d + 0x68, axis2);
        M.F(d + 0x6C, leftA);
        M.F(d + 0x70, leftB);
        M.F(d + 0x54, button0);
        M.F(d + 0x58, button1);
        M.F(d + 0x5C, button2);
    }
}
