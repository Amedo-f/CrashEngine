using System.Numerics;
using CrashEngine.Importer;
using CrashEngine.Runtime.Camera;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.Character.Vehicles;

// Amedo
public sealed class SkidMark
{
    private const float Eps = 2.4999998e-09f;
    private const uint Faded = 0x00FFFFFFu;
    private static readonly Vector4 Up = new(0f, 1f, 0f, 1f);

    public bool Active;
    public Vector4 Last;
    public Vector4 Bottom;
    public uint Color30, Color34;
    public readonly SkidMarkRing Ring = new();
    public int Index;
    public int Idle;
    public float Length;

    public void Clear_161C68()
    {
        Array.Clear(Ring.Used);
        Length = 0f;
        Active = false;
    }

    public void Destroy_161B98() => Array.Clear(Ring.Used);

    public void Idle_161C40()
    {
        Idle++;
        if (Idle < 4) return;
        Length = 0f;
        Idle = 0;
        Active = false;
    }

    public void Lay_15BDB0(in Vector4 point, in Vector4 side, IReadOnlyList<CollisionWorld.Triangle> tris, CollisionWorld world,
                           float offset, float width, Func<float, float> signed)
    {
        if (Length < 2.5f)
        {
            float k = Length * 0.4f;
            if (k < 0f) k = 0f;
            if (1.1f < k) k = 1.1f;
            float r = MathF.Sqrt(k);
            float k2 = k * k;
            r = MathF.Sqrt(r);
            offset *= r;
            k2 *= k2;
            width *= k2;
        }
        float jitter = signed(0.25f);
        Idle = 0;
        var p = point;
        p.X += side.X * offset;
        p.Y += side.Y * offset;
        p.Z += side.Z * offset;
        width *= jitter + 0.9f;
        float drift = signed(0.02f);
        p.X += side.X * drift;
        p.Y += side.Y * drift;
        p.Z += side.Z * drift;
        float dx = p.X - Last.X, dy = p.Y - Last.Y, dz = p.Z - Last.Z;
        float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        if (Active && !(0.3f < dist)) return;
        Length += dist;
        if (dist < 1.5f)
            for (int i = 0; i < tris.Count; i++)
            {
                var tri = tris[i];
                if (((world.SurfaceAt(tri.Surface).Mask >> 11) & 1) == 0) continue;
                if (!Clip_15C660(tri, Last, p, out var q0, out var q1)) continue;
                var d0 = Drop_15C5E0(q0, tri);
                var d1 = Drop_15C5E0(q1, tri);
                if (q0.Y + 0.5f < d0.Y) continue;
                Segment(d0, d1, side, width);
            }
        Active = true;
        Last = p;
        Bottom = p;
        Bottom.Y -= width;
    }

    private void Segment(in Vector4 d0, in Vector4 d1, in Vector4 side, float width)
    {
        var s = new Vector3(side.X * width, side.Y * width, side.Z * width);
        var a = new Vector3(d0.X, d0.Y, d0.Z);
        var b = new Vector3(d1.X, d1.Y, d1.Z);
        var c = new Vector3(d0.X, d0.Y + -width, d0.Z) - s;
        var d = new Vector3(d1.X, d1.Y + -width, d1.Z) - s;
        var n = HullQueries.PlaneFrom3_18D0A0(a, c, d);
        var light = new Vector4(1.2f, 0.8f, 0.5f, 1f);
        float inv = GameMath.InvLength_11C030(Eps, light);
        light = new Vector4(light.X * inv, light.Y * inv, light.Z * inv, 1f);
        if (n.Y < 0f) n = new Vector4(-n.X, -n.Y, -n.Z, n.W);
        int v = GameMath.Trunc((n.X * light.X + n.Y * light.Y + n.Z * light.Z) * 48f - 24f);
        uint color = ((uint)(v < 0 ? -v : v) << 24) + Faded;
        Ring.Quads[Index] = new SkidQuad
        {
            A = a, B = b, C = c, D = d,
            ColorA = Color30, ColorB = color, ColorC = Color34, ColorD = Faded,
            Add = 0 < v,
        };
        Color34 = Faded;
        Color30 = color;
        Ring.Used[Index] = true;
        Index++;
        if (Index == SkidMarkRing.Count) Index = 0;
    }

    private static Vector3 Vertex(in CollisionWorld.Triangle t, int i) => i switch { 0 => t.A, 1 => t.B, _ => t.C };

    private static bool Clip_15C660(in CollisionWorld.Triangle tri, Vector4 last, Vector4 p, out Vector4 q0, out Vector4 q1)
    {
        q0 = last;
        q1 = p;
        Span<Vector4> planes = stackalloc Vector4[3];
        for (int i = 0; i < 3; i++)
            if (!GameMath.PlaneThroughEdge_186050(out planes[i], new Vector4(Vertex(tri, (i + 1) % 3), 1f), new Vector4(Vertex(tri, i), 1f), Up)) return false;
        for (int k = 0; k < 3; k++)
        {
            var pl = planes[k];
            float d0 = pl.X * q0.X + pl.Y * q0.Y + pl.Z * q0.Z + pl.W;
            float d1 = pl.X * q1.X + pl.Y * q1.Y + pl.Z * q1.Z + pl.W;
            if (d0 < 0.001f && d1 < 0.001f) continue;
            if (-0.001f < d0 && -0.001f <= d1) return false;
            float ex = p.X - last.X, ey = p.Y - last.Y, ez = p.Z - last.Z;
            float lp = last.X * pl.X + last.Y * pl.Y + last.Z * pl.Z;
            float ep = ex * pl.X + ey * pl.Y + ez * pl.Z;
            float t = -(pl.W + lp) / ep;
            float u = 1f - t;
            var x = new Vector4(last.X * u + p.X * t, last.Y * u + p.Y * t, last.Z * u + p.Z * t, 1f);
            if (0f <= d0) q0 = x;
            else q1 = x;
        }
        return true;
    }

    private static Vector4 Drop_15C5E0(Vector4 q, in CollisionWorld.Triangle tri)
    {
        var pl = HullQueries.PlaneFrom3_18D0A0(tri.A, tri.B, tri.C);
        return new Vector4(q.X, -(pl.W + pl.X * q.X + pl.Z * q.Z) / pl.Y, q.Z, 1f);
    }
}
