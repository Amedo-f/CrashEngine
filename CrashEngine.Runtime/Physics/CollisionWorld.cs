using System.Numerics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;

namespace CrashEngine.Runtime.Physics;

// Amedo 2026-09-27
public sealed class CollisionWorld
{
    private const float Far = 1e30f;
    private const float EdgeSlack = 0.1f;

    private const int MaxLeaves = 256;
    private const float PlaneEpsilon = 5e-5f;
    private const float LengthEpsilon = 2.4999998e-09f;

    private readonly Vector3[] _verts;
    private readonly float[] _vertW;
    private readonly (int A, int B, int C, int Surface)[] _tris;
    private readonly (int Size, int Offset)[] _groups;
    private readonly (Vector3 Min, Vector3 Max, int First, int Second)[] _nodes;
    private readonly Surface[] _surfaces;

    public int TriangleCount => _tris.Length;
    public int NodeCount => _nodes.Length;

    public struct Hit
    {
        public Vector3 Point;
        public float T;
        public int Triangle;
        public int Surface;
    }

    public readonly record struct Surface(uint Mask, float Grip, int Id, float Bounce = 1f, float Roll = 1f, float Twist = 1f, bool Event = false,
                                          float Accel = 1e6f, float Slide = 0f, float SlideLimit = 0f, Vector3 Conveyor = default,
                                          Vector3 HitSource = default, uint HitKind = 0u, int HitAmount = 0, // Amedo 2026-09-28
                                          ushort[]? EffectIds = null, float[]? Phys = null)
    {
        public bool Deadly => ((Mask >> 9) & 1) != 0; // Amedo 2026-09-28
        public bool HurtsAgents => ((Mask >> 8) & 1) != 0; // Amedo

        // Amedo 2026-09-28
        public ushort SoundAt(int index, out float volume)
        {
            (int id, int vol) = index switch { 0 => (0, 0), 1 => (3, 3), 2 => (4, 3), 3 => (5, 4), 4 => (1, 1), 5 => (2, 2), _ => (-1, -1) };
            volume = vol >= 0 && Phys is { } p && vol < p.Length ? p[vol] : -1f;
            return id >= 0 && EffectIds is { } e && id < e.Length ? e[id] : (ushort)0xFFFF;
        }

        // Amedo 2026-09-28
        public ushort ParticleAt(int index)
        {
            int id = index switch { 0 => 6, 1 or 2 => 8, 4 => 7, _ => -1 };
            return id >= 0 && EffectIds is { } e && id < e.Length ? e[id] : (ushort)0xFFFF;
        }
    }

    public readonly record struct Triangle(Vector3 A, Vector3 B, Vector3 C, float WB, float WC, float WA, int Surface);

    public static CollisionWorld? FromLevel(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2)
    {
        if (rm2.GetItem<PS2AnyCollisionData>((uint)Constants.LEVEL_COLLISION_ITEM) is not { } data) return null;
        return new CollisionWorld(data, Surfaces(rm2) ?? (globalRm2 is not null ? Surfaces(globalRm2) : null) ?? new List<Surface>());
    }

    public static IReadOnlyList<Surface> LevelSurfaces(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2) => // Amedo 2026-10-04
        Surfaces(rm2) ?? (globalRm2 is not null ? Surfaces(globalRm2) : null) ?? new List<Surface>();

