using System.Numerics;
using CrashEngine.Importer;
using CrashEngine.Runtime.Camera;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed class GrapleRope : MeshDecoder.IJointControllers, MeshDecoder.IJointPoser
{
    private const int Off = 0, FadeIn = 1, On = 2, FadeOut = 3, NoPending = 4;
    private const float Eps = 2.4999998e-09f;

    private readonly AgentLabAgent _hand;
    private AnimatedObject? _anim;
    private bool _plugged;
    private int _mode = Off, _pending = NoPending;
    public float Weight;
    private int _start, _duration;
    public Vector4 Start = new(0f, 0f, 0f, 1f), Head = new(0f, -1f, 0f, 1f);
    public float RestLength = 1f;
    public readonly float Stiffness = 10000f, Damping = 100f, HeadMass = 1f;
    private Matrix4x4 _node = Matrix4x4.Identity, _toNode = Matrix4x4.Identity;

    public GrapleRope(AgentLabAgent hand) => _hand = hand;

    public int Mode => _mode;
    public bool Plugged => _plugged;

    private bool Plug_18FAF0()
    {
        if (_plugged || _hand.Destroyed || _hand.Anim is not { } anim) return false;
        _plugged = true;
        _anim = anim;
        if (anim.Controllers is null) anim.Controllers = this;
        else if (!ReferenceEquals(anim.Controllers, this))
            _hand.World.Unimplemented("animation", 0x298378, "graple rope shares a model with another joint controller");
        return true;
    }

    public void Enable_1912A8(int duration)
    {
        if (!Plug_18FAF0()) return;
        if (duration == 0)
        {
            _pending = On;
            return;
        }
        _duration = duration;
        _start = 0;
        _pending = FadeIn;
    }

    public void Step_162220(int now, Vector4 head, Vector4 start)
    {
        Place_1909A0(start, head);
        Update_18FFA8(now);
    }

    private void Place_1909A0(Vector4 start, Vector4 head)
    {
        var step = new Vector3(head.X - start.X, head.Y - start.Y, head.Z - start.Z);
        RestLength = MathF.Sqrt(step.X * step.X + step.Y * step.Y + step.Z * step.Z);
        Start = start;
        Head = new Vector4(start.X + step.X, start.Y + step.Y, start.Z + step.Z, start.W);
    }

    private void Update_18FFA8(int now)
    {
        if (_pending != NoPending)
        {
            _mode = _pending;
            _start = now;
            _pending = NoPending;
        }
        switch (_mode)
        {
            case On:
                Weight = 1f;
                break;
            case FadeIn:
                if (Fade(now, out float tIn))
                {
                    Weight = 1f;
                    _pending = On;
                }
                else Weight = tIn;
                break;
            case FadeOut:
                if (Fade(now, out float tOut)) _pending = Off;
                Weight = 1f - tOut;
                break;
        }
        if (_mode == Off || _hand.Destroyed) return;
        _node = _hand.GameMatrix;
        _toNode = CamMath.RigidInverse_18E808(_node);
    }

    private bool Fade(int now, out float t)
    {
        int elapsed = unchecked(now - _start);
        if (!(elapsed < _duration))
        {
            t = 1f;
            return true;
        }
        t = elapsed * CamMath.TickToSec / (_duration * CamMath.TickToSec);
        return false;
    }

    public void Rotate(int reactId, ref Quaternion rot, bool hasRotation) { }

    public bool Transform(int reactId, ref Matrix4x4 model) => false;

    public bool Pose(int reactId, int joint, ref Vector3 translate, ref Quaternion rot, ref int flags, in Matrix4x4 current, in Matrix4x4 parent, bool hasParent)
    {
        if (!_plugged) return false;
        return reactId switch
        {
            0 => Aim_18FC50(0, 1, joint, ref translate, ref rot, ref flags, current, parent, hasParent),
            1 => Place_191170(1, joint, ref translate, ref flags, parent, hasParent),
            _ => false,
        };
    }

    private Vector4 Point(int i) => i == 0 ? Start : Head;

    private bool Place_191170(int a, int joint, ref Vector3 translate, ref int flags, in Matrix4x4 parent, bool hasParent)
    {
        if (_mode == Off) return false;
        var p = CamMath.Transform_18E4F0(_toNode, Point(a));
        if (hasParent) p = CamMath.Transform_18E4F0(CamMath.RigidInverse_18E808(parent), p);
        Translate_297E68(joint, ref translate, ref flags, p);
        return true;
    }

    private bool Aim_18FC50(int a, int b, int joint, ref Vector3 translate, ref Quaternion rot, ref int flags, in Matrix4x4 current, in Matrix4x4 parent, bool hasParent)
    {
        if (_mode == Off || Ogi is not { } ogi || ChildByReact_297FA0(ogi, joint, b) is not { } child) return false;
        var pa = CamMath.Transform_18E4F0(_toNode, Point(a));
        var pb = CamMath.Transform_18E4F0(_toNode, Point(b));
        var ct = child.LocalTranslation;
        var tip = CamMath.Transform_18E4F0(current, new Vector4(ct.X, ct.Y, ct.Z, ct.W));
        var d1 = new Vector4(tip.X - current.M41, tip.Y - current.M42, tip.Z - current.M43, tip.W);
        var d2 = new Vector4(pb.X - pa.X, pb.Y - pa.Y, pb.Z - pa.Z, pb.W);
        float k1 = CamMath.InvLengthGuarded_11C030(Eps, d1);
        var u1 = new Vector3(d1.X * k1, d1.Y * k1, d1.Z * k1);
        float k2 = CamMath.InvLengthGuarded_11C030(Eps, d2);
        var u2 = new Vector3(d2.X * k2, d2.Y * k2, d2.Z * k2);
        int angle = CamMath.Trunc(CamMath.VectorAngle_187BC8(u1, u2) * Weight);
        var q = Quaternion.Identity;
        if (angle != 0)
        {
            var axis = new Vector3(u1.Y * u2.Z - u1.Z * u2.Y, u1.Z * u2.X - u1.X * u2.Z, u1.X * u2.Y - u1.Y * u2.X);
            q = CamMath.AxisAngle_18D798(axis, angle);
        }
        if (hasParent) pa = CamMath.Transform_18E4F0(CamMath.RigidInverse_18E808(parent), pa);
        if ((flags & 1) != 0) rot = CamMath.Mul_18D558(q, rot);
        else
        {
            rot = q;
            flags |= 1;
        }
        Translate_297E68(joint, ref translate, ref flags, pa);
        return true;
    }

    private void Translate_297E68(int joint, ref Vector3 translate, ref int flags, Vector4 p)
    {
        if ((flags & 2) != 0)
        {
            translate = new Vector3(translate.X + (p.X - translate.X) * Weight, translate.Y + (p.Y - translate.Y) * Weight,
                                    translate.Z + (p.Z - translate.Z) * Weight);
            return;
        }
        Twinsanity.TwinsanityInterchange.Common.Vector4? bind = Ogi is { } ogi && joint < ogi.Joints.Count ? ogi.Joints[joint].LocalTranslation : null; // Amedo
        float bx = bind?.X ?? 0f, by = bind?.Y ?? 0f, bz = bind?.Z ?? 0f;
        translate = new Vector3(bx + (p.X - bx) * Weight, by + (p.Y - by) * Weight, bz + (p.Z - bz) * Weight);
        flags |= 2;
    }

    private PS2AnyOGI? Ogi => _anim?.CurrentGroup?.Ogi;

    private static Twinsanity.TwinsanityInterchange.Common.TwinJoint? ChildByReact_297FA0(PS2AnyOGI ogi, int joint, int react)
    {
        int index = ogi.Joints[joint].Index;
        foreach (var c in ogi.Joints)
        {
            if (c.ParentIndex != index || ReferenceEquals(c, ogi.Joints[joint])) continue;
            if (c.ReactId == react) return c;
            int at = ogi.Joints.IndexOf(c);
            if (ChildByReact_297FA0(ogi, at, react) is { } deeper) return deeper;
        }
        return null;
    }
}
