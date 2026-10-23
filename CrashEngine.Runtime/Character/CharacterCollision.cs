using System.Numerics;
using CrashEngine.Runtime.Physics;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-27
public sealed class CharacterCollision
{
    public const int Dynamic  = 0x1;
    public const int Static   = 0x2;
    public const int Touched  = 0x10;
    public const int Ceiling  = 0x20;
    public const int Support  = 0x240;
    public const int Excluded = 0x1000;

    private const float Far = 1e30f;
    private static readonly float CreaseEps = BitConverter.Int32BitsToSingle(0x358637BE); // Amedo

    public sealed class Record
    {
        public Vector4[] Planes = [];
        public int Flags;
        public int LastPlane = -1;
        public Vector3 Push;
        public float Special;
        public Vector4 Sphere; // Amedo 2026-09-30
        public CollisionWorld.Triangle Triangle;
        public object? Owner; // Amedo 2026-09-27
        public int Part;
        public bool NonSolid; // Amedo 2026-10-05
    }

    public readonly List<Record> Solids = new();
    public readonly List<Record> Others = new();
    public Vector3 BoxMin, BoxMax;

    private readonly List<int> _query = new();

    public void Begin()
    {
        Solids.Clear();
        Others.Clear();
    }

    // Amedo 2026-09-29
    public void MarkPart_287E90(object owner, int part, int flag)
    {
        for (int i = 0; i < Solids.Count; i++)
        {
            if ((Solids[i].Flags & Dynamic) == 0 || !ReferenceEquals(Solids[i].Owner, owner)) continue;
            if (i + part < Solids.Count && ReferenceEquals(Solids[i + part].Owner, owner))
            {
                Solids[i + part].Flags |= flag;
                return;
            }
        }
    }

    // Amedo
    public static bool LimbCast(TriangleCache cache, Vector3 from, Vector3 to, out Vector3 hit, out Vector3 normal)
    {
        normal = default;
        if (!TriangleListRayCast_2928E8(cache, from, to, out _, out hit, out var tri)) return false;
        TrianglePlane(tri.A, tri.B, tri.C, out normal, out _);
        return true;
    }

    // Amedo
    public static bool TriangleListRayCast_2928E8(TriangleCache cache, Vector3 from, Vector3 to, out float t, out Vector3 point,
                                                  out CollisionWorld.Triangle triangle)
    {
        float best = Far;
        point = default;
        triangle = default;
        foreach (var tri in cache.Triangles)
        {
            float at = CheckTriangleIntersection_2803D0(tri.A, tri.B, tri.C, from, to, out var hit);
            if (!(at < Far) || !(at < best)) continue;
            best = at;
            point = hit;
            triangle = tri;
        }
        t = best;
        return best < Far;
    }

    private static float CheckTriangleIntersection_2803D0(Vector3 a, Vector3 b, Vector3 c, Vector3 from, Vector3 to, out Vector3 point) // Amedo
    {
        point = default;
        var d = to - from;
        var n = Vector3.Cross(b - a, c - a);
        if (-1e-4f < Vector3.Dot(from - a, n) * Vector3.Dot(to - a, n)) return 1e30f;
        var e0 = Vector3.Cross(d, b - a);
        var e1 = Vector3.Cross(d, c - b);
        var e2 = Vector3.Cross(d, a - c);
        if (SameSide(Vector3.Dot(a - from, e0), Vector3.Dot(c - from, e0)) ||
            SameSide(Vector3.Dot(b - from, e1), Vector3.Dot(a - from, e1)) ||
            SameSide(Vector3.Dot(c - from, e2), Vector3.Dot(b - from, e2))) return 1e30f;
        TrianglePlane(a, b, c, out var pn, out float pd);
        float t = -(Vector3.Dot(pn, from) + pd) / Vector3.Dot(pn, d);
        point = from + d * t;
        return t;
    }

    private static bool SameSide(float x, float y) => (1e-4f < x && 1e-4f < y) || (x < -1e-4f && y < -1e-4f);