    private static List<Surface>? Surfaces(PS2AnyTwinsanityRM2 source)
    {
        for (int lid = Constants.LEVEL_LAYOUT_1_SECTION; lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
        {
            var sec = source.GetItem<BaseTwinSection>((uint)lid)?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_SURFACES_SECTION);
            if (sec is null || sec.GetItemsAmount() == 0) continue;
            var list = new List<Surface>(sec.GetItemsAmount());
            for (int i = 0; i < sec.GetItemsAmount(); i++)
                list.Add(sec.GetItem(i) is PS2AnyCollisionSurface s
                    ? new Surface((uint)s.CollisionMask, s.PhysicsParameters.Length > 6 ? s.PhysicsParameters[6] : 1f, (int)s.SurfaceId,
                                  s.PhysicsParameters.Length > 7 ? s.PhysicsParameters[7] : 1f,
                                  Event: s.UnkBoundingBox is { Length: > 1 } box && BitConverter.SingleToUInt32Bits(box[1].X) != 0,
                                  Accel: s.PhysicsParameters.Length > 5 ? s.PhysicsParameters[5] : 1e6f,
                                  Slide: s.PhysicsParameters.Length > 8 ? s.PhysicsParameters[8] : 0f,
                                  SlideLimit: s.PhysicsParameters.Length > 9 ? s.PhysicsParameters[9] : 0f,
                                  Conveyor: new Vector3(s.UnkVec.X, s.UnkVec.Y, s.UnkVec.Z),
                                  HitSource: s.UnkBoundingBox is { Length: > 1 } hb ? new Vector3(hb[0].X, hb[0].Y, hb[0].Z) : default, // Amedo 2026-09-28
                                  HitKind: s.UnkBoundingBox is { Length: > 1 } hk ? BitConverter.SingleToUInt32Bits(hk[1].X) : 0u,
                                  HitAmount: s.UnkBoundingBox is { Length: > 1 } ha ? (int)(BitConverter.SingleToUInt32Bits(ha[1].Y) & 0xFF) : 0,
                                  EffectIds: [s.LandSoundId1, s.UnkId3, s.UnkSoundId, s.StepSoundId1, s.StepSoundId2, s.LandSoundId2, // Amedo 2026-09-28
                                              s.WalkOnParticleSystemId, s.WalkOnParticleSystemId2, s.LandOnParticleSystemId],
                                  Phys: s.PhysicsParameters.ToArray())
                    : new Surface(0u, 1f, i));
            return list;
        }
        return null;
    }

    public CollisionWorld(PS2AnyCollisionData data, IReadOnlyList<Surface> surfaces)
    {
        _verts  = data.Vectors.Select(v => new Vector3(v.X, v.Y, v.Z)).ToArray();
        _vertW  = data.Vectors.Select(v => v.W).ToArray();
        _tris   = data.Triangles.Select(t => (t.Vector1Index, t.Vector2Index, t.Vector3Index, t.SurfaceIndex)).ToArray();
        _groups = data.Groups.Select(g => ((int)g.Size, (int)g.Offset)).ToArray();
        _nodes  = data.Triggers.Select(n => (new Vector3(n.V1.X, n.V1.Y, n.V1.Z), new Vector3(n.V2.X, n.V2.Y, n.V2.Z),
                                              n.MinTriggerIndex, n.MaxTriggerIndex)).ToArray();
        _surfaces = surfaces.ToArray();
    }

    public (Vector3 A, Vector3 B, Vector3 C) TriangleVerts(int index)
    {
        var t = _tris[index];
        return (_verts[t.A], _verts[t.B], _verts[t.C]);
    }

    public Triangle TriangleAt(int index)
    {
        var t = _tris[index];
        return new Triangle(_verts[t.A], _verts[t.B], _verts[t.C], _vertW[t.B], _vertW[t.C], _vertW[t.A], t.Surface);
    }

    public uint SurfaceMask(int surface) => surface >= 0 && surface < _surfaces.Length ? _surfaces[surface].Mask : 0u;

    public Surface SurfaceAt(int surface) =>
        surface >= 0 && surface < _surfaces.Length ? _surfaces[surface] : new Surface(0u, 1f, surface);

    public int BoxQuery(Vector3 min, Vector3 max, uint mask, List<int> result, int limit = 0x400)
    {
        result.Clear();
        if (_nodes.Length == 0) return 0;
        var leaves = new List<int>(16);
        CollectLeaves(0, min, max, leaves);
        foreach (int g in leaves)
        {
            if (g < 0 || g >= _groups.Length) continue;
            var (size, offset) = _groups[g];
            for (int j = 0; j < size; j++)
            {
                int ti = offset + j;
                if (ti < 0 || ti >= _tris.Length) continue;
                var t = _tris[ti];
                if ((SurfaceMask(t.Surface) & mask) == 0) continue;
                if (!BoxTouchesTriangle(min, max, _verts[t.A], _verts[t.B], _verts[t.C])) continue;
                result.Add(ti);
                if (result.Count == limit) return result.Count;
            }
        }
        return result.Count;
    }

