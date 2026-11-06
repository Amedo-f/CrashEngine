using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PointLight = Twinsanity.TwinsanityInterchange.Common.Lights.PointLight;
using NegativeLight = Twinsanity.TwinsanityInterchange.Common.Lights.NegativeLight;
using Light = Twinsanity.TwinsanityInterchange.Common.Lights.Light;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void DrawInspectorWorldLighting(Entity e)
    {
        if (e.Get<WorldLightingSettings>() is { } lighting)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("World Lighting##worldlight", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("Saved into the level's own SM2 on Save Chunk / Build ISO (updates\nexisting lights only - can't add a new light to an unlit level).");

                // Amedo 2026-09-21
                var intensity = lighting.Intensity;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.SliderFloat("Intensity##wli", ref intensity, 0f, 5f))
                    lighting.Intensity = intensity;

                var sceneryBrightness = lighting.SceneryBrightness;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.SliderFloat("Scenery Brightness##wlsb", ref sceneryBrightness, 0f, 3f))
                {
                    lighting.SceneryBrightness = sceneryBrightness;
                    var chunkRootForLight = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
                    if (chunkRootForLight is not null) ApplySceneryBrightness(chunkRootForLight, lighting);
                }

                var chunkRootForLinked = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
                bool hasLinks = chunkRootForLinked?.Get<ChunkSource>()?.Sm2
                    ?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM)
                    ?.LinksList.Count > 0;
                ImGui.BeginDisabled(!hasLinks || chunkRootForLinked is null);
                if (ImGui.Button("Apply to Linked Scenes", new Vector2(-1f, 0f)))
                    ApplyToLinkedScenes(chunkRootForLinked!, lighting);
                ImGui.EndDisabled();

                ImGui.Separator();
                int fogIdx = lighting.FogColorIndex;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.Combo("Fog Color##wlfog", ref fogIdx, CrashEngine.Importer.MeshDecoder.FogColorNames, CrashEngine.Importer.MeshDecoder.FogColorNames.Length))
                    lighting.FogColorIndex = fogIdx;

                var ambient = lighting.AmbientColor;
                if (ImGui.ColorEdit3("Ambient##wl", ref ambient)) lighting.AmbientColor = ambient;

                for (int i = 0; i < lighting.Directional.Count; i++)
                {
                    ImGui.PushID(i);
                    var (col, dir) = lighting.Directional[i];
                    ImGui.Text($"Directional light {i + 1}");
                    if (ImGui.ColorEdit3("Color##wldc", ref col)) lighting.Directional[i] = (col, dir);
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.DragFloat3("Direction##wldd", ref dir, 0.01f, -1f, 1f))
                    {
                        var n = dir.LengthSquared() > 1e-6f ? Vector3.Normalize(dir) : dir;
                        lighting.Directional[i] = (col, n);
                    }
                    ImGui.PopID();
                }
                if (lighting.Directional.Count == 0)
                    ImGui.TextDisabled("(this level has no directional lights of its own)");

                // Amedo 2026-09-19
                var sceneryLights = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Sm2
                    ?.GetItem<PS2AnyScenery>((uint)TwinConstants.SCENERY_SECENERY_ITEM);
                if (sceneryLights is not null)
                    DrawSceneryPointNegativeLights(sceneryLights);

                ImGui.Separator();
                ImGui.TextDisabled("Copy ambient + directional lights from a different level:");
                ImGui.SetNextItemWidth(-1f);
                ImGui.InputTextWithHint("##lightFilter", "Search levels...", ref _lightTargetFilter, 128);
                ImGui.BeginChild("##lightList", new Vector2(-1f, 160f), ImGuiChildFlags.Border);
                foreach (var lvl in GetSwapLevelList())
                {
                    if (!string.IsNullOrWhiteSpace(_lightTargetFilter) &&
                        !lvl.Contains(_lightTargetFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (ImGui.Selectable(lvl))
                        SwapWorldLighting(lighting, lvl);
                }
                ImGui.EndChild();
            }
        }
    }

    private void SwapWorldLighting(WorldLightingSettings lighting, string levelPath)
    {
        var basePath = levelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)
            ? levelPath[..^4] : levelPath;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            pkg.ShadowDir = SavedChunksDir;
            using var stream = pkg.OpenByPath($"{basePath}.sm2");
            if (stream is null) { _browser.Log($"Copy Lighting: '{basePath}.sm2' not found."); return; }

            var sourceSm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
            using (var reader = new BinaryReader(stream))
                sourceSm2.Read(reader, (int)stream.Length);

            var wl = MeshDecoder.DecodeWorldLighting(sourceSm2);
            if (wl is null) { _browser.Log($"Copy Lighting: '{basePath}' has no lighting data of its own."); return; }

            lighting.AmbientColor = wl.Value.Ambient;
            lighting.Directional.Clear();
            lighting.Directional.AddRange(wl.Value.Directional);

            var lightingEnt = lighting.Entity;
            foreach (var child in lightingEnt.Children.ToList())
                lightingEnt.RemoveChild(child);
            lighting.ArrowRenderers.Clear();
            foreach (var (col, _) in lighting.Directional)
            {
                var arrowEnt = new Entity("Light Direction");
                var mesh = BuildSingleArrowMesh(Engine.Instance.GL, new Vector4(col.X, col.Y, col.Z, 1f));
                lighting.ArrowRenderers.Add(arrowEnt.Add(new GizmoRenderer { Mesh = mesh }));
                lightingEnt.AddChild(arrowEnt);
            }

            _browser.Log($"Copy Lighting: copied ambient + {lighting.Directional.Count} directional light(s) " +
                          $"from '{basePath}'. Not undoable — Reload Level discards it. Save Chunk only updates " +
                          "EXISTING light slots in this level's own native data (can't add a new light to an " +
                          "unlit level — see the World Lighting note above): if the source has MORE directional " +
                          "lights than this level natively has slots for, only the first ones will actually " +
                          "persist on save even though all of them show live here.");
        }
        catch (Exception ex) { _browser.Log($"Copy Lighting: {ex.Message}"); }
    }

    // Amedo 2026-09-19
    private void DrawSceneryPointNegativeLights(PS2AnyScenery scenery)
    {
        ImGui.Separator();

        DrawSceneryLightList(scenery, scenery.PointLights, false, MakeEditorPointLight, PointLightLabels);
        DrawSceneryLightList(scenery, scenery.NegativeLights, true, MakeEditorNegativeLight, NegativeLightLabels);

        ImGui.TextDisabled("Point/Negative lights update live on lit objects (Game Lighting);\nscenery stays unlit, like the game. Save Chunk to persist.");
    }

    private sealed record LightListLabels(string Title, string TreeId, int IdBase, string Item, string Select, string Remove,
                                          string Color, string Radius, string Add, string MarkerPrefix);

    private static readonly LightListLabels PointLightLabels = new("Point Lights", "ptlights", 0, "Point light", "Select##ptsel",
        "Remove##ptrm", "Color##pc", "Radius##pr", "+ Add Point Light##addpt", "PointLight");

    private static readonly LightListLabels NegativeLightLabels = new("Negative Lights", "neglights", 1000, "Negative light", "Select##ngsel",
        "Remove##ngrm", "Color##nc", "Radius##nr", "+ Add Negative Light##addng", "NegLight");

    private void DrawSceneryLightList<T>(PS2AnyScenery scenery, List<T> lights, bool negative,
                                         Func<PS2AnyScenery, TwinVec4, T> make, LightListLabels l) where T : Light
    {
        if (!ImGui.TreeNode($"{l.Title} ({lights.Count})##{l.TreeId}")) return;
        int removeAt = -1;
        for (int i = 0; i < lights.Count; i++)
        {
            ImGui.PushID(l.IdBase + i);
            var light = lights[i];
            ImGui.Text($"{l.Item} {i + 1}");
            ImGui.SameLine();
            if (ImGui.SmallButton(l.Select)) SelectLightMarkerFor(light);
            ImGui.SameLine();
            if (ImGui.SmallButton(l.Remove)) removeAt = i;

            var col = new Vector3(light.Color.X, light.Color.Y, light.Color.Z);
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.ColorEdit3(l.Color, ref col)) { light.Color.X = col.X; light.Color.Y = col.Y; light.Color.Z = col.Z; }
            float r = light.Radius;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.DragFloat(l.Radius, ref r, 0.05f, 0f, float.MaxValue)) light.Radius = r;
            ImGui.PopID();
            ImGui.Spacing();
        }
        if (removeAt >= 0) { var rm = lights[removeAt]; lights.RemoveAt(removeAt); RemoveLightMarkerFor(rm); }
        ImGui.BeginDisabled(!scenery.HasLighting);
        if (ImGui.SmallButton(l.Add))
        {
            var light = make(scenery, new TwinVec4(0f, 0f, 0f, 1f));
            lights.Add(light);
            EnableAddedSceneryLights(scenery);
            var m = AddOneLightMarker(light, negative, $"{l.MarkerPrefix}_{lights.Count - 1}");
            if (m is not null) SelectClicked(m, false);
        }
        ImGui.EndDisabled();
        if (!scenery.HasLighting) { ImGui.SameLine(); ImGui.TextDisabled("(lighting disabled for this level)"); }
        ImGui.TreePop();
    }

    // Amedo 2026-09-19
    private static PointLight MakeEditorPointLight(PS2AnyScenery scenery, TwinVec4 pos)
    {
        var pl = new PointLight
        {
            UnkData  = 0x00000102,
            UnkShort = 2,
            Radius   = 150f,
            Color    = new TwinVec4(1f, 1f, 1f, 0f),
            Position = pos,
            UnkVec1  = new TwinVec4(pos.X - 6000f, pos.Y - 6000f, pos.Z - 6000f, 1f),
            UnkVec2  = new TwinVec4(pos.X + 6000f, pos.Y + 6000f, pos.Z + 6000f, 1f),
        };
        var tmpl = scenery.PointLights.FirstOrDefault(p => p.UnkShort > 0);
        if (tmpl is not null)
        {
            pl.UnkData  = tmpl.UnkData;
            pl.UnkShort = tmpl.UnkShort;
            pl.Radius   = tmpl.Radius;
            pl.Color.W  = tmpl.Color.W;
            pl.UnkVec1  = new TwinVec4(tmpl.UnkVec1.X, tmpl.UnkVec1.Y, tmpl.UnkVec1.Z, tmpl.UnkVec1.W);
            pl.UnkVec2  = new TwinVec4(tmpl.UnkVec2.X, tmpl.UnkVec2.Y, tmpl.UnkVec2.Z, tmpl.UnkVec2.W);
        }
        return pl;
    }

    // Amedo 2026-09-19
    private static NegativeLight MakeEditorNegativeLight(PS2AnyScenery scenery, TwinVec4 pos)
    {
        var nl = new NegativeLight
        {
            UnkData  = 0x00000102,
            Radius   = 150f,
            Color    = new TwinVec4(1f, 1f, 1f, 0f),
            Position = pos,
            UnkVec1  = new TwinVec4(pos.X - 6000f, pos.Y - 6000f, pos.Z - 6000f, 1f),
            UnkVec2  = new TwinVec4(pos.X + 6000f, pos.Y + 6000f, pos.Z + 6000f, 1f),
        };
        var tmpl = scenery.NegativeLights.FirstOrDefault();
        if (tmpl is not null)
        {
            nl.UnkData    = tmpl.UnkData;
            nl.Radius     = tmpl.Radius;
            nl.Color.W    = tmpl.Color.W;
            nl.UnkVec1    = new TwinVec4(tmpl.UnkVec1.X, tmpl.UnkVec1.Y, tmpl.UnkVec1.Z, tmpl.UnkVec1.W);
            nl.UnkVec2    = new TwinVec4(tmpl.UnkVec2.X, tmpl.UnkVec2.Y, tmpl.UnkVec2.Z, tmpl.UnkVec2.W);
            nl.UnkVec3    = new TwinVec4(tmpl.UnkVec3.X, tmpl.UnkVec3.Y, tmpl.UnkVec3.Z, tmpl.UnkVec3.W);
            nl.UnkFloat1  = tmpl.UnkFloat1;
            nl.UnkFloat2  = tmpl.UnkFloat2;
            nl.UnkUInt1   = tmpl.UnkUInt1;
            nl.UnkUInt2   = tmpl.UnkUInt2;
            nl.UnkUShort1 = tmpl.UnkUShort1;
            nl.UnkUShort2 = tmpl.UnkUShort2;
        }
        return nl;
    }
}
