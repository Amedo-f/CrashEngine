using CrashEngine.Core;
using CrashEngine.Importer;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void SyncCollisionBoxes(Entity chunkRoot) =>
        SyncCollisionShapes<CollisionBoxMarker>(chunkRoot, m => (m.Corners, CollisionBoxFaceTris), "test box(es)");

    private void SyncCollisionCylinders(Entity chunkRoot) =>
        SyncCollisionShapes<CollisionCylinderMarker>(chunkRoot, m => BuildCylinderTopology(m.Radius, m.Height, m.Segments), "cylinder(s)");

    private void SyncCollisionPlanes(Entity chunkRoot) =>
        SyncCollisionShapes<CollisionPlaneMarker>(chunkRoot, m => BuildPlaneTopology(m.Width, m.Length), "plane(s)");

    private void SyncCollisionShapes<TMarker>(Entity chunkRoot, Func<TMarker, (Vector3[] Verts, (int A, int B, int C)[] Tris)> topology,
                                              string shapeLabel) where TMarker : CollisionShapeMarker
    {
        var rm2  = chunkRoot.Get<ChunkSource>()?.Rm2;
        var coll = rm2?.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (coll is null || rm2 is null) return;

        var markers = Roots.SelectMany(AllEntities).Where(e => e.Has<TMarker>()).ToList();
        if (markers.Count == 0) return;

        int? defaultSurfaceIndex = null;
        int newCount = 0, updatedCount = 0;
        foreach (var e in markers)
        {
            var marker = e.Get<TMarker>()!;
            var (localVerts, faceTris) = topology(marker);
            var corners = ToRawCollisionSpace(localVerts, e.Transform.World);

            if (marker.OwnedVecIndices.Count == 0)
            {
                if (marker.SurfaceIndex < 0) marker.SurfaceIndex = defaultSurfaceIndex ??= DefaultCollisionSurfaceIndex(chunkRoot, rm2);
                AppendCollisionShape(coll, marker, corners, faceTris);
                newCount++;
            }
            else
            {
                UpdateCollisionShape(coll, marker, corners);
                updatedCount++;
            }
        }

        FinishCollisionShapeSync(chunkRoot, coll, rm2, newCount, updatedCount, shapeLabel);
    }

    private static int DefaultCollisionSurfaceIndex(Entity chunkRoot, PS2AnyTwinsanityRM2 rm2)
    {
        int idx = MeshDecoder.BuildSurfaceTypeLookup(rm2, chunkRoot.Get<ChunkSource>()?.GlobalRm2)?.IndexOf(SurfaceType.SURF_DEFAULT) ?? -1;
        return idx < 0 ? 0 : idx;
    }

    private static Vector3[] ToRawCollisionSpace(IReadOnlyList<Vector3> localVerts, Matrix4x4 world)
    {
        var raw = new Vector3[localVerts.Count];
        for (int i = 0; i < raw.Length; i++)
            raw[i] = ToRawCollisionSpace(Vector3.Transform(localVerts[i], world));
        return raw;
    }

    private void AppendCollisionShape(PS2AnyCollisionData coll, CollisionShapeMarker marker, Vector3[] corners, (int A, int B, int C)[] faceTris)
    {
        foreach (var c in corners)
        {
            coll.Vectors.Add(new TwinVec4(c.X, c.Y, c.Z, 1f));
            marker.OwnedVecIndices.Add(coll.Vectors.Count - 1);
        }
        int triBase = coll.Triangles.Count;
        foreach (var (a, b, cc) in faceTris)
        {
            coll.Triangles.Add(new TwinCollisionTriangle
            {
                Vector1Index = marker.OwnedVecIndices[a], Vector2Index = marker.OwnedVecIndices[b],
                Vector3Index = marker.OwnedVecIndices[cc], SurfaceIndex = marker.SurfaceIndex,
            });
            marker.OwnedTriIndices.Add(coll.Triangles.Count - 1);
        }
        coll.Groups.Add(new TwinGroupInformation { Offset = (uint)triBase, Size = (uint)faceTris.Length });
        if (_collisionTileMap is not null)
            for (int k = 0; k < faceTris.Length; k++) _collisionTileMap.Add(null);
    }

    private static void UpdateCollisionShape(PS2AnyCollisionData coll, CollisionShapeMarker marker, Vector3[] corners)
    {
        for (int i = 0; i < corners.Length && i < marker.OwnedVecIndices.Count; i++)
        {
            var idx = marker.OwnedVecIndices[i];
            if (idx < 0 || idx >= coll.Vectors.Count) continue;
            var v = coll.Vectors[idx];
            v.X = corners[i].X; v.Y = corners[i].Y; v.Z = corners[i].Z;
            coll.Vectors[idx] = v; // Amedo
        }
        foreach (var triIdx in marker.OwnedTriIndices)
            if (triIdx >= 0 && triIdx < coll.Triangles.Count)
                coll.Triangles[triIdx].SurfaceIndex = marker.SurfaceIndex;
    }

    private void FinishCollisionShapeSync(Entity chunkRoot, PS2AnyCollisionData coll, PS2AnyTwinsanityRM2 rm2,
                                           int newCount, int updatedCount, string shapeLabel)
    {
        if (newCount == 0 && updatedCount == 0) return;
        RebuildCollisionTriggers(coll);
        _browser.Log($"Synced {newCount} new + {updatedCount} existing collision {shapeLabel}" +
                     $" — rebuilt Coll.Triggers ({coll.Triggers.Count} node(s)) so it stays reachable in-game.");
        RedrawLevelCollision(chunkRoot, coll, rm2);
    }

    private void SyncGeneratedMeshCollision(Entity chunkRoot)
    {
        var rm2  = chunkRoot.Get<ChunkSource>()?.Rm2;
        var coll = rm2?.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (coll is null || rm2 is null) return;

        var markers = Roots.SelectMany(AllEntities).Where(e => e.Has<GeneratedCollisionMarker>()).ToList();
        if (markers.Count == 0) return;

        const float floorNormalYThreshold = 0.6f;
        int newCount = 0, updatedCount = 0;

        foreach (var e in markers)
        {
            var marker = e.Get<GeneratedCollisionMarker>()!;
            var raw = ToRawCollisionSpace(marker.LocalVertices, e.Transform.World);

            if (marker.OwnedVecIndices.Count == 0)
            {
                foreach (var idx in marker.DegenerateOnFirstSync)
                {
                    if (idx < 0 || idx >= coll.Triangles.Count) continue;
                    var tri = coll.Triangles[idx];
                    coll.Triangles[idx] = new TwinCollisionTriangle
                    {
                        Vector1Index = tri.Vector1Index, Vector2Index = tri.Vector1Index,
                        Vector3Index = tri.Vector1Index, SurfaceIndex = tri.SurfaceIndex,
                    };
                    if (_collisionTileMap is not null && idx < _collisionTileMap.Count) _collisionTileMap[idx] = null;
                }
                marker.DegenerateOnFirstSync.Clear();

                foreach (var v in raw)
                {
                    coll.Vectors.Add(new TwinVec4(v.X, v.Y, v.Z, 1f));
                    marker.OwnedVecIndices.Add(coll.Vectors.Count - 1);
                }

                int triBase = coll.Triangles.Count;
                foreach (var (a, b, c) in marker.LocalTriangles)
                {
                    var pa = raw[a]; var pb = raw[b]; var pc = raw[c];
                    var n = Vector3.Cross(pb - pa, pc - pa);
                    if (n.LengthSquared() < 1e-8f) continue;
                    int ib = b, ic = c;
                    if (MathF.Abs(Vector3.Normalize(n).Y) >= floorNormalYThreshold && n.Y > 0f)
                        (ib, ic) = (ic, ib);

                    coll.Triangles.Add(new TwinCollisionTriangle
                    {
                        Vector1Index = marker.OwnedVecIndices[a], Vector2Index = marker.OwnedVecIndices[ib],
                        Vector3Index = marker.OwnedVecIndices[ic], SurfaceIndex = marker.SurfaceIndex,
                    });
                    marker.OwnedTriIndices.Add(coll.Triangles.Count - 1);
                }
                if (marker.OwnedTriIndices.Count > 0)
                    coll.Groups.Add(new TwinGroupInformation { Offset = (uint)triBase, Size = (uint)(coll.Triangles.Count - triBase) });
                if (_collisionTileMap is not null)
                    for (int k = 0; k < marker.OwnedTriIndices.Count; k++) _collisionTileMap.Add(null);
                newCount++;
            }
            else
            {
                UpdateCollisionShape(coll, marker, raw);
                updatedCount++;
            }
        }

        FinishCollisionShapeSync(chunkRoot, coll, rm2, newCount, updatedCount, "generated mesh shape(s)");
    }
}
