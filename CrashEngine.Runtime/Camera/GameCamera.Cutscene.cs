using System.Numerics;
using CrashEngine.Runtime.AgentLab;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-29
public sealed partial class GameCamera
{
    public const uint VtCutDirector = 0x304880, VtCutTargetRig = 0x304980, VtCutEyeRig = 0x3049B8;
    private static readonly float Tiny = BitConverter.Int32BitsToSingle(0x312BCC76);
    private static readonly Vector4 UpAxis = new(0f, 1f, 0f, 1f);

    private readonly Dictionary<PathSpline, uint> _pathHandles = new();

    public uint G { get; private set; }
    public uint Command { get; private set; }
    public Func<int, uint>? CodeObject { get; set; }
    public Func<int, Vector4?>? CodePosition { get; set; }
    public Func<int, PathSpline?>? AgentPath { get; set; }
    public Dictionary<uint, PathSpline> Paths { get; } = new();

    public uint CreateGlobals(uint player, uint cameraObj)
    {
        G = Alloc(0x600, "game globals");
        Command = Alloc(0x60, "camera command");
        M.U(G + 0x570, HandleOf(player));
        M.U(G + 0x200, HandleOf(cameraObj));
        CutDirectorCtor_27A4F8(G + 0x210);
        return G;
    }

    public uint Component_192118(uint obj, int id) => obj != 0 ? M.U(obj + 0xD8 + (uint)(id & 0xFF) * 4) : 0u;

    public uint PlayerObject()
    {
        uint slot = (M.U(G + 0x4F4) >> 8) & 0xF;
        return slot == 6 ? 0u : Handle(G + 0x570 + slot * 4);
    }

    public int CameraIndex => (int)((M.Q(G) >> 19) & 0xF);

    private uint PathHandle(PathSpline? path)
    {
        if (path is null) return 0;
        if (_pathHandles.TryGetValue(path, out uint h)) return h;
        h = Alloc(0x10, "path");
        _pathHandles[path] = h;
        Paths[h] = path;
        return h;
    }

    private Vector4 PathPosition_188CB0(uint path, float along) =>
        Paths.TryGetValue(path, out var p) ? new Vector4(p.PositionAt_188CB0(along), 1f) : new Vector4(0f, 0f, 0f, 1f);

    private Vector4 PathDirection_188DC0(uint path, float along) =>
        Paths.TryGetValue(path, out var p) ? new Vector4(p.DirectionAt(along), 1f) : new Vector4(0f, 0f, 0f, 1f);

    private void ZeroXyz(uint a) { M.F(a, 0f); M.F(a + 4, 0f); M.F(a + 8, 0f); }

    private Vector4 ObjectPosition(uint obj)
    {
        uint node = M.U(obj + 8);
        if ((M.Q(node + 0x60) & 4) != 0)
        {
            M.Copy16(node + 0x40, node + 0x30);
            M.Q(node + 0x60, M.Q(node + 0x60) & 0xFFFFFFFFFFFFFFFAUL);
        }
        return M.V(node + 0x40);
    }

    // Amedo
    public void CutDirectorPrepare_27DEC0(uint d)
    {
        const ulong keep = 0xFFFFFF0FFFFFFFFFUL;
        M.F(d + 0x114, 6f);
        M.Q(d + 0xC0, (M.Q(d + 0xC0) & keep) | (3UL << 36));
        M.Q(d + 0xF0, (M.Q(d + 0xF0) & keep) | (1UL << 36));
        M.F(d + 0xE4, 6f);
    }

    public void CutDirectorCtor_27A4F8(uint d)
    {
        for (uint k = 4; k <= 0x14; k += 4) M.U(d + k, 0);
        M.U(d + 0x30, VtDirectorBase);
        M.U(d, 0);
        FilterCtor_27D610(d + 0xC0);
        M.U(d + 0x30, VtCutDirector);
        FilterCtor_27D610(d + 0xF0);
        uint x = d + 0x120;
        M.U(x + 0x64, VtTargetBase);
        M.U(x, 0);
        M.B(x + 0x61, 1);
        M.B(x + 0x60, 0);
        M.V(x + 0x10, new Vector4(0f, 0f, 0f, 1f));
        Zero4(x + 0x20);
        M.U(x + 0x64, VtCutTargetRig);
        CutTargetInit_27C3D0(x);
        CutEyeCtor_277F40(d + 0x1D0);
        M.Q(d + 0x40, (M.Q(d + 0x40) & ~3UL) | 4);
        CutTargetsReset_27DDB0(d);
        CutLink_27DF20(d);
    }

