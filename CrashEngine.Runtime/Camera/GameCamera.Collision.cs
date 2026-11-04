using System.Numerics;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed partial class GameCamera
{
    public CollisionWorld? Collision { get; set; }
    private readonly Dictionary<uint, TriangleCache> _eyeCaches = new();

    private static Vector3 P3(Vector4 v) => new(v.X, v.Y, v.Z);

    public bool Segment_281ED8(Vector4 from, Vector4 to, uint mask, out Vector4 hitPoint)
    {
        hitPoint = to;
        if (Collision is not { } world) { Missing("camera collision world"); return false; }
        if (!world.SegmentCast(P3(from), P3(to), mask, out var hit)) return false;
        hitPoint = new Vector4(hit.Point, 1f);
        return true;
    }

    public bool LineOfSight_275F80(uint y, Vector4 from, Vector4 to, bool objects, bool losMask, out Vector4 hitPoint)
    {
        uint mask = losMask ? 0x80u : 0x20u;
        if (!objects) return Segment_281ED8(from, to, mask, out hitPoint);
        return SegmentHitsAnything_281408(y, from, to, mask, out hitPoint); // Amedo
    }

    // Amedo
    private bool SegmentHitsAnything_281408(uint y, Vector4 from, Vector4 to, uint mask, out Vector4 hitPoint)
    {
        hitPoint = to;
        if (Collision is not { } world) { Missing("camera collision world"); return false; }
        var end = to;
        bool hit = world.SegmentCast(P3(from), P3(to), mask, out var h);
        if (hit)
        {
            hitPoint = new Vector4(h.Point, 1f);
            if (!(0f < h.T)) return true;
            end = from * h.T + to * (1f - h.T);
            end.W = 1f;
        }
        if (SegmentObjects_281F18(from, end, out var stop, ObjectAt?.Invoke(M.U(y + 0x3F0))))
        {
            hitPoint = stop;
            return true;
        }
        return hit;
    }

    public bool Obstructed_276780(uint y, Vector4 eye, Vector4 target)
    {
        ulong f = M.Q(y + 0x40);
        if ((f & 0x100000) != 0) return false;
        if ((f & 0x200000000000UL) != 0) return true;
        bool blocked = false;
        if ((f & 0x600000000UL) != 0)
        {
            float dx = eye.X - target.X, dy = eye.Y - target.Y, dz = eye.Z - target.Z;
            if (dx * dx + dy * dy + dz * dz < 4f) blocked = true;
        }
        if (blocked) return true;
        var above = M.V(M.U(y + 0x50) + 0x40);
        above.Y += 1f;
        return LineOfSight_275F80(y, above, eye, (f & 0x400000000UL) != 0, true, out _); // Amedo
    }

    public bool Obstruction_2760B8(uint y, Vector4 eye, Vector4 target, out Vector4 result, bool objects) // Amedo
    {
        float r = M.F(y + 0x3C0);
        float far = r + 2f + 0.1f;
        float lim2 = r * r + 4f;
        bool ok = false;
        result = eye;
        if (!LineOfSight_275F80(y, target, eye, objects, false, out var h)) { result = eye; ok = true; }
        else if (TryPull(target, h, 4f, lim2, 0.2f, far, out var pulled)) { result = pulled; ok = true; }
        int yawAlt = (M.I(y + 0x108) & 0xFFFF) - 0x8000;
        if (!ok)
        {
            var m = GameMath.RotXY_182AD0(M.I(0x30A658), yawAlt);
            m.M41 = target.X; m.M42 = target.Y; m.M43 = target.Z; m.M44 = target.W;
            var cand = GameMath.Transform_18E4F0(m, new Vector4(0f, 0f, -10f, 1f));
            if (!LineOfSight_275F80(y, target, cand, objects, false, out h)) { result = cand; ok = true; }
            else if (TryPull(target, h, 4f, lim2, 0.2f, far, out var pulled)) { result = pulled; ok = true; }
        }
        if (!ok)
        {
            var m = GameMath.RotXY_182AD0(M.I(y + 0x68), yawAlt);
            m.M41 = target.X; m.M42 = target.Y; m.M43 = target.Z; m.M44 = target.W;
            var cand = GameMath.Transform_18E4F0(m, new Vector4(0f, 0f, -10f, 1f));
            if (!LineOfSight_275F80(y, target, cand, objects, false, out h)) { result = cand; ok = true; }
            else if (TryPull(target, h, 4f, far, 0.2f, lim2, out var pulled))
            {
                result = new Vector4(pulled.X, pulled.Y, cand.Z, 1f);
                ok = true;
            }
        }
        if (!ok) return false;
        M.Q(y + 0x40, (M.Q(y + 0x40) & 0xFFFEFFFCFFFDFFFFUL) | 0x4000000000UL);
        return true;
    }

    private static bool TryPull(in Vector4 target, in Vector4 hit, float min2, float near2, float nearPull, float farPull, out Vector4 result)
    {
        var d = new Vector4(hit.X - target.X, hit.Y - target.Y, hit.Z - target.Z, 1f);
        float d2 = d.X * d.X + d.Y * d.Y + d.Z * d.Z;
        result = hit;
        if (!(min2 < d2)) return false;
        float pull = d2 <= near2 ? nearPull : farPull;
        float k = GameMath.InvLength_11C030(2.4999998e-09f, d);
        result = new Vector4(target.X + (d.X - d.X * k * pull), target.Y + (d.Y - d.Y * k * pull), target.Z + (d.Z - d.Z * k * pull), 1f);
        return true;
    }

    public uint EyeProbe_271AF8(uint y, Vector4 target, Vector4 eye, bool commit)
    {
        uint bits = 0;
        int count = 0;
        bool floorClip = false;
        float k = M.F(y + 0x64);
        var dir = target - eye;
        dir.W = 1f;
        float inv = GameMath.InvLength_11C030(2.4999998e-09f, dir);
        dir = new Vector4(dir.X * inv, dir.Y * inv, dir.Z * inv, 1f);
        var right = new Vector4(-dir.Z, 0f, dir.X, 1f);
        var up = new Vector4(right.Y * dir.Z - right.Z * dir.Y, right.Z * dir.X - right.X * dir.Z, right.X * dir.Y - right.Y * dir.X, 1f);
        Vector4 nearTarget;
        if ((M.Q(y + 0x40) & 4) == 0)
        {
            var off = Lerp3(M.V(y + 0x220), M.V(y + 0x230), k);
            off = RotateRows(right, up, dir, off);
            var baseP = M.V(M.U(y + 0x50) + 0x40);
            nearTarget = new Vector4(off.X + baseP.X, off.Y + baseP.Y + 1f, off.Z + baseP.Z, 1f);
        }
        else nearTarget = target;
        var nearEye = RotateRows(right, up, dir, Lerp3(M.V(y + 0x240), M.V(y + 0x250), k));
        nearEye = new Vector4(nearEye.X + eye.X, nearEye.Y + eye.Y, nearEye.Z + eye.Z, 1f);
        Span<float> a = stackalloc float[4], b = stackalloc float[4];
        for (uint i = 0; i < 4; i++)
        {
            a[(int)i] = M.F(y + 0x1F8 + i * 4) * k + M.F(y + 0x1D8 + i * 4) * (1f - k);
            b[(int)i] = M.F(y + 0x208 + i * 4) * k + M.F(y + 0x1E8 + i * 4) * (1f - k);
        }
        ulong Idx() => (M.Q(y + 0x40) >> 43) & 3;
        void Advance(ulong keepMask, int resultBit, uint value)
        {
            ulong f = M.Q(y + 0x40);
            ulong next = ((((f >> 43) & 3) + 1) & 3) << 43;
            f = (f & 0xFFFFE7FFFFFFFFFFUL) | next;
            if (commit) f = (f & keepMask) | ((ulong)value << resultBit);
            M.Q(y + 0x40, f);
        }
        if (((M.I(y + 0x54) >> 3) & 1) != 0)
        {
            M.V(y + 0x260, Place(right, up, dir, nearTarget, new Vector4(0f, a[0], 0f, 1f)));
            M.V(y + 0x270, Place(right, up, dir, nearTarget, new Vector4(0f, a[1], 0f, 1f)));
            M.V(y + 0x2A0, Place(right, up, dir, nearEye, new Vector4(0f, b[0], 0f, 1f)));
            M.V(y + 0x2B0, Place(right, up, dir, nearEye, new Vector4(0f, b[1], 0f, 1f)));
            bool skipRest = false;
            if ((M.Q(y + 0x40) & 0x180000000000UL) == 0)
            {
                bool hit = Segment_281ED8(M.V(y + 0x2A0), M.V(y + 0x260), 0x20, out _);
                bits = hit ? 1u : 0u;
                Advance(0xFFFFFDFFFFFFFFFFUL, 41, bits);
                count = 1;
            }
            else if ((M.Q(y + 0x40) & 0x20000000000UL) != 0) bits = 1;
            if (M.F(y + 0x274) <= M.F(y + 0x3C4))
            {
                floorClip = true;
                skipRest = true;
            }
            if (!skipRest)
            {
                if (count < 4 && Idx() == 1)
                {
                    if (Segment_281ED8(M.V(y + 0x2B0), M.V(y + 0x270), 0x20, out _)) bits |= 2;
                    count++;
                    Advance(0xFFFFFBFFFFFFFFFFUL, 42, bits >> 1);
                }
                else if ((M.Q(y + 0x40) & 0x40000000000UL) != 0) bits |= 2;
            }
        }
        if (((M.I(y + 0xF4) >> 3) & 1) != 0)
        {
            M.V(y + 0x280, Place(right, up, dir, nearTarget, new Vector4(a[2], 0f, 0f, 1f)));
            M.V(y + 0x290, Place(right, up, dir, nearTarget, new Vector4(a[3], 0f, 0f, 1f)));
            M.V(y + 0x2C0, Place(right, up, dir, nearEye, new Vector4(b[2], 0f, -0.42f, 1f)));
            M.V(y + 0x2D0, Place(right, up, dir, nearEye, new Vector4(b[3], 0f, -0.42f, 1f)));
            if (count < 4 && (M.Q(y + 0x40) & 0x180000000000UL) == 0x100000000000UL)
            {
                if (Segment_281ED8(M.V(y + 0x2C0), M.V(y + 0x280), 0x20, out _)) bits |= 4;
                count++;
                Advance(0xFFFFFF7FFFFFFFFFUL, 39, bits >> 2);
            }
            else if ((M.Q(y + 0x40) & 0x8000000000UL) != 0) bits |= 4;
            if (count < 4 && (M.Q(y + 0x40) & 0x180000000000UL) == 0x180000000000UL)
            {
                if (Segment_281ED8(M.V(y + 0x2D0), M.V(y + 0x290), 0x20, out _)) bits |= 8;
                ulong f = M.Q(y + 0x40) & 0xFFFFE7FFFFFFFFFFUL;
                if (commit) f = (f & 0xFFFFFEFFFFFFFFFFUL) | ((ulong)(bits >> 3) << 40);
                M.Q(y + 0x40, f);
            }
            else if ((M.Q(y + 0x40) & 0x10000000000UL) != 0) bits |= 8;
        }
        Trace?.Invoke($"probe commit {commit} count {count} bits 0x{bits:X} floor {floorClip} y40 0x{M.Q(y + 0x40):X} 274 {M.F(y + 0x274):0.000} 3C4 {M.F(y + 0x3C4):0.000}");
        uint low = bits & 3;
        if (((M.I(y + 0x144) >> 3) & 1) != 0)
        {
            bool notBoth = (bits & 0xC) != 0xC;
            bool check = true;
            if (notBoth || low != 0)
            {
                if (low != 3 && notBoth) { }
                else bits |= 0x20;
            }
            else if (!floorClip) bits |= 0x22;
            if (check && (M.Q(y + 0x40) & 8) != 0 && bits != 0 && low != 3 && notBoth && 2f < M.F(y + 0x158)) bits |= 0x40;
        }
        M.B(y + 0x1D4, (byte)(M.B(y + 0x1D4) | (byte)bits));
        return bits;
    }

    private static Vector4 Lerp3(in Vector4 a, in Vector4 b, float k) =>
        new(a.X + (b.X - a.X) * k, a.Y + (b.Y - a.Y) * k, a.Z + (b.Z - a.Z) * k, 1f);

    private static Vector4 RotateRows(in Vector4 r, in Vector4 u, in Vector4 f, in Vector4 v) =>
        new(r.X * v.X + u.X * v.Y + f.X * v.Z, r.Y * v.X + u.Y * v.Y + f.Y * v.Z, r.Z * v.X + u.Z * v.Y + f.Z * v.Z, v.W);

    private static Vector4 Place(in Vector4 r, in Vector4 u, in Vector4 f, in Vector4 origin, in Vector4 v)
    {
        var p = RotateRows(r, u, f, v);
        return new Vector4(p.X + origin.X, p.Y + origin.Y, p.Z + origin.Z, 1f);
    }

    public bool CollideScenery_275B00(uint y, Vector4 eye, Vector4 target, out Vector4 result)
    {
        bool hitAny = false;
        var pos = eye;
        if ((M.Q(y + 0x48) & 0x40) == 0)
        {
            var old = M.V(y + 0x20);
            if (Segment_281ED8(old, eye, 0x20, out var h))
            {
                var back = new Vector4(old.X - h.X, old.Y - h.Y, old.Z - h.Z, 1f);
                float k = GameMath.InvLength_11C030(2.4999998e-09f, back);
                pos = new Vector4(h.X + back.X * k * 0.01f, h.Y + back.Y * k * 0.01f, h.Z + back.Z * k * 0.01f, 1f);
            }
        }
        result = new Vector4(pos.X, pos.Y, pos.Z, 1f);
        float r = M.F(y + 0x3C0);
        if (Collision is { } world)
        {
            if (!_eyeCaches.TryGetValue(y, out var cache)) _eyeCaches[y] = cache = new TriangleCache(0x20);
            var min = new Vector3(pos.X - r - 0.1f, pos.Y - r - 0.1f, pos.Z - r - 0.1f);
            var max = new Vector3(pos.X + r + 0.1f, pos.Y + r + 0.1f, pos.Z + r + 0.1f);
            cache.Refresh(world, min, max);
            var start = P3(result);
            foreach (var tri in cache.Triangles)
            {
                if (!CollisionWorld.SphereTriangle(tri, P3(result), r, out var pushed)) continue;
                hitAny = true;
                result = new Vector4(result.X + (pushed.X - pos.X), result.Y + (pushed.Y - pos.Y), result.Z + (pushed.Z - pos.Z), 1f);
            }
        }
        else Missing("camera collision world");
        ulong f40 = M.Q(y + 0x40);
        if (!hitAny)
        {
            f40 = (f40 & 0x200000000UL) == 0 ? f40 | 0x4000000000UL : f40 & 0xFFFFFFBFFFFFFFFFUL;
            M.Q(y + 0x40, f40 & 0xFFFFDFFDFFFFFFFFUL);
        }
        else
        {
            ulong cleared = f40 & 0xFFFFFFBFFFFFFFFFUL;
            float dOld = LenSq(eye - target), dNew = LenSq(result - target);
            M.Q(y + 0x40, dOld - dNew < 5e-5f ? cleared | 0x200200000000UL : (f40 & 0xFFFFDFBFFFFFFFFFUL) | 0x200000000UL);
        }
        return hitAny;
    }

    private static float LenSq(Vector4 v) => v.X * v.X + v.Y * v.Y + v.Z * v.Z;

    public Func<Vector3, Vector3, IReadOnlyList<Character.CharacterBody>>? ObjectBodies { get; set; }

    public bool CollideObjects_275728(uint y, Vector4 eye, Vector4 current, out Vector4 result)
    {
        if ((M.Q(y + 0x40) & 0x400000000UL) == 0) M.F(y + 0x3BC, 0.4f);
        else
        {
            float grow = M.F(y + 0x3BC) + M.F(y + 0x388);
            M.F(y + 0x3BC, 0.5f < grow ? 0.5f : grow);
        }
        result = eye;
        if (ObjectBodies is null)
        {
            Missing("0x275728 camera against objects (no object bodies)");
            M.Q(y + 0x40, M.Q(y + 0x40) & 0xFFFFFFFBFFFFFFFFUL);
            return false;
        }
        if (SegmentObjects_281F18(current, eye, out var stop)) result = stop;
        float r = M.F(y + 0x3BC);
        var p = P3(result);
        var min = p - new Vector3(r + 0.1f);
        var max = p + new Vector3(r + 0.1f);
        bool hit = false;
        uint surfaceMask = Collision?.SurfaceMask(0) ?? 0x1FF0F0;
        foreach (var body in ObjectBodies(min, max))
            foreach (var (hull, world) in body.Parts)
            {
                if ((surfaceMask & 0x20) == 0) continue;
                if (!Matrix4x4.Invert(world, out var inv)) continue;
                var local = Vector3.Transform(P3(result), inv);
                if (!HullQueries.SphereHull_1FC818(r, hull, local, out var push)) continue;
                hit = true;
                var w = Vector3.TransformNormal(push, world);
                result = new Vector4(result.X + w.X, result.Y + w.Y, result.Z + w.Z, 1f);
            }
        ulong f = M.Q(y + 0x40);
        M.Q(y + 0x40, hit ? f | 0x400000000UL : f & 0xFFFFFFFBFFFFFFFFUL);
        return hit;
    }
    public Func<uint, object?>? ObjectAt { get; set; } // Amedo

    public bool SegmentObjects_281F18(Vector4 from, Vector4 to, out Vector4 point, object? skip = null)
    {
        point = to;
        if (ObjectBodies is null) return false;
        var a = P3(from);
        var b = P3(to);
        var bodies = ObjectBodies(Vector3.Min(a, b), Vector3.Max(a, b));
        float best = HullQueries.SegmentBodies_1F1740(skip is null ? bodies : bodies.Where(x => !ReferenceEquals(x.Owner, skip)), a, b);
        if (!(0f <= best && best <= 1f)) return false;
        point = new Vector4(from.X * (1f - best) + to.X * best, from.Y * (1f - best) + to.Y * best, from.Z * (1f - best) + to.Z * best, 1f);
        return true;
    }
}
