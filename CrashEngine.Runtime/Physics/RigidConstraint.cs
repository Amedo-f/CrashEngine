using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Physics;

// Amedo 2026-09-29
public sealed class RigidConstraint
{
    public uint Flags;
    public Vector3 AxisWorld;
    public float Limit;
    public Quaternion A, B;
    public Quaternion Rest;
    public Vector3 Point;
    public Vector3 LineOrigin, LineDir;
    public Vector4 Plane;

    private static Quaternion Scale(Quaternion q, float k) => new(q.X * k, q.Y * k, q.Z * k, q.W * k);

    private static float Dot4(Quaternion a, Quaternion b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    public void Point_291128(Vector3 p)
    {
        Flags = (Flags & ~0x70u) | 0x10;
        Point = p;
    }

    public void Line_291148(Vector3 origin, Vector3 dir)
    {
        Flags = (Flags & ~0x70u) | 0x20;
        LineOrigin = origin;
        LineDir = dir * GameMath.InverseLength(dir);
    }

    public void Plane_2911E0(Vector3 n, Vector3 p)
    {
        Flags = (Flags & ~0x70u) | 0x40;
        Plane = new Vector4(n, -(n.X * p.X + n.Y * p.Y + n.Z * p.Z));
    }

    public void Limit_2910B8(float limit, Quaternion rest)
    {
        Limit = limit;
        Flags |= 0x100;
        Rest = rest;
    }

    public void Axis_2880F8(Vector3 l, Vector3 w)
    {
        Flags |= 1;
        AxisWorld = w;
        var cr = new Vector3(l.Y * w.Z - l.Z * w.Y, l.Z * w.X - l.X * w.Z, l.X * w.Y - l.Y * w.X);
        float s = MathF.Sqrt(cr.X * cr.X + cr.Y * cr.Y + cr.Z * cr.Z);
        float c = l.X * w.X + l.Y * w.Y + l.Z * w.Z;
        if (0f <= s)
        {
            s = MathF.Sqrt((1f - c) * 0.5f);
            c = MathF.Sqrt(c * 0.5f + 0.5f);
        }
        else
        {
            s = MathF.Sqrt((1f - c) * 0.5f);
            c = -MathF.Sqrt((c + 1f) * 0.5f);
        }
        var axis = cr * GameMath.InverseLength(cr) * s;
        var a = new Quaternion(axis, c);
        var b = new Quaternion(
            axis.Y * l.Z - axis.Z * l.Y + l.X * c,
            axis.Z * l.X - axis.X * l.Z + l.Y * c,
            axis.X * l.Y - axis.Y * l.X + l.Z * c,
            -(l.X * axis.X + l.Y * axis.Y + l.Z * axis.Z));
        a = Scale(a, GameMath.QuatInvLength_11C0B8(a, 0f, 0f));
        b = Scale(b, GameMath.QuatInvLength_11C0B8(b, 0f, 0f));
        A = new Quaternion(-a.X, -a.Y, -a.Z, a.W);
        B = new Quaternion(-b.X, -b.Y, -b.Z, b.W);
    }

    public void Apply_288F58(RigidBody body)
    {
        body.Position = ProjectPoint_288430(body.Position);
        body.Momentum = ProjectVector_2885B0(body.Momentum);
        body.Velocity = ProjectVector_2885B0(body.Velocity);
        body.Orientation = ProjectOrientation_2886F8(body.Orientation);
        if ((Flags & 1) == 0) return;
        body.AngularMomentum = AxisWorld * GameMath.Dot(body.AngularMomentum, AxisWorld);
        body.Spin = AxisWorld * GameMath.Dot(body.Spin, AxisWorld);
    }

    private Vector3 ProjectPoint_288430(Vector3 p)
    {
        if ((Flags & 0x10) != 0) return Point;
        if ((Flags & 0x20) != 0) return LineOrigin + LineDir * GameMath.Dot(LineDir, p - LineOrigin);
        if ((Flags & 0x40) == 0) return p;
        var n = new Vector3(Plane.X, Plane.Y, Plane.Z);
        return p - n * (GameMath.Dot(n, p) + Plane.W);
    }

    private Vector3 ProjectVector_2885B0(Vector3 v)
    {
        if ((Flags & 0x10) != 0) return Vector3.Zero;
        if ((Flags & 0x20) != 0) return LineDir * GameMath.Dot(LineDir, v);
        if ((Flags & 0x40) == 0) return v;
        var n = new Vector3(Plane.X, Plane.Y, Plane.Z);
        return v - n * GameMath.Dot(n, v);
    }

    private Quaternion ProjectOrientation_2886F8(Quaternion q)
    {
        if ((Flags & 1) != 0)
        {
            float ab = Dot4(A, B);
            float kb = Dot4(q, new Quaternion(A.X * ab - B.X, A.Y * ab - B.Y, A.Z * ab - B.Z, A.W * ab - B.W)) / (ab * ab - 1f);
            float ka = Dot4(q, A) - kb * ab;
            var r = new Quaternion(A.X * ka + B.X * kb, A.Y * ka + B.Y * kb, A.Z * ka + B.Z * kb, A.W * ka + B.W * kb);
            q = 9.999999e-09f < Dot4(r, r) ? Scale(r, GameMath.QuatInvLength_11C0B8(r, 0f, 0f)) : A;
        }
        if ((Flags & 0x100) != 0) q = Limit_288938(q);
        return q;
    }

    private Quaternion Limit_288938(Quaternion q)
    {
        float k = GameMath.QuatInvLength_11C0B8(Rest, 0f, 0f);
        var inverse = new Quaternion(-Rest.X * k, -Rest.Y * k, -Rest.Z * k, Rest.W * k);
        var rel = GameMath.Mul_18D558(inverse, q);
        int angle = GameMath.AxisAngle_186B50(ref rel, out var axis);
        float a = angle * GameMath.UnitToRad;
        if (!(a < -Limit) && !(Limit < a)) return q;
        a = a < -Limit ? -Limit : Limit;
        rel = GameMath.AxisAngle_18D798(axis, GameMath.Trunc(a * GameMath.RadToUnit));
        return GameMath.Mul_18D558(Rest, rel);
    }
}
