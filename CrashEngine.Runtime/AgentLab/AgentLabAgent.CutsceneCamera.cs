using System.Numerics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-29
public sealed partial class AgentLabAgent
{
    internal AgentLabAgent? CameraCodeObject(int code) => CodeObject(code);

    internal Vector3? CameraCodePosition(int code) => CodePosition(code, out var p) ? p : null;

    internal PathSpline? PathAt(int index) => index < _paths.Count ? _paths[index] : null;

    public bool LocalBox(out Vector4 min, out Vector4 max)
    {
        min = max = new Vector4(0f, 0f, 0f, 1f);
        if (Ogi?.BoundingBox is not { Length: >= 2 } bb) return false;
        min = new Vector4(bb[0].X, bb[0].Y, bb[0].Z, bb[0].W);
        max = new Vector4(bb[1].X, bb[1].Y, bb[1].Z, bb[1].W);
        return true;
    }

    private void CutsceneCamera(int op, List<uint> a)
    {
        if (World.CutsceneCamera is not { } cam)
        {
            World.Unimplemented("command", op, "cutscene camera without a game camera");
            return;
        }
        switch (op)
        {
            case 594: cam.ToggleCutsceneCamera_111520(a); break;
            case 595: cam.CutsceneTargets_1118C8(this, a); break;
            case 591: cam.CutsceneShot_110360(a); break;
            case 626: // Amedo
            case 627:
            case 628:
            case 560:
            case 652: cam.CameraNode_1201B8(op, a); break; // Amedo
            case 559: // Amedo
                uint flags = a.Count > 0 ? a[0] : 0u;
                cam.SetCamera_10F1D0(flags, (flags & 1) != 0 ? RootController().AngleValue_209320(a.Count > 1 ? a[1] : 0u) : 0, a.Count > 2 ? BitConverter.UInt32BitsToSingle(a[2]) : 0f);
                break;
        }
    }
}