    private void CollectLeaves(int index, Vector3 min, Vector3 max, List<int> leaves)
    {
        var node = _nodes[index];
        if (node.First < 0)
        {
            if (leaves.Count < MaxLeaves) leaves.Add(~node.First);
            return;
        }
        if (Overlap(min, max, _nodes[node.First].Min, _nodes[node.First].Max)) CollectLeaves(node.First, min, max, leaves);
        if (Overlap(min, max, _nodes[node.Second].Min, _nodes[node.Second].Max)) CollectLeaves(node.Second, min, max, leaves);
    }

    private static bool BoxTouchesTriangle(Vector3 min, Vector3 max, Vector3 v1, Vector3 v2, Vector3 v3)
    {
        if (!Overlap(Vector3.Min(Vector3.Min(v1, v2), v3), Vector3.Max(Vector3.Max(v1, v2), v3), min, max)) return false;
        Plane(v1, v2, v3, out var n, out float w);
        if (!BoxStraddles(min, max, n, w)) return false;
        return !EdgeRejects(min, max, v1, v2, v3, n)
            && !EdgeRejects(min, max, v2, v3, v1, n)
            && !EdgeRejects(min, max, v3, v1, v2, n);
    }

    private static bool EdgeRejects(Vector3 min, Vector3 max, Vector3 a, Vector3 b, Vector3 opposite, Vector3 n)
    {
        if (!Plane(a, b, a + n, out var en, out float ew)) return false;
        if (0f < Vector3.Dot(en, opposite) + ew) { en = -en; ew = -ew; }
        return BoxOutside(min, max, en, ew);
    }

    private static bool BoxStraddles(Vector3 min, Vector3 max, Vector3 n, float w)
    {
        int above = 0, below = 0;
        for (int i = 0; i < 8; i++)
        {
            if (0f < Vector3.Dot(n, Corner(min, max, i)) + w) above++;
            else below++;
            if (above != 0 && below != 0) return true;
        }
        return false;
    }

    private static bool BoxOutside(Vector3 min, Vector3 max, Vector3 n, float w)
    {
        for (int i = 0; i < 8; i++)
            if (Vector3.Dot(n, Corner(min, max, i)) + w < 0f) return false;
        return true;
    }

    private static Vector3 Corner(Vector3 min, Vector3 max, int i) =>
        new((i & 1) != 0 ? min.X : max.X, (i & 2) != 0 ? min.Y : max.Y, (i & 4) != 0 ? min.Z : max.Z);

    public static bool Overlap(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax) =>
        !(bMax.X < aMin.X) && !(bMax.Y < aMin.Y) && !(bMax.Z < aMin.Z)
        && !(aMax.X < bMin.X) && !(aMax.Y < bMin.Y) && !(aMax.Z < bMin.Z);

    public static bool BoxInside(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax) =>
        !(aMin.X < bMin.X) && !(aMin.Y < bMin.Y) && !(aMin.Z < bMin.Z)
        && !(bMax.X < aMax.X) && !(bMax.Y < aMax.Y) && !(bMax.Z < aMax.Z);

    public static bool Plane(Vector3 v1, Vector3 v2, Vector3 v3, out Vector3 normal, out float w)
    {
        var n = Vector3.Cross(v2 - v1, v3 - v1);
        float len = MathF.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
        normal = len > 0f ? n * (1f / len) : Vector3.Zero;
        w = -(normal.X * v1.X + normal.Y * v1.Y + normal.Z * v1.Z);
        return PlaneEpsilon < len;
    }

    // Amedo 2026-10-05
    public static bool EllipsoidTriangle_280258(in Triangle tri, Vector3 center, Vector3 radii, out Vector3 result)
    {
        var inv = new Vector3(1f / radii.X, 1f / radii.Y, 1f / radii.Z);
        var local = tri with { A = (tri.A - center) * inv, B = (tri.B - center) * inv, C = (tri.C - center) * inv, WA = 1f, WB = 1f, WC = 1f }; // Amedo
        if (!SphereTriangle(local, Vector3.Zero, 1f, out var p))
        {
            result = center;
            return false;
        }
        result = center + p * radii;
        return true;
    }