    public void CutLink_27DF20(uint d)
    {
        M.U(d + 0x10, d + 0x120);
        M.U(d + 0xC, d + 0xF0);
        M.U(d + 0x14, d + 0x1D0);
        M.U(d + 8, d + 0xC0);
    }

    public void CutTargetsReset_27DDB0(uint d)
    {
        M.U(d + 0x48, 0);
        M.Q(d + 0x40, (M.Q(d + 0x40) & 0xFFFFFFFFFFFFFF37UL) | 0x130);
        M.U(d + 0x44, 0);
        ZeroXyz(d + 0x50);
        ZeroXyz(d + 0x60);
        M.U(d + 0xB8, 0);
        M.U(d + 0xB4, 0);
    }

    public void CutTarget1Object_27DCB0(uint d)
    {
        ZeroXyz(d + 0x50);
        M.Q(d + 0x40, M.Q(d + 0x40) & ~0x40UL);
    }

    public void CutTarget2Object_27DD10(uint d)
    {
        ZeroXyz(d + 0x60);
        M.Q(d + 0x40, M.Q(d + 0x40) & ~0x80UL);
    }

    public void CutEyeCtor_277F40(uint y)
    {
        M.B(y + 0x30, 1);
        M.U(y + 0x34, VtEyeBase);
        M.U(y, 0);
        M.U(y + 4, M.U(0x30A450));
        M.V(y + 0x10, new Vector4(0f, 0f, 0f, 1f));
        Zero4(y + 0x20);
        M.U(y + 0x34, VtCutEyeRig);
        CutEyeInit_277FE8(y);
    }

    public void CutEyeInit_277FE8(uint y)
    {
        M.Q(y + 0x40, M.Q(y + 0x40) & ~0x1FUL);
        M.V(y + 0x10, new Vector4(0f, 0f, 0f, 1f));
        Zero4(y + 0x20);
        M.V(y + 0x10, Vector4.Zero);
        Zero4(y + 0x50);
        Zero4(y + 0x60);
        M.U(y + 0x70, 0);
        M.U(y + 0x74, 0);
        M.U(y + 0x80, 0);
        M.Q(y + 0x40, M.Q(y + 0x40) & ~0x20UL);
        M.U(y + 0x78, 0);
        M.U(y, 0);
        M.U(y + 0x44, 0);
        M.U(y + 0x88, 0);
        M.U(y + 4, M.U(0x30A450));
    }

    public void CutEyeReset_27C2F0(uint y)
    {
        M.U(y + 0x78, 0);
        M.U(y + 0x7C, 0);
        ZeroXyz(y + 0x50);
        M.U(y + 4, M.U(y + 0x74));
        M.Copy16(y + 0x60, y + 0x20);
        M.Q(y + 0x40, M.Q(y + 0x40) & ~0x3FUL);
    }

    public void CutTargetInit_27C3D0(uint x)
    {
        M.Q(x + 0x70, M.Q(x + 0x70) & ~0xFUL);
        M.V(x + 0x10, new Vector4(0f, 0f, 0f, 1f));
        Zero4(x + 0x20);
        Zero4(x + 0x80);
        Zero4(x + 0x90);
        M.U(x + 0xA8, 0);
        M.U(x, 0);
    }

    public void CutTargetReset_27C498(uint x)
    {
        M.U(x + 0xA4, 0);
        M.Copy16(x + 0x90, x + 0x20);
        M.Q(x + 0x70, M.Q(x + 0x70) & ~0xFUL);
        M.U(x + 0xA0, 0);
        M.Copy16(x + 0x80, x + 0x20);
    }

    private static float Smooth(float k)
    {
        float k3 = k * k * k;
        return k * k * 3f - (k3 + k3);
    }

    public float CutTargetProgress_278B58(uint x, uint time)
    {
        int dur = M.I(x + 0xA4);
        if (dur <= 0) return 1f;
        ulong fl = M.Q(x + 0x70);
        if ((fl & 1) == 0)
        {
            M.Q(x + 0x70, fl | 1);
            M.U(x + 0xA0, M.U(time + 4));
            return 0f;
        }
        int el = M.I(time + 4) - M.I(x + 0xA0);
        if (el < dur)
        {
            float spt = M.F(SecondsPerTick);
            float k = el * spt / (dur * spt);
            return ((fl >> 1) & 7) == 2 ? Smooth(k) : k;
        }
        M.I(x + 0xA4, 0);
        M.Q(x + 0x70, fl & ~1UL);
        return 1f;
    }

