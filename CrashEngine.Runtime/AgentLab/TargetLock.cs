using System.Numerics;
using CrashEngine.Runtime.Camera;
using CrashEngine.Runtime.Physics;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo
public sealed class TargetLock
{
    private const uint QueryMask = 0x3FF000;
    private const float Eps = 2.4999998e-09f;
    private static readonly HullBuilder Builder = new();
    private static readonly List<(AgentLabAgent Agent, int Score)> Results = new();

    public ulong Word;
    public uint Mask;
    public Vector4 Offset;
    public float Length, Near, Far;
    public readonly (byte Id, byte Priority)[] Kinds = new (byte, byte)[16];
    public int Confirmed;
    private AgentLabAgent? _target, _marker;
    public Matrix4x4 TargetMatrix;
    public Matrix4x4 Frame;
    public ConvexHull Hull = new();

    public int KindCount => (int)((Word >> 8) & 0xF);
    public bool AnyInstance => (Word & 0x1000) != 0;
    public AgentLabAgent? Target => Live(ref _target);
    public AgentLabAgent? Marker => Live(ref _marker);
    public Vector3 AimPoint => new(TargetMatrix.M41, TargetMatrix.M42, TargetMatrix.M43);

    private static AgentLabAgent? Live(ref AgentLabAgent? handle)
    {
        if (handle is { Destroyed: true }) handle = null;
        return handle;
    }

    public void Construct_15D128()
    {
        Mask = 0;
        _target = null;
        _marker = null;
        Hull = new ConvexHull();
        Word &= ~0xFFFFFFFFUL;
        Reset_15D178();
    }

    public void Reset_15D178()
    {
        ulong keep = Word & 0x1F00UL;
        Word = (Word & ~0xFFFFFFFFUL) | keep;
        Confirmed = 0;
        _target = null;
        _marker = null;
    }

    public void SetShape_161CC8(float length, float near, float far)
    {
        Length = length;
        Near = near;
        Far = far;
        MakeHull_15CE18(false);
    }

    public void SetFlatShape_161CF8(float length, float near, float far)
    {
        Length = length;
        Near = near;
        Far = far;
        MakeHull_15CE18(true);
    }

    public void AddKind_161DE8(byte id, byte priority)
    {
        int k = KindCount;
        Kinds[k] = (id, priority);
        Word = (Word & ~0xF00UL) | ((ulong)((k + 1) & 0xF) << 8);
    }

    private void MakeHull_15CE18(bool flat)
    {
        float b = Near;
        float f5 = Far * 0.5f + b * 0.5f;
        float f7 = Length * 0.5f + -0.25f;
        float f4, f6;
        if (flat) { f4 = -b; f6 = -f5; }
        else { f4 = -f5; f6 = f4; }
        float c = Far, a = Length;
        Span<Vector4> v =
        [
            new(-b, -b, -0.5f, 1f), new(b, -b, -0.5f, 1f), new(b, b, -0.5f, 1f), new(-b, b, -0.5f, 1f),
            new(f6, f4, f7, 1f), new(f5, f4, f7, 1f), new(f5, f5, f7, 1f), new(f6, f5, f7, 1f),
            new(-c, f4, a, 1f), new(c, f4, a, 1f), new(c, f5, a, 1f), new(-c, f5, a, 1f),
        ];
        var hb = Builder;
        hb.Begin_1FFF00();
        foreach (var p in v) hb.AddPoint_1FFF80(p);
        hb.AddQuad_1FB450(v[0], v[1], v[2], v[3]);
        hb.AddQuad_1FB450(v[0], v[4], v[5], v[1]);
        hb.AddQuad_1FB450(v[1], v[5], v[6], v[2]);
        hb.AddQuad_1FB450(v[3], v[2], v[6], v[7]);
        hb.AddQuad_1FB450(v[3], v[7], v[4], v[0]);
        hb.AddQuad_1FB450(v[4], v[8], v[9], v[5]);
        hb.AddQuad_1FB450(v[5], v[9], v[10], v[6]);
        hb.AddQuad_1FB450(v[7], v[6], v[10], v[11]);
        hb.AddQuad_1FB450(v[7], v[11], v[8], v[4]);
        hb.AddQuad_1FB450(v[9], v[8], v[11], v[10]);
        hb.EndPoints_1FFF20();
        if (hb.Finish_1FFF28(out var hull)) Hull = hull;
    }

    private static bool HasComponent(AgentLabAgent a, byte id) => id < 32 && (a.ComponentMask & (1u << id)) != 0;