    public static bool SphereTriangle(in Triangle tri, Vector3 pos, float r, out Vector3 result)
    {
        result = pos;
        var v1 = tri.A; var v2 = tri.B; var v3 = tri.C;
        var reach = new Vector3(r);
        if (!Overlap(Vector3.Min(Vector3.Min(v1, v2), v3), Vector3.Max(Vector3.Max(v1, v2), v3), pos - reach, pos + reach)) return false;
        Plane(v1, v2, v3, out var n, out float w);
        float dist = n.X * pos.X + n.Y * pos.Y + n.Z * pos.Z + w;
        if (r < dist || dist < -r) return false;

        Span<float> d = stackalloc float[3];
        d[0] = EdgeDistance(v1, v2, tri.WB, n, pos);
        d[1] = EdgeDistance(v2, v3, tri.WC, n, pos);
        d[2] = EdgeDistance(v3, v1, tri.WA, n, pos);
        if (d[0] < 0.001f && d[1] < 0.001f && d[2] < 0.001f)
        {
            result = pos - n * (dist + r);
            return true;
        }
        for (int i = 0; i < 3; i++)
            if (r < d[i]) return false;

        Span<Vector3> v = stackalloc Vector3[3] { v1, v2, v3 };
        Span<Vector3> e = stackalloc Vector3[3] { v2 - v1, v3 - v2, v1 - v3 };
        for (int i = 0; i < 3; i++) e[i] *= InverseLength(e[i]);
        float rr = r * r;
        for (int i = 0; i < 3; i++)
        {
            if (!(0f < d[i])) continue;
            if (!(0f < Dot(e[i], pos) - Dot(e[i], v[i]))) continue;
            if (!(Dot(e[i], pos) - Dot(e[i], v[(i + 1) % 3]) < 0f)) continue;
            var q = v[i] - pos;
            var perp = q - e[i] * Dot(q, e[i]);
            float lenSq = Dot(perp, perp);
            if (!(lenSq < rr)) return false;
            float inv = InverseLength(perp);
            result = pos + perp * inv * (MathF.Sqrt(lenSq) - r);
            return true;
        }

        int nearest = -1;
        float best = 1e30f;
        for (int i = 0; i < 3; i++)
        {
            var q = v[i] - pos;
            float dsq = Dot(q, q);
            if (dsq < best) { best = dsq; nearest = i; }
        }
        var toVertex = v[nearest] - pos;
        if (!(Dot(toVertex, toVertex) < rr)) return false;
        result = v[nearest] + toVertex * InverseLength(toVertex) * -r;
        return true;
    }

    private static float EdgeDistance(Vector3 a, Vector3 b, float wb, Vector3 n, Vector3 pos)
    {
        var d = b - a;
        var c = new Vector3(d.Y * n.Z - d.Z * n.Y, d.Z * n.X - d.X * n.Z, d.X * n.Y - d.Y * n.X);
        float lenSq = c.X * c.X + c.Y * c.Y + c.Z * c.Z;
        float w = wb;
        if (LengthEpsilon < lenSq)
        {
            c *= 1f / MathF.Sqrt(lenSq);
            w = -(c.X * a.X + c.Y * a.Y + c.Z * a.Z);
        }
        return c.X * pos.X + c.Y * pos.Y + c.Z * pos.Z + w;
    }

    private static float InverseLength(Vector3 v)
    {
        float lenSq = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
        return LengthEpsilon < lenSq ? 1f / MathF.Sqrt(lenSq) : 0f;
    }

    private static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public bool SegmentCast(Vector3 start, Vector3 end, uint mask, out Hit hit)
    {
        hit = new Hit { T = Far, Triangle = -1, Surface = -1 };
        if (_nodes.Length > 0) Visit(0, start, end, mask, ref hit);
        return hit.T < Far;
    }