    public void CutTargetUpdate_2789B0(uint x, uint time)
    {
        float k = CutTargetProgress_278B58(x, time);
        if (!(k < 1f))
        {
            M.Copy16(x + 0x20, x + 0x90);
            M.Copy16(x + 0x80, x + 0x90);
            return;
        }
        uint path = M.U(x + 0xA8);
        if (path == 0)
        {
            M.F(x + 0x20, M.F(x + 0x80) + (M.F(x + 0x90) - M.F(x + 0x80)) * k);
            M.F(x + 0x24, M.F(x + 0x84) + (M.F(x + 0x94) - M.F(x + 0x84)) * k);
            M.F(x + 0x2C, 1f);
            M.F(x + 0x28, M.F(x + 0x88) + (M.F(x + 0x98) - M.F(x + 0x88)) * k);
            return;
        }
        if (k <= 0.0001f) M.V(x + 0x20, PathPosition_188CB0(path, 0f));
        else
        {
            var p = PathPosition_188CB0(path, k);
            M.F(x + 0x20, (M.F(x + 0x20) + p.X) * 0.5f);
            M.F(x + 0x24, (M.F(x + 0x24) + p.Y) * 0.5f);
            M.F(x + 0x28, (M.F(x + 0x28) + p.Z) * 0.5f);
        }
        var dir = PathDirection_188DC0(path, k);
        M.F(x + 0x20, M.F(x + 0x20) + dir.X);
        M.F(x + 0x24, M.F(x + 0x24) + dir.Y);
        M.F(x + 0x28, M.F(x + 0x28) + dir.Z);
    }

    private float CutEyeRatio_2787E8(uint y, int now)
    {
        float spt = M.F(SecondsPerTick), tps = M.F(TicksPerSecond);
        float whole = M.I(y + 0x7C) * spt;
        int el = now - CamMath.Trunc(M.I(y + 0x78) * spt * tps);
        float k = CamMath.Trunc(el * spt / whole * tps) * spt;
        return ((M.Q(y + 0x40) >> 2) & 7) == 2 ? Smooth(k) : k;
    }

    private float CutEyeRatio2_2788C0(uint y, int now)
    {
        float spt = M.F(SecondsPerTick), tps = M.F(TicksPerSecond);
        float whole = M.I(y + 0x84) * spt;
        int el = now - CamMath.Trunc(M.I(y + 0x80) * spt * tps);
        float k = CamMath.Trunc(el * spt / whole * tps) * spt;
        if (((M.Q(y + 0x40) >> 2) & 7) == 2)
        {
            k = Smooth(k);
            if ((M.Q(y + 0x40) & 2) != 0) k = 1f - k;
        }
        return k;
    }

    public float CutEyeProgress_278550(uint y, uint time)
    {
        float spt = M.F(SecondsPerTick), tps = M.F(TicksPerSecond);
        float k = 1f;
        int now = M.I(time + 4);
        if (M.I(y + 0x78) == 0)
        {
            M.I(y + 0x78, now);
            M.Copy16(y + 0x50, y + 0x20);
            M.U(y + 0x70, M.U(y + 4));
        }
        int start = CamMath.Trunc(M.I(y + 0x78) * spt * tps);
        if (M.I(y + 0x7C) >= now - start)
        {
            ulong fl = M.Q(y + 0x40);
            if ((fl & 1) == 0 && (fl & 2) == 0) k = CutEyeRatio_2787E8(y, now);
            else
            {
                if (M.I(y + 0x80) == 0) M.I(y + 0x80, now);
                float edge = CutEyeRatio2_2788C0(y, now);
                float whole = M.I(y + 0x7C) * spt;
                float part = CamMath.Trunc(M.I(y + 0x84) * spt / whole * tps) * spt;
                if ((fl & 1) == 0)
                {
                    int rest = M.I(y + 0x7C) - CamMath.Trunc(M.I(y + 0x84) * spt * tps);
                    k = CamMath.Trunc(rest * spt / whole * tps) * spt + edge * part;
                }
                else k = edge * part;
                if (edge == 1f)
                {
                    M.U(y + 0x80, 0);
                    M.Q(y + 0x40, M.Q(y + 0x40) & ((fl & 1) == 0 ? ~2UL : ~1UL));
                }
            }
        }
        if (k == 1f) M.I(y + 0x78, 0);
        return k;
    }

