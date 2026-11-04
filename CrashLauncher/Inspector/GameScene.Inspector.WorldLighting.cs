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
using PS2AnySound = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnySound;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PointLight = Twinsanity.TwinsanityInterchange.Common.Lights.PointLight;
using NegativeLight = Twinsanity.TwinsanityInterchange.Common.Lights.NegativeLight;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;
using ITwinCamera = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout.ITwinCamera;

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
                if (ImGui.IsItemHovered())
                    MaybeTooltip("Per-level brightness multiplier on the real ambient + directional\n" +
                                      "lights. Your value is remembered for this level (persists on exit/\n" +
                                      "reload) and baked into the real colours only when building the ISO,\n" +
                                      "so it never compounds. Use it to fix levels the devs left too dark\n" +
                                      "or too bright.");

                var sceneryBrightness = lighting.SceneryBrightness;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.SliderFloat("Scenery Brightness##wlsb", ref sceneryBrightness, 0f, 3f))
                {
                    lighting.SceneryBrightness = sceneryBrightness;
                    var chunkRootForLight = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
                    if (chunkRootForLight is not null) ApplySceneryBrightness(chunkRootForLight, lighting);
                }
                if (ImGui.IsItemHovered())
                    MaybeTooltip("Scales scenery's OWN real, static per-vertex colours (separate from\n" +
                                      "the Intensity slider above, which only affects Instances' real-time\n" +
                                      "ambient/directional lighting — scenery has no equivalent runtime\n" +
                                      "lighting path). Always scales from the ORIGINAL colours, never\n" +
                                      "compounds across repeated drags. Save Chunk to persist.");

                var chunkRootForLinked = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
                bool hasLinks = chunkRootForLinked?.Get<ChunkSource>()?.Sm2
                    ?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM)
                    ?.LinksList.Count > 0;
                ImGui.BeginDisabled(!hasLinks || chunkRootForLinked is null);
                if (ImGui.Button("Apply to Linked Scenes", new Vector2(-1f, 0f)))
                    ApplyToLinkedScenes(chunkRootForLinked!, lighting);
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    MaybeTooltip("Pushes the CURRENT Scenery Brightness + World Lighting values out\n" +
                                      "to every level THIS one directly links to — real saves to their\n" +
                                      "own files, no need to open them yourself. Click once after settling\n" +
                                      "on the values you want (repeated clicks keep multiplying from\n" +
                                      "whatever those levels already have, not their true original).");

                ImGui.Separator();
                int fogIdx = lighting.FogColorIndex;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.Combo("Fog Color##wlfog", ref fogIdx, CrashEngine.Importer.MeshDecoder.FogColorNames, CrashEngine.Importer.MeshDecoder.FogColorNames.Length))
                    lighting.FogColorIndex = fogIdx;
                if (ImGui.IsItemHovered())
                    MaybeTooltip("The game's own fog pass: every pixel farther than ~25 units from the\n" + // Amedo 2026-09-26
                                      "camera (sky included) is blended 29.7% toward this colour, taken\n" +
                                      "from the game's per-index fog palettes. 'None' turns the fog off.\n" +
                                      "Only these 7 values exist in the game. Updates live; Save Chunk\n" +
                                      "to persist.");

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

        if (ImGui.TreeNode($"Point Lights ({scenery.PointLights.Count})##ptlights"))
        {
            int removeAt = -1;
            for (int i = 0; i < scenery.PointLights.Count; i++)
            {
                ImGui.PushID(i);
                var pl = scenery.PointLights[i];
                ImGui.Text($"Point light {i + 1}");
                ImGui.SameLine();
                if (ImGui.SmallButton("Select##ptsel")) SelectLightMarkerFor(pl);
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove##ptrm")) removeAt = i;

                var col = new Vector3(pl.Color.X, pl.Color.Y, pl.Color.Z);
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.ColorEdit3("Color##pc", ref col)) { pl.Color.X = col.X; pl.Color.Y = col.Y; pl.Color.Z = col.Z; }
                float r = pl.Radius;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.DragFloat("Radius##pr", ref r, 0.05f, 0f, float.MaxValue)) pl.Radius = r;
                ImGui.PopID();
                ImGui.Spacing();
            }
            if (removeAt >= 0) { var rm = scenery.PointLights[removeAt]; scenery.PointLights.RemoveAt(removeAt); RemoveLightMarkerFor(rm); }
            ImGui.BeginDisabled(!scenery.HasLighting);
            if (ImGui.SmallButton("+ Add Point Light##addpt"))
            {
                var pl = MakeEditorPointLight(scenery, new TwinVec4(0f, 0f, 0f, 1f));
                scenery.PointLights.Add(pl);
                EnableAddedSceneryLights(scenery); // Amedo 2026-09-26
                var m = AddOneLightMarker(pl, false, $"PointLight_{scenery.PointLights.Count - 1}");
                if (m is not null) SelectClicked(m, false);
            }
            ImGui.EndDisabled();
            if (!scenery.HasLighting) { ImGui.SameLine(); ImGui.TextDisabled("(lighting disabled for this level)"); }
            ImGui.TreePop();
        }

        if (ImGui.TreeNode($"Negative Lights ({scenery.NegativeLights.Count})##neglights"))
        {
            int removeAt = -1;
            for (int i = 0; i < scenery.NegativeLights.Count; i++)
            {
                ImGui.PushID(1000 + i);
                var nl = scenery.NegativeLights[i];
                ImGui.Text($"Negative light {i + 1}");
                ImGui.SameLine();
                if (ImGui.SmallButton("Select##ngsel")) SelectLightMarkerFor(nl);
                ImGui.SameLine();
                if (ImGui.SmallButton("Remove##ngrm")) removeAt = i;

                var col = new Vector3(nl.Color.X, nl.Color.Y, nl.Color.Z);
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.ColorEdit3("Color##nc", ref col)) { nl.Color.X = col.X; nl.Color.Y = col.Y; nl.Color.Z = col.Z; }
                float r = nl.Radius;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.DragFloat("Radius##nr", ref r, 0.05f, 0f, float.MaxValue)) nl.Radius = r;
                ImGui.PopID();
                ImGui.Spacing();
            }
            if (removeAt >= 0) { var rm = scenery.NegativeLights[removeAt]; scenery.NegativeLights.RemoveAt(removeAt); RemoveLightMarkerFor(rm); }
            ImGui.BeginDisabled(!scenery.HasLighting);
            if (ImGui.SmallButton("+ Add Negative Light##addng"))
            {
                var nl = MakeEditorNegativeLight(scenery, new TwinVec4(0f, 0f, 0f, 1f));
                scenery.NegativeLights.Add(nl);
                EnableAddedSceneryLights(scenery); // Amedo 2026-09-26
                var m = AddOneLightMarker(nl, true, $"NegLight_{scenery.NegativeLights.Count - 1}");
                if (m is not null) SelectClicked(m, false);
            }
            ImGui.EndDisabled();
            if (!scenery.HasLighting) { ImGui.SameLine(); ImGui.TextDisabled("(lighting disabled for this level)"); }
            ImGui.TreePop();
        }

        ImGui.TextDisabled("Point/Negative lights update live on lit objects (Game Lighting);\nscenery stays unlit, like the game. Save Chunk to persist.");
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