    private void Visit(int index, Vector3 start, Vector3 end, uint mask, ref Hit best)
    {
        var node = _nodes[index];
        if (node.First >= 0)
        {
            if (RayCrossesBox_1F9058(_nodes[node.First].Min, _nodes[node.First].Max, start, end)) Visit(node.First, start, end, mask, ref best);
            if (RayCrossesBox_1F9058(_nodes[node.Second].Min, _nodes[node.Second].Max, start, end)) Visit(node.Second, start, end, mask, ref best);
            return;
        }
        int g = ~node.First;
        if (g < 0 || g >= _groups.Length) return;
        var (size, offset) = _groups[g];
        for (int i = size - 1; i >= 0; i--)
        {
            int ti = offset + i;
            if (ti < 0 || ti >= _tris.Length) continue;
            var t = _tris[ti];
            if ((SurfaceMask(t.Surface) & mask) == 0) continue;
            if (!SegmentTriangle(_verts[t.A], _verts[t.B], _verts[t.C], start, end, best.T, out var point, out float at)) continue;
            best = new Hit { Point = point, T = at, Triangle = ti, Surface = t.Surface };
        }
    }

    private static bool SegmentTriangle(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 start, Vector3 end, float bestT,
                                        out Vector3 point, out float t)
    {
        point = default;
        t = Far;
        var e1 = v2 - v1;
        var n = Vector3.Cross(e1, v3 - v1);
        float nn = n.LengthSquared();
        if (!(nn > 0f)) return false;
        var nu = n * (1f / MathF.Sqrt(nn));
        float d = Vector3.Dot(nu, v1);
        float ds = Vector3.Dot(start, nu) - d;
        float de = Vector3.Dot(end, nu) - d;
        if ((ds < 0f) == (de < 0f)) return false;
        t = MathF.Abs(ds) / (MathF.Abs(ds) + MathF.Abs(de));
        if (!(t < bestT)) return false;
        point = start + (end - start) * t;
        return Inside(n, e1, v1, point) && Inside(n, v3 - v2, v3, point) && Inside(n, v1 - v3, v1, point);
    }

    private static bool Inside(Vector3 normal, Vector3 edge, Vector3 onEdge, Vector3 point)
    {
        var inward = Vector3.Cross(normal, edge);
        float len = inward.LengthSquared();
        if (!(len > 0f)) return false;
        inward *= 1f / MathF.Sqrt(len);
        return Vector3.Dot(inward, point) - Vector3.Dot(inward, onEdge) + EdgeSlack >= 0f;
    }

    // Amedo
    private static bool RayCrossesBox_1F9058(Vector3 min, Vector3 max, Vector3 p0, Vector3 p1)
    {
        if (InBox(min, max, p0) || InBox(min, max, p1)) return true;
        if ((p0.X < min.X && p1.X < min.X) || (p0.Y < min.Y && p1.Y < min.Y) || (p0.Z < min.Z && p1.Z < min.Z)) return false;
        if ((max.X < p0.X && max.X < p1.X) || (max.Y < p0.Y && max.Y < p1.Y) || (max.Z < p0.Z && max.Z < p1.Z)) return false;
        var d = p1 - p0;
        for (int a = 0; a < 3; a++)
        {
            int b = a == 0 ? 1 : a == 1 ? 2 : 0, c = a == 0 ? 2 : a == 1 ? 0 : 1;
            float s0 = p0[a], s1 = p1[a];
            for (int j = 0; j < 2; j++)
            {
                if (s0 == s1) continue;
                float plane = j == 0 ? min[a] : max[a];
                if (!(s0 < plane && plane < s1) && !(s1 < plane && plane < s0)) continue;
                float t = (plane - s0) / (s1 - s0);
                if (!(0f <= t) || !(t < 1f)) continue;
                var pt = p0 + d * t;
                if (min[b] < pt[b] && pt[b] < max[b] && min[c] < pt[c] && pt[c] < max[c]) return true;
            }
        }
        return false;
    }

    private static bool InBox(Vector3 min, Vector3 max, Vector3 p) =>
        p.X >= min.X && p.X <= max.X && p.Y >= min.Y && p.Y <= max.Y && p.Z >= min.Z && p.Z <= max.Z;
}
