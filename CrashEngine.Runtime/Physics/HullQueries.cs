using System.Numerics;

namespace CrashEngine.Runtime.Physics;

// Amedo 2026-09-28
public static class HullQueries
{
    private const float Eps = 5e-5f;

    public static float Plane(Vector4 pl, Vector3 p) => pl.X * p.X + pl.Y * p.Y + pl.Z * p.Z + pl.W;

    public static float InvLength(Vector3 v)
    {
        float l = v.LengthSquared();
        return 2.4999998e-09f < l ? 1f / MathF.Sqrt(l) : 0f; // Amedo 2026-09-28
    }

    public static bool SphereHull_1FC818(float r, ConvexHull h, Vector3 p, out Vector3 push)
    {
        push = default;
        int n = h.Faces.Length;
        Span<float> d = n <= 64 ? stackalloc float[n] : new float[n];
        for (int i = 0; i < n; i++)
        {
            d[i] = Plane(h.Faces[i], p);
            if (r < d[i]) return false;
        }
        int scanned = 0, deepest = -1;
        float most = -1e30f;
        while (scanned < n && d[scanned] <= 0f)
        {
            if (most < d[scanned]) { most = d[scanned]; deepest = scanned; }
            scanned++;
        }
        if (scanned == n && deepest >= 0)
        {
            var f = h.Faces[deepest];
            push = new Vector3(f.X, f.Y, f.Z) * (r - most);
            return true;
        }
        for (int i = 0; i < n; i++)
        {
            if (!(0f < d[i]) || i >= h.FaceStart.Length) continue;
            int start = h.FaceStart[i];
            int count = start < h.FaceVerts.Length ? h.FaceVerts[start] : 0;
            var nrm = new Vector3(h.Faces[i].X, h.Faces[i].Y, h.Faces[i].Z);
            int j = 0;
            for (; j < count; j++)
            {
                var va = h.Points[h.FaceVerts[start + 1 + j]];
                var vb = h.Points[h.FaceVerts[j == count - 1 ? start + 1 : start + 2 + j]];
                if (0f < Plane(PlaneFrom3_185DC0(va, vb, va + nrm), p)) break;
            }
            if (j == count)
            {
                push = nrm * (r - d[i]);
                return true;
            }
        }
        float r2 = r * r, bestEdge = r2;
        int edge = -1;
        for (int e = 0; e < h.Edges.Length; e++)
        {
            var va = h.Points[h.Edges[e].Item1];
            var vb = h.Points[h.Edges[e].Item2];
            var dir = (vb - va) * InvLength(vb - va);
            float pd = Vector3.Dot(p, dir);
            if (!((pd - Vector3.Dot(va, dir)) * (pd - Vector3.Dot(vb, dir)) < 0f)) continue;
            var t = va - p;
            var perp = t - dir * Vector3.Dot(t, dir);
            float dd = perp.LengthSquared();
            if (dd < bestEdge) { bestEdge = dd; edge = e; }
        }
        if (edge != -1)
        {
            var va = h.Points[h.Edges[edge].Item1];
            var vb = h.Points[h.Edges[edge].Item2];
            var dir = (vb - va) * InvLength(vb - va);
            var t = va - p;
            var perp = t - dir * Vector3.Dot(t, dir);
            push = perp * InvLength(perp) * (MathF.Sqrt(bestEdge) - r);
            return true;
        }
        float bestVertex = r2;
        int vertex = -1;
        for (int v = 0; v < h.Points.Length; v++)
        {
            float dd = (h.Points[v] - p).LengthSquared();
            if (dd < bestVertex) { bestVertex = dd; vertex = v; }
        }
        if (vertex == -1) return false;
        var away = p - h.Points[vertex];
        push = away * InvLength(away) * (r - MathF.Sqrt(bestVertex));
        return true;
    }

