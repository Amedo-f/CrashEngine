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
    private void SpawnCollisionBoxesMatchingScenery()
    {
        bool UnderLinkedScenery(Entity e)
        {
            for (var p = e; p is not null; p = p.Parent)
                if (p.Name == "LinkedScenery") return true;
            return false;
        }

        var tiles = new List<Entity>();
        void CollectSelectedTiles(Entity e)
        {
            if (UnderLinkedScenery(e)) return;
            if (IsStaticGeometryEntity(e)) tiles.Add(e);
            foreach (var c in e.Children) CollectSelectedTiles(c);
        }
        foreach (var sel in _selectedSet) CollectSelectedTiles(sel);
        tiles = tiles.Distinct().ToList();
        if (tiles.Count == 0) { _browser.Log("Collision Box (match bounds): no Scenery tile or Object instance selected."); return; }

        int made = 0;
        Entity? lastEntity = null;
        foreach (var tile in tiles)
        {
            if (!TryGetWorldAABB(tile, out var min, out var max)) continue;
            var size = max - min;
            if (size.X <= 0f || size.Y <= 0f || size.Z <= 0f) continue;

            var center = (min + max) * 0.5f;

            _cubeCount++;
            Entity entity = new Entity($"CollisionBox_{_cubeCount}");
            var marker = new CollisionBoxMarker();
            DirectCubeRenderer directCubeRenderer = entity.Add(new DirectCubeRenderer
            {
                Mesh = BuildCollisionBoxMesh(Engine.Instance.GL, marker.Corners),
                Color = CollisionDebugColor,
                OwnsMesh = true
            });
            directCubeRenderer.Mat.AlphaBlend = true;

            tile.AddChild(entity);

            var desiredWorld = Matrix4x4.CreateScale(size.X * 0.5f, size.Y * 0.5f, size.Z * 0.5f)
                              * Matrix4x4.CreateTranslation(center.X, min.Y, center.Z);
            var parentWorld = tile.Transform.World;
            if (Matrix4x4.Invert(parentWorld, out var parentInv) &&
                Matrix4x4.Decompose(desiredWorld * parentInv, out var scl, out var rot, out var pos))
            {
                entity.Transform.Scale    = scl;
                entity.Transform.Rotation = rot;
                entity.Transform.Position = pos;
            }
            else
            {
                entity.Transform.LocalMatrix = desiredWorld * (Matrix4x4.Invert(parentWorld, out var pinv2) ? pinv2 : Matrix4x4.Identity);
            }

            _cubes.Add(new CubeEntry(entity, $"Collision Box {_cubeCount} (matches {tile.Name})", directCubeRenderer));
            entity.Add(marker);
            made++;
            lastEntity = entity;
        }

        if (made > 0)
        {
            _browser.Log($"Collision Box (match bounds): created {made} box(es), nested under their object(s). " +
                         "Move/resize freely; Save Chunk to write it into the real collision data.");
            if (tiles.Count == 1 && lastEntity is not null) SelectClicked(lastEntity, additive: false);
        }
        else
        {
            _browser.Log("Collision Box (match bounds): selected object(s) had no measurable mesh bounds.");
        }
    }

    private void GenerateMeshCollision()
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var rm2 = chunkRoot?.Get<ChunkSource>()?.Rm2;
        if (rm2 is null) { _browser.Log("Generate Mesh Collision: no loaded chunk."); return; }

        var coll = rm2.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (coll is null) { _browser.Log("Generate Mesh Collision: this level has no collision data."); return; }

        bool UnderLinkedScenery(Entity e)
        {
            for (var p = e; p is not null; p = p.Parent)
                if (p.Name == "LinkedScenery") return true;
            return false;
        }

        var tiles = new List<Entity>();
        void CollectSelectedTiles(Entity e)
        {
            if (UnderLinkedScenery(e)) return;
            if (e.Get<CrashEngine.Importer.MeshRenderer>() is not null) tiles.Add(e);
            foreach (var c in e.Children) CollectSelectedTiles(c);
        }
        foreach (var sel in _selectedSet) CollectSelectedTiles(sel);
        tiles = tiles.Distinct().ToList();

        if (tiles.Count == 0)
        {
            _browser.Log("Generate Mesh Collision: select one or more Scenery tiles or Object " +
                          "instances first — this only ever looks at (and touches) what's " +
                          "selected, never anything else in the level.");
            return;
        }

        static Entity? OwningSceneryTile(Entity e)
        {
            for (var p = e; p is not null; p = p.Parent)
                if (p.Has<CrashEngine.Importer.SceneryTile>()) return p;
            return null;
        }
        var tileClaimedOldCollision = new HashSet<Entity>();

        if (_collisionTileMap is null || _collisionTileMap.Count != coll.Triangles.Count)
            BuildCollisionTileMap(chunkRoot!);

        var surfTypes = MeshDecoder.BuildSurfaceTypeLookup(rm2, chunkRoot?.Get<ChunkSource>()?.GlobalRm2);
        int defaultSurfaceIndex = surfTypes?.IndexOf(SurfaceType.SURF_DEFAULT) ?? -1;
        if (defaultSurfaceIndex < 0) defaultSurfaceIndex = 0;

        const int MaxPackedIndex = 0x3FFFF;
        const float MergeQuantum = 0.02f;

        int madeCount = 0, emptyCount = 0, skippedCount = 0;
        var newSelection = new List<Entity>();

        foreach (var tile in tiles)
        {
            if (!Matrix4x4.Invert(tile.Transform.World, out var invTileWorld))
            {
                _browser.Log($"Generate Mesh Collision: \"{tile.Name}\" has a non-invertible transform — skipped.");
                skippedCount++;
                continue;
            }

            var owningTile = OwningSceneryTile(tile);
            var votes = new Dictionary<int, int>();
            var ownedTriIndices = new List<int>();
            if (_collisionTileMap is not null && owningTile is not null && tileClaimedOldCollision.Add(owningTile))
            {
                for (int i = 0; i < _collisionTileMap.Count && i < coll.Triangles.Count; i++)
                {
                    if (_collisionTileMap[i] != owningTile) continue;
                    var s = coll.Triangles[i].SurfaceIndex;
                    votes[s] = votes.TryGetValue(s, out var c) ? c + 1 : 1;
                    ownedTriIndices.Add(i);
                }
            }
            int surfaceIndex = _genCollSurfaceOverride >= 0
                ? _genCollSurfaceOverride
                : (votes.Count > 0 ? votes.OrderByDescending(kv => kv.Value).First().Key : defaultSurfaceIndex);

            var localVerts  = new List<Vector3>();
            var localTris   = new List<(int, int, int)>();
            var vertexByPos = new Dictionary<(long, long, long), int>();
            bool overflowed = false;

            int GetOrAddVertex(Vector3 p)
            {
                var key = ((long)MathF.Round(p.X / MergeQuantum), (long)MathF.Round(p.Y / MergeQuantum), (long)MathF.Round(p.Z / MergeQuantum));
                if (vertexByPos.TryGetValue(key, out var existing)) return existing;
                int idx = localVerts.Count;
                localVerts.Add(p);
                vertexByPos[key] = idx;
                return idx;
            }

            void CollectFromEntity(Entity e)
            {
                var mr = e.Get<CrashEngine.Importer.MeshRenderer>();
                if (mr?.Mesh is { RaycastPositions: { } pos, RaycastIndices: { } idx } && idx.Length >= 3)
                {
                    var localToTile = e.Transform.World * invTileWorld;
                    for (int t = 0; t + 2 < idx.Length; t += 3)
                    {
                        if (overflowed) break;
                        if (localVerts.Count >= MaxPackedIndex) { overflowed = true; break; }
                        var a = Vector3.Transform(pos[(int)idx[t]],     localToTile);
                        var b = Vector3.Transform(pos[(int)idx[t + 1]], localToTile);
                        var c = Vector3.Transform(pos[(int)idx[t + 2]], localToTile);
                        int ia = GetOrAddVertex(a), ib = GetOrAddVertex(b), ic = GetOrAddVertex(c);
                        if (ia == ib || ib == ic || ia == ic) continue;
                        localTris.Add((ia, ib, ic));
                    }
                }
            }
            CollectFromEntity(tile);

            if (overflowed)
            {
                _browser.Log($"Generate Mesh Collision: \"{tile.Name}\" aborted at {localVerts.Count:N0} unique " +
                              "vertices — needs more than the PS2 format's 18-bit collision index field can " +
                              "hold (262,143 max), even after merging duplicate vertex positions. Skipped.");
                skippedCount++;
                continue;
            }
            if (localTris.Count == 0) { emptyCount++; continue; }

            var markerEntity = new Entity($"{tile.Name}_collision");
            var marker = new GeneratedCollisionMarker { SurfaceIndex = surfaceIndex, DegenerateOnFirstSync = ownedTriIndices };
            marker.LocalVertices.AddRange(localVerts);
            marker.LocalTriangles.AddRange(localTris);

            var directCubeRenderer = markerEntity.Add(new DirectCubeRenderer
            {
                Mesh = BuildGeneratedCollisionMesh(Engine.Instance.GL, localVerts, localTris),
                Color = CollisionDebugColor,
                OwnsMesh = true,
            });
            directCubeRenderer.Mat.AlphaBlend = true;

            tile.AddChild(markerEntity);
            markerEntity.Add(marker);

            _cubeCount++;
            _cubes.Add(new CubeEntry(markerEntity, $"Generated Collision {_cubeCount} ({tile.Name})", directCubeRenderer));
            newSelection.Add(markerEntity);
            madeCount++;
        }

        if (madeCount == 0)
        {
            _browser.Log(emptyCount > 0
                ? $"Generate Mesh Collision: no mesh geometry found on the {tiles.Count} selected tile(s) — nothing generated."
                : "Generate Mesh Collision: nothing generated.");
            return;
        }

        _browser.Log($"Generate Mesh Collision: created {madeCount} live collision shape(s), nested under " +
                      "their object(s) — move/reposition freely, Save Chunk bakes them into the real " +
                      "collision data (and degenerates whatever old collision they replace at that point, " +
                      "not now)." +
                      (emptyCount > 0 ? $" {emptyCount} selected tile(s) had no mesh geometry, skipped." : "") +
                      (skippedCount > 0 ? $" {skippedCount} skipped (see log above)." : ""));

        if (newSelection.Count > 0)
        {
            _selectedSet.Clear();
            foreach (var e in newSelection) _selectedSet.Add(e);
            _selected = newSelection[0];
            _revealSelectionInTree = true;
        }
    }

    private static GpuMesh BuildGeneratedCollisionMesh(GL gl, List<Vector3> localVerts, List<(int A, int B, int C)> localTris)
    {
        var verts = new GpuMesh.Vertex[localTris.Count * 3];
        int vi = 0;
        foreach (var (a, b, c) in localTris)
        {
            var pa = localVerts[a]; var pb = localVerts[b]; var pc = localVerts[c];
            var n = Vector3.Cross(pb - pa, pc - pa);
            n = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.UnitY;
            verts[vi++] = new GpuMesh.Vertex { Position = pa, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
            verts[vi++] = new GpuMesh.Vertex { Position = pb, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
            verts[vi++] = new GpuMesh.Vertex { Position = pc, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
        }
        return new GpuMesh(gl, verts, PrimitiveType.Triangles);
    }
}
