using System.Numerics;
using System.Runtime.CompilerServices;
using CrashEngine.Importer;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;
using PS2AnyOGI = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-27
public sealed class CharacterLimbs
{
    public readonly record struct Part(int Exit, Vector3 Offset, float Strength);

    public static readonly Part[] Crash =
    [
        new(7, new Vector3(0f, 0f, 0.2f), 1f),
        new(6, new Vector3(0f, 0f, 0.2f), 1f),
        new(0, Vector3.Zero, 4f),
        new(1, new Vector3(0f, 0f, 0.13f), 1.6f),
        new(4, Vector3.Zero, 3f),
        new(3, Vector3.Zero, 3f),
    ];

    // Amedo
    public static readonly Part[] Cortex =
    [
        new(4, Vector3.Zero, 4f),
        new(3, Vector3.Zero, 4f),
        new(1, Vector3.Zero, 4f),
    ];

    // Amedo
    public static readonly Part[] Kind3 =
    [
        new(4, Vector3.Zero, 0.7f),
        new(3, Vector3.Zero, 0.7f),
        new(2, Vector3.Zero, 1f),
        new(1, new Vector3(0f, 0.35f, 0f), 1f),
    ];

    // Amedo
    public static readonly Part[] Mecha =
    [
        new(7, new Vector3(0f, 0f, 3f), 2f),
        new(6, new Vector3(0f, 0f, -3f), 2f),
        new(13, Vector3.Zero, 2f),
        new(14, new Vector3(0f, 0f, -2f), 4f),
        new(16, new Vector3(0f, 0f, -2f), 4f),
        new(9, new Vector3(0f, -1f, 3.2f), 4f),
        new(10, new Vector3(0f, -1f, 3.2f), 4f),
    ];

    // Amedo
    public static Part[]? ForKind_13C758(int kind) => kind switch
    {
        0 => Crash,
        1 => Cortex,
        2 or 4 => [],
        3 => Kind3,
        5 => Mecha,
        _ => null,
    };

    private static readonly ConditionalWeakTable<PS2AnyOGI, Dictionary<int, Matrix4x4>> BindCache = new();

    public Part[] Parts { get; set; } = Crash;
    public AnimatedObject? Anim { get; set; }
    public Vector3 Accumulated;
    public Vector3 Smoothed;
    public Vector3[] LastPosition { get; private set; } = new Vector3[Crash.Length];
    public Vector3[] LastPush { get; private set; } = new Vector3[Crash.Length];

    public Vector3 Push(CollisionWorld world, CharacterCollision collision, ConvexHull stand, Matrix4x4 game, Vector3 center,
                        Vector3 disp, float dt, CharacterController? owner = null)
    {
        if (LastPosition.Length != Parts.Length)
        {
            LastPosition = new Vector3[Parts.Length];
            LastPush = new Vector3[Parts.Length];
        }
        if (owner is not null) owner.LimbTouched = null; // Amedo 2026-09-30
        for (int k = 0; k < Parts.Length && owner?.Controlled_13FD18() != true; k++) // Amedo
        {
            var part = Parts[k];
            var pos = PartPosition(part, game) ?? center;
            var d = pos - center;
            var push = Vector3.Zero;
            if (0f < d.Y && !CharacterCollision.InsideHull(stand, d))
            {
                var from = new Vector3(center.X, pos.Y, center.Z);
                if (owner?.Triangles2B0 is { } cache && CharacterCollision.LimbCast(cache, from, pos, out var hit, out var n)) // Amedo
                {
                    if (GameMath.Dot(pos - from, n) < 0f) n = -n;
                    if (-0.707f < n.Y && n.Y < 0.707f) // Amedo 2026-09-27
                    {
                        var gap = hit - pos;
                        push = n * -MathF.Sqrt(GameMath.Dot(gap, gap));
                    }
                }
                if (owner?.LimbBodies is { } bodies && DynamicCast(bodies(Vector3.Min(from, pos), Vector3.Max(from, pos)), from, pos, owner,
                                                                   out var body, out var bodyHit, out var bn)) // Amedo 2026-09-27
                {
                    if (-0.707f < bn.Y && bn.Y < 0.707f)
                    {
                        var gap = bodyHit - pos;
                        push += bn * MathF.Sqrt(GameMath.Dot(gap, gap));
                        owner.TouchSide(body);
                        owner.LimbTouched = body; // Amedo 2026-09-30
                        owner.LimbTouchPush = Smooth_13CA60();
                    }
                }
            }
            LastPosition[k] = pos;
            LastPush[k] = push;
            Accumulated += push * part.Strength;
            if (k == 3 && owner is { Bones.Kind: not 4 } nose && 0.01f < GameMath.Length(push)) nose.Bones.Squash_15F350(1f - GameMath.Length(push) * 5f); // Amedo 2026-09-30
        }
        Smooth_13CA60(); // Amedo 2026-09-30
        var v = new Vector3(Smoothed.X, 0f, Smoothed.Z);
        if (!(5e-5f < v.X * v.X + v.Z * v.Z)) return disp;
        float k5 = dt * 5f;
        var step = new Vector3(v.X * k5, 0f, v.Z * k5);
        var mv = disp;
        var dir = step * GameMath.InverseLength(step);
        float into = GameMath.Dot(dir, mv);
        if (into < 0f) mv -= dir * into;
        return mv + step;
    }