    internal static bool TrianglePlane(Vector3 a, Vector3 b, Vector3 c, out Vector3 n, out float d)
    {
        n = Vector3.Cross(b - a, c - a);
        float len = MathF.Sqrt(Vector3.Dot(n, n));
        n *= 1f / len;
        d = -Vector3.Dot(n, a);
        return 5e-5f < len;
    }

    public static bool InsideHull(ConvexHull hull, Vector3 p)
    {
        foreach (var f in hull.Faces)
            if (5e-5f < f.X * p.X + f.Y * p.Y + f.Z * p.Z + f.W) return false;
        return true;
    }

    public static (Vector3 Min, Vector3 Max) LocalBounds(ConvexHull hull)
    {
        var min = hull.Points[0];
        var max = min;
        for (int i = 1; i < hull.Points.Length; i++)
        {
            min = Vector3.Min(min, hull.Points[i]);
            max = Vector3.Max(max, hull.Points[i]);
        }
        return (min, max);
    }

    public void Gather(CollisionWorld world, Vector3 p, ref Vector3 mv, ConvexHull hull)
    {
        var step = mv * 0.2f;
        var (lmin, lmax) = LocalBounds(hull);
        for (int k = 0; k < 5; k++)
        {
            var min = lmin + p;
            var max = lmax + p;
            mv = step * (5f - k);
            float m = MathF.Abs(mv.X);
            if (m < MathF.Abs(mv.Y)) m = MathF.Abs(mv.Y);
            if (m < MathF.Abs(mv.Z)) m = MathF.Abs(mv.Z);
            min -= new Vector3(m);
            max += new Vector3(m);
            min -= new Vector3(0.3f);
            max += new Vector3(0.3f);
            int n = world.BoxQuery(min, max, 0x10, _query, 32);
            BoxMin = min;
            BoxMax = max;
            if (n < 32) break;
        }
        foreach (int ti in _query)
        {
            var tri = world.TriangleAt(ti);
            var rec = Build(tri, hull);
            if (((world.SurfaceMask(tri.Surface) >> 20) & 1) != 0) Solids.Add(rec);
            else Others.Add(rec);
        }
    }

    // Amedo 2026-10-05
    public void GatherBox_283F98(CollisionWorld world, Vector3 min, Vector3 max, ConvexHull hull)
    {
        world.BoxQuery(min, max, 0x10, _query, 32);
        BoxMin = min;
        BoxMax = max;
        foreach (int ti in _query)
        {
            var tri = world.TriangleAt(ti);
            var rec = Build(tri, hull);
            if (((world.SurfaceMask(tri.Surface) >> 20) & 1) != 0) Solids.Add(rec);
            else Others.Add(rec);
        }
    }

    // Amedo 2026-10-05
    public void ResetFlags_287F88()
    {
        foreach (var r in Solids) r.Flags = (r.Flags & 0x873) | (r.NonSolid ? 0x80 : 0);
        foreach (var r in Others) r.Flags &= 0x873;
    }

    private static Record Build(in CollisionWorld.Triangle tri, ConvexHull hull)
    {
        var pts = hull.Points;
        Span<Vector3> diffs = stackalloc Vector3[3 * 8];
        int k = 0;
        for (int j = 0; j < 8; j++) diffs[k++] = tri.A - pts[j];
        for (int j = 0; j < 8; j++) diffs[k++] = tri.B - pts[j];
        for (int j = 0; j < 8; j++) diffs[k++] = tri.C - pts[j];
        var axes = hull.Axes(tri, Matrix4x4.Identity);
        if (axes.Count > 32) axes.RemoveRange(32, axes.Count - 32);
        var planes = new Vector4[axes.Count * 2];
        for (int a = 0; a < axes.Count; a++)
        {
            var d = axes[a].Dir;
            float lo = Dot(diffs[0], d), hi = lo;
            for (int i = 1; i < diffs.Length; i++)
            {
                float v = Dot(diffs[i], d);
                if (v < lo) lo = v;
                if (hi < v) hi = v;
            }
            planes[2 * a]     = new Vector4(-d.X, -d.Y, -d.Z, lo);
            planes[2 * a + 1] = new Vector4(d.X, d.Y, d.Z, -hi);
        }
        return new Record { Planes = planes, Flags = Static, Triangle = tri };
    }