    // Amedo 2026-09-30
    public static bool SphereBox_1F8988(float r, Vector3 min, Vector3 max, Vector3 c, out Vector3 push)
    {
        push = Vector3.Zero;
        if (c.X < min.X - r || max.X + r < c.X || c.Y < min.Y - r || max.Y + r < c.Y || c.Z < min.Z - r || max.Z + r < c.Z) return false;
        bool o0 = false, o1 = false, o2 = false, o3 = false, o4 = false, o5 = false;
        int count = 0, face = -1;
        float best = -1e30f;
        for (int i = 0; i < 6; i++)
        {
            float d = i switch
            {
                0 => min.X - c.X,
                1 => c.X - max.X,
                2 => min.Y - c.Y,
                3 => c.Y - max.Y,
                4 => min.Z - c.Z,
                _ => c.Z - max.Z,
            };
            bool outside = 0f < d;
            switch (i)
            {
                case 0: o0 = outside; break;
                case 1: o1 = outside; break;
                case 2: o2 = outside; break;
                case 3: o3 = outside; break;
                case 4: o4 = outside; break;
                default: o5 = outside; break;
            }
            if (outside) count++;
            else if (best < d)
            {
                best = d;
                face = i;
            }
        }
        if (count == 0)
        {
            switch (face)
            {
                case 0: push = new Vector3(best - r, 0f, 0f); break;
                case 1: push = new Vector3(r - best, 0f, 0f); break;
                case 2: push = new Vector3(0f, best - r, 0f); break;
                case 3: push = new Vector3(0f, r - best, 0f); break;
                case 4: push = new Vector3(0f, 0f, best - r); break;
                case 5: push = new Vector3(0f, 0f, r - best); break;
            }
            return true;
        }
        if (count == 1)
        {
            if (o0) push = new Vector3(min.X - r - c.X, 0f, 0f);
            else if (o1) push = new Vector3(max.X + r - c.X, 0f, 0f);
            else if (o2) push = new Vector3(0f, min.Y - r - c.Y, 0f);
            else if (o3) push = new Vector3(0f, max.Y + r - c.Y, 0f);
            else if (o4) push = new Vector3(0f, 0f, min.Z - r - c.Z);
            else if (o5) push = new Vector3(0f, 0f, max.Z + r - c.Z);
            return true;
        }
        var q = new Vector3(o0 ? min.X : max.X, o2 ? min.Y : max.Y, o4 ? min.Z : max.Z);
        if (count != 2)
        {
            var v = c - q;
            float d2 = v.LengthSquared();
            if (!(d2 < r * r)) return false;
            push = v * InvLength(v) * (r - MathF.Sqrt(d2));
            return true;
        }
        Vector3 axis;
        if (!o0 && !o1)
        {
            q.X = min.X;
            axis = Vector3.UnitX;
        }
        else if (!o2 && !o3)
        {
            q.Y = min.Y;
            axis = Vector3.UnitY;
        }
        else
        {
            q.Z = min.Z;
            axis = Vector3.UnitZ;
        }
        var e = q - c;
        var w = e - axis * Vector3.Dot(e, axis);
        float w2 = w.LengthSquared();
        if (!(w2 < r * r)) return false;
        push = w * InvLength(w) * (MathF.Sqrt(w2) - r);
        return true;
    }

    // Amedo 2026-10-05
    public static Vector4 PlaneFrom3_18D0A0(Vector3 a, Vector3 b, Vector3 c)
    {
        var e1 = b - a;
        var e2 = c - a;
        var n = new Vector3(e1.Y * e2.Z - e1.Z * e2.Y, e1.Z * e2.X - e1.X * e2.Z, e1.X * e2.Y - e1.Y * e2.X);
        float len = MathF.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
        n *= 0f < len ? 1f / len : float.MaxValue;
        return new Vector4(n, -(n.X * a.X + n.Y * a.Y + n.Z * a.Z));
    }

    [ThreadStatic] private static ConvexHull? _unitSpace;

    // Amedo 2026-10-05
    public static bool EllipsoidHull_1FD178(ConvexHull h, Matrix4x4 frame, Vector3 radii, Matrix4x4 hullWorld, out Vector3 push) =>
        EllipsoidHull_1FD178(h, frame, radii, hullWorld, out push, out _);

