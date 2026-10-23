using System.Numerics;

namespace CrashEngine.Runtime.Streaming;

// Amedo 2026-09-28
public sealed class WallHull
{
    public Vector3[] Corners { get; } = new Vector3[4];
    public Vector4 Plane { get; private set; }
    public Vector4[] Sides { get; } = new Vector4[4];

    public static WallHull Build_1EBCE0(Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3)
    {
        var w = new WallHull();
        w.Corners[0] = c0; w.Corners[1] = c1; w.Corners[2] = c2; w.Corners[3] = c3;
        w.Plane = PlaneFrom3_185DC0(c0, c2, c1);
        var center = (c0 + c1 + c2 + c3) * 0.25f;
        var up = new Vector3(w.Plane.X, w.Plane.Y, w.Plane.Z);
        for (int i = 0; i < 4; i++)
        {
            var side = PlaneFromEdge_186050(w.Corners[i], w.Corners[(i + 1) & 3], up, w.Sides[i]);
            if (Dot(side, center) < 0f) side = -side;
            w.Sides[i] = side;
        }
        return w;
    }

    public bool Contains_1EC0C8(Vector3 p)
    {
        float d = Dot(Plane, p);
        if (!(d < 2f) || !(0f < d)) return false;
        foreach (var s in Sides)
            if (!(0f <= Dot(s, p))) return false;
        return true;
    }

    private static float Dot(Vector4 pl, Vector3 p) => pl.X * p.X + pl.Y * p.Y + pl.Z * p.Z + pl.W;

    private static Vector4 PlaneFrom3_185DC0(Vector3 a, Vector3 b, Vector3 c)
    {
        var v1 = b - a;
        var v2 = c - a;
        var n = new Vector3(v1.Y * v2.Z - v1.Z * v2.Y, v1.Z * v2.X - v1.X * v2.Z, v1.X * v2.Y - v1.Y * v2.X);
        float l = n.X * n.X + n.Y * n.Y + n.Z * n.Z;
        float k = 2.4999998e-09f < l ? 1f / MathF.Sqrt(l) : 0f;
        n *= k;
        return new Vector4(n, -(n.X * a.X + n.Y * a.Y + n.Z * a.Z));
    }

    private static Vector4 PlaneFromEdge_186050(Vector3 a, Vector3 b, Vector3 dir, Vector4 previous)
    {
        var e = b - a;
        var n = new Vector3(e.Y * dir.Z - e.Z * dir.Y, e.Z * dir.X - e.X * dir.Z, e.X * dir.Y - e.Y * dir.X);
        float l = n.X * n.X + n.Y * n.Y + n.Z * n.Z;
        if (!(2.4999998e-9f < l)) return new Vector4(n, previous.W);
        n *= 1f / MathF.Sqrt(l);
        return new Vector4(n, -(n.X * a.X + n.Y * a.Y + n.Z * a.Z));
    }
}