    // Amedo 2026-09-27
    public void GatherBodies(IReadOnlyList<CharacterBody> bodies, Vector3 min, Vector3 max, ConvexHull shape)
    {
        foreach (var body in bodies)
        {
            if (body.Sphere is { } sphere) // Amedo 2026-09-30
            {
                if (32 <= Solids.Count) return;
                Solids.Add(new Record { Flags = Dynamic | 0x800, Owner = body.Owner, Sphere = sphere });
                continue;
            }
            for (int k = 0; k < body.Parts.Count; k++)
            {
                var (hull, m) = body.Parts[k];
                var (bmin, bmax) = hull.Bounds(m);
                if (!CollisionWorld.Overlap(min, max, bmin, bmax)) continue;
                if (32 <= Solids.Count) return;
                var rec = BuildHull(hull, m, shape, body.Owner, k);
                if (!body.Collidable) // Amedo 2026-09-28
                {
                    rec.Flags |= 0x80;
                    rec.NonSolid = true; // Amedo 2026-10-05
                }
                Solids.Add(rec);
            }
        }
    }

    // Amedo 2026-09-30
    public Vector3 SpherePush_284DF0(Vector3 p, Vector3 mv, float height, float radius)
    {
        var total = Vector3.Zero;
        var min = new Vector3(-radius, 0f, -radius);
        var max = new Vector3(radius, height, radius);
        foreach (var r in Solids)
        {
            if ((r.Flags & Dynamic) == 0 || (r.Flags & 0x800) == 0) continue;
            var c = new Vector3(r.Sphere.X, r.Sphere.Y, r.Sphere.Z) - (p + mv);
            if (!HullQueries.SphereBox_1F8988(r.Sphere.W, min, max, c, out var hit))
            {
                r.Special = 0f;
                continue;
            }
            var half = hit * 0.5f;
            r.Flags |= 0x410;
            r.Special = half.Y;
            total -= half;
            r.Push -= half;
        }
        return total;
    }

    // Amedo 2026-09-27
    private static Record BuildHull(ConvexHull hull, Matrix4x4 m, ConvexHull shape, object owner, int part)
    {
        var world = new Vector3[hull.Points.Length];
        for (int i = 0; i < world.Length; i++) world[i] = Vector3.Transform(hull.Points[i], m);
        var diffs = new Vector3[world.Length * shape.Points.Length];
        int k = 0;
        foreach (var a in world)
            foreach (var b in shape.Points)
                diffs[k++] = a - b;
        var axes = new List<Vector3>();
        foreach (var n in hull.Normals) axes.Add(Vector3.TransformNormal(n, m));
        foreach (var n in shape.Normals) axes.Add(n);
        foreach (var ea in hull.EdgeDirs)
        {
            var da = Vector3.TransformNormal(ea, m);
            foreach (var eb in shape.EdgeDirs)
            {
                var c = new Vector3(da.Y * eb.Z - da.Z * eb.Y, da.Z * eb.X - da.X * eb.Z, da.X * eb.Y - da.Y * eb.X);
                if (!(1e-8f < c.X * c.X + c.Y * c.Y + c.Z * c.Z)) continue;
                axes.Add(c * InverseLength(c));
            }
        }
        if (axes.Count > 0x100) axes.RemoveRange(0x100, axes.Count - 0x100);
        var planes = new Vector4[axes.Count * 2];
        for (int a = 0; a < axes.Count; a++)
        {
            var d = axes[a];
            float lo = Dot(diffs[0], d), hi = lo;
            for (int i = 1; i < diffs.Length; i++)
            {
                float v = Dot(diffs[i], d);
                if (v < lo) lo = v;
                if (hi < v) hi = v;
            }
            planes[2 * a]     = new Vector4(-d.X, -d.Y, -d.Z, lo);
            planes[2 * a + 1] = new Vector4(d.X, d.Y, d.Z, -hi);
        }
        return new Record { Planes = planes, Flags = Dynamic, Owner = owner, Part = part };
    }

