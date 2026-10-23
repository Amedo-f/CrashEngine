using System.Numerics;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed partial class GameCamera
{
    public bool CameraTransform_27B760(uint c9, Matrix4x4 m)
    {
        bool ok = true;
        foreach (uint d in new[] { M.U(c9 + 0x2C), M.U(c9 + 0x30) })
        {
            if (d == 0) continue;
            if (M.U(d + 0x30) is VtPlayerDirector or VtCutDirector) ok &= DirectorTransform_26EF20(d, m); // Amedo 2026-09-29
            else { Missing($"director vt+0x24 for vtable 0x{M.U(d + 0x30):X}"); ok = false; }
        }
        return ok;
    }

    public bool DirectorTransform_26EF20(uint d, Matrix4x4 m)
    {
        bool ok = true;
        foreach (uint f in new[] { M.U(d + 8), M.U(d + 0xC) })
        {
            if (f == 0) continue;
            if (M.U(f) == VtFilter) TransformPoint_1E9A60(m, f + 0x10, true);
            else { Missing($"filter vt+0x44 for vtable 0x{M.U(f):X}"); ok = false; }
        }
        uint x = M.U(d + 0x10);
        if (x != 0)
        {
            if (M.U(x + 0x64) == VtTargetRig) TargetTransform_27C040(x, m);
            else if (M.U(x + 0x64) == VtCutTargetRig) CutTargetTransform_27AF40(x, m); // Amedo 2026-09-29
            else { Missing($"target rig vt+0x2C for vtable 0x{M.U(x + 0x64):X}"); ok = false; }
        }
        uint y = M.U(d + 0x14);
        if (y != 0)
        {
            if (M.U(y + 0x34) == VtEyeRig) EyeTransform_273FC8(y, m);
            else if (M.U(y + 0x34) == VtCutEyeRig) CutEyeTransform_27B9E8(y, m); // Amedo 2026-09-29
            else { Missing($"eye rig vt+0x2C for vtable 0x{M.U(y + 0x34):X}"); ok = false; }
        }
        return ok;
    }

    public void TargetTransform_27C040(uint x, Matrix4x4 m)
    {
        var lo = Vector4.Transform(new Vector4(0f, M.F(x + 0x88), 0f, 1f), m);
        var hi = Vector4.Transform(new Vector4(0f, M.F(x + 0x8C), 0f, 1f), m);
        M.F(x + 0x88, lo.Y);
        M.F(x + 0x8C, hi.Y);
        TransformPoint_1E9A60(m, x + 0xC0, true);
        TransformQuat_1EFA40(m, x + 0x10);
        TransformPoint_1E9A60(m, x + 0x20, true);
    }

    public void EyeTransform_273FC8(uint y, Matrix4x4 m)
    {
        TransformPoint_1E9A60(m, y + 0x1A0, true);
        TransformQuat_1EFA40(m, y + 0x10);
        TransformPoint_1E9A60(m, y + 0x20, true);
        uint xs = M.U(y + 0x50);
        var v = new Vector4(M.F(xs + 0x20) - M.F(y + 0x20), M.F(xs + 0x24) - M.F(y + 0x24), M.F(xs + 0x28) - M.F(y + 0x28), 1f);
        int yaw = CamMath.Yaw_18C830(v);
        var flat = CamMath.Rotate3_18E4B8(CamMath.RotY_18BBC0(-yaw), v);
        M.I(y + 0x68, CamMath.Wrap16(CamMath.Pitch_18C898(flat)));
        M.I(y + 0x108, CamMath.Wrap16(yaw));
    }

    public void TransformPoint_1E9A60(Matrix4x4 m, uint v, bool translate)
    {
        if (!translate) { m.M41 = 0f; m.M42 = 0f; m.M43 = 0f; m.M44 = 1f; }
        M.V(v, Vector4.Transform(M.V(v), m));
    }

    public void TransformQuat_1EFA40(Matrix4x4 m, uint q)
    {
        var r = CamMath.QuatToMatrix_18E620(ToQ(M.V(q)));
        r.M41 = 0f; r.M42 = 0f; r.M43 = 0f; r.M44 = 1f;
        M.V(q, ToV(CamMath.MatrixToQuat_186900(r * m)));
    }
}
