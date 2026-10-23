using System.Numerics;
using TwinBoundingBoxBuilder = Twinsanity.TwinsanityInterchange.Common.TwinBoundingBoxBuilder;

namespace CrashEngine.Runtime.Physics;

// Amedo 2026-09-27
public sealed class ConvexHull
{
    private const float Far = 1e30f;

    public Vector3[] Points = [];
    public Vector4[] Faces = [];
    public Vector3[] EdgeDirs = [];
    public Vector3[] Normals = [];
    public (byte A, byte B)[] Edges = [];
    public byte[] FaceStart = [];
    public byte[] FaceVerts = [];

    public static ConvexHull From(TwinBoundingBoxBuilder b) => new()
    {
        Points    = b.BoundingBoxPoints.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray(),
        Faces     = b.UnkVectors1.Select(v => new Vector4(v.X, v.Y, v.Z, v.W)).ToArray(),
        EdgeDirs  = b.UnkVectors2.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray(),
        Normals   = b.UnkVectors3.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray(),
        Edges     = b.UnkShorts.Select(s => ((byte)(s & 0xFF), (byte)(s >> 8))).ToArray(),
        FaceStart = b.UnkBytes1.ToArray(),
        FaceVerts = b.UnkBytes2.ToArray(),
    };

    // Amedo 2026-09-28
    public static ConvexHull Box(Vector3 min, Vector3 max) => new()
    {
        Points =
        [
            new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z), new(max.X, min.Y, max.Z), new(min.X, min.Y, max.Z),
            new(min.X, max.Y, min.Z), new(max.X, max.Y, min.Z), new(max.X, max.Y, max.Z), new(min.X, max.Y, max.Z),
        ],
        Faces =
        [
            new(0f, -1f, 0f, min.Y), new(0f, 0f, -1f, min.Z), new(1f, 0f, 0f, -max.X),
            new(0f, 0f, 1f, -max.Z), new(-1f, 0f, 0f, min.X), new(0f, 1f, 0f, -max.Y),
        ],
        EdgeDirs = [new(-1f, 0f, 0f), new(0f, 0f, -1f), new(0f, -1f, 0f)],
        Normals  = [new(0f, -1f, 0f), new(0f, 0f, -1f), new(1f, 0f, 0f)],
    };

    public void Transform(Matrix4x4 m)
    {
        for (int i = 0; i < Points.Length; i++) Points[i] = Vector3.Transform(Points[i], m);
        for (int f = 0; f < Faces.Length; f++)
        {
            int o = FaceStart[f];
            CollisionWorld.Plane(Points[FaceVerts[o + 1]], Points[FaceVerts[o + 2]], Points[FaceVerts[o + 3]], out var n, out float w);
            Faces[f] = new Vector4(n, w);
        }
    }

    public (Vector3 Min, Vector3 Max) Bounds(Matrix4x4 m)
    {
        var min = Vector3.Transform(Points[0], m);
        var max = min;
        for (int i = 1; i < Points.Length; i++)
        {
            var p = Vector3.Transform(Points[i], m);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return (min, max);
    }

    public int Side(Matrix4x4 m, Vector3 n, float w)
    {
        bool above = false, below = false;
        foreach (var point in Points)
        {
            var p = Vector3.Transform(point, m);
            if (n.X * p.X + n.Y * p.Y + n.Z * p.Z + w < 0f) below = true;
            else above = true;
            if (below && above) return 0;
        }
        return above ? 1 : -1;
    }

    internal readonly record struct Axis(Vector3 Dir, int Type);

    // Amedo 2026-09-27
    public static bool Overlaps(ConvexHull a, Matrix4x4 ma, ConvexHull b, Matrix4x4 mb)
    {
        var pa = new Vector3[a.Points.Length];
        for (int i = 0; i < pa.Length; i++) pa[i] = Vector3.Transform(a.Points[i], ma);
        var pb = new Vector3[b.Points.Length];
        for (int i = 0; i < pb.Length; i++) pb[i] = Vector3.Transform(b.Points[i], mb);
        foreach (var n in a.Normals)
            if (Separated(pa, pb, Vector3.TransformNormal(n, ma))) return false;
        foreach (var n in b.Normals)
            if (Separated(pa, pb, Vector3.TransformNormal(n, mb))) return false;
        foreach (var ea in a.EdgeDirs)
        {
            var da = Vector3.TransformNormal(ea, ma);
            foreach (var eb in b.EdgeDirs)
            {
                var db = Vector3.TransformNormal(eb, mb);
                var c = new Vector3(da.Y * db.Z - da.Z * db.Y, da.Z * db.X - da.X * db.Z, da.X * db.Y - da.Y * db.X);
                if (!(1e-8f < c.X * c.X + c.Y * c.Y + c.Z * c.Z)) continue;
                if (Separated(pa, pb, c * InverseLength(c))) return false;
            }
        }
        return true;
    }

    // Amedo
    private static Vector3[] _relPoints = new Vector3[64];

    // Amedo
    public static bool Overlaps_200370(ConvexHull a, Matrix4x4 ma, ConvexHull b, Matrix4x4 mb) =>
        Test_1FC0B0(a, b, mb * Camera.CamMath.RigidInverse_18E808(ma));

    private static bool Test_1FC0B0(ConvexHull a, ConvexHull b, Matrix4x4 rel)
    {
        int n = b.Points.Length;
        if (_relPoints.Length < n) _relPoints = new Vector3[n];
        var pts = _relPoints;
        for (int i = 0; i < n; i++) pts[i] = Vector3.Transform(b.Points[i], rel);
        foreach (var normal in a.Normals)
            if (Separated_1FBF18(a, normal, pts, n)) return false;
        foreach (var normal in b.Normals)
            if (Separated_1FBF18(a, Vector3.TransformNormal(normal, rel), pts, n)) return false;
        foreach (var ea in a.EdgeDirs)
            foreach (var eb in b.EdgeDirs)
            {
                var r = Vector3.TransformNormal(eb, rel);
                var c = new Vector3(ea.Y * r.Z - ea.Z * r.Y, ea.Z * r.X - ea.X * r.Z, ea.X * r.Y - ea.Y * r.X);
                if (2.4999998e-09f < c.X * c.X + c.Y * c.Y + c.Z * c.Z && Separated_1FBF18(a, c, pts, n)) return false;
            }
        return true;
    }

    private static bool Separated_1FBF18(ConvexHull a, Vector3 axis, Vector3[] pts, int n)
    {
        float aMin = Far, aMax = -Far, bMin = Far, bMax = -Far;
        foreach (var p in a.Points)
        {
            float v = axis.X * p.X + axis.Y * p.Y + axis.Z * p.Z;
            if (v < aMin) aMin = v;
            if (aMax < v) aMax = v;
        }
        for (int i = 0; i < n; i++)
        {
            var p = pts[i];
            float v = axis.X * p.X + axis.Y * p.Y + axis.Z * p.Z;
            if (v < bMin) bMin = v;
            if (bMax < v) bMax = v;
        }
        return aMin < bMin ? aMax < bMin : bMax < aMin;
    }

    private static bool Separated(Vector3[] pa, Vector3[] pb, Vector3 axis)
    {
        float aMin = float.MaxValue, aMax = float.MinValue, bMin = float.MaxValue, bMax = float.MinValue;
        foreach (var p in pa) { float v = Dot(p, axis); if (v < aMin) aMin = v; if (aMax < v) aMax = v; }
        foreach (var p in pb) { float v = Dot(p, axis); if (v < bMin) bMin = v; if (bMax < v) bMax = v; }
        return aMax < bMin || bMax < aMin;
    }

    private static float InverseLength(Vector3 v)
    {
        float lenSq = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
        return lenSq > 2.4999998e-09f ? 1f / MathF.Sqrt(lenSq) : 0f;
    }

    private static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    internal List<Axis> Axes(in CollisionWorld.Triangle tri, Matrix4x4 m)
    {
        var axes = new List<Axis>();
        var e1 = tri.B - tri.A;
        var e2 = tri.C - tri.B;
        var e3 = tri.A - tri.C;
        var n = Vector3.Cross(e1, e2);
        axes.Add(new Axis(n * InverseLength(n), 0));
        foreach (var normal in Normals) axes.Add(new Axis(Vector3.TransformNormal(normal, m), 1));
        Span<Vector3> edges = [e1, e2, e3];
        foreach (var dir in EdgeDirs)
        {
            var d = Vector3.TransformNormal(dir, m);
            foreach (var e in edges)
            {
                var c = Vector3.Cross(e, d);
                if (!(9.999999e-09f < c.X * c.X + c.Y * c.Y + c.Z * c.Z)) continue; // Amedo
                axes.Add(new Axis(c * InverseLength(c), 2));
            }
        }
        return axes;
    }

    public bool Collide(in CollisionWorld.Triangle tri, Matrix4x4 m, out Vector3 push, out Vector3 contact)
    {
        push = default;
        contact = new Vector3(-Far);
        CollisionWorld.Plane(tri.A, tri.B, tri.C, out var plane, out float planeW);
        if (Side(m, plane, planeW) != 0) return false;

        var world = new Vector3[Points.Length];
        for (int j = 0; j < Points.Length; j++) world[j] = Vector3.Transform(Points[j], m);
        int count = 3 * Points.Length / 4 * 4;
        var diffs = new Vector3[count];
        Span<Vector3> corners = [tri.A, tri.B, tri.C];
        for (int i = 0, k = 0; i < 3; i++)
            for (int j = 0; j < world.Length && k < count; j++, k++)
                diffs[k] = corners[i] - world[j];

        var axes = Axes(tri, m);
        float best = -Far;
        int bestIndex = -1;
        var bestDir = Vector3.Zero;
        for (int a = 0; a < axes.Count; a++)
        {
            var dir = axes[a].Dir;
            float lo = 3.689349e19f, hi = -3.689349e19f;
            foreach (var d in diffs)
            {
                float v = Dot(d, dir);
                if (v < lo) lo = v;
                if (hi < v) hi = v;
            }
            if (0f < lo || hi < 0f) return false;
            if (best < lo) { best = lo; bestIndex = a; push = dir * lo; bestDir = -dir; }
            if (best < -hi) { best = -hi; bestIndex = a; push = dir * hi; bestDir = dir; }
        }
        if (bestIndex < 0) return true;

        var moved = m;
        moved.Translation += push;
        switch (axes[bestIndex].Type)
        {
            case 0:
            {
                float deepest = -Far;
                foreach (var point in Points)
                {
                    var p = Vector3.Transform(point, moved);
                    float d = Dot(p, plane);
                    if (deepest < d) { deepest = d; contact = p; }
                }
                break;
            }
            case 1:
                if (Faces.Length > 0)
                {
                    contact = tri.A;
                    float d = Dot(tri.A, bestDir);
                    if (d < Dot(tri.B, bestDir)) { contact = tri.B; d = Dot(tri.B, bestDir); }
                    if (d < Dot(tri.C, bestDir)) contact = tri.C;
                }
                break;
            case 2:
            {
                float d0 = Dot(tri.A, bestDir), d1 = Dot(tri.B, bestDir), d2 = Dot(tri.C, bestDir);
                Vector3 a0, a1;
                if (d0 < d1 && d0 < d2) { a0 = tri.B; a1 = tri.C; }
                else if (d1 < d0 && d1 < d2) { a0 = tri.A; a1 = tri.C; }
                else { a0 = tri.A; a1 = tri.B; }
                ExtremeEdge(moved, -bestDir, out var p, out var q);
                contact = ClosestOnSecond(a0, a1, p, q);
                break;
            }
        }
        return true;
    }

    // Amedo
    internal static List<Axis> HullHullAxes_282148(ConvexHull a, Matrix4x4 ma, ConvexHull b, Matrix4x4 mb)
    {
        var axes = new List<Axis>();
        foreach (var n in a.Normals) axes.Add(new Axis(Vector3.TransformNormal(n, ma), 0));
        foreach (var n in b.Normals) axes.Add(new Axis(Vector3.TransformNormal(n, mb), 1));
        foreach (var ea in a.EdgeDirs)
        {
            var da = Vector3.TransformNormal(ea, ma);
            foreach (var eb in b.EdgeDirs)
            {
                var db = Vector3.TransformNormal(eb, mb);
                var c = new Vector3(da.Y * db.Z - da.Z * db.Y, da.Z * db.X - da.X * db.Z, da.X * db.Y - da.Y * db.X);
                if (!(9.999999e-09f < c.X * c.X + c.Y * c.Y + c.Z * c.Z)) continue;
                if (0x100 <= axes.Count) return axes;
                axes.Add(new Axis(c * InverseLength(c), 2));
            }
        }
        return axes;
    }

    // Amedo
    public static bool HullsContact_282778(ConvexHull a, Matrix4x4 ma, ConvexHull b, Matrix4x4 mb, out Vector3 push, out Vector3 contact)
    {
        push = default;
        contact = default;
        var pa = new Vector3[a.Points.Length];
        for (int i = 0; i < pa.Length; i++) pa[i] = Vector3.Transform(a.Points[i], ma);
        var pb = new Vector3[b.Points.Length];
        for (int i = 0; i < pb.Length; i++) pb[i] = Vector3.Transform(b.Points[i], mb);
        var axes = HullHullAxes_282148(a, ma, b, mb);
        float best = -Far;
        int bestIndex = -1;
        var bestDir = Vector3.Zero;
        int usable = pa.Length * pb.Length >> 2 << 2; // Amedo
        for (int k = 0; k < axes.Count; k++)
        {
            var dir = axes[k].Dir;
            float lo = 3.689349e19f, hi = -3.689349e19f;
            for (int i = 0, n = 0; i < pa.Length && n < usable; i++) // Amedo
                for (int j = 0; j < pb.Length && n < usable; j++, n++)
                {
                    float v = Dot(pa[i] - pb[j], dir);
                    if (v < lo) lo = v;
                    if (hi < v) hi = v;
                }
            if (0f < lo || hi < 0f) return false;
            if (best < lo) { best = lo; bestIndex = k; push = dir * lo; bestDir = -dir; }
            if (best < -hi) { best = -hi; bestIndex = k; push = dir * hi; bestDir = dir; }
        }
        if (bestIndex < 0) return true;
        var moved = mb;
        moved.Translation += push;
        switch (axes[bestIndex].Type)
        {
            case 0: contact = b.Support_200648(moved, -bestDir); break;
            case 1: contact = a.Support_200648(ma, bestDir); break;
            case 2:
                a.ExtremeEdge(ma, bestDir, out var a0, out var a1);
                b.ExtremeEdge(moved, -bestDir, out var b0, out var b1);
                contact = ClosestOnSecond(a0, a1, b0, b1);
                break;
        }
        return true;
    }

    // Amedo
    private Vector3 Support_200648(Matrix4x4 m, Vector3 dir)
    {
        float best = -Far;
        var at = Vector3.Zero;
        foreach (var point in Points)
        {
            var p = Vector3.Transform(point, m);
            float d = Dot(p, dir);
            if (!(best < d)) continue;
            best = d;
            at = p;
        }
        return at;
    }

    private void ExtremeEdge(Matrix4x4 m, Vector3 dir, out Vector3 p, out Vector3 q)
    {
        p = q = default;
        float best = -Far;
        foreach (var (a, b) in Edges)
        {
            var pa = Vector3.Transform(Points[a], m);
            var pb = Vector3.Transform(Points[b], m);
            float d = MathF.Min(Dot(pb, dir), Dot(pa, dir));
            if (!(best < d)) continue;
            best = d;
            p = pa;
            q = pb;
        }
    }

    private static Vector3 ClosestOnSecond(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
    {
        var u = a1 - a0;
        var v = b1 - b0;
        var w = a0 - b0;
        float uu = Dot(u, u), uv = Dot(u, v), vv = Dot(v, v);
        float det = uu * vv - uv * uv;
        float t = MathF.Abs(det) < 1e-4f ? 0.5f : (Dot(w, v) * uu - Dot(w, u) * uv) / det;
        if (t < 0f) t = 0f;
        if (1f < t) t = 1f;
        return b0 + v * t;
    }
}