    // Amedo 2026-09-27
    public Vector3 PushOut(Vector3 p, bool dynamic, bool statics, bool mark) => PushOut_2849C8(p, dynamic, statics, mark, null, out _);

    // Amedo
    public Vector3 PushOut_2849C8(Vector3 p, bool dynamic, bool statics, bool mark, Func<object, RigidBody?>? bodyOf, out float depth)
    {
        depth = 0f;
        var push = Vector3.Zero;
        foreach (var r in Solids)
        {
            if ((r.Flags & 0x800) != 0) continue;
            if (!((dynamic && (r.Flags & Dynamic) != 0) || (statics && (r.Flags & Static) != 0))) continue;
            var q = p + push;
            if (!Inside(r, q, 0f)) continue;
            if ((r.Flags & 0x80) != 0)
            {
                r.Flags |= 0x100;
                continue;
            }
            if (!Penetration(r, q, out var out1)) continue;
            if (mark)
            {
                r.Flags |= 4;
                if ((r.Flags & Dynamic) != 0 && r.Owner is { } owner && bodyOf?.Invoke(owner) is { IsBox: true } body) // Amedo
                {
                    body.MoveBy_291500(out1 * -0.5f);
                    r.Push += out1 * 0.5f;
                    r.Flags |= 0x400;
                }
            }
            push += out1;
            depth += MathF.Sqrt(out1.X * out1.X + out1.Y * out1.Y + out1.Z * out1.Z); // Amedo
        }
        return push;
    }

    // Amedo 2026-09-27
    public void ClassFilter(int shapeClass, Func<object, uint> stateFlags, Func<object, int>? motionStateOf = null) // Amedo
    {
        if (shapeClass == 0) return;
        foreach (var r in Solids)
        {
            if ((r.Flags & Dynamic) == 0 || r.Owner is not { } owner) continue;
            if ((stateFlags(owner) & (1u << (shapeClass & 31))) == 0) r.Flags |= 0x80;
            else if (motionStateOf?.Invoke(owner) is 13 or 14) r.Flags |= 0x80; // Amedo
        }
    }

    private static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static float Side(Vector4 q, Vector3 p) => q.X * p.X + q.Y * p.Y + q.Z * p.Z + q.W;

    private static bool Inside(Record r, Vector3 p, float threshold)
    {
        var planes = r.Planes;
        int c = r.LastPlane;
        if (c >= 0 && c < planes.Length && threshold <= Side(planes[c], p)) return false;
        for (int i = 0; i < planes.Length; i++)
        {
            if (threshold > Side(planes[i], p)) continue;
            r.LastPlane = i;
            return false;
        }
        return true;
    }

    public static bool Contains(Record r, Vector3 p) => Inside(r, p, 0f); // Amedo 2026-09-28

    public bool PointTest(Vector3 p, int mask, float threshold)
    {
        foreach (var r in Solids)
            if (!r.NonSolid && (r.Flags & mask) == 0 && Inside(r, p, threshold)) return true; // Amedo 2026-10-05
        return false;
    }

    // Amedo 2026-10-06
    public bool PointTest_280C60(Vector3 p, out Vector3 push, List<object>? touched)
    {
        push = Vector3.Zero;
        bool hit = false;
        foreach (var r in Solids)
        {
            if (r.NonSolid || (r.Flags & 0x800) != 0 || !Inside(r, p, 0f)) continue;
            hit = true;
            if (Penetration(r, p, out var d)) push += d;
            if ((r.Flags & Dynamic) != 0 && touched is { Count: < 16 } && r.Owner is { } owner) touched.Add(owner);
        }
        push *= CrashEngine.Runtime.Camera.CamMath.InvLengthGuarded_11C030(2.4999998e-09f, new Vector4(push, 0f));
        return hit;
    }

    public int PointCount(Vector3 p, int mask, float threshold)
    {
        int n = 0;
        foreach (var r in Solids)
            if (!r.NonSolid && (r.Flags & mask) == 0 && Inside(r, p, threshold)) n++; // Amedo 2026-10-05
        return n;
    }

