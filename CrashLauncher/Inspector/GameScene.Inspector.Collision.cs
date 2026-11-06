using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;

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

                    if (ImGui.Button("Generate Mesh Collision (exact, selected object(s))##gencoll", new Vector2(-1f, 0f)))
                        GenerateMeshCollision();


                    if (ImGui.Button("+ Collision Box (match bounds)##boxmatchscenery", new Vector2(-1f, 0f)))
                        SpawnCollisionBoxesMatchingScenery();
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
