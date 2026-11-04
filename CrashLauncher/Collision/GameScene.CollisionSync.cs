using CrashEngine.Assets;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using ITwinTexture = Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void SyncCollisionBoxes(Entity chunkRoot)
    {
        var rm2  = chunkRoot.Get<ChunkSource>()?.Rm2;
        var coll = rm2?.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (coll is null) return;

        var markers = Roots.SelectMany(AllEntities).Where(e => e.Has<CollisionBoxMarker>()).ToList();
        if (markers.Count == 0) return;

        List<SurfaceType>? surfTypes = null;
        int ResolveDefaultSurfaceIndex()
        {
            surfTypes ??= MeshDecoder.BuildSurfaceTypeLookup(rm2!, chunkRoot.Get<ChunkSource>()?.GlobalRm2);
            int idx = surfTypes?.IndexOf(SurfaceType.SURF_DEFAULT) ?? -1;
            return idx < 0 ? 0 : idx;
        }

        var faceTris = CollisionBoxFaceTris;

        int newCount = 0, updatedCount = 0;
        foreach (var e in markers)
        {
            var marker = e.Get<CollisionBoxMarker>()!;
            var world  = e.Transform.World;
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++) corners[i] = ToRawCollisionSpace(Vector3.Transform(marker.Corners[i], world));

            if (marker.OwnedVecIndices.Count == 0)
            {
                if (marker.SurfaceIndex < 0) marker.SurfaceIndex = ResolveDefaultSurfaceIndex();
                int vecBase = coll.Vectors.Count;
                for (int i = 0; i < 8; i++)
                {
                    coll.Vectors.Add(new TwinVec4(corners[i].X, corners[i].Y, corners[i].Z, 1f));
                    marker.OwnedVecIndices.Add(vecBase + i);
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
                newCount++;
            }
            else
            {
                for (int i = 0; i < 8 && i < marker.OwnedVecIndices.Count; i++)
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
                updatedCount++;
            }
        }

        if (newCount > 0 || updatedCount > 0)
        {
            {
                var rebuiltTriggers = BuildTriggerTree(coll.Groups, coll.Triangles, coll.Vectors);
                coll.Triggers.Clear();
                coll.Triggers.AddRange(rebuiltTriggers);
            }

            _browser.Log($"Synced {newCount} new + {updatedCount} existing collision test box(es)" +
                         $" — rebuilt Coll.Triggers ({coll.Triggers.Count} node(s)) so it stays reachable in-game.");
            var collRoot  = AllEntities(chunkRoot).FirstOrDefault(e => e.Has<CollisionMesh>());
            var collChild = collRoot?.Children.FirstOrDefault();
            var collMr    = collChild?.Get<CrashEngine.Importer.MeshRenderer>();
            surfTypes ??= MeshDecoder.BuildSurfaceTypeLookup(rm2!, chunkRoot.Get<ChunkSource>()?.GlobalRm2);
            if (collMr is not null && MeshDecoder.DecodeCollision(Engine.Instance.GL, coll, surfTypes) is { } rebuilt)
            {
                collMr.Mesh?.Dispose();
                collMr.Mesh     = rebuilt.Mesh;
                collMr.Material = rebuilt.Mat;
            }
        }
    }

    private void FinishCollisionShapeSync(Entity chunkRoot, PS2AnyCollisionData coll, PS2AnyTwinsanityRM2 rm2,
                                           int newCount, int updatedCount, string shapeLabel)
    {
        if (newCount == 0 && updatedCount == 0) return;
        {
            var rebuiltTriggers = BuildTriggerTree(coll.Groups, coll.Triangles, coll.Vectors);
            coll.Triggers.Clear();
            coll.Triggers.AddRange(rebuiltTriggers);
        }
        _browser.Log($"Synced {newCount} new + {updatedCount} existing collision {shapeLabel}(s)" +
                     $" — rebuilt Coll.Triggers ({coll.Triggers.Count} node(s)) so it stays reachable in-game.");
        var collRoot  = AllEntities(chunkRoot).FirstOrDefault(e => e.Has<CollisionMesh>());
        var collChild = collRoot?.Children.FirstOrDefault();
        var collMr    = collChild?.Get<CrashEngine.Importer.MeshRenderer>();
        var surfTypes = MeshDecoder.BuildSurfaceTypeLookup(rm2, chunkRoot.Get<ChunkSource>()?.GlobalRm2);
        if (collMr is not null && MeshDecoder.DecodeCollision(Engine.Instance.GL, coll, surfTypes) is { } rebuilt)
        {
            collMr.Mesh?.Dispose();
            collMr.Mesh     = rebuilt.Mesh;
            collMr.Material = rebuilt.Mat;
        }
    }

    private void SyncCollisionCylinders(Entity chunkRoot)
    {
        var rm2  = chunkRoot.Get<ChunkSource>()?.Rm2;
        var coll = rm2?.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (coll is null || rm2 is null) return;

        var markers = Roots.SelectMany(AllEntities).Where(e => e.Has<CollisionCylinderMarker>()).ToList();
        if (markers.Count == 0) return;

        List<SurfaceType>? surfTypes = null;
        int ResolveDefaultSurfaceIndex()
        {
            surfTypes ??= MeshDecoder.BuildSurfaceTypeLookup(rm2, chunkRoot.Get<ChunkSource>()?.GlobalRm2);
            int idx = surfTypes?.IndexOf(SurfaceType.SURF_DEFAULT) ?? -1;
            return idx < 0 ? 0 : idx;
        }

        int newCount = 0, updatedCount = 0;
        foreach (var e in markers)
        {
            var marker = e.Get<CollisionCylinderMarker>()!;
            var world  = e.Transform.World;
            var (localVerts, faceTris) = BuildCylinderTopology(marker.Radius, marker.Height, marker.Segments);
            var corners = new Vector3[localVerts.Length];
            for (int i = 0; i < localVerts.Length; i++)
                corners[i] = ToRawCollisionSpace(Vector3.Transform(localVerts[i], world));

            if (marker.OwnedVecIndices.Count == 0)
            {
                if (marker.SurfaceIndex < 0) marker.SurfaceIndex = ResolveDefaultSurfaceIndex();
                int vecBase = coll.Vectors.Count;
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
                newCount++;
            }
            else
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
                updatedCount++;
            }
        }

        FinishCollisionShapeSync(chunkRoot, coll, rm2, newCount, updatedCount, "cylinder");
    }

    private void SyncCollisionPlanes(Entity chunkRoot)
    {
        var rm2  = chunkRoot.Get<ChunkSource>()?.Rm2;
        var coll = rm2?.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (coll is null || rm2 is null) return;

        var markers = Roots.SelectMany(AllEntities).Where(e => e.Has<CollisionPlaneMarker>()).ToList();
        if (markers.Count == 0) return;

        List<SurfaceType>? surfTypes = null;
        int ResolveDefaultSurfaceIndex()
        {
            surfTypes ??= MeshDecoder.BuildSurfaceTypeLookup(rm2, chunkRoot.Get<ChunkSource>()?.GlobalRm2);
            int idx = surfTypes?.IndexOf(SurfaceType.SURF_DEFAULT) ?? -1;
            return idx < 0 ? 0 : idx;
        }

        int newCount = 0, updatedCount = 0;
        foreach (var e in markers)
        {
            var marker = e.Get<CollisionPlaneMarker>()!;
            var world  = e.Transform.World;
            var (localVerts, faceTris) = BuildPlaneTopology(marker.Width, marker.Length);
            var corners = new Vector3[localVerts.Length];
            for (int i = 0; i < localVerts.Length; i++)
                corners[i] = ToRawCollisionSpace(Vector3.Transform(localVerts[i], world));

            if (marker.OwnedVecIndices.Count == 0)
            {
                if (marker.SurfaceIndex < 0) marker.SurfaceIndex = ResolveDefaultSurfaceIndex();
                int vecBase = coll.Vectors.Count;
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
                newCount++;
            }
            else
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
                updatedCount++;
            }
        }

        FinishCollisionShapeSync(chunkRoot, coll, rm2, newCount, updatedCount, "plane");
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
            var world  = e.Transform.World;

            var raw = new Vector3[marker.LocalVertices.Count];
            for (int i = 0; i < raw.Length; i++)
                raw[i] = ToRawCollisionSpace(Vector3.Transform(marker.LocalVertices[i], world));

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
                for (int i = 0; i < raw.Length && i < marker.OwnedVecIndices.Count; i++)
                {
                    var idx = marker.OwnedVecIndices[i];
                    if (idx < 0 || idx >= coll.Vectors.Count) continue;
                    var v = coll.Vectors[idx];
                    v.X = raw[i].X; v.Y = raw[i].Y; v.Z = raw[i].Z;
                    coll.Vectors[idx] = v; // Amedo
                }
                foreach (var triIdx in marker.OwnedTriIndices)
                    if (triIdx >= 0 && triIdx < coll.Triangles.Count)
                        coll.Triangles[triIdx].SurfaceIndex = marker.SurfaceIndex;
                updatedCount++;
            }
        }

        FinishCollisionShapeSync(chunkRoot, coll, rm2, newCount, updatedCount, "generated mesh shape");
    }
}