    private static bool Penetration(Record r, Vector3 p, out Vector3 push)
    {
        push = Vector3.Zero;
        float best = Far;
        int bestPlane = -1;
        for (int i = 0; i < r.Planes.Length; i++)
        {
            float s = Side(r.Planes[i], p);
            if (-5e-5f < s) return false;
            float depth = -s;
            if (!(depth < best)) continue;
            best = depth;
            bestPlane = i;
        }
        if (bestPlane < 0) return false;
        var n = r.Planes[bestPlane];
        push = new Vector3(n.X * best, n.Y * best, n.Z * best);
        return true;
    }

    private int Crossed(Vector3 cur, Vector3 step, Span<Vector4> planes, Span<int> recs, ref int last)
    {
        int hits = 0;
        for (int i = 0; i < Solids.Count; i++)
        {
            var r = Solids[i];
            if ((r.Flags & 0x884) != 0) continue;
            var target = cur + step;
            if (!Inside(r, target, 0f)) continue;
            foreach (var q in r.Planes)
            {
                float sc = Side(q, cur);
                float st = Side(q, target);
                if (!(-1e-4f < sc) || !(st < 1e-4f)) continue;
                bool dup = false;
                for (int k = 0; k < hits && !dup; k++)
                {
                    var d = q - planes[k];
                    dup = d.X * d.X + d.Y * d.Y + d.Z * d.Z + d.W * d.W < 0.001f;
                }
                if (dup) continue;
                planes[hits] = q;
                recs[hits] = i;
                hits++;
                last = i;
                if (hits == planes.Length) return hits;
            }
        }
        return hits;
    }

    private static Vector3 Xyz(Vector4 q) => new(q.X, q.Y, q.Z);

    private bool SubStep(Vector3 cur, Vector3 step, out Vector3 result, bool grounded)
    {
        Span<Vector4> planes = stackalloc Vector4[4];
        Span<int> recs = stackalloc int[4];
        int best0 = -1, best1 = -1, last = -1;
        int n = Crossed(cur, step, planes, recs, ref last);
        if (n == 0)
        {
            result = cur + step;
            return true;
        }
        float bestDist = Far;
        var bestPos = Vector3.Zero;
        for (int k = 0; k < n; k++)
        {
            Solids[recs[k]].Flags |= Touched;
            var nrm = Xyz(planes[k]);
            float s = step.X * nrm.X + step.Y * nrm.Y + step.Z * nrm.Z;
            var slide = step - nrm * s + nrm * 2e-5f;
            if (grounded && MathF.Sqrt(slide.X * slide.X + slide.Z * slide.Z) < slide.Y) continue;
            var cand = cur + slide;
            if (PointTest(cand, 0x88C, 0f)) continue;
            float d = (cand - slide).Length();
            if (!(d < bestDist)) continue;
            bestDist = d;
            bestPos = cand;
            best0 = recs[k];
        }
        if (bestDist == Far)
        {
            Span<Vector4> planes2 = stackalloc Vector4[4];
            Span<int> recs2 = stackalloc int[4];
            for (int k = 0; k < n; k++)
            {
                var n1 = Xyz(planes[k]);
                float s1 = step.X * n1.X + step.Y * n1.Y + step.Z * n1.Z;
                var slide1 = step - n1 * s1 + n1 * 2e-5f;
                int m = Crossed(cur, slide1, planes2, recs2, ref last);
                for (int j = 0; j < m; j++)
                {
                    Solids[recs2[j]].Flags |= Touched;
                    var n2 = Xyz(planes2[j]);
                    var e = new Vector3(n1.Y * n2.Z - n1.Z * n2.Y, n1.Z * n2.X - n1.X * n2.Z, n1.X * n2.Y - n1.Y * n2.X);
                    float es = e.X * step.X + e.Y * step.Y + e.Z * step.Z;
                    var crease = e * es + (n1 + n2) * 2e-5f;
                    if (crease.X * crease.X + crease.Y * crease.Y + crease.Z * crease.Z < CreaseEps) continue; // Amedo: 0x358637BE
                    if (grounded && MathF.Sqrt(crease.X * crease.X + crease.Z * crease.Z) < crease.Y) continue;
                    var cand = cur + crease;
                    if (PointTest(cand, 0x88C, 0f)) continue;
                    float d = (cand - crease).Length();
                    if (!(d < bestDist)) continue;
                    bestDist = d;
                    bestPos = cand;
                    best0 = recs[k];
                    best1 = recs2[j];
                }
            }
        }
        if (bestDist == Far)
        {
            result = cur;
            return false;
        }
        result = bestPos;
        Contact(best0, step, cur, bestPos);
        Contact(best1, step, cur, bestPos);
        return true;
    }

