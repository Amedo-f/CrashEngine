using CrashEngine.Core;
using CrashEngine.Importer;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    // Amedo 2026-11-15
    private CrashEngine.Runtime.Physics.CollisionWorld? _levelCollision;
    private object? _levelCollisionRm2;

    // Amedo 2026-11-15
    private CrashEngine.Runtime.Physics.CollisionWorld? LevelCollision(ChunkSource src)
    {
        if (!ReferenceEquals(_levelCollisionRm2, src.Rm2))
        {
            _levelCollisionRm2 = src.Rm2;
            _levelCollision = CrashEngine.Runtime.Physics.CollisionWorld.FromLevel(src.Rm2, src.GlobalRm2);
        }
        return _levelCollision;
    }

    // Amedo 2026-11-15
    private bool TryRaycastLevel(Vector2 mousePos, int sw, int sh, Entity space, out Vector3 local)
    {
        local = default;
        if (Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>() is not { } src || LevelCollision(src) is not { } world) return false;
        if (!Matrix4x4.Invert(space.Transform.World, out var toLocal)) return false;
        var (orig, dir) = GetMouseRay(mousePos, sw, sh);
        var o = Vector3.Transform(orig, toLocal);
        var d = Vector3.Normalize(Vector3.TransformNormal(dir, toLocal));
        if (!world.SegmentCast(o, o + d * LevelRayLength, 0xFFFFFFFFu, out var hit)) return false;
        local = hit.Point;
        return true;
    }
    private const float LevelRayLength = 2000f;

    private static bool TryGetWorldAABB(Entity root, out Vector3 min, out Vector3 max)
    {
        var lo  = new Vector3(float.MaxValue);
        var hi  = new Vector3(float.MinValue);
        bool any = false;

        void Collect(Entity e)
        {
            var mr = e.Get<CrashEngine.Importer.MeshRenderer>();
            if (mr?.Mesh?.RaycastPositions is { } pts)
            {
                var world = e.Transform.World;
                foreach (var p in pts)
                {
                    var wp = Vector3.Transform(p, world);
                    lo  = Vector3.Min(lo, wp);
                    hi  = Vector3.Max(hi, wp);
                    any = true;
                }
            }
            foreach (var c in e.Children) Collect(c);
        }
        Collect(root);
        min = lo; max = hi;
        return any;
    }

    private Entity? RaycastScene(Vector2 mousePos, int sw, int sh)
    {
        var (orig, dir) = GetMouseRay(mousePos, sw, sh);
        Entity? best  = null;
        float   bestT = float.MaxValue;
        Entity? bestCollision  = null;
        float   bestCollisionT = float.MaxValue;

        foreach (var c in _cubes)
        {
            var p  = c.Ent.Transform.Position;
            var mn = p + new Vector3(-1f, 0f, -1f);
            var mx = p + new Vector3( 1f, 2f,  1f);
            if (RayAABB(orig, dir, mn, mx, out float t) && t < bestT)
            { bestT = t; best = c.Ent; }
        }

        void Visit(Entity e)
        {
            if (!e.Active) return;
            if (e.Has<ArrayPreviewMarker>()) return;
            if ((e.Has<InstanceData>() || e.Has<CrashEngine.Importer.SceneryTile>() || e.Has<CrashEngine.Importer.CollisionMesh>()
                 || e.Has<CrashEngine.Importer.LinkedSceneryLink>()
                 || e.Get<CrashEngine.Importer.MeshRenderer>() is not null
                 || e.Children.Any(c => c.Get<CrashEngine.Importer.MeshRenderer>() is not null)) &&
                TryGetWorldBounds(e, out var center, out var radius) &&
                RaySphere(orig, dir, center, radius, out _))
            {
                bool isCollision = e.Has<CrashEngine.Importer.CollisionMesh>();
                void CheckMesh(Entity m)
                {
                    var mr = m.Get<CrashEngine.Importer.MeshRenderer>();
                    if (mr is not null && RaycastMeshRenderer(mr, orig, dir, out float wt))
                    {
                        if (isCollision) { if (wt < bestCollisionT) { bestCollisionT = wt; bestCollision = e; } }
                        else             { if (wt < bestT)          { bestT          = wt; best          = e; } }
                    }
                    foreach (var mc in m.Children) CheckMesh(mc);
                }
                CheckMesh(e);
            }
            foreach (var child in e.Children) Visit(child);
        }
        foreach (var root in Roots) Visit(root);

        void VisitMarkerRoot(Entity? root)
        {
            if (root is null || !root.Active) return;
            foreach (var m in root.Children)
            {
                if (!m.Active) continue;
                if (!(m.Has<CrashEngine.Importer.TriggerMarker>() || m.Has<CrashEngine.Importer.CameraMarker>() ||
                      m.Has<CrashEngine.Importer.PositionMarker>() || m.Has<CrashEngine.Importer.AiPositionMarker>() ||
                      m.Has<CrashEngine.Importer.ParticleEmitterMarker>() || m.Has<CrashEngine.Importer.SceneryLightMarker>())) // Amedo 2026-09-19
                    continue;
                var p = m.Transform.Position;
                var s = m.Transform.Scale;
                Vector3 mn, mx;
                if (m.Has<CrashEngine.Importer.SceneryLightMarker>())
                {
                    // Amedo 2026-09-19
                    const float R = 1.7f;
                    mn = p + new Vector3(-R, -R, -R);
                    mx = p + new Vector3( R,  R,  R);
                }
                else if (m.Has<CrashEngine.Importer.AiPositionMarker>() && IsIconMarker(m))
                {
                    // Amedo
                    if (RayHitsAiIcon(m.Children[0], orig, dir, out float ti) && ti < bestT) { bestT = ti; best = m; }
                    continue;
                }
                else if (m.Has<CrashEngine.Importer.PositionMarker>() && _positionPin is { } pin && IsIconMarker(m))
                {
                    // Amedo 2026-11-15
                    if (RayHitsMarkerModel(m.Children[0], pin, orig, dir, out float tp) && tp < bestT) { bestT = tp; best = m; }
                    continue;
                }
                else
                {
                    mn = p + new Vector3(-s.X, 0f, -s.Z);
                    mx = p + new Vector3( s.X, 2f * s.Y, s.Z);
                }
                if (RayAABB(orig, dir, mn, mx, out float t) && t < bestT) { bestT = t; best = m; }
            }
        }
        VisitMarkerRoot(_triggersRoot);
        VisitMarkerRoot(_camerasRoot);
        VisitMarkerRoot(_positionsRoot);
        VisitMarkerRoot(_aiPosRoot);
        VisitMarkerRoot(_particleEmittersRoot);
        VisitMarkerRoot(_lightsRoot); // Amedo 2026-09-19

        return bestCollision ?? best;
    }

    private static bool RaycastMeshRenderer(CrashEngine.Importer.MeshRenderer mr, Vector3 worldOrig, Vector3 worldDir, out float worldT)
    {
        worldT = float.MaxValue;
        if (mr.Mesh is null || mr.Entity is null) return false;
        var world = mr.Entity.Transform.World;
        if (!Matrix4x4.Invert(world, out var inv)) return false;

        var localOrig = Vector3.Transform(worldOrig, inv);
        var localDir  = Vector3.TransformNormal(worldDir, inv);
        if (!mr.Mesh.RaycastTriangles(localOrig, localDir, out float localT)) return false;

        var worldHit = Vector3.Transform(localOrig + localDir * localT, world);
        worldT = Vector3.Distance(worldOrig, worldHit);
        return true;
    }

    private static bool TryGetWorldBounds(Entity instanceRoot, out Vector3 center, out float radius)
    {
        var centers = new List<Vector3>();
        var radii   = new List<float>();

        void Collect(Entity e)
        {
            var mr = e.Get<CrashEngine.Importer.MeshRenderer>();
            if (mr?.Material?.LocalCenter is { } lc)
            {
                var world = e.Transform.World;
                centers.Add(Vector3.Transform(lc, world));
                var scale = Vector3.TransformNormal(Vector3.One, world).Length() / MathF.Sqrt(3f);
                radii.Add(mr.Material.BoundingRadius * MathF.Max(scale, 0.01f));
            }
            foreach (var child in e.Children) Collect(child);
        }
        Collect(instanceRoot);

        if (centers.Count == 0)
        {
            center = instanceRoot.Transform.World.Translation;
            radius = 0f;
            return false;
        }

        center = Vector3.Zero;
        foreach (var c in centers) center += c;
        center /= centers.Count;

        radius = 0f;
        for (int i = 0; i < centers.Count; i++)
            radius = MathF.Max(radius, Vector3.Distance(center, centers[i]) + radii[i]);
        radius = MathF.Max(radius, 0.3f);
        return true;
    }

    private static bool RaySphere(Vector3 orig, Vector3 dir, Vector3 center, float radius, out float t)
    {
        t = 0f;
        var oc   = orig - center;
        float b  = Vector3.Dot(oc, dir);
        float c  = Vector3.Dot(oc, oc) - radius * radius;
        float disc = b * b - c;
        if (disc < 0f) return false;
        float sq = MathF.Sqrt(disc);
        float t0 = -b - sq, t1 = -b + sq;
        t = t0 > 0f ? t0 : t1;
        return t > 0f;
    }

    private (Vector3 orig, Vector3 dir) GetMouseRay(Vector2 mp, int sw, int sh)
    {
        if (_camera is null) return (Vector3.Zero, Vector3.UnitZ);
        mp -= ViewOrigin; // Amedo
        float nx = (2f * mp.X / sw) - 1f;
        float ny = 1f - (2f * mp.Y / sh);
        Matrix4x4.Invert(_camera.Projection, out var invP);
        Matrix4x4.Invert(_camera.View,       out var invV);
        var n4 = new Vector4(nx, ny, -1f, 1f);
        var f4 = new Vector4(nx, ny,  1f, 1f);
        var nv = Vector4.Transform(n4, invP); nv /= nv.W;
        var fv = Vector4.Transform(f4, invP); fv /= fv.W;
        var nw = Vector4.Transform(nv, invV);
        var fw = Vector4.Transform(fv, invV);
        var orig = new Vector3(nw.X, nw.Y, nw.Z);
        return (orig, Vector3.Normalize(new Vector3(fw.X, fw.Y, fw.Z) - orig));
    }

    private static bool RayAABB(Vector3 o, Vector3 d,
                                 Vector3 mn, Vector3 mx, out float t)
    {
        t = 0f;
        float tmin = float.MinValue, tmax = float.MaxValue;
        if (!Slab(o.X, d.X, mn.X, mx.X, ref tmin, ref tmax)) return false;
        if (!Slab(o.Y, d.Y, mn.Y, mx.Y, ref tmin, ref tmax)) return false;
        if (!Slab(o.Z, d.Z, mn.Z, mx.Z, ref tmin, ref tmax)) return false;
        t = tmin > 0f ? tmin : tmax;
        return t > 0f;
    }

    private static bool Slab(float o, float d, float mn, float mx,
                              ref float tmin, ref float tmax)
    {
        if (MathF.Abs(d) > 1e-6f)
        {
            float t1 = (mn - o) / d, t2 = (mx - o) / d;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tmin = MathF.Max(tmin, t1);
            tmax = MathF.Min(tmax, t2);
            return tmin <= tmax;
        }
        return o >= mn && o <= mx;
    }
}
