using System.Numerics;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed partial class AgentLabAgent
{
    private bool HasExit(int exit) => exit != WholeBody && HasExitNode_2952A0(exit); // Amedo

    private void AttachSpring_24CE40(List<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint flags = Arg(9);
        int holderExit = (int)(Arg(10) & 0xFF), ownExit = (int)((Arg(10) >> 8) & 0xFF);
        var point = new Vector3(F(5), F(6), F(7));
        AgentLabAgent? holder = null;
        if ((flags & 0x200000) != 0)
        {
            if (!_hasFocusObject || _focusObject is not { } focus) return;
            if (focus.Asleep)
            {
                _hasFocusObject = _hasFocusPosition = false;
                _focusObject = null;
                return;
            }
            if (ReferenceEquals(focus, this)) return;
            holder = focus;
        }
        var p = holder is not null ? Vector3.Transform(point, holder.LocalMatrix) : SourcePosition(SourceNone, SourceNone, (int)(flags & 0xF), point);
        Vector3? ownOffset = (flags & 0x100000) != 0 ? new Vector3(F(1), F(2), F(3)) : null;
        Vector3 q;
        if (ownExit != WholeBody)
        {
            var m = ExitAnchor_293B70(ownExit);
            q = m.Translation + (ownOffset is { } o ? Vector3.TransformNormal(o, m) : Vector3.Zero);
        }
        else q = GamePosition + (ownOffset is { } o ? Vector3.TransformNormal(o, LocalMatrix) : Vector3.Zero);
        var d = q - p;
        float length = (flags & 0x400000) != 0 ? F(13) : MathF.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
        if (holder is not null) p = point;
        AgentLabAgent? hung = null;
        if ((flags & 0xFFFF0) != 0xFFFF0) hung = Spawn((ushort)((flags >> 4) & 0xFFFF), null, q, Matrix4x4.Identity, null);
        AttachSpring_195F20(p, ownExit, ownOffset, hung, holder, holderExit, F(11), F(12), length);
        if (hung is not null && (flags & 0x800000) != 0) hung.AgentFlags &= ~0x10u;
    }

    private bool AttachSpring_195F20(Vector3 point, int ownExit, Vector3? ownOffset, AgentLabAgent? hung, AgentLabAgent? holder, int holderExit,
                                     float stiffness, float damping, float length)
    {
        AttachmentsOf_196E20(); // Amedo
        if (Add_195E48(hung) is not { } node) return false;
        SetAttachmentExitPoint_192EA0(node, ownExit);
        SetAttachmentSpring_192D08(node, point, ownOffset, holder, holderExit, stiffness, damping);
        node.Length = length;
        if (hung is not null) HangFromHolder_192F10(node, hung);
        Trace($"spring to {holder?.Entity.Name ?? $"{point}"} (stiffness {stiffness}, damping {damping}, length {length:0.###}){(hung is not null ? $", hangs {hung.Entity.Name}" : "")}");
        return true;
    }

    // Amedo
    private void SetAttachmentSpring_192D08(Attachment node, Vector3 point, Vector3? ownOffset, AgentLabAgent? holder, int holderExit, float stiffness, float damping)
    {
        node.SpringPoint = point;
        node.Holder = holder;
        node.Stiffness = stiffness;
        node.Damping = damping;
        node.HolderExit = holderExit;
        if (ownOffset is { } o)
        {
            node.Relative = Matrix4x4.CreateTranslation(o);
            node.Flags |= RelativeFlag; // Amedo
        }
        node.Mode = SpringMode;
        node.Kind = KindSpring;
    }

    private void DetachAllSprings_1969B0()
    {
        if (!_pathAllocated) return; // Amedo
        for (int i = 0; i < _attachments.Count;)
        {
            var node = _attachments[i];
            if (node.Mode != SpringMode)
            {
                i++;
                continue;
            }
            if (node.Child is { } beam)
            {
                if (node.Hang)
                {
                    Trace($"spring released, {beam.Entity.Name} goes to sleep");
                    beam.Sleep_197508();
                }
                if (!UnlinkInstance_196520(beam)) i++;
                if (!_pathAllocated) break;
                continue;
            }
            Trace("spring released");
            RemoveFromPath_194CC0(i, false);
            UnlinkMissing_196B00();
        }
        FreeEmptyPath_195848();
    }

    private void StartSprings_2380C0(WobbleState w)
    {
        w.RestQuat = LocalRotation;
        if (!_hasBox) LoadBox();
        var max = _hasBox ? _boxMax : Vector3.Zero;
        w.SpringBox = max;
        float r = MathF.Sqrt(max.X * max.X + max.Y * max.Y + max.Z * max.Z);
        r += r;
        w.SettleDistance2 = r * r;
    }

    private void SpringsReady_2380C0(WobbleState w)
    {
        _springRest_88 = true;
        StorePlace_23D168(this);
        if (_physics is { } ctrl && (ComponentMask & NoiseMask_309D80) == 0) ClearRigidBodyGravity_253A08(ctrl); // Amedo
        Trace($"springy body (k {WFloat(w.Args, 9)}, damping {WFloat(w.Args, 8)}, side {WFloat(w.Args, 10)}, box {w.SpringBox})");
    }

    private void SpringCorners_2396DC(WobbleState w, RigidBody body)
    {
        var b = w.SpringBox;
        float k = WFloat(w.Args, 9), d = WFloat(w.Args, 8), s = WFloat(w.Args, 10);
        float x2 = b.X + b.X, z2 = b.Z + b.Z;
        var m = _storedSpace ?? LocalMatrix;
        var ax = new Vector3(m.M11, m.M12, m.M13);
        var ay = new Vector3(m.M21, m.M22, m.M23);
        var az = new Vector3(m.M31, m.M32, m.M33);
        var local = b;
        var corner = m.Translation + ax * b.X + az * b.Z + ay * b.Y;
        body.LevelSpring_28A4F0(corner, local, k, d, s);
        local.Z -= z2;
        corner -= az * z2;
        body.LevelSpring_28A4F0(corner, local, k, d, s);
        local.X -= x2;
        corner -= ax * x2;
        body.LevelSpring_28A4F0(corner, local, k, d, s);
        local.Z += z2;
        corner += az * z2;
        body.LevelSpring_28A4F0(corner, local, k, d, s);
    }

    private Vector3 SpringAnchor_193018(Attachment node)
    {
        if (node.Holder is not { Destroyed: false } holder) return node.SpringPoint;
        if (node.HolderExit != WholeBody && holder.HasExit(node.HolderExit)) return holder.ExitAnchor_293B70(node.HolderExit).Translation;
        return Vector3.Transform(node.SpringPoint, holder.LocalMatrix);
    }

    private void UpdateSpring_194608(Attachment node, Matrix4x4 anchor)
    {
        var target = SpringAnchor_193018(node);
        var own = node.Applied is { } r ? (r * anchor).Translation : anchor.Translation; // Amedo
        if (_physics?.Body is { } body)
        {
            var local = node.Applied is null && !node.ExitNode ? Vector3.Zero : Vector3.Transform(own, body.Inverse); // Amedo
            body.Spring_28A198(target, local, false, node.Length, node.Stiffness, node.Damping);
        }
        if (!node.Hang || node.Child is not { } beam) return; // Amedo
        var d = target - own;
        float len = MathF.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
        float stretch = len / beam.BoxExtent.Z;
        var n = d * (1f / len);
        var c = Vector3.Cross(n, new Vector3(anchor.M11, anchor.M12, anchor.M13));
        if (MathF.Sqrt(c.X * c.X + c.Y * c.Y + c.Z * c.Z) <= 5e-5f) return;
        c *= GameMath.InverseLength(c);
        var b = Vector3.Cross(n, c);
        beam.GameMatrix = new Matrix4x4(b.X, b.Y, b.Z, 0f, c.X, c.Y, c.Z, 0f, n.X * stretch, n.Y * stretch, n.Z * stretch, 0f, own.X, own.Y, own.Z, 1f);
    }
}