    private void Contact(int index, Vector3 step, Vector3 cur, Vector3 pos)
    {
        if (index < 0 || index >= Solids.Count) return;
        var r = Solids[index];
        if ((r.Flags & Dynamic) != 0 && (r.Flags & 0x800) == 0)
        {
            r.Push += (pos - cur - step) * 20f;
            r.Flags |= 0x400;
        }
        bool ceiling = 0f < step.Y && Inside(r, pos + new Vector3(0f, 0.05f, 0f), -0.01f);
        r.Flags |= ceiling ? Ceiling : Touched;
    }

    private int SupportUnder(Vector3 p, out Vector3 normal)
    {
        normal = Vector3.Zero;
        for (int i = 0; i < Solids.Count; i++)
        {
            var r = Solids[i];
            if ((r.Flags & 0x880) != 0) continue;
            if (!Inside(r, p, 0f)) continue;
            Penetration(r, p, out var dir); // Amedo: FindGround 0x285FA0 ignores the result (push = 0 when near a face)
            float h = MathF.Sqrt(dir.X * dir.X + dir.Z * dir.Z);
            if (!(h == 0f || h < dir.Y)) continue;
            r.Flags |= Support;
            normal = dir * InverseLength(dir);
            return i;
        }
        return -1;
    }

    private static float InverseLength(Vector3 v)
    {
        float lenSq = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
        return 2.4999998e-09f < lenSq ? 1f / MathF.Sqrt(lenSq) : 0f;
    }

    public int Probe(Vector3 start, Vector3 vec, out Vector3 hitPos, out Vector3 normal)
    {
        hitPos = start;
        normal = Vector3.Zero;
        foreach (var r in Solids)
        {
            if ((r.Flags & 0x880) != 0) { r.Flags |= Excluded; continue; }
            if (Inside(r, start, 0f)) r.Flags |= Excluded;
        }
        var cur = start;
        var lastFree = start;
        var lastInside = start;
        var step = vec;
        int hit = -1;
        while (1e-4f < step.Length())
        {
            var test = cur + step;
            int r = PointCount(test, Excluded, 0f);
            if (r > 0) { hit = r; lastInside = test; }
            else { cur = test; lastFree = test; }
            step *= 0.5f;
        }
        if (hit == -1) return -2;
        hitPos = lastFree;
        int best = -1;
        float bestY = -1e10f;
        for (int i = 0; i < Solids.Count; i++)
        {
            var r = Solids[i];
            if ((r.Flags & Excluded) != 0) continue;
            if (!Inside(r, lastInside, 0f)) continue;
            int walkable = 0;
            foreach (var q in r.Planes)
            {
                if (!(0f <= Side(q, lastFree))) continue;
                if (0.705f < q.Y) walkable++;
                if (!(0.705f <= q.Y) || !(bestY < q.Y)) continue;
                bestY = q.Y;
                best = i;
                normal = Xyz(q);
            }
            if (walkable > 0) r.Flags |= Support;
        }
        return best;
    }

