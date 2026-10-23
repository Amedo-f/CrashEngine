namespace CrashEngine.Runtime.Camera;

// Amedo
public sealed partial class GameCamera
{
    public void CameraNodeEnableFlags_1201B8()
    {
        uint comp = Component_192118(PlayerObject(), 0x16);
        if (comp == 0) return;
        M.Q(comp + 0x380, M.Q(comp + 0x380) | 0x800);
        EyeZoneReset_274170(comp + 0x340);
        M.Q(comp + 0x1A0, M.Q(comp + 0x1A0) | 0x100000);
        TargetReset_27C128(comp + 0x130);
    }

    public void CameraNodeClearFlags_111A90()
    {
        uint comp = Component_192118(PlayerObject(), 0x16);
        if (comp == 0) return;
        M.U(comp + 0x630, 0);
        M.Q(comp + 0x630, (M.Q(comp + 0x630) & ~0x10UL) | 0x10);
        M.Q(comp + 0x380, M.Q(comp + 0x380) & ~0x800UL);
        EyeZoneReset_274170(comp + 0x340);
        M.U(comp + 0x2B0, 0);
        M.Q(comp + 0x2B0, (M.Q(comp + 0x2B0) & ~0x10UL) | 0x10);
        M.Q(comp + 0x1A0, M.Q(comp + 0x1A0) & ~0x100000UL);
        TargetReset_27C128(comp + 0x130);
    }

    public void RestoreCameraDefaults_11FDC0()
    {
        uint player = PlayerObject();
        uint comp = Component_192118(player, 0x16);
        if (comp == 0) return;
        uint p = comp + 0x30;
        PlayerBind_1422F0(p + 0x20, M.U(Component_192118(player, 0xC) + 0x18));
        M.Q(p + 0x350, (M.Q(p + 0x350) & 0xFFFFBFFFFFFFFFFFUL) | (((M.Q(p) >> 1) & 1) << 46));
    }

    public void SetCameraNodeValue_111C68()
    {
        uint comp = Component_192118(PlayerObject(), 0x16);
        if (comp == 0) return;
        uint cmd = Command;
        switch (M.U(cmd + 0xC) & 7)
        {
            case 0:
                M.Q(comp + 0x630, M.Q(comp + 0x630) | 0x1000);
                M.U(comp + 0x698, M.U(cmd + 0x10));
                break;
            case 1:
                M.Q(comp + 0x630, M.Q(comp + 0x630) | 0x8000);
                M.I(comp + 0x6A0, CamMath.FromValue_11BFA0(M.F(cmd + 0x10), 0));
                break;
        }
    }

    // Amedo
    public void SetCamera_10F1D0(uint flags, int angle, float value)
    {
        uint comp = Component_192118(PlayerObject(), 0x16);
        if (comp == 0) return;
        if ((flags & 2) != 0)
        {
            M.U(comp + 0x484, M.U(comp + 0x484) | 1);
            M.F(comp + 0x490, value);
        }
        if ((flags & 1) != 0)
        {
            M.U(comp + 0x394, M.U(comp + 0x394) | 1);
            M.I(comp + 0x3A0, angle);
        }
    }

    // Amedo
    public void CameraTopdownMode_112060()
    {
        uint comp = Component_192118(PlayerObject(), 0x16);
        if (comp == 0) return;
        uint orbit = Alloc(0x50, "orbit sub-camera (652)");
        M.U(orbit, 1);
        M.U(orbit + 0xC, OrbitCamera.BaseTable);
        M.U(orbit + 8, 0);
        M.B(orbit + 0x40, 1);
        M.U(orbit + 0xC, OrbitCamera.Table);
        M.F(orbit + 0x10, 11f);
        M.F(orbit + 0x1C, 16f);
        M.F(orbit + 0x20, 20f);
        M.V(orbit + 0x30, new System.Numerics.Vector4(BitConverter.UInt32BitsToSingle(0xBDA0C9DA), BitConverter.UInt32BitsToSingle(0x3FA0FCF8),
                                                      BitConverter.UInt32BitsToSingle(0xBE33DAF9), 1f));
        M.F(orbit + 0x18, 16f);
        M.U(orbit + 0x14, 0);
        M.U(orbit + 0x24, 0);
        SubCameras[orbit] = new OrbitCamera(orbit);
        int angle = CamMath.FromValue_11BFA0(60f, 1);
        M.Q(comp + 0x630, M.Q(comp + 0x630) | 0x80);
        M.I(comp + 0x678, angle);
        M.I(comp + 0x67C, angle);
        M.U(comp + 0x63C, orbit);
        M.Q(comp + 0x380, M.Q(comp + 0x380) | 0x800);
        EyeZoneReset_274170(comp + 0x340);
        M.U(comp + 0x2B0, 0);
        M.Q(comp + 0x2B0, (M.Q(comp + 0x2B0) & ~0x10UL) | 0x10);
        M.Q(comp + 0x1A0, M.Q(comp + 0x1A0) | 0x100000);
        TargetReset_27C128(comp + 0x130);
    }
}