    // Amedo 2026-09-30
    private Vector3 Smooth_13CA60()
    {
        Smoothed = Smoothed * 0.5f + Accumulated * 0.5f;
        if (MathF.Sqrt(GameMath.Dot(Smoothed, Smoothed)) < 0.1f) Smoothed = Vector3.Zero;
        return Smoothed;
    }

    public Vector3? ExitPosition(int exit, float forward, Matrix4x4 game) => // Amedo 2026-09-27
        PartPosition(new Part(exit, new Vector3(0f, 0f, forward), 1f), game);

    // Amedo 2026-09-27
    private static bool DynamicCast(IReadOnlyList<CharacterBody> bodies, Vector3 from, Vector3 to, CharacterController owner,
                                    out object hitOwner, out Vector3 hit, out Vector3 normal)
    {
        hitOwner = null!;
        hit = default;
        normal = default;
        float best = float.PositiveInfinity;
        foreach (var body in bodies)
        {
            foreach (var (hull, m) in body.Parts)
            {
                if (!RayHull(hull, m, from, to, out float t, out var n) || !(0f <= t && t <= 1f) || best <= t) continue;
                best = t;
                hitOwner = body.Owner;
                normal = n;
            }
        }
        if (float.IsPositiveInfinity(best)) return false;
        if (ReferenceEquals(hitOwner, owner.Pushed) || !owner.SolidForClass(hitOwner)) return false; // Amedo 2026-09-30
        hit = from + (to - from) * best;
        return true;
    }

    // Amedo 2026-09-27
    private static bool RayHull(ConvexHull hull, Matrix4x4 m, Vector3 from, Vector3 to, out float t, out Vector3 normal)
    {
        t = 0f;
        normal = default;
        if (!Matrix4x4.Invert(m, out var inv)) return false;
        var a = Vector3.Transform(from, inv);
        var d = Vector3.Transform(to, inv) - a;
        float tIn = 0f, tOut = 1f;
        int face = -1;
        for (int i = 0; i < hull.Faces.Length; i++)
        {
            var f = hull.Faces[i];
            var n = new Vector3(f.X, f.Y, f.Z);
            float dist = GameMath.Dot(n, a) + f.W;
            float denom = GameMath.Dot(n, d);
            if (denom == 0f)
            {
                if (0f < dist) return false;
                continue;
            }
            float ti = -dist / denom;
            if (denom < 0f)
            {
                if (tIn < ti) { tIn = ti; face = i; }
            }
            else if (ti < tOut) tOut = ti;
            if (tOut < tIn) return false;
        }
        if (face < 0) return false;
        t = tIn;
        var fn = hull.Faces[face];
        normal = Vector3.TransformNormal(new Vector3(fn.X, fn.Y, fn.Z), m);
        normal *= GameMath.InverseLength(normal);
        return true;
    }

    private Vector3? PartPosition(Part part, Matrix4x4 game)
    {
        if (ExitWorld(part.Exit, game) is not { } w) return null; // Amedo 2026-09-28
        return w.Translation + new Vector3(w.M11, w.M12, w.M13) * part.Offset.X
                             + new Vector3(w.M21, w.M22, w.M23) * part.Offset.Y
                             + new Vector3(w.M31, w.M32, w.M33) * part.Offset.Z;
    }

    // Amedo
    public Func<int, Matrix4x4?>? ExitPoint_293B70 { get; set; }

    // Amedo 2026-09-28
    private Matrix4x4? ExitWorld(int index, Matrix4x4 game)
    {
        if (ExitPoint_293B70 is { } kept) return kept(index); // Amedo
        if (Anim is not { } anim || (anim.CurrentGroup ?? anim.DefaultGroup) is not { Ogi: { } ogi }) return null;
        if (index < 0 || ogi.ExitPoints.Count <= index) return null;
        var exit = ogi.ExitPoints[index];
        var joints = anim.JointWorlds ?? BindCache.GetValue(ogi, MeshDecoder.ComputeJointWorlds);
        if (!joints.TryGetValue((int)exit.ParentJointIndex, out var joint)) return null;
        var m = exit.Matrix;
        var local = new Matrix4x4(m.Column1.X, m.Column1.Y, m.Column1.Z, m.Column1.W,
                                  m.Column2.X, m.Column2.Y, m.Column2.Z, m.Column2.W,
                                  m.Column3.X, m.Column3.Y, m.Column3.Z, m.Column3.W,
                                  m.Column4.X, m.Column4.Y, m.Column4.Z, m.Column4.W);
        return local * joint * game;
    }

    // Amedo 2026-09-28
    public Matrix4x4? ExitMatrix(int index, Matrix4x4 game)
    {
        if (ExitPoint_293B70 is { } kept) return kept(index); // Amedo
        if (ExitWorld(index, game) is not { } w) return null;
        var x = new Vector3(w.M11, w.M12, w.M13);
        var y = new Vector3(w.M21, w.M22, w.M23);
        var z = new Vector3(w.M31, w.M32, w.M33);
        x *= GameMath.InverseLength(x);
        y *= GameMath.InverseLength(y);
        z *= GameMath.InverseLength(z);
        return new Matrix4x4(x.X, x.Y, x.Z, 0f, y.X, y.Y, y.Z, 0f, z.X, z.Y, z.Z, 0f, w.M41, w.M42, w.M43, 1f);
    }
}
