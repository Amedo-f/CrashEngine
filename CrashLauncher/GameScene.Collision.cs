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
    private static bool IsStaticGeometryEntity(Entity e) =>
        e.Has<CrashEngine.Importer.SceneryTile>() || e.Has<CrashEngine.Importer.InstanceData>() ||
        e.Get<CrashEngine.Importer.MeshRenderer>() is not null ||
        e.Children.Any(c => c.Get<CrashEngine.Importer.MeshRenderer>() is not null);

    private int _genCollSurfaceOverride = -1;

    private void DrawCollisionSection(Entity e)
    {
        if (IsStaticGeometryEntity(e) || e.Has<CollisionShapeMarker>())
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Collision##coll", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (IsStaticGeometryEntity(e))
                {
                    var genRm2 = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
                    var genSurfTypes = genRm2?.Rm2 is { } genRm2Val
                        ? MeshDecoder.BuildSurfaceTypeLookup(genRm2Val, genRm2.GlobalRm2)
                        : null;

                    ImGui.TextUnformatted("Surface Type for Generate:");
                    ImGui.SetNextItemWidth(-1f);
                    string genLabel = _genCollSurfaceOverride < 0
                        ? "Auto (nearest existing collision)"
                        : (genSurfTypes is { } gst && _genCollSurfaceOverride < gst.Count
                            ? $"[{_genCollSurfaceOverride}] {gst[_genCollSurfaceOverride]}"
                            : $"[{_genCollSurfaceOverride}] (unknown)");
                    if (ImGui.BeginCombo("##gencollsurftype", genLabel))
                    {
                        if (ImGui.Selectable("Auto (nearest existing collision)##gencollsurfauto", _genCollSurfaceOverride < 0))
                            _genCollSurfaceOverride = -1;
                        if (genSurfTypes is not null)
                        {
                            for (int gsi = 0; gsi < genSurfTypes.Count; gsi++)
                            {
                                bool selected = _genCollSurfaceOverride == gsi;
                                if (ImGui.Selectable($"[{gsi}] {genSurfTypes[gsi]}##gencollsurf{gsi}", selected))
                                    _genCollSurfaceOverride = gsi;
                                if (selected) ImGui.SetItemDefaultFocus();
                            }
                        }
                        ImGui.EndCombo();
                    }
                    if (ImGui.IsItemHovered())
                        MaybeTooltip("Auto = same as before: copies whichever real surface type the\n" +
                                     "nearest pre-existing collision under this tile already uses.\n" +
                                     "Pick a specific one (sand/grass/rock/...) to force it instead.");

                    if (ImGui.Button("Generate Mesh Collision (exact, selected object(s))##gencoll", new Vector2(-1f, 0f)))
                        GenerateMeshCollision();
                    if (ImGui.IsItemHovered())
                        MaybeTooltip("Looks only at THIS object's own mesh (never anything else in the\n" +
                                          "level) and copies its real triangles 1:1 — true per-triangle\n" +
                                          "collision, not a box approximation. Near-horizontal triangles\n" +
                                          "get their winding corrected to the verified real floor\n" +
                                          "convention; steep/wall-like ones are left as the mesh's own\n" +
                                          "natural order (real beach.rm2 data shows no single winding rule\n" +
                                          "applies to those — experimental). Creates a live, editable shape\n" +
                                          "nested under the object (and any other currently-selected\n" +
                                          "Scenery tile or Object instance — one each) — move/reposition it\n" +
                                          "freely afterward, it follows the object. Only actually written\n" +
                                          "into the real collision data (replacing whatever old collision it\n" +
                                          "supersedes) on Save Chunk. Delete removes it; Multi-select in the\n" +
                                          "Hierarchy first to do several at once.");


                    if (ImGui.Button("+ Collision Box (match bounds)##boxmatchscenery", new Vector2(-1f, 0f)))
                        SpawnCollisionBoxesMatchingScenery();
                    if (ImGui.IsItemHovered())
                        MaybeTooltip("Drops a real, freely-editable + Collision Box sized/positioned to\n" +
                                          "exactly match this object's own bounding box (and any other\n" +
                                          "currently-selected Scenery tile or Object instance's — one box\n" +
                                          "each). Nested under the object itself in the Hierarchy tree (not\n" +
                                          "the generic \"Collision\" folder), so it's easy to find again and\n" +
                                          "moving it later carries its box along. Once created it's an\n" +
                                          "ordinary collision box — drag/resize it same as any + Collision\n" +
                                          "Box, Save Chunk to write it into the real collision data.");
                }

                if (e.Get<CollisionShapeMarker>() is { } shapeMarker)
                {
                    var scRm2 = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
                    var surfTypes = scRm2?.Rm2 is { } rm2forSurf
                        ? MeshDecoder.BuildSurfaceTypeLookup(rm2forSurf, scRm2.GlobalRm2)
                        : null;

                    ImGui.Spacing();
                    ImGui.TextUnformatted("Surface Type:");
                    ImGui.SetNextItemWidth(-1f);
                    int effIdx = shapeMarker.SurfaceIndex;
                    if (effIdx < 0 && surfTypes is not null)
                    { effIdx = surfTypes.IndexOf(SurfaceType.SURF_DEFAULT); if (effIdx < 0) effIdx = 0; }
                    string currentLabel = surfTypes is { } stLbl && effIdx >= 0 && effIdx < stLbl.Count
                        ? $"[{effIdx}] {stLbl[effIdx]}" + (shapeMarker.SurfaceIndex < 0 ? " (default)" : "")
                        : $"[{shapeMarker.SurfaceIndex}] (unknown)";
                    if (ImGui.BeginCombo("##surfacetype", currentLabel))
                    {
                        if (surfTypes is not null)
                        {
                            for (int si = 0; si < surfTypes.Count; si++)
                            {
                                bool selected = effIdx == si;
                                if (ImGui.Selectable($"[{si}] {surfTypes[si]}##surf{si}", selected))
                                    shapeMarker.SurfaceIndex = si;
                                if (selected) ImGui.SetItemDefaultFocus();
                            }
                        }
                        else
                        {
                            ImGui.TextDisabled("No real surface table found for this level (neither\nits own rm2 nor Startup\\Default.rm2 has one).");
                        }
                        ImGui.EndCombo();
                    }
                    if (ImGui.IsItemHovered())
                        MaybeTooltip("Real per-level surface material (sand/grass/rock/lava/...) — the\n" +
                                     "SAME table the actual game's own floor triangles use, read straight\n" +
                                     "from Startup\\Default.rm2. Takes effect on Save Chunk, same as this\n" +
                                     "shape's own geometry.");
                }

                if (e.Get<CollisionBoxMarker>() is { } marker)
                {
                    ImGui.TextDisabled(marker.OwnedTriIndices.Count > 0
                        ? $"Collision test box — synced ({marker.OwnedTriIndices.Count} triangles). " +
                          "Move/resize freely; Save Chunk re-syncs to the new position."
                        : "Collision test box — not synced yet. Save Chunk to write it into the real collision data.");

                    ImGui.Spacing();
                    if (ImGui.TreeNodeEx("Shape (corners)##collcorners", ImGuiTreeNodeFlags.None))
                    {
                        if (ImGui.IsItemHovered())
                            MaybeTooltip("Drag any corner to sculpt this box into a slope, ramp, or\n" +
                                              "wedge instead of a plain box. Whatever shape you see here is\n" +
                                              "exactly what Save Chunk writes into the real collision data.");
                        bool changed = false;
                        string[] labels = { "-X -Z (bottom)", "+X -Z (bottom)", "-X -Z (top)", "+X -Z (top)",
                                             "-X +Z (bottom)", "+X +Z (bottom)", "-X +Z (top)", "+X +Z (top)" };
                        for (int i = 0; i < 8; i++)
                        {
                            var c = marker.Corners[i];
                            if (ImGui.DragFloat3($"{labels[i]}##corner{i}", ref c, 0.05f))
                            {
                                marker.Corners[i] = c;
                                changed = true;
                            }
                        }

                        ImGui.Spacing();
                        if (ImGui.Button("Reset to Box##collcornersreset"))
                        {
                            Array.Copy(CollisionBoxMarker.DefaultCorners(), marker.Corners, 8);
                            changed = true;
                        }
                        if (ImGui.IsItemHovered()) MaybeTooltip("Restores the plain axis-aligned box shape.");
                        ImGui.SameLine();
                        ImGui.TextDisabled("Ramp:");
                        ImGui.SameLine();
                        if (ImGui.Button("+X##rampposx")) { RampCorners(marker.Corners, +1f, 0); changed = true; }
                        ImGui.SameLine();
                        if (ImGui.Button("-X##rampnegx")) { RampCorners(marker.Corners, -1f, 0); changed = true; }
                        ImGui.SameLine();
                        if (ImGui.Button("+Z##rampposz")) { RampCorners(marker.Corners, +1f, 2); changed = true; }
                        ImGui.SameLine();
                        if (ImGui.Button("-Z##rampnegz")) { RampCorners(marker.Corners, -1f, 2); changed = true; }
                        if (ImGui.IsItemHovered())
                            MaybeTooltip("One-click slope: flattens the top corners on the LOW side down\n" +
                                              "to ground level, so the box becomes a ramp climbing toward the\n" +
                                              "labelled direction. Still fine-tunable by hand above afterwards.");

                        if (changed) RebuildCollisionBoxMesh(e, marker);
                        ImGui.TreePop();
                    }
                }

                if (e.Get<CollisionCylinderMarker>() is { } cyl)
                {
                    ImGui.TextDisabled(cyl.OwnedTriIndices.Count > 0
                        ? $"Collision test cylinder — synced ({cyl.OwnedTriIndices.Count} triangles). " +
                          "Radius/Height move freely; Save Chunk re-syncs to the new values."
                        : "Collision test cylinder — not synced yet. Save Chunk to write it into the real collision data.");

                    ImGui.Spacing();
                    bool changed = false;
                    if (ImGui.DragFloat("Radius##cylradius", ref cyl.Radius, 0.05f, 0.05f, 1000f)) changed = true;
                    if (ImGui.DragFloat("Height##cylheight", ref cyl.Height, 0.05f, 0.05f, 1000f)) changed = true;
                    ImGui.BeginDisabled(cyl.OwnedVecIndices.Count > 0);
                    int segments = cyl.Segments;
                    if (ImGui.DragInt("Segments##cylsegments", ref segments, 0.2f, 3, 64))
                    {
                        cyl.Segments = segments;
                        changed = true;
                    }
                    ImGui.EndDisabled();
                    if (cyl.OwnedVecIndices.Count > 0 && ImGui.IsItemHovered())
                        MaybeTooltip("Locked once synced — changing the side count changes the actual\n" +
                                          "vertex/triangle count, which this test shape doesn't support\n" +
                                          "reshaping in place. Delete and re-add a fresh one instead.");
                    if (changed) RebuildCollisionCylinderMesh(e, cyl);
                }

                if (e.Get<CollisionPlaneMarker>() is { } pln)
                {
                    ImGui.TextDisabled(pln.OwnedTriIndices.Count > 0
                        ? $"Collision test plane — synced ({pln.OwnedTriIndices.Count} triangles). " +
                          "Width/Length move freely; Save Chunk re-syncs to the new values."
                        : "Collision test plane — not synced yet. Save Chunk to write it into the real collision data.");

                    ImGui.Spacing();
                    bool changed = false;
                    if (ImGui.DragFloat("Width##plnwidth", ref pln.Width, 0.05f, 0.05f, 1000f)) changed = true;
                    if (ImGui.DragFloat("Length##plnlength", ref pln.Length, 0.05f, 0.05f, 1000f)) changed = true;
                    if (changed) RebuildCollisionPlaneMesh(e, pln);
                }
            }
        }
    }

    private void SetCollisionVisible(bool visible)
    {
        foreach (var root in Roots)
            foreach (var e in AllEntities(root))
                if (e.Has<CollisionMesh>()) e.Active = visible;
    }

    private void BuildCollisionTileMap(Entity chunkRoot)
    {
        _collisionTileMap = null;
        _collisionTileBaselinePos.Clear();

        var rm2  = chunkRoot.Get<ChunkSource>()?.Rm2;
        var coll = rm2?.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (coll is null || coll.Triangles.Count == 0 || coll.Vectors.Count == 0) return;

        var collRoot  = AllEntities(chunkRoot).FirstOrDefault(e => e.Has<CollisionMesh>());
        var collChild = collRoot?.Children.FirstOrDefault();
        if (collChild is null) return;
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
        if (tiles.Count == 0) return;

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
        _collisionTileMap = map;
        foreach (var owner in map.Where(o => o is not null).Distinct())
            _collisionTileBaselinePos[owner!] = owner!.Transform.World;

        _browser.Log($"Collision baseline: {matched}/{map.Count} triangles matched to a scenery tile " +
                      "(rest are open ground/unmatched — Regenerate Collision never touches those).");
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

    private static readonly (int A, int B, int C)[] CollisionBoxFaceTris =
    {
        (4,5,7), (4,7,6),
        (1,0,2), (1,2,3),
        (5,1,3), (5,3,7),
        (0,4,6), (0,6,2),
        (2,6,7), (2,7,3),
        (0,1,5), (0,5,4),
    };

    private static GpuMesh BuildCollisionBoxMesh(GL gl, Vector3[] corners)
    {
        var verts = new GpuMesh.Vertex[CollisionBoxFaceTris.Length * 3];
        int vi = 0;
        foreach (var (a, b, c) in CollisionBoxFaceTris)
        {
            var pa = corners[a]; var pb = corners[b]; var pc = corners[c];
            var n = Vector3.Cross(pb - pa, pc - pa);
            n = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.UnitY;
            verts[vi++] = new GpuMesh.Vertex { Position = pa, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
            verts[vi++] = new GpuMesh.Vertex { Position = pb, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
            verts[vi++] = new GpuMesh.Vertex { Position = pc, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
        }
        return new GpuMesh(gl, verts, PrimitiveType.Triangles);
    }

    private void RebuildCollisionBoxMesh(Entity e, CollisionBoxMarker marker)
    {
        var rdr = e.Get<DirectCubeRenderer>();
        if (rdr is null) return;
        var old = rdr.Mesh;
        rdr.Mesh = BuildCollisionBoxMesh(Engine.Instance.GL, marker.Corners);
        rdr.OwnsMesh = true;
        if (old is not null && old != _cubeMesh) old.Dispose();
    }

    private static void RampCorners(Vector3[] corners, float highSign, int axis)
    {
        for (int i = 0; i < 8; i++)
        {
            if ((i & 2) == 0) continue;
            float cornerSign = axis == 0 ? ((i & 1) == 0 ? -1f : 1f) : ((i & 4) == 0 ? -1f : 1f);
            if (cornerSign != highSign) corners[i].Y = 0f;
        }
    }

    private static (Vector3[] Verts, (int A, int B, int C)[] Tris) BuildCylinderTopology(float radius, float height, int segments)
    {
        segments = Math.Max(3, segments);
        var verts = new Vector3[segments * 2];
        for (int i = 0; i < segments; i++)
        {
            float ang = MathF.Tau * i / segments;
            float x = MathF.Cos(ang) * radius, z = MathF.Sin(ang) * radius;
            verts[i] = new Vector3(x, 0f, z);
            verts[segments + i] = new Vector3(x, height, z);
        }

        var tris = new List<(int, int, int)>();
        for (int i = 0; i < segments; i++)
        {
            int iN = (i + 1) % segments;
            int bA = iN, bB = i, tA = segments + i, tB = segments + iN;
            tris.Add((bA, bB, tA));
            tris.Add((bA, tA, tB));
        }
        for (int k = 1; k < segments - 1; k++)
            tris.Add((0, k, k + 1));
        for (int k = 1; k < segments - 1; k++)
            tris.Add((segments, segments + (segments - k), segments + (segments - k - 1)));

        return (verts, tris.ToArray());
    }

    private static (Vector3[] Verts, (int A, int B, int C)[] Tris) BuildPlaneTopology(float width, float length)
    {
        float hw = width * 0.5f, hl = length * 0.5f;
        var verts = new[]
        {
            new Vector3(-hw, 0f, -hl),
            new Vector3( hw, 0f, -hl),
            new Vector3( hw, 0f,  hl),
            new Vector3(-hw, 0f,  hl),
        };
        var tris = new (int, int, int)[] { (0, 1, 2), (0, 2, 3) };
        return (verts, tris);
    }

    private static GpuMesh BuildMeshFromTopology(GL gl, Vector3[] corners, (int A, int B, int C)[] triList)
    {
        var verts = new GpuMesh.Vertex[triList.Length * 3];
        int vi = 0;
        foreach (var (a, b, c) in triList)
        {
            var pa = corners[a]; var pb = corners[b]; var pc = corners[c];
            var n = Vector3.Cross(pb - pa, pc - pa);
            n = n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.UnitY;
            verts[vi++] = new GpuMesh.Vertex { Position = pa, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
            verts[vi++] = new GpuMesh.Vertex { Position = pb, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
            verts[vi++] = new GpuMesh.Vertex { Position = pc, Normal = n, UV = Vector2.Zero, Color = Vector4.One };
        }
        return new GpuMesh(gl, verts, PrimitiveType.Triangles);
    }

    private static GpuMesh BuildCollisionCylinderMesh(GL gl, float radius, float height, int segments)
    {
        var (verts, tris) = BuildCylinderTopology(radius, height, segments);
        return BuildMeshFromTopology(gl, verts, tris);
    }

    private static GpuMesh BuildCollisionPlaneMesh(GL gl, float width, float length)
    {
        var (verts, tris) = BuildPlaneTopology(width, length);
        return BuildMeshFromTopology(gl, verts, tris);
    }

    private void RebuildCollisionCylinderMesh(Entity e, CollisionCylinderMarker marker)
    {
        var rdr = e.Get<DirectCubeRenderer>();
        if (rdr is null) return;
        var old = rdr.Mesh;
        rdr.Mesh = BuildCollisionCylinderMesh(Engine.Instance.GL, marker.Radius, marker.Height, marker.Segments);
        rdr.OwnsMesh = true;
        old?.Dispose();
    }

    private void RebuildCollisionPlaneMesh(Entity e, CollisionPlaneMarker marker)
    {
        var rdr = e.Get<DirectCubeRenderer>();
        if (rdr is null) return;
        var old = rdr.Mesh;
        rdr.Mesh = BuildCollisionPlaneMesh(Engine.Instance.GL, marker.Width, marker.Length);
        rdr.OwnsMesh = true;
        old?.Dispose();
    }

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
                }
                foreach (var triIdx in marker.OwnedTriIndices)
                    if (triIdx >= 0 && triIdx < coll.Triangles.Count)
                        coll.Triangles[triIdx].SurfaceIndex = marker.SurfaceIndex;
                updatedCount++;
            }
        }

        FinishCollisionShapeSync(chunkRoot, coll, rm2, newCount, updatedCount, "generated mesh shape");
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
        foreach (var v in coll.Vectors)
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

        var collMr = collChild?.Get<CrashEngine.Importer.MeshRenderer>();
        if (collMr is not null)
        {
            var surfTypes = MeshDecoder.BuildSurfaceTypeLookup(rm2!, chunkRoot.Get<ChunkSource>()?.GlobalRm2);
            if (MeshDecoder.DecodeCollision(Engine.Instance.GL, coll, surfTypes) is { } rebuilt)
            {
                collMr.Mesh?.Dispose();
                collMr.Mesh     = rebuilt.Mesh;
                collMr.Material = rebuilt.Mat;
            }
        }

        _browser.Log($"Synced Collision Transform move: shifted ALL {coll.Vectors.Count} collision " +
                      $"vertice(s) by ({rdx:F2},{rdy:F2},{rdz:F2}) raw-space (whole-level move — " +
                      "collision has no per-piece id in this game's format, so this can't move just " +
                      "one part). Save Chunk + Build ISO, then test in an emulator.");
    }


    private int _maxPolysPerGroup = 22;

    private void ExportCollisionToObj()
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var rm2  = chunkRoot?.Get<ChunkSource>()?.Rm2;
        var coll = rm2?.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (chunkRoot is null || coll is null) { _browser.Log("Export Collision: no loaded chunk/collision."); return; }
        if (coll.Vectors.Count == 0 || coll.Triangles.Count == 0) { _browser.Log("Export Collision: no collision data to export."); return; }

        ShowSaveFileDialog("Export Collision to OBJ", "Wavefront OBJ\0*.obj\0All Files\0*.*\0\0", "collision.obj", path =>
        {
            if (path is null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# CrashEngine collision export — same X-mirror convention as Twinsanity Editor's CollisionImporter");
            sb.AppendLine($"# {coll.Vectors.Count} vertices, {coll.Triangles.Count} triangles");
            sb.AppendLine("# Edit freely in Blender (move/sculpt/delete/add) then use \"Import Collision from OBJ...\".");
            foreach (var v in coll.Vectors)
                sb.AppendLine($"v {(-v.X).ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                               $"{v.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)} " +
                               $"{v.Z.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            foreach (var t in coll.Triangles)
            {
                if (t.Vector1Index < 0 || t.Vector2Index < 0 || t.Vector3Index < 0 ||
                    t.Vector1Index >= coll.Vectors.Count || t.Vector2Index >= coll.Vectors.Count || t.Vector3Index >= coll.Vectors.Count)
                    continue;
                sb.AppendLine($"f {t.Vector1Index + 1} {t.Vector2Index + 1} {t.Vector3Index + 1}");
            }
            File.WriteAllText(path, sb.ToString());
            _browser.Log($"Exported collision to {path} ({coll.Vectors.Count} vertice(s), {coll.Triangles.Count} triangle(s)).");
        });
    }

    private void ImportCollisionFromObj()
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var rm2  = chunkRoot?.Get<ChunkSource>()?.Rm2;
        var coll = rm2?.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
        if (chunkRoot is null || coll is null) { _browser.Log("Import Collision: no loaded chunk/collision."); return; }

        ShowOpenFileDialog("Import Collision from OBJ", "Wavefront OBJ\0*.obj\0All Files\0*.*\0\0", path =>
        {
        if (path is null) return;

        var newVerts = new List<TwinVec4>();
        var rawTris  = new List<(int a, int b, int c)>();
        int skippedFaces = 0;

        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            if (parts[0] == "v" && parts.Length >= 4)
            {
                if (float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) &&
                    float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
                    newVerts.Add(new TwinVec4(-x, y, z, 1f));
            }
            else if (parts[0] == "f" && parts.Length >= 4)
            {
                int ParseRef(string token)
                {
                    var posStr = token.Split('/')[0];
                    if (!int.TryParse(posStr, out var n)) return -1;
                    return n > 0 ? n - 1 : newVerts.Count + n;
                }
                var faceIdx = new int[parts.Length - 1];
                bool ok = true;
                for (int i = 1; i < parts.Length; i++)
                {
                    faceIdx[i - 1] = ParseRef(parts[i]);
                    if (faceIdx[i - 1] < 0 || faceIdx[i - 1] >= newVerts.Count) ok = false;
                }
                if (!ok) { skippedFaces++; continue; }
                for (int i = 1; i + 1 < faceIdx.Length; i++)
                    rawTris.Add((faceIdx[0], faceIdx[i], faceIdx[i + 1]));
            }
        }

        if (newVerts.Count == 0 || rawTris.Count == 0)
        { _browser.Log($"Import Collision: no usable geometry found in {path}."); return; }

        var triGroups   = new List<List<(int a, int b, int c)>>();
        var vertToGroup = new Dictionary<int, int>();
        foreach (var tri in rawTris)
        {
            int grp;
            bool found = vertToGroup.TryGetValue(tri.a, out grp) || vertToGroup.TryGetValue(tri.b, out grp) || vertToGroup.TryGetValue(tri.c, out grp);
            if (found && triGroups[grp].Count >= _maxPolysPerGroup) found = false;
            if (!found)
            {
                grp = triGroups.Count;
                triGroups.Add(new List<(int, int, int)>());
            }
            triGroups[grp].Add(tri);
            vertToGroup[tri.a] = grp; vertToGroup[tri.b] = grp; vertToGroup[tri.c] = grp;
        }
        for (int i = 0; i < triGroups.Count; i++)
        {
            if (triGroups[i].Count == 0) continue;
            var verts = new HashSet<int>();
            foreach (var t in triGroups[i]) { verts.Add(t.a); verts.Add(t.b); verts.Add(t.c); }
            for (int j = i + 1; j < triGroups.Count; j++)
            {
                if (triGroups[j].Count == 0) continue;
                if (triGroups[i].Count + triGroups[j].Count > _maxPolysPerGroup) continue;
                bool shares = triGroups[j].Any(t => verts.Contains(t.a) || verts.Contains(t.b) || verts.Contains(t.c));
                if (shares) { triGroups[i].AddRange(triGroups[j]); triGroups[j].Clear(); }
            }
        }
        triGroups.RemoveAll(g => g.Count == 0);

        var newTris   = new List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle>();
        var newGroups = new List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation>();
        foreach (var g in triGroups)
        {
            uint off = (uint)newTris.Count;
            foreach (var (a, b, c) in g)
                newTris.Add(new Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle
                { Vector1Index = a, Vector2Index = b, Vector3Index = c, SurfaceIndex = 0 });
            newGroups.Add(new Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation
            { Offset = off, Size = (uint)g.Count });
        }

        var newTriggers = BuildTriggerTree(newGroups, newTris, newVerts);

        var collRoot = AllEntities(chunkRoot).FirstOrDefault(e => e.Has<CollisionMesh>());

        coll.Vectors.Clear();   coll.Vectors.AddRange(newVerts);
        coll.Triangles.Clear(); coll.Triangles.AddRange(newTris);
        coll.Groups.Clear();    coll.Groups.AddRange(newGroups);
        coll.Triggers.Clear();  coll.Triggers.AddRange(newTriggers);

        if (collRoot is not null)
        {
            var collChild = collRoot.Children.FirstOrDefault();
            var collMr = collChild?.Get<CrashEngine.Importer.MeshRenderer>();
            if (collMr is not null)
            {
                var surfTypes = MeshDecoder.BuildSurfaceTypeLookup(rm2!, chunkRoot!.Get<ChunkSource>()?.GlobalRm2);
                if (MeshDecoder.DecodeCollision(Engine.Instance.GL, coll, surfTypes) is { } rebuilt)
                {
                    collMr.Mesh?.Dispose();
                    collMr.Mesh     = rebuilt.Mesh;
                    collMr.Material = rebuilt.Mat;
                }
            }
        }

        _browser.Log($"Imported collision from {path}: {newVerts.Count} vertice(s), {newTris.Count} triangle(s), " +
                     $"{newGroups.Count} group(s), {newTriggers.Count} trigger-tree node(s) " +
                     $"({skippedFaces} malformed face(s) skipped, all SurfaceIndex=0/default). " +
                     "Save Chunk + Build ISO, then test.");
        });
    }

    private sealed class TriggerTreeNode
    {
        public bool IsLeaf;
        public TriggerTreeNode? Left;
        public TriggerTreeNode? Right;
        public int Id;
        public int LeafPtr;
    }

    internal static List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTrigger> BuildTriggerTree(
        List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation> groups,
        List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle> tris,
        List<TwinVec4> verts)
    {
        var result = new List<Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTrigger>();
        int n = groups.Count;
        if (n == 0) return result;

        var root = new TriggerTreeNode { IsLeaf = true };
        var leaves = new List<TriggerTreeNode> { root };
        int fullLevels = (int)Math.Truncate(Math.Log(n, 2));
        for (int lvl = 0; lvl < fullLevels; lvl++)
        {
            var next = new List<TriggerTreeNode>(leaves.Count * 2);
            foreach (var leaf in leaves)
            {
                leaf.IsLeaf = false;
                leaf.Left  = new TriggerTreeNode { IsLeaf = true };
                leaf.Right = new TriggerTreeNode { IsLeaf = true };
                next.Add(leaf.Left);
                next.Add(leaf.Right);
            }
            leaves = next;
        }
        int endings = n - (int)Math.Pow(2, fullLevels);
        for (int i = 0; i < endings; i++)
        {
            var leaf = leaves[i];
            leaf.IsLeaf = false;
            leaf.Left  = new TriggerTreeNode { IsLeaf = true };
            leaf.Right = new TriggerTreeNode { IsLeaf = true };
        }

        int leafPtr = 1;
        int AssignIds(TriggerTreeNode node, int id)
        {
            node.Id = id;
            if (node.IsLeaf) { node.LeafPtr = leafPtr++; return 1; }
            int leftSize = AssignIds(node.Left!, id + 1);
            int rightSize = AssignIds(node.Right!, id + 1 + leftSize);
            return 1 + leftSize + rightSize;
        }
        AssignIds(root, 0);

        var flat = new Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTrigger[2 * n - 1];
        void Flatten(TriggerTreeNode node)
        {
            var trig = new Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTrigger();
            if (node.IsLeaf)
            {
                trig.MinTriggerIndex = -node.LeafPtr;
                trig.MaxTriggerIndex = -node.LeafPtr;
            }
            else
            {
                trig.MinTriggerIndex = node.Left!.Id;
                trig.MaxTriggerIndex = node.Right!.Id;
            }
            flat[node.Id] = trig;
            if (!node.IsLeaf) { Flatten(node.Left!); Flatten(node.Right!); }
        }
        Flatten(root);

        void Recalculate(int idx)
        {
            var t = flat[idx];
            if (t.MinTriggerIndex >= 0)
            {
                Recalculate(t.MinTriggerIndex);
                Recalculate(t.MaxTriggerIndex);
                var l = flat[t.MinTriggerIndex]; var r = flat[t.MaxTriggerIndex];
                t.V1 = new Twinsanity.TwinsanityInterchange.Common.Vector3(MathF.Min(l.V1.X, r.V1.X), MathF.Min(l.V1.Y, r.V1.Y), MathF.Min(l.V1.Z, r.V1.Z));
                t.V2 = new Twinsanity.TwinsanityInterchange.Common.Vector3(MathF.Max(l.V2.X, r.V2.X), MathF.Max(l.V2.Y, r.V2.Y), MathF.Max(l.V2.Z, r.V2.Z));
            }
            else
            {
                int groupIdx = -t.MinTriggerIndex - 1;
                var g = groups[groupIdx];
                float x1 = float.MaxValue, y1 = float.MaxValue, z1 = float.MaxValue;
                float x2 = float.MinValue, y2 = float.MinValue, z2 = float.MinValue;
                for (uint k = g.Offset; k < g.Offset + g.Size; k++)
                {
                    var tr = tris[(int)k];
                    foreach (var vi in new[] { tr.Vector1Index, tr.Vector2Index, tr.Vector3Index })
                    {
                        var v = verts[vi];
                        x1 = MathF.Min(x1, v.X); y1 = MathF.Min(y1, v.Y); z1 = MathF.Min(z1, v.Z);
                        x2 = MathF.Max(x2, v.X); y2 = MathF.Max(y2, v.Y); z2 = MathF.Max(z2, v.Z);
                    }
                }
                t.V1 = new Twinsanity.TwinsanityInterchange.Common.Vector3(x1, y1, z1);
                t.V2 = new Twinsanity.TwinsanityInterchange.Common.Vector3(x2, y2, z2);
            }
        }
        Recalculate(0);

        result.AddRange(flat);
        return result;
    }
}