    public void CutEyeUpdate_278108(uint y, uint time, uint x)
    {
        var t = M.V(x + 0x20);
        bool same = M.F(y + 0x20) == M.F(y + 0x60) && M.F(y + 0x24) == M.F(y + 0x64) && M.F(y + 0x28) == M.F(y + 0x68);
        if (!same)
        {
            if (M.I(y + 0x7C) < 1)
            {
                uint fov = M.U(y + 0x74);
                M.U(y + 4, fov);
                M.Copy16(y + 0x20, y + 0x60);
                M.Copy16(y + 0x50, y + 0x60);
                M.U(y + 0x70, fov);
            }
            else
            {
                float k = CutEyeProgress_278550(y, time);
                if (k <= 1f)
                {
                    uint path = M.U(y + 0x88);
                    if (path == 0)
                    {
                        M.F(y + 0x20, M.F(y + 0x50) + (M.F(y + 0x60) - M.F(y + 0x50)) * k);
                        M.F(y + 0x24, M.F(y + 0x54) + (M.F(y + 0x64) - M.F(y + 0x54)) * k);
                        M.F(y + 0x2C, 1f);
                        M.F(y + 0x28, M.F(y + 0x58) + (M.F(y + 0x68) - M.F(y + 0x58)) * k);
                    }
                    else if (0.0001f < k)
                    {
                        var p = PathPosition_188CB0(path, k);
                        M.F(y + 0x20, (M.F(y + 0x20) + p.X) * 0.5f);
                        M.F(y + 0x24, (M.F(y + 0x24) + p.Y) * 0.5f);
                        M.F(y + 0x28, (M.F(y + 0x28) + p.Z) * 0.5f);
                    }
                    else M.V(y + 0x20, PathPosition_188CB0(path, k));
                    int from = M.I(y + 0x70);
                    int diff = M.I(y + 0x74) - from;
                    M.I(y + 4, from + CamMath.Trunc(k * diff * CamMath.UnitToRad * CamMath.RadToUnit));
                    if ((M.Q(y + 0x40) & 0x20) != 0)
                    {
                        var a = M.V(y + 0x50) - t;
                        var b = M.V(y + 0x60) - t;
                        float dist = (1f - k) * MathF.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z) + k * MathF.Sqrt(b.X * b.X + b.Y * b.Y + b.Z * b.Z);
                        var v = new Vector4(M.F(y + 0x20) - t.X, M.F(y + 0x24) - t.Y, M.F(y + 0x28) - t.Z, 1f);
                        float inv = CamMath.InvLengthGuarded_11C030(Tiny, v);
                        M.V(y + 0x20, new Vector4(t.X + v.X * inv * dist, t.Y + v.Y * inv * dist, t.Z + v.Z * inv * dist, 1f));
                    }
                }
                if (k == 1f) M.I(y + 0x7C, 0);
            }
        }
        M.V(y + 0x10, ToV(CamMath.MatrixToQuat_186900(CamMath.LookAt_185668(M.V(y + 0x20), t, UpAxis))));
    }

    public void CutTargetTransform_27AF40(uint x, Matrix4x4 m)
    {
        TransformQuat_1EFA40(m, x + 0x10);
        TransformPoint_1E9A60(m, x + 0x20, true);
    }

    public void CutEyeTransform_27B9E8(uint y, Matrix4x4 m)
    {
        TransformQuat_1EFA40(m, y + 0x10);
        TransformPoint_1E9A60(m, y + 0x20, true);
    }

    public void CutFrame_27A720(uint d)
    {
        ulong fl = M.Q(d + 0x40);
        if ((fl & 0x30) == 0) return;
        uint o1 = M.U(d + 0x44), o2 = M.U(d + 0x48);
        if (o1 == 0)
        {
            if ((fl & 0x40) != 0 && o2 != 0) M.V(d + 0x60, ObjectPosition(o2));
        }
        else
        {
            M.V(d + 0x50, ObjectPosition(o1));
            if (o2 != 0) M.V(d + 0x60, ObjectPosition(o2));
            else if ((fl & 0x80) == 0)
            {
                uint node = M.U(o1 + 8);
                NodeRefresh_18AFA8(node);
                var f = M.V(node + 0x20);
                M.V(d + 0x60, new Vector4(M.F(d + 0x50) + f.X, M.F(d + 0x54) + f.Y, M.F(d + 0x58) + f.Z, 1f));
            }
        }
        var z = new Vector4(M.F(d + 0x60) - M.F(d + 0x50), M.F(d + 0x64) - M.F(d + 0x54), M.F(d + 0x68) - M.F(d + 0x58), 1f);
        float k = CamMath.InvLengthGuarded_11C030(Tiny, z);
        z = new Vector4(z.X * k, z.Y * k, z.Z * k, 1f);
        var up = UpAxis;
        var ax = new Vector4(z.Y * up.Z - z.Z * up.Y, z.Z * up.X - z.X * up.Z, z.X * up.Y - z.Y * up.X, 1f);
        k = CamMath.InvLengthGuarded_11C030(Tiny, ax);
        ax = new Vector4(ax.X * k, ax.Y * k, ax.Z * k, 1f);
        var ay = up;
        if ((M.Q(d + 0x40) & 0x100) == 0)
        {
            ay = new Vector4(ax.Y * z.Z - ax.Z * z.Y, ax.Z * z.X - ax.X * z.Z, ax.X * z.Y - ax.Y * z.X, 1f);
            k = CamMath.InvLengthGuarded_11C030(Tiny, ay);
            ay = new Vector4(ay.X * k, ay.Y * k, ay.Z * k, 1f);
        }
        M.M(d + 0x70, new Matrix4x4(ax.X, ay.X, z.X, 0f, ax.Y, ay.Y, z.Y, 0f, ax.Z, ay.Z, z.Z, 0f, 0f, 0f, 0f, 1f));
        M.Q(d + 0x40, M.Q(d + 0x40) & ~0x20UL);
    }

    public void LoadCommand(IReadOnlyList<uint> args)
    {
        for (int i = 0; i < 0x14; i++) M.U(Command + 0xC + (uint)i * 4, i < args.Count ? args[i] : 0u);
    }

    public void ToggleCutsceneCamera_111520()
    {
        uint cmd = Command;
        uint w = M.U(cmd + 0xC);
        float t = M.F(cmd + 0x10);
        float tps = M.F(TicksPerSecond);
        switch (w & 7)
        {
            case 1:
                if (0f < t) TransitionCamera_175CE0(3, CamMath.Trunc(t * tps), false, (w >> 5) & 7);
                else SwitchCamera_1759B0(3, true);
                return;
            case 0:
                bool snap = (((int)w >> 4) & 1) != 0;
                if (0f < t) TransitionCamera_175CE0(0, CamMath.Trunc(t * tps), snap, (w >> 5) & 7);
                else SwitchCamera_1759B0(0, snap);
                if ((M.U(cmd + 0xC) & 8) == 0) return;
                uint comp = Component_192118(PlayerObject(), 0x16);
                uint py = M.U(comp + 0x64), px = M.U(comp + 0x60);
                M.Copy16(py + 0x20, G + 0x400);
                M.Copy16(py + 0x10, G + 0x3F0);
                M.Copy16(px + 0x20, G + 0x350);
                EyeSnapToEye_273640(py);
                return;
            case 2:
                SwitchCamera_1759B0(4, (((int)w >> 4) & 1) != 0);
                return;
        }
    }

    public void SwitchCamera_1759B0(int cam, bool snap)
    {
        M.Q(G, (M.Q(G) & 0xFFFFFFFFFF87FFFFUL) | ((ulong)(cam & 0xF) << 19));
        if (cam == 3)
        {
            uint c9 = Component_192118(Handle(G + 0x200), 9);
            var pos = ObjectPosition(PlayerObject());
            SetDirector_27B888(c9, G + 0x210, snap);
            M.V(G + 0x3C0, pos);
        }
        else if (cam == 0)
        {
            uint player = PlayerObject();
            uint comp = Component_192118(player, 0x16);
            uint camObj = Handle(comp + 0x20);
            Controller_15E828(comp + 0x30, camObj, player, snap);
            M.U(camObj + 4, M.U(camObj + 4) & 0xFFFDFFFF);
            if (Handle(G + 0x200) != camObj) M.U(G + 0x200, camObj == 0 ? 0u : HandleOf(camObj));
        }
        else if (cam == 4) Missing("0x1759B0 camera 4");
    }

    public void TransitionCamera_175CE0(int cam, int ticks, bool activate, uint mode)
    {
        if (cam == 0)
        {
            uint comp = Component_192118(PlayerObject(), 0x16);
            uint camObj = Handle(comp + 0x20);
            uint c9 = Component_192118(camObj, 9);
            if (activate) DirectorActivateVirtual(comp + 0x50, camObj);
            StartTransition_2700C0(c9, comp + 0x50, ticks, mode & 0xFF);
        }
        else if (cam == 3) StartTransition_2700C0(Component_192118(Handle(G + 0x200), 9), G + 0x210, ticks, mode & 0xFF);
    }

    public void Controller_15E828(uint p, uint camObj, uint player, bool snap)
    {
        uint c9 = Component_192118(camObj, 9);
        if ((M.Q(p) & 0x10) == 0) M.U(p + 0x720, SetDirector_27B888(c9, p + 0x20, snap));
        uint c12 = Component_192118(player, 0xC);
        uint d = M.U(p + 0x720);
        if (M.U(d + 0x30) == VtPlayerDirector) PlayerBind_1422F0(d, M.U(c12 + 0x18));
        else Missing($"director vt+0x34 for vtable 0x{M.U(d + 0x30):X}");
        ZoneRequestReset_15E9D8(p);
        M.U(d + 4, M.U(p + 4));
    }

    public void CutsceneTargets_1118C8()
    {
        uint cmd = Command, d = G + 0x210;
        CutTargetsReset_27DDB0(d);
        for (uint n = 0; n < 2; n++)
        {
            if (M.B(cmd + 0x14 + n) == 0xFF)
            {
                uint obj = CodeObject?.Invoke(M.B(cmd + 0xC + n)) ?? 0;
                if (obj == 0)
                {
                    if (CodePosition?.Invoke(M.B(cmd + 0xE + n)) is { } p)
                    {
                        M.U(G + 0x254 + n * 4, 0);
                        M.V(G + 0x260 + n * 0x10, p);
                        M.Q(G + 0x250, M.Q(G + 0x250) | (0x40UL << (int)n));
                    }
                }
                else
                {
                    if (n == 0) CutTarget1Object_27DCB0(d);
                    else CutTarget2Object_27DD10(d);
                    M.U(G + 0x254 + n * 4, obj);
                }
            }
            else M.U(G + 0x2C4 + n * 4, PathHandle(AgentPath?.Invoke(M.B(cmd + 0x14 + n))));
        }
        ulong f = M.Q(G + 0x250);
        ulong b3 = (ulong)(M.B(cmd + 0x10) & 1) << 3;
        M.Q(G + 0x250, (f & 0xFFFFFFFFFFFFFFE7UL) | b3 | ((ulong)((M.I(cmd + 0x10) >> 1) & 1) << 4));
    }

    private void ShotTiming_110D20(float targetSeconds, float eyeSeconds, uint cmd, uint x, uint y)
    {
        CutEyeReset_27C2F0(y);
        CutTargetReset_27C498(x);
        float tps = M.F(TicksPerSecond);
        M.I(x + 0xA4, CamMath.Trunc(targetSeconds * tps));
        M.I(y + 0x84, CamMath.Trunc(tps * 0.5f));
        M.I(y + 0x7C, CamMath.Trunc(eyeSeconds * tps));
        M.U(y + 0x44, M.U(cmd + 0x2C));
        ulong fl = M.Q(y + 0x40);
        ulong b0 = (ulong)(M.S(cmd + 0xE) & 1);
        M.Q(y + 0x40, (fl & ~3UL) | b0 | ((ulong)((M.I(cmd + 0xC) >> 17) & 1) << 1));
        uint mode = (M.U(cmd + 0xC) >> 18) & 7;
        if (mode == 0)
        {
            M.Q(x + 0x70, M.Q(x + 0x70) & 0xFFFFFFFFFFFFFFF1UL);
            M.Q(y + 0x40, M.Q(y + 0x40) & 0xFFFFFFFFFFFFFFE3UL);
        }
        else if (mode == 1)
        {
            M.Q(x + 0x70, (M.Q(x + 0x70) & 0xFFFFFFFFFFFFFFF1UL) | 4);
            M.Q(y + 0x40, (M.Q(y + 0x40) & 0xFFFFFFFFFFFFFFE3UL) | 8);
        }
        M.Q(y + 0x40, (M.Q(y + 0x40) & ~0x20UL) | ((ulong)((M.I(cmd + 0xC) >> 15) & 1) << 5));
    }

    private void ShotCenter_110908(uint cmd, uint d, ref Vector4 center)
    {
        uint o1 = M.U(d + 0x44), o2 = M.U(d + 0x48);
        var p1 = o1 != 0 ? ObjectPosition(o1) : M.V(d + 0x50);
        var p2 = o2 != 0 ? ObjectPosition(o2) : M.V(d + 0x60);
        switch ((M.U(cmd + 0xC) >> 9) & 7)
        {
            case 0: center = p1; break;
            case 1: center = p2; break;
            case 2: center = new Vector4((p2.X - p1.X) * 0.5f, (p2.Y - p1.Y) * 0.5f, (p2.Z - p1.Z) * 0.5f, 1f); break;
        }
    }

    private void ShotFraming_110E90(uint cmd, uint d, int fov, ref int a, ref int b, ref float c)
    {
        switch ((M.U(cmd + 0xC) >> 6) & 7)
        {
            case 0: a = 0; b = 0; c = 1f; break;
            case 1: a = CamMath.Divide_11C008(6f, fov); b = CamMath.Divide_11C008(2f, fov); c = 1f / CamMath.Cos_18B8E8(a); break;
            case 2: a = CamMath.Divide_11C008(3f, fov); b = CamMath.Divide_11C008(2f, fov); c = 1f / CamMath.Cos_18B8E8(a); break;
            case 3: a = CamMath.Divide_11C008(2f, fov); b = CamMath.Divide_11C008(1.5f, fov); c = 1f / CamMath.Cos_18B8E8(a); break;
        }
        a = CamMath.Trunc(a * (((M.Q(d + 0x40) >> 3) & 1) != 0 ? -1f : 1f));
    }

    private void ShotTable_11FE28(uint cmd, ref float size, ref float spread)
    {
        switch ((M.U(cmd + 0xC) >> 3) & 7)
        {
            case 0: size = 1f; spread = 1.4f; break;
            case 1: size = 1f; spread = 0.9f; break;
            case 2: size = 0.9f; spread = 0.75f; break;
            case 3: size = 0.8f; spread = 0.7f; break;
            case 4: size = 0.6f; spread = 0.6f; break;
            case 5: size = 0.4f; spread = 0.5f; break;
            case 6: size = 0f; spread = 0.35f; break;
            case 7: size = 0f; spread = 0.2f; break;
        }
    }

    private void ShotVertical_11FF38(float size, uint cmd, uint obj, ref Vector4 vert)
    {
        if (obj == 0) { vert.Y = M.F(cmd + 0x20); return; }
        float h = M.F(obj + 0x74) - M.F(obj + 0x64);
        vert.Y = h * 0.5f + (size + M.F(cmd + 0x20)) * 0.25f * h;
    }

    private float ShotSize_11FF80(float size, uint obj) =>
        obj == 0 ? 1f : M.F(obj + 0x6C) * (1f - size) + (M.F(obj + 0x74) - M.F(obj + 0x64)) * 0.25f * size;

    private static float ShotDistance_11FFD0(float size, float c, float spread, float width) => MathF.Abs(c * size / (spread * 0.5f * width));

    private float ShotRadius_110AF0(uint cmd, uint d, Vector4 center)
    {
        uint o1 = M.U(d + 0x44), o2 = M.U(d + 0x48);
        var p1 = o1 != 0 ? ObjectPosition(o1) : M.V(d + 0x50);
        var p2 = o2 != 0 ? ObjectPosition(o2) : M.V(d + 0x50);
        var p = Vector4.Zero;
        switch ((M.U(cmd + 0xC) >> 12) & 7)
        {
            case 0: p = p1; break;
            case 1: p = p2; break;
            case 2: p = new Vector4((p2.X - p1.X) * 0.5f, (p2.Y - p1.Y) * 0.5f, (p2.Z - p1.Z) * 0.5f, 1f); break;
        }
        float dx = center.X - p.X, dy = center.Y - p.Y, dz = center.Z - p.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private int ShotYaw_111038(float dist, float r, uint cmd, uint d, int b)
    {
        uint w = M.U(cmd + 0xC);
        int yaw;
        if ((w & 0x7000) == 0x3000)
        {
            float t = (w & 7) switch { 0 => -145f, 1 => -15f, 2 => -180f, 3 => -90f, _ => 0f };
            yaw = CamMath.Trunc((t + M.F(cmd + 0x24)) * 182.04445f);
        }
        else
        {
            int e = Asin_18B7F8(dist * CamMath.Sin_18B8C0(b) / r);
            yaw = -(CamMath.FromValue_11BFA0(M.F(cmd + 0x24), 1) + e + b);
        }
        return ((M.Q(d + 0x40) >> 3) & 1) == 0 ? yaw : -yaw;
    }

    public void CutsceneShot_110360()
    {
        uint cmd = Command, d = G + 0x210, x = G + 0x330, y = G + 0x3E0;
        int fov = M.I(G + 0x3E4);
        if ((M.U(cmd + 0xC) & 0x200000) == 0) M.I(cmd + 0x38, fov);
        uint path1 = M.U(G + 0x2C4), path2 = M.U(G + 0x2C8);
        M.U(G + 0x3D8, path1);
        M.U(G + 0x468, path2);
        if (path1 != 0 || path2 != 0)
        {
            ShotTiming_110D20(M.F(cmd + 0x18), M.F(cmd + 0x1C), cmd, x, y);
            var center = new Vector4(0f, 0f, 0f, 1f);
            if (path1 == 0)
            {
                ShotCenter_110908(cmd, d, ref center);
                M.V(G + 0x3C0, center);
                M.V(G + 0x440, PathPosition_188CB0(path2, M.F(cmd + 0x34)));
            }
            else if (path2 == 0)
            {
                ShotCenter_110908(cmd, d, ref center);
                M.V(G + 0x3C0, PathPosition_188CB0(path1, M.F(cmd + 0x30)));
                M.V(G + 0x440, center);
            }
            else if (path1 == path2)
            {
                var pos = PathPosition_188CB0(path1, M.F(cmd + 0x30));
                var dir = PathDirection_188DC0(path1, M.F(cmd + 0x30));
                M.V(G + 0x3C0, new Vector4(pos.X + dir.X, pos.Y + dir.Y, pos.Z + dir.Z, 1f));
                M.V(G + 0x440, pos);
            }
            else
            {
                M.V(G + 0x3C0, PathPosition_188CB0(path1, M.F(cmd + 0x30)));
                M.V(G + 0x440, PathPosition_188CB0(path2, M.F(cmd + 0x34)));
            }
            M.U(G + 0x454, M.U(cmd + 0x38));
            return;
        }
        float width = 2f * Tan_18B968(fov) / 1.3333334f;
        uint o1 = M.U(d + 0x44), o2 = M.U(d + 0x48);
        var c0 = new Vector4(0f, 0f, 0f, 1f);
        var vert = new Vector4(0f, 0f, 0f, 1f);
        float c = 0f, size = 0f, spread = 0f, s = 0f;
        ShotTiming_110D20(M.F(cmd + 0x18), M.F(cmd + 0x1C), cmd, x, y);
        ShotCenter_110908(cmd, d, ref c0);
        int a = 0, b = 0;
        ShotFraming_110E90(cmd, d, fov, ref a, ref b, ref c);
        ShotTable_11FE28(cmd, ref size, ref spread);
        uint mode = (M.U(cmd + 0xC) >> 9) & 7;
        if (mode <= 2)
        {
            uint obj = mode == 1 ? o2 : o1;
            ShotVertical_11FF38(size, cmd, obj, ref vert);
            s = ShotSize_11FF80(size, obj);
        }
        float dist = ShotDistance_11FFD0(s, c, spread, width) + M.F(cmd + 0x14);
        float r = ShotRadius_110AF0(cmd, d, c0);
        int yaw = ShotYaw_111038(dist, r, cmd, d, b);
        int pitch = CamMath.FromValue_11BFA0(M.F(cmd + 0x10), 1);
        var rot = CamMath.RotXYZ_183220(pitch, yaw, CamMath.FromValue_11BFA0(0f, 0), Missing);
        var off = CamMath.Rotate3_18E4B8(rot, new Vector4(0f, 0f, -dist, 1f));
        CutFrame_27A720(d);
        off = CamMath.Rotate3_18E4B8(M.M(d + 0x70), off);
        var back = CamMath.RotateAxis_18DE00(new Vector4(-off.X, -off.Y, -off.Z, 1f), UpAxis, -a, true);
        var eye = new Vector4(c0.X + vert.X + off.X, c0.Y + vert.Y + off.Y, c0.Z + vert.Z + off.Z, 1f);
        M.V(x + 0x90, new Vector4(eye.X + back.X, eye.Y + back.Y, eye.Z + back.Z, 1f));
        M.V(y + 0x60, eye);
        M.U(G + 0x454, M.U(cmd + 0x38));
    }


    private static float Tan_18B968(int angle)
    {
        var (s, c) = CamMath.SinCos(angle * CamMath.UnitToRad);
        if (c == 0f) return s <= 0f ? -1e30f : 1e30f;
        return s / c;
    }

    private static int Asin_18B7F8(float v) => CrashEngine.Core.GameMath.Asin16_18B7F8(v); // Amedo
}
