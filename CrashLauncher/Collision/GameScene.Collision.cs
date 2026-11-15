using CrashEngine.Core;
using CrashEngine.Importer;
using System.Numerics;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private static bool IsStaticGeometryEntity(Entity e) =>
        e.Has<CrashEngine.Importer.SceneryTile>() || e.Has<CrashEngine.Importer.InstanceData>() ||
        e.Get<CrashEngine.Importer.MeshRenderer>() is not null ||
        e.Children.Any(c => c.Get<CrashEngine.Importer.MeshRenderer>() is not null);

    private void SetCollisionVisible(bool visible)
    {
        foreach (var root in Roots)
            foreach (var e in AllEntities(root))
                if (e.Has<CollisionMesh>()) e.Active = visible;
    }

    private void BuildCollisionTileMap(Entity chunkRoot) => ApplyCollisionTileMatch(MatchCollisionTiles(chunkRoot));

    // Amedo 2026-11-12: the matching reads only the level's own entities, so it can run off the main thread while a level loads
    private sealed record CollisionTileMatch(List<Entity?> Map, int Matched);

    private void ApplyCollisionTileMatch(CollisionTileMatch? match)
    {
        _collisionTileMap = null;
        _collisionTileBaselinePos.Clear();
        if (match is null) return;
        var map = match.Map;
        _collisionTileMap = map;
        foreach (var owner in map.Where(o => o is not null).Distinct())
            _collisionTileBaselinePos[owner!] = owner!.Transform.World;

        _browser.Log($"Collision baseline: {match.Matched}/{map.Count} triangles matched to a scenery tile " +
                      "(rest are open ground/unmatched — Regenerate Collision never touches those).");
    }

    private static CollisionTileMatch? MatchCollisionTiles(Entity chunkRoot)
    {
        var rm2  = chunkRoot.Get<ChunkSource>()?.Rm2;
        var coll = rm2?.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (coll is null || coll.Triangles.Count == 0 || coll.Vectors.Count == 0) return null;

        var collRoot  = AllEntities(chunkRoot).FirstOrDefault(e => e.Has<CollisionMesh>());
        var collChild = collRoot?.Children.FirstOrDefault();
        if (collChild is null) return null;
        var world = collChild.Transform.World;

        var tiles = new List<(Entity Ent, Vector3 Min, Vector3 Max, Vector3[] Verts)>();
        void CollectTiles(Entity e)
        {
            if (e.Has<CrashEngine.Importer.SceneryTile>() && TryGetWorldAABB(e, out var mn, out var mx))
            {
                var pts = new List<Vector3>();
                void CollectVerts(Entity ee)
                {
                    var mr = ee.Get<CrashEngine.Importer.MeshRenderer>();
                    if (mr?.Mesh?.RaycastPositions is { } raw)
                    {
                        var w = ee.Transform.World;
                        const int maxSample = 800;
                        int step = Math.Max(1, raw.Length / maxSample);
                        for (int i = 0; i < raw.Length; i += step) pts.Add(Vector3.Transform(raw[i], w));
                    }
                    foreach (var c in ee.Children) CollectVerts(c);
                }
                CollectVerts(e);
                tiles.Add((e, mn, mx, pts.ToArray()));
            }
            foreach (var c in e.Children) CollectTiles(c);
        }
        CollectTiles(chunkRoot);
        if (tiles.Count == 0) return null;

        const float matchTolerance   = 20f;
        const float matchToleranceSq = matchTolerance * matchTolerance;
        var map = new List<Entity?>(coll.Triangles.Count);
        int matched = 0;
        foreach (var tri in coll.Triangles)
        {
            Entity? owner = null;
            if (tri.Vector1Index >= 0 && tri.Vector2Index >= 0 && tri.Vector3Index >= 0 &&
                tri.Vector1Index < coll.Vectors.Count && tri.Vector2Index < coll.Vectors.Count && tri.Vector3Index < coll.Vectors.Count)
            {
                var v1 = coll.Vectors[tri.Vector1Index];
                var v2 = coll.Vectors[tri.Vector2Index];
                var v3 = coll.Vectors[tri.Vector3Index];
                var a = Vector3.Transform(new Vector3(v1.X, v1.Y, v1.Z), world);
                var b = Vector3.Transform(new Vector3(v2.X, v2.Y, v2.Z), world);
                var c = Vector3.Transform(new Vector3(v3.X, v3.Y, v3.Z), world);
                var centroid = (a + b + c) / 3f;

                float bestDist = float.MaxValue;
                foreach (var (ent, mn, mx, verts) in tiles)
                {
                    var padMn = mn - new Vector3(matchTolerance);
                    var padMx = mx + new Vector3(matchTolerance);
                    var clamped = Vector3.Clamp(centroid, padMn, padMx);
                    if (Vector3.DistanceSquared(centroid, clamped) > 0f) continue;
                    if (verts.Length == 0) continue;

                    float localBest = float.MaxValue;
                    foreach (var v in verts)
                    {
                        float d = Vector3.DistanceSquared(centroid, v);
                        if (d < localBest) localBest = d;
                        if (localBest < 0.0001f) break;
                    }
                    if (localBest < bestDist) { bestDist = localBest; owner = ent; }
                }
                if (bestDist >= matchToleranceSq) owner = null;
            }
            if (owner is not null) matched++;
            map.Add(owner);
        }
        return new CollisionTileMatch(map, matched);
    }

    private readonly Dictionary<Entity, Matrix4x4> _collisionTileBaselinePos = new();


    private void ClearCollisionData()
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var rm2 = chunkRoot?.Get<ChunkSource>()?.Rm2;
        if (rm2 is null) { _browser.Log("Clear Collision: no loaded chunk."); return; }

        var coll = rm2.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (coll is null) { _browser.Log("Clear Collision: this level has no collision data."); return; }

        int triCount = coll.Triangles.Count;
        var action = new ClearCollisionAction { Coll = coll };
        action.Redo();
        _undoStack.Push(action);

        foreach (var e in AllEntities(chunkRoot!).Where(e => e.Has<CollisionMesh>()).ToList())
            e.Parent?.RemoveChild(e);

        _browser.Log($"Cleared {triCount} collision triangles (Ctrl+Z to undo). Save Chunk + Build ISO, then test in an emulator.");
    }

    private static Vector3 ToRawCollisionSpace(Vector3 worldPos) => new(-worldPos.X, worldPos.Y, worldPos.Z);

    private void RedrawLevelCollision(Entity chunkRoot, PS2AnyCollisionData coll, PS2AnyTwinsanityRM2 rm2)
    {
        var collMr = AllEntities(chunkRoot).FirstOrDefault(e => e.Has<CollisionMesh>())?.Children.FirstOrDefault()?.Get<CrashEngine.Importer.MeshRenderer>();
        if (collMr is null) return;
        var surfTypes = MeshDecoder.BuildSurfaceTypeLookup(rm2, chunkRoot.Get<ChunkSource>()?.GlobalRm2);
        if (MeshDecoder.DecodeCollision(Engine.Instance.GL, coll, surfTypes) is { } rebuilt)
        {
            collMr.Mesh?.Dispose();
            collMr.Mesh     = rebuilt.Mesh;
            collMr.Material = rebuilt.Mat;
        }
    }

    private List<Entity?>? _collisionTileMap;

    private Vector3? _collisionRootBaselinePos;
    private Vector3? _collisionChildBaselinePos;

    private void SyncCollisionMeshTransform(Entity chunkRoot)
    {
        var rm2  = chunkRoot.Get<ChunkSource>()?.Rm2;
        var coll = rm2?.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (coll is null || coll.Vectors.Count == 0) return;

        var collRoot = AllEntities(chunkRoot).FirstOrDefault(e => e.Has<CollisionMesh>());
        if (collRoot is null) return;
        var collChild = collRoot.Children.FirstOrDefault();

        var rootPos = collRoot.Transform.Position;
        var childPos = collChild?.Transform.Position ?? Vector3.Zero;

        if (_collisionRootBaselinePos is null || (collChild is not null && _collisionChildBaselinePos is null))
        {
            _collisionRootBaselinePos  = rootPos;
            _collisionChildBaselinePos = collChild is not null ? childPos : null;
            _browser.Log($"Collision Transform baseline set (root={rootPos}, child={childPos}) — " +
                          "this first sync after a level load never moves anything by itself; drag " +
                          "the Collision entity to a NEW spot and Save Chunk again to actually apply a shift.");
            return;
        }

        var rootDelta  = rootPos - _collisionRootBaselinePos.Value;
        var childDelta = collChild is not null ? childPos - _collisionChildBaselinePos!.Value : Vector3.Zero;
        var delta      = rootDelta + childDelta;
        if (delta.LengthSquared() < 1e-8f) return;

        float rdx = -delta.X, rdy = delta.Y, rdz = delta.Z;
        foreach (ref var v in System.Runtime.InteropServices.CollectionsMarshal.AsSpan(coll.Vectors)) // Amedo
        {
            v.X += rdx; v.Y += rdy; v.Z += rdz;
        }

        collRoot.Transform.Position = Vector3.Zero;
        _collisionRootBaselinePos = Vector3.Zero;
        if (collChild is not null)
        {
            collChild.Transform.Position = Vector3.Zero;
            _collisionChildBaselinePos = Vector3.Zero;
        }

        RedrawLevelCollision(chunkRoot, coll, rm2!);

        _browser.Log($"Synced Collision Transform move: shifted ALL {coll.Vectors.Count} collision " +
                      $"vertice(s) by ({rdx:F2},{rdy:F2},{rdz:F2}) raw-space (whole-level move — " +
                      "collision has no per-piece id in this game's format, so this can't move just " +
                      "one part). Save Chunk + Build ISO, then test in an emulator.");
    }
}