    // Amedo
    public static bool EllipsoidHull_1FD178(ConvexHull h, Matrix4x4 frame, Vector3 radii, Matrix4x4 hullWorld, out Vector3 push, out Vector3 normal)
    {
        push = default;
        normal = default;
        var inverse = Camera.CamMath.RigidInverse_18E808(frame);
        var scale = Matrix4x4.Identity;
        scale.M11 = 1f / radii.X;
        scale.M22 = 1f / radii.Y;
        scale.M33 = 1f / radii.Z;
        var m = hullWorld * inverse * scale;
        var u = _unitSpace ??= new ConvexHull();
        if (u.Points.Length != h.Points.Length) u.Points = new Vector3[h.Points.Length];
        if (u.Faces.Length != h.Faces.Length) u.Faces = new Vector4[h.Faces.Length];
        for (int i = 0; i < h.Points.Length; i++) u.Points[i] = Vector3.Transform(h.Points[i], m);
        for (int i = 0; i < h.Faces.Length; i++)
        {
            int k = i < h.FaceStart.Length ? h.FaceStart[i] : 0;
            if (k + 3 >= h.FaceVerts.Length) { u.Faces[i] = h.Faces[i]; continue; }
            u.Faces[i] = PlaneFrom3_18D0A0(u.Points[h.FaceVerts[k + 1]], u.Points[h.FaceVerts[k + 2]], u.Points[h.FaceVerts[k + 3]]);
        }
        u.Edges = h.Edges;
        u.FaceStart = h.FaceStart;
        u.FaceVerts = h.FaceVerts;
        if (!SphereHull_1FC818(1f, u, Vector3.Zero, out var p)) return false;
        push = Vector3.TransformNormal(p * radii, frame);
        normal = Vector3.TransformNormal(p / radii, frame);
        normal *= InvLength(normal);
        return true;
    }

    public static Vector4 PlaneFrom3_185DC0(Vector3 a, Vector3 b, Vector3 c)
    {
        var n = Vector3.Cross(b - a, c - a);
        if (MathF.Abs(n.X) <= Eps && MathF.Abs(n.Y) <= Eps && MathF.Abs(n.Z) <= Eps) n = Vector3.UnitY;
        n *= InvLength(n);
        return new Vector4(n, -Vector3.Dot(n, a));
    }

    public static bool SegmentHull_1FC470(ConvexHull h, Vector3 a, Vector3 b, out float t)
    {
        t = 0f;
        bool startInside = true;
        foreach (var pl in h.Faces)
        {
            float d0 = Plane(pl, a);
            if (Eps < d0 && Eps < Plane(pl, b)) return false;
            if (-Eps < d0) startInside = false;
        }
        if (startInside) return false;
        var e0 = a;
        var e1 = b;
        float t0 = 0f, t1 = 1f;
        int enter = -1;
        for (int i = 0; i < h.Faces.Length; i++)
        {
            var pl = h.Faces[i];
            bool endOut = Eps < Plane(pl, e1);
            if (Plane(pl, e0) <= Eps)
            {
                if (!endOut) continue;
                float f = Clip_1FC378(pl, e0, e1, out var q);
                e1 = q;
                t1 = t0 + (t1 - t0) * f;
            }
            else
            {
                if (endOut) return false;
                float f = Clip_1FC378(pl, e0, e1, out var q);
                e0 = q;
                t0 = t0 + (t1 - t0) * f;
                enter = i;
            }
        }
        if (enter == -1) return false;
        t = t0;
        return true;
    }

    public static float Clip_1FC378(Vector4 pl, Vector3 a, Vector3 b, out Vector3 q)
    {
        float f = -Plane(pl, a) / (pl.X * (b.X - a.X) + pl.Y * (b.Y - a.Y) + pl.Z * (b.Z - a.Z));
        q = a + (b - a) * f;
        return f;
    }

    public static float SegmentBodies_1F1740(IEnumerable<Character.CharacterBody> bodies, Vector3 a, Vector3 b) => SegmentBodies_1F1740(bodies, a, b, out _); // Amedo

    // Amedo
    public static float SegmentBodies_1F1740(IEnumerable<Character.CharacterBody> bodies, Vector3 a, Vector3 b, out Character.CharacterBody? hit)
    {
        float best = 1e30f;
        hit = null;
        foreach (var body in bodies)
            foreach (var (hull, world) in body.Parts)
            {
                if (!Matrix4x4.Invert(world, out var inv)) continue;
                if (!SegmentHull_1FC470(hull, Vector3.Transform(a, inv), Vector3.Transform(b, inv), out float t) || !(t < best)) continue;
                best = t;
                hit = body;
            }
        return best;
    }
}
