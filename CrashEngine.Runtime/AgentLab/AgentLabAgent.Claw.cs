using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    private Vector4 _grapleStart, _grapleEnd;

    internal int ParamInt_2619D8(int index)
    {
        var p = Instance.Source.ParamList3;
        int count = p?.Count ?? 0;
        if (index < count) return (int)p![index];
        var d = GameObject?.InstIntegers;
        int k = index - count;
        return d is not null && k < d.Count ? (int)d[k] : 0;
    }

    internal bool IsGrabbable => ControllerType == 17;
    internal bool IsHookGrabbable_17AAB0 => ParamInt_2619D8(0) == 1;

    internal Vector4? GrabbableLandingPoint_1724E8(Matrix4x4 node)
    {
        int n = ParamInt_2619D8(0);
        if (n == 1) return null;
        int need = n != 0 ? n : 1;
        if (_keys.Count < need) return null;
        Vector3? best = null;
        float bestDot = -1e30f;
        var pos = node.Translation;
        for (int i = 0; i < need; i++)
        {
            var p = _keys[i];
            var d = new Vector4(p.X - pos.X, p.Y - pos.Y, p.Z - pos.Z, 1f);
            float k = GameMath.InvLength_11C030(2.4999998e-09f, d);
            float dot = d.X * k * node.M31 + d.Y * k * node.M32 + d.Z * k * node.M33;
            if (bestDot < dot)
            {
                bestDot = dot;
                best = p;
            }
        }
        return best is { } b ? new Vector4(b, 1f) : null;
    }

    internal void SetMotionVelocity_145270(Vector3 v)
    {
        _prevVelocity = _velocity;
        _velocity = v;
    }

    internal Quaternion NodeQuat => GameMath.MatrixToQuat_186900(GameMatrix);

    internal void SetNodeQuat(Quaternion q)
    {
        var r = GameMath.QuatToMatrix_18E620(q);
        r.Translation = GamePosition;
        if (r != GameMatrix) GameMatrix = r;
    }

    internal void SetNodePosition(Vector3 p)
    {
        if (p != GamePosition) SetLocalPosition(p);
    }

    internal void SetGrapleEnds_140FA8(Vector4 start, Vector4 end)
    {
        _grapleStart = start;
        _grapleEnd = end;
    }

    internal void GrapleGroup_140FC0()
    {
        if (ControllerType != 19 || _destroyed || !Entity.Active) return;
        if ((AgentFlags & 0x400) != 0) Swing_13D8A8();
    }

    private void Swing_13D8A8()
    {
        var start = new Vector3(_grapleStart.X, _grapleStart.Y, _grapleStart.Z);
        var d = new Vector3(_grapleEnd.X - start.X, _grapleEnd.Y - start.Y, _grapleEnd.Z - start.Z);
        float len = MathF.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
        if (MathF.Abs(len) <= 5e-05f) return;
        var old = GameMatrix;
        var move = d;
        LineOfSight_280958(start, ref move, 0x10, 0x15B010);
        float cut = MathF.Sqrt(move.X * move.X + move.Y * move.Y + move.Z * move.Z);
        float k;
        if (MathF.Abs(cut) <= 5e-05f)
        {
            move = d;
            k = 0.01f / len;
        }
        else k = cut < 0.51f ? 0.01f / cut : (cut - 0.5f) / cut;
        var head = new Vector3(move.X * k + start.X, move.Y * k + start.Y, move.Z * k + start.Z);
        SetNodePosition(head);
        if (_rope is not { } rope) return;
        rope.Step_162220(World.NowTicks, new Vector4(head, _grapleEnd.W), _grapleStart);
        var inv = GameMath.RigidInverse_18E808(old);
        var p1 = GameMath.Transform_18E4F0(inv, rope.Head);
        var p2 = GameMath.Transform_18E4F0(inv, rope.Start);
        var min = new Vector3(1e30f);
        var max = new Vector3(-1e30f);
        foreach (var p in (ReadOnlySpan<Vector4>)[p1, p2])
        {
            if (p.X < min.X) min.X = p.X;
            if (p.Y < min.Y) min.Y = p.Y;
            if (p.Z < min.Z) min.Z = p.Z;
            if (max.X < p.X) max.X = p.X;
            if (max.Y < p.Y) max.Y = p.Y;
            if (max.Z < p.Z) max.Z = p.Z;
        }
        _boxMin = min - new Vector3(0.5f);
        _boxMax = max + new Vector3(0.5f);
        _hasBox = true;
    }

    private GrapleRope? _rope;
    public GrapleRope? Rope => _rope;

    private void GrapleInit_140EB0()
    {
        if (_rope is not null) return;
        _rope = new GrapleRope(this);
        _rope.Enable_1912A8(0);
    }
}