    private int SlideAll(Vector3 p, Vector3 mv, out Vector3 result, out Vector3 normal, bool grounded, float scale)
    {
        normal = Vector3.Zero;
        float stepLen = 0.05f / scale;
        var cur = p;
        float len = MathF.Sqrt(mv.X * mv.X + mv.Y * mv.Y + mv.Z * mv.Z);
        int steps = (int)(len / stepLen) + 1;
        if (!(steps < 7)) steps = 6;
        float seg = len / steps;
        var stepVec = mv * InverseLength(mv) * seg;
        bool stuck = false;
        if (2.5e-5f < mv.X * mv.X + mv.Y * mv.Y + mv.Z * mv.Z)
        {
            for (int s = 0; s < steps; s++)
            {
                SubStep(cur, stepVec, out var next, grounded);
                if ((next - cur).Length() < 0.001f) { stuck = true; break; }
                cur = next;
            }
        }
        int support = SupportUnder(cur, out var n1);
        if (support != -1) normal = n1;
        else
        {
            float d = 0.1f;
            float hLen = MathF.Sqrt(mv.X * mv.X + mv.Z * mv.Z);
            if (d < hLen) d = hLen;
            if (d < seg) d = seg;
            bool found = false;
            var hitPos = Vector3.Zero;
            int r = Probe(cur, new Vector3(0f, -d, 0f), out var hp, out var hn);
            if (r >= 0)
            {
                support = r;
                hitPos = hp;
                normal = hn;
                found = true;
            }
            else
            {
                Span<float> tbl = [d, 0f, -d];
                for (int k = 0; k < 9; k++)
                {
                    var v = new Vector3(tbl[k % 3], -d, tbl[k / 3]);
                    v = v * InverseLength(v) * d;
                    r = Probe(cur, v, out hp, out hn);
                    if (r < 0) continue;
                    support = r;
                    hitPos = hp;
                    normal = hn;
                    found = true;
                    break;
                }
            }
            if (mv.Y < 0.001f && found)
            {
                var diff = hitPos - cur;
                float dist = diff.Length();
                if (0.05f < dist) cur += diff * InverseLength(diff) * (dist - 0.05f);
            }
        }
        result = cur;
        if (support != -1) return support;
        support = SpecialUp();
        if (support != -1)
        {
            normal = Vector3.UnitY;
            return support;
        }
        return stuck && mv.Y < 0f ? -2 : -1;
    }

    private int SpecialUp()
    {
        for (int i = 0; i < Solids.Count; i++)
        {
            var r = Solids[i];
            if ((r.Flags & 0x800) == 0 || !(r.Special < 0f)) continue;
            r.Flags |= 0x200;
            return i;
        }
        return -1;
    }

    public int Slide(Vector3 p, Vector3 mv, out Vector3 result, out Vector3 normal, bool grounded, float scale = 1f)
    {
        int r = SlideAll(p, mv, out result, out normal, grounded, scale);
        if (r != -2) return r;
        r = SlideAll(p, new Vector3(0f, -0.125f, 0f), out result, out normal, grounded, scale);
        if (r != -2) return r;
        var test = p + new Vector3(0f, -0.125f, 0f);
        int found = -2;
        for (int i = 0; i < Solids.Count; i++)
        {
            var rec = Solids[i];
            if (rec.NonSolid || (rec.Flags & 0x800) != 0) continue; // Amedo: MoveCharacter 0x2866F0 walks the solid list only
            if (!Inside(rec, test, 0f)) continue;
            if ((rec.Flags & Dynamic) != 0) rec.Flags |= Support;
            normal = Vector3.UnitY;
            found = i;
        }
        return found != -2 ? found : 0; // Amedo: MoveCharacter 0x2866F0
    }

