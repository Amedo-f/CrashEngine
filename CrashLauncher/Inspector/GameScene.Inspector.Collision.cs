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
}