    private int Priority(AgentLabAgent a)
    {
        for (int j = 0; j < KindCount; j++)
            if (HasComponent(a, Kinds[j].Id)) return Kinds[j].Priority << 24;
        return 0;
    }

    public bool CanTarget_15C978(AgentLabAgent? a)
    {
        if (a is null || a.Destroyed || (a.AgentFlags & 1) != 0) return false;
        if (!AnyInstance && !a.LockOnFlag_200) return false;
        return 0 < Priority(a);
    }

    public bool TargetPoint_161DB8(out Vector3 point)
    {
        point = AimPoint;
        return Target is not null;
    }

    public void Keep_161D28(int now, AgentLabAgent self, AgentLabAgent? weapon)
    {
        if (Target is { } current && CanTarget_15C978(current)) Confirmed = now;
        PlaceMarker_15CA60(self, weapon);
    }

    public void Drop_15D938()
    {
        if (Target is not null) _target = null;
        if (Marker is { Rendered: true } m) m.SetRendered(false);
    }

    public void Search_15D250(int now, AgentLabAgent self, AgentLabAgent? weapon)
    {
        var m = self.GameMatrix;
        var off = GameMath.Rotate3_18E4B8(m, Offset);
        m.M41 += off.X;
        m.M42 += off.Y;
        m.M43 += off.Z;
        Frame = m;
        var origin = new Vector3(m.M41, m.M42, m.M43);
        Results.Clear();
        var current = Target;
        bool listed = false;
        foreach (var o in self.World.QueryOverlap(Hull, m, QueryMask, 1, self, 0x40, points: true)) // Amedo
        {
            if (o is not AgentLabAgent a) continue;
            if (!CanTarget_15C978(a) && (a.ComponentMask & Mask) == 0) continue;
            var (mn, mx) = a.TreeBox;
            var center = (mx - mn) * 0.5f + mn;
            if (self.Chunk.Collision is { } world && world.SegmentCast(origin, center, 0x10, out _)) continue;
            int priority = Priority(a);
            var d = new Vector4(center.X - origin.X, center.Y - origin.Y, center.Z - origin.Z, 1f);
            float dist2 = d.X * d.X + d.Y * d.Y + d.Z * d.Z;
            float inv = GameMath.InvLength_11C030(Eps, d);
            float lateral = MathF.Abs(d.X * inv * m.M11 + d.Y * inv * m.M12 + d.Z * inv * m.M13) * 100f;
            Results.Add((a, unchecked(priority + GameMath.Trunc(lateral * dist2))));
            if (ReferenceEquals(Target, a)) listed = true;
        }
        AgentLabAgent? best = null;
        int bestScore = 0;
        foreach (var (a, score) in Results)
            if (best is null || score < bestScore) { best = a; bestScore = score; }
        if (best is not null && (best.ComponentMask & Mask) != 0)
        {
            Results.Clear();
            best = null;
        }
        if (ReferenceEquals(current, best)) Confirmed = now;
        else if (!(unchecked(now - Confirmed) < GameMath.Trunc(GameMath.SecToTick * 0.25f))) _target = best;
        else if (listed || best is not null)
        {
            if (!CanTarget_15C978(current))
            {
                _target = best;
                Confirmed = now;
            }
        }
        else _target = best;
        PlaceMarker_15CA60(self, weapon);
    }

    public void PlaceMarker_15CA60(AgentLabAgent self, AgentLabAgent? weapon)
    {
        if (Marker is null && weapon is not null && weapon.HasLinkList && weapon.LinkCount != 0) _marker = weapon.FirstLink;
        if (Marker is not { } marker) return;
        if (Target is not { } target)
        {
            if (marker.Rendered) marker.SetRendered(false);
            return;
        }
        var cam = self.World.CutsceneCamera?.Pose ?? Matrix4x4.Identity;
        TargetMatrix = target.GameMatrix;
        var (mn, mx) = target.TreeBox;
        var p = (mx - mn) * 0.5f + mn;
        TargetMatrix.M41 = p.X;
        TargetMatrix.M42 = p.Y;
        TargetMatrix.M43 = p.Z;
        float dx = p.X - cam.M41, dy = p.Y - cam.M42, dz = p.Z - cam.M43;
        float len = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        if (2f < len)
        {
            float k = (len - 2f) / len;
            dz *= k;
            dx *= k;
            dy *= k;
        }
        cam.M41 += dx;
        cam.M42 += dy;
        cam.M43 += dz;
        marker.SetRendered(true);
        marker.GameMatrix = cam;
    }
}