    public bool StepUp(Vector3 newPos, Vector3 mv, ref Vector3 moved, bool grounded)
    {
        var hMv = new Vector3(mv.X, 0f, mv.Z);
        var hMoved = new Vector3(moved.X, 0f, moved.Z);
        float lenMv = MathF.Sqrt(mv.X * mv.X + mv.Y * mv.Y + mv.Z * mv.Z);
        float lenMoved = MathF.Sqrt(moved.X * moved.X + moved.Y * moved.Y + moved.Z * moved.Z);
        if (1e-4f < hMv.Length()) hMv *= InverseLength(hMv);
        if (1e-4f < hMoved.Length()) hMoved *= InverseLength(hMoved);
        if (!grounded) return false;
        float hLen = MathF.Sqrt(mv.X * mv.X + mv.Z * mv.Z);
        if (!(0.01f < hLen) || !(lenMoved < 0.2f) || !(lenMoved < lenMv)) return false;
        if (!(Dot(hMoved, hMv) < 0.866f)) return false;
        var rem = new Vector3(mv.X - moved.X, 0f, mv.Z - moved.Z);
        float remLen = rem.Length();
        var dir = new Vector3(mv.X, 0f, mv.Z);
        dir *= InverseLength(dir);
        float up = hLen * 3f;
        if (0.2f < up) up = 0.2f;
        var stepV = new Vector3(dir.X * remLen, up, dir.Z * remLen);
        var test = newPos + stepV;
        if (PointTest(test, 0x800, 0f)) return false;
        float dist = DownProbe(test, 0f, MathF.Abs(stepV.Y), out _, out var hitNormal);
        if (dist != Far && !(0.866f < hitNormal.Y)) return false;
        moved = stepV;
        if (mv.Y < 0.001f && 0.001f < lenMv)
        {
            float t = lenMv;
            while (true)
            {
                var probe = newPos + moved;
                probe.Y -= t;
                if (!PointTest(probe, 0x880, 0f)) { moved.Y -= t; break; }
                t -= lenMv * 0.2f;
                if (!(0.001f < t)) break;
            }
        }
        return true;
    }

    public float DownProbe(Vector3 p, float up, float down, out Vector3 hitPos, out Vector3 normal) =>
        DownProbe(p, up, down, out hitPos, out normal, out _); // Amedo 2026-10-05

    // Amedo 2026-10-05
    public float DownProbe(Vector3 p, float up, float down, out Vector3 hitPos, out Vector3 normal, out object? owner)
    {
        hitPos = Vector3.Zero;
        normal = Vector3.Zero;
        owner = null;
        float best = Far, bestY = -Far;
        foreach (var r in Solids)
        {
            var a = new Vector3(p.X, p.Y + up, p.Z);
            var b = new Vector3(p.X, p.Y - down, p.Z);
            if ((r.Flags & 0x800) != 0)
            {
                if (r.Special < 0f)
                {
                    owner = null; // Amedo 2026-10-05
                    return 0f;
                }
                continue;
            }
            if ((r.Flags & 0x80) != 0) continue;
            if (!Inside(r, b, 0f)) continue;
            if (SegmentClip(r, a, b, out var hit, out var n) != 1) continue;
            if (!(bestY < hit.Y)) continue;
            bestY = hit.Y;
            hitPos = hit;
            best = p.Y - hit.Y;
            normal = n;
            owner = (r.Flags & Dynamic) != 0 ? r.Owner : null; // Amedo 2026-10-05
        }
        return best;
    }

    private static int SegmentClip(Record r, Vector3 a, Vector3 b, out Vector3 hit, out Vector3 normal)
    {
        hit = a;
        normal = Vector3.Zero;
        int k = 0;
        while (k < r.Planes.Length && Classify(r.Planes[k], a, 5e-5f) != 1) k++;
        if (k == r.Planes.Length) return 2;
        foreach (var q in r.Planes)
        {
            bool aOut = 1e-5f < Side(q, a);
            bool bOut = 1e-5f < Side(q, b);
            if (aOut && bOut) return 0;
            if (aOut) { a = Intersect(q, a, b); normal = Xyz(q); }
            else if (bOut) b = Intersect(q, a, b);
        }
        hit = a;
        return 1;
    }

    private static int Classify(Vector4 q, Vector3 p, float eps)
    {
        float s = Side(q, p);
        if (s < -eps) return 0f < s ? 1 : 3;
        if (!(eps < s)) return 2;
        return 0f < s ? 1 : 3;
    }

    private static Vector3 Intersect(Vector4 q, Vector3 a, Vector3 b)
    {
        var d = b - a;
        float t = -(q.X * a.X + q.Y * a.Y + q.Z * a.Z + q.W) / (q.X * d.X + q.Y * d.Y + q.Z * d.Z);
        return a + d * t;
    }
}
