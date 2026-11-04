using System.Numerics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Streaming;

// Amedo 2026-09-28
public sealed class WallHull
{
    public Vector3[] Corners { get; } = new Vector3[4];
    public Vector4 Plane { get; private set; }
    public Vector4[] Sides { get; } = new Vector4[4];

    public static WallHull Build_1EBCE0(in Vector3 c0, in Vector3 c1, in Vector3 c2, in Vector3 c3)
    {
        var w = new WallHull();
        w.Corners[0] = c0; w.Corners[1] = c1; w.Corners[2] = c2; w.Corners[3] = c3;
        w.Plane = GameMath.PlaneFromTriangle_185DC0(c0, c2, c1);
        var center = (c0 + c1 + c2 + c3) * 0.25f;
        var up = new Vector4(w.Plane.X, w.Plane.Y, w.Plane.Z, 0f);
        for (int i = 0; i < 4; i++)
        {
            GameMath.PlaneThroughEdge_186050(out var side, new Vector4(w.Corners[i], 1f), new Vector4(w.Corners[(i + 1) & 3], 1f), up);
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
}
