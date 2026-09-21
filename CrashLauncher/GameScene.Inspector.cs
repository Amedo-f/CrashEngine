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
    private string _trigInstanceAddBuf = "";

    private static readonly Dictionary<int, string> ParamList2Labels = new()
    {
        [1]  = "WalkSpeedPercentage",
        [3]  = "BaseGravity",
        [4]  = "AirGravity",
        [5]  = "StrafingSpeed",
        [6]  = "WalkSpeed",
        [7]  = "RunSpeed",
        [9]  = "SpinThrowForwardForce",
        [10] = "SpinLength",
        [11] = "SpinDelay",
        [15] = "JumpAirSpeed",
        [16] = "JumpHeight",
        [17] = "JumpArcUnk18",
        [18] = "JumpArcUnk19",
        [19] = "JumpEdgeSpeed",
        [20] = "DoubleJumpHeight",
        [21] = "DoubleJumpUnk22",
        [22] = "DoubleJumpArcUnk",
        [23] = "SlideJumpForce1",
        [24] = "SlideJumpForce2",
        [25] = "SlideJumpForce3",
        [26] = "SlideJumpArc",
        [32] = "BodyslamUpwardForce",
        [33] = "BodyslamGravityForce",
        [40] = "CrawlSpeed",
        [41] = "CrawlTimeFromStand",
        [42] = "CrawlTimeToStand",
        [43] = "CrawlTimeToRun",
        [44] = "SlideSpeed",
        [45] = "SlideSlowdownTime1",
        [46] = "SlideSlowdownTime2",
        [47] = "SlideSlowdownTime3",
        [48] = "SlideUnk49",
        [49] = "SlideUnk50",
    };

    private void DrawInspector(Entity e)
    {
        bool active = e.Active;
        if (ImGui.Checkbox("##active", ref active)) e.Active = active;
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(1f, 1f, 0.4f, 1f), e.Name);

        if (ImGui.Button("Export Object (OBJ + textures)...", new Vector2(-1f, 0f)))
        {
            ExportSelectedObjectToObj();
        }
        if (ImGui.IsItemHovered())
        {
            MaybeTooltip("Exports this object (and every child part) as a real Wavefront .obj +\n.mtl + .png texture files, ready to open directly in Blender — full shape,\nUVs, and textures, not just raw positions like Export Collision to OBJ.");
        }

        if (e.Get<SceneryTile>() is not null)
        {
            if (ImGui.Button("Convert to Object (real)...", new Vector2(-1f, 0f)))
            {
                ConvertSceneryTileToObject(e);
            }
            if (ImGui.IsItemHovered())
            {
                MaybeTooltip("Turns this scenery tile into a real Object + Instance, reusing its\nSAME already-compiled mesh/material/texture data as-is (not re-baked),\nthen removes the original tile. Real Instances never go through the\nscenery tree's own visibility gate — sidesteps the 'disappears near\ncamera' bug entirely. Non-uniform scale on the tile can't be kept\n(Instances have no scale field). Save Chunk + Build ISO to test.");
            }

            var sceneryTargets = (_selectedSet.Count > 0 ? _selectedSet : new HashSet<Entity> { e })
                .Where(x => x.Get<SceneryTile>() is not null).ToList();
            if (sceneryTargets.Count > 0)
            {
                var label = sceneryTargets.Count > 1 ? $"Add {sceneryTargets.Count} Tiles to Assets..." : "Add to Assets...";
                if (ImGui.Button(label + "##addtoassets", new Vector2(-1f, 0f)))
                {
                    OpenAddToAssetsPopup(sceneryTargets);
                }
                if (ImGui.IsItemHovered())
                {
                    MaybeTooltip("Saves this tile (or every selected tile, keeping their relative\nlayout) as a reusable custom asset — pick or create a named folder,\nthen import the whole group into ANY level later from the Assets\npanel's 'My Assets' tab. Grafted as independent leaf(s) on import,\nnever merged into one shared tree with the destination's own scenery.");
                }
            }
        }

        if (e.Get<InstanceData>() is not null && e.Get<SceneryTile>() is null)
        {
            if (ImGui.Button("Add Object to Assets...##addobjtoassets", new Vector2(-1f, 0f)))
                OpenAddObjectToAssetsPopup(e);
            if (ImGui.IsItemHovered())
                MaybeTooltip("Saves this OBJECT (its full graph: code/behaviours/OGI/animations/\ngraphics + this instance's settings) as a reusable asset in 'My Assets'.\nRe-import into ANY level later. v1 references the source level, so keep\nit around. A 3D thumbnail is baked from this object's current mesh.");
        }

        ImGui.Separator();
        if (ImGui.CollapsingHeader("Transform", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var lmForPos = e.Transform.LocalMatrix;
            var pos = lmForPos is { } m0 ? new Vector3(m0.M41, m0.M42, m0.M43) : e.Transform.Position;
            ImGui.SetNextItemWidth(-1f);
            bool posChanged = ImGui.DragFloat3("Pos##t", ref pos, 0.05f);
            if (ImGui.IsItemActivated()) _inspectorDragBefore = new TransformSnapshot(e.Transform);
            if (posChanged)
            {
                if (lmForPos is { } m1)
                {
                    m1.M41 = pos.X; m1.M42 = pos.Y; m1.M43 = pos.Z;
                    e.Transform.LocalMatrix = m1;
                }
                else e.Transform.Position = pos;
            }
            PushInspectorUndoIfDeactivated(e);

            var lmForRot = e.Transform.LocalMatrix;
            bool haveRot = true;
            Vector3 scaleForRot = Vector3.One, posForRot = Vector3.Zero;
            Vector3 eulerDeg;
            if (lmForRot is { } mRot)
            {
                if (Matrix4x4.Decompose(mRot, out scaleForRot, out var rotQ, out posForRot))
                    eulerDeg = MatrixToEulerXYZ(Matrix4x4.CreateFromQuaternion(rotQ)) * (180f / MathF.PI);
                else { haveRot = false; eulerDeg = Vector3.Zero; }
            }
            else
            {
                eulerDeg = MatrixToEulerXYZ(Matrix4x4.CreateFromQuaternion(e.Transform.Rotation)) * (180f / MathF.PI);
            }

            ImGui.BeginDisabled(!haveRot);
            ImGui.SetNextItemWidth(-1f);
            bool rotChanged = ImGui.DragFloat3("Rot##t", ref eulerDeg, 0.5f);
            if (!haveRot && ImGui.IsItemHovered())
                MaybeTooltip("This object's transform is a mirrored/degenerate matrix — rotation\n" +
                             "can't be cleanly separated from its scale to show as angles.");
            if (ImGui.IsItemActivated()) _inspectorDragBefore = new TransformSnapshot(e.Transform);
            if (rotChanged)
            {
                var newQuat = EulerXYZToQuaternion(eulerDeg * (MathF.PI / 180f));
                if (lmForRot is not null)
                {
                    e.Transform.LocalMatrix = Matrix4x4.CreateScale(scaleForRot)
                                             * Matrix4x4.CreateFromQuaternion(newQuat)
                                             * Matrix4x4.CreateTranslation(posForRot);
                }
                else e.Transform.Rotation = newQuat;
            }
            PushInspectorUndoIfDeactivated(e);
            ImGui.EndDisabled();

            var scale = lmForRot is not null ? scaleForRot : e.Transform.Scale;

            void ApplyScale(Vector3 newScale)
            {
                if (lmForRot is not null)
                {
                    var rotQ2 = Matrix4x4.Decompose(lmForRot.Value, out _, out var q2, out _) ? q2 : Quaternion.Identity;
                    e.Transform.LocalMatrix = Matrix4x4.CreateScale(newScale)
                                             * Matrix4x4.CreateFromQuaternion(rotQ2)
                                             * Matrix4x4.CreateTranslation(posForRot);
                }
                else e.Transform.Scale = newScale;
            }

            float uniform = (scale.X + scale.Y + scale.Z) / 3f;
            ImGui.SetNextItemWidth(-1f);
            bool uniformChanged = ImGui.DragFloat("Uniform Scale##t", ref uniform, 0.01f, 0.001f, 1000f);
            if (ImGui.IsItemActivated()) _inspectorDragBefore = new TransformSnapshot(e.Transform);
            if (uniformChanged) { scale = new Vector3(uniform, uniform, uniform); ApplyScale(scale); }
            PushInspectorUndoIfDeactivated(e);

            ImGui.SetNextItemWidth(-1f);
            bool scaleChanged = ImGui.DragFloat3("Scale##t", ref scale, 0.01f, 0.001f, 1000f);
            if (ImGui.IsItemActivated()) _inspectorDragBefore = new TransformSnapshot(e.Transform);
            if (scaleChanged) ApplyScale(scale);
            PushInspectorUndoIfDeactivated(e);

            var wp = e.Transform.World.Translation;
            ImGui.TextDisabled($"World  {wp.X:F2}  {wp.Y:F2}  {wp.Z:F2}");
        }

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
                    MaybeTooltip("Real distance-fog colour this level blends distant geometry toward\n" +
                                      "(exponential curve, capped at 40% blend). Not a bug/LOD issue - a\n" +
                                      "genuine PS2-era technique to soften the far draw distance. Limited\n" +
                                      "to these 6 real values (that's all the format supports), unlike the\n" +
                                      "free colour pickers above. Save Chunk to persist.");

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

        if (e.Has<ChunkSource>())
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Whole-Chunk Transform##chunktf", ImGuiTreeNodeFlags.DefaultOpen))
            {
                bool hasOffset = e.Transform.Position.LengthSquared() > 1e-8f ||
                                  Quaternion.Dot(e.Transform.Rotation, Quaternion.Identity) < 0.999999f;
                ImGui.BeginDisabled(!hasOffset);
                if (ImGui.Button("Apply Chunk Transform (bake + save + reload)##applychunktf", new Vector2(-1f, 0f)))
                    ApplyChunkTransform();
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered()) MaybeTooltip(
                    "Move/rotate the level using the Pos/Rot fields above (or the gizmo), then\n" +
                    "click this to make it REAL: bakes the offset into every Instance, Scenery\n" +
                    "tile, Collision vertex/trigger, layout Trigger/Camera/Position/AI Position\n" +
                    "in the chunk, resets this Transform back to zero, Saves Chunk, and reloads.\n" +
                    "NOT on the Undo stack (touches too many independent systems at once) and\n" +
                    "writes to disk immediately — this is a one-way bake, like Blender's Apply\n" +
                    "Transform, not a live/re-editable offset. Cameras/Triggers' own internal\n" +
                    "facing/rotation fields are NOT adjusted (undecoded) — exact for a pure move,\n" +
                    "an approximation if you also rotated.");
                if (!hasOffset) ImGui.TextDisabled("(move or rotate this chunk above to enable)");
            }
        }

        DrawCollisionSection(e);
        DrawArraySection(e);

        var mr = e.Get<CrashEngine.Importer.MeshRenderer>();
        if (mr is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("MeshRenderer", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var mat = mr.Material;
                if (mat is not null)
                {
                    var col = mat.BaseColor;
                    if (ImGui.ColorEdit4("Base Color##mr", ref col))
                        mat.BaseColor = col;

                    if (!string.IsNullOrEmpty(mat.ShaderType))
                        ImGui.TextDisabled($"PS2 Shader: {mat.ShaderType}");

                    string[] cullLabels = { "Front", "Back", "Both" };
                    int cullIdx = (int)mat.Culling;
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.Combo("Cull Mode##mr", ref cullIdx, cullLabels, cullLabels.Length))
                        mat.Culling = (Material.CullMode)cullIdx;

                    bool unlit = mat.Unlit;
                    if (ImGui.Checkbox("Unlit##mr", ref unlit))
                    {
                        mat.Unlit = unlit;
                        mat.UnlitToggledByUser = true;
                    }

                    bool ab = mat.AlphaBlend;
                    if (ImGui.Checkbox("Alpha Blend##mr", ref ab))
                        mat.AlphaBlend = ab;

                    if (mat.AlphaBlend)
                    {
                        string[] blendLabels = { "Standard", "Additive", "Subtractive" };
                        int blendIdx = (int)mat.Blend;
                        ImGui.SetNextItemWidth(-1f);
                        if (ImGui.Combo("Blend Mode##mr", ref blendIdx, blendLabels, blendLabels.Length))
                            mat.Blend = (Material.BlendMode)blendIdx;
                    }

                    bool fog = mat.FogEnabled;
                    if (ImGui.Checkbox("Fog##mr", ref fog))
                        mat.FogEnabled = fog;

                    var scroll = mat.UvScrollSpeed;
                    if (scroll.X != 0f || scroll.Y != 0f)
                        ImGui.TextDisabled($"UV Scroll: {scroll.X:F4} , {scroll.Y:F4} /s");

                    if (mat.Albedo is not null)
                    {
                        var tex = mat.Albedo;
                        ImGui.TextDisabled($"Texture: {tex.Width}x{tex.Height}");
                        float avail  = ImGui.GetContentRegionAvail().X;
                        float aspect = tex.Height > 0 ? (float)tex.Width / tex.Height : 1f;
                        float th     = MathF.Min(avail / aspect, 160f);
                        float tw     = th * aspect;
                        ImGui.Image((nint)tex.GlId, new Vector2(tw, th), new Vector2(0f, 1f), new Vector2(1f, 0f));

                        if (ImGui.Button("Import Texture...##mrimport", new Vector2(-1f, 0f)))
                            ImportTextureOverMaterial(mat);
                        if (ImGui.IsItemHovered())
                            MaybeTooltip("Replaces this PS2 texture (re-encoded, kept for Save/Build) —\n" +
                                              "every object using the same texture updates together.");

                        if (ImGui.Button("Import From Level...##mrimportlevel", new Vector2(-1f, 0f)))
                        {
                            _texturePickerTargetMat = mat;
                            _texturePickerLevel = null;
                            _showTexturePicker = true;
                        }
                        if (ImGui.IsItemHovered())
                            MaybeTooltip("Browse a DIFFERENT level's own real textures and pick one to\n" +
                                              "copy here — automatically resized/re-encoded to match this\n" +
                                              "texture's own real slot (same safe path as Import Texture).\n" +
                                              "If this is a scenery texture, also auto-finds EVERY live tile\n" +
                                              "in the current scene using this same texture slot and pulls\n" +
                                              "along that source mesh's vertex colours onto all of them — no\n" +
                                              "manual selection needed, texture alone isn't the real color.");

                        if (ImGui.Button("Reset Texture to Original##mrreset", new Vector2(-1f, 0f)))
                            ResetTextureToOriginal(mat);
                        if (ImGui.IsItemHovered())
                            MaybeTooltip("Re-reads this texture fresh from the real disc archive\n" +
                                              "(ignores any imported/saved edit) — a safety net if an\n" +
                                              "import went wrong or looks broken.");
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Texture: none");
                    }

                    if (e.Parent?.Get<SceneryTile>() is not null)
                    {
                        if (ImGui.Button("Copy Vertex Colors From Level...##mrvcopy", new Vector2(-1f, 0f)))
                        {
                            _vertexColorPickerTargets = new List<Entity> { e };
                            _vertexColorPickerLevel = null;
                            _showVertexColorPicker = true;
                        }
                        if (ImGui.IsItemHovered())
                            MaybeTooltip("This mesh's actual colour comes from PER-VERTEX data baked\n" +
                                              "into the geometry, not the texture — browse another level's\n" +
                                              "scenery meshes and copy its vertex colours onto this one.\n" +
                                              "1:1 if vertex counts match, otherwise broadcasts the source's\n" +
                                              "average colour uniformly. Rebuilds this tile's visuals live.");

                        var sceneryTargets = _selectedSet.Where(x => x.Parent?.Get<SceneryTile>() is not null).ToList();
                        if (sceneryTargets.Count > 1)
                        {
                            if (ImGui.Button($"Copy Vertex Colors To {sceneryTargets.Count} Selected Tiles From Level...##mrvcopybatch", new Vector2(-1f, 0f)))
                            {
                                _vertexColorPickerTargets = sceneryTargets;
                                _vertexColorPickerLevel = null;
                                _showVertexColorPicker = true;
                            }
                            if (ImGui.IsItemHovered())
                                MaybeTooltip("Ctrl+Click multiple scenery tiles first (e.g. every visible\n" +
                                                  "ground patch) — applies the same picked source mesh to every\n" +
                                                  "DISTINCT underlying mesh id in the selection at once, instead\n" +
                                                  "of repeating the single-tile button per ground variant.");
                        }
                    }

                    if (mat.LocalCenter.HasValue)
                    {
                        var lc = mat.LocalCenter.Value;
                        ImGui.TextDisabled($"Local center: ({lc.X:F2}, {lc.Y:F2}, {lc.Z:F2})");
                    }
                }
                else
                {
                    ImGui.TextDisabled("(no material)");
                }
                if (mr.Mesh is not null)
                {
                    int faces = mr.Mesh.IndexCount > 0
                        ? mr.Mesh.IndexCount / 3
                        : mr.Mesh.VertexCount / 3;
                    ImGui.TextDisabled($"Vertices:  {mr.Mesh.VertexCount:N0}");
                    ImGui.TextDisabled($"Triangles: {faces:N0}");
                }
            }
        }

        {
            var importRoot = e;
            while (importRoot.Parent is not null) importRoot = importRoot.Parent;
            if (_importedRoots.Contains(importRoot))
            {
                ImGui.Separator();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.55f, 0.12f, 0.12f, 1f));
                if (ImGui.Button("Remove Model from Scene", new Vector2(-1f, 0f)))
                    if (!_pendingDelImports.Contains(importRoot))
                        _pendingDelImports.Add(importRoot);
                ImGui.PopStyleColor();
            }
        }

        var rmr = e.Get<CrashEngine.Renderer.MeshRenderer>();
        if (rmr is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("MeshRenderer##rmr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var mat = rmr.Material;

                var col = mat.BaseColor;
                if (ImGui.ColorEdit4("Base Color##rmr", ref col))
                    mat.BaseColor = col;

                string[] cullLabels = { "Front", "Back", "Both" };
                int cullIdx = (int)mat.Culling;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.Combo("Cull Mode##rmr", ref cullIdx, cullLabels, cullLabels.Length))
                    mat.Culling = (Material.CullMode)cullIdx;

                bool unlit = mat.Unlit;
                if (ImGui.Checkbox("Unlit##rmr", ref unlit)) { mat.Unlit = unlit; mat.UnlitToggledByUser = true; }

                bool ab = mat.AlphaBlend;
                if (ImGui.Checkbox("Alpha Blend##rmr", ref ab)) mat.AlphaBlend = ab;
                if (mat.AlphaBlend)
                {
                    string[] blendLabels = { "Standard", "Additive", "Subtractive" };
                    int blendIdx = (int)mat.Blend;
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.Combo("Blend Mode##rmr", ref blendIdx, blendLabels, blendLabels.Length))
                        mat.Blend = (Material.BlendMode)blendIdx;
                }

                bool fog = mat.FogEnabled;
                if (ImGui.Checkbox("Fog##rmr", ref fog)) mat.FogEnabled = fog;

                bool aot = mat.AlwaysOnTop;
                if (ImGui.Checkbox("Always On Top##rmr", ref aot)) mat.AlwaysOnTop = aot;

                var scroll = mat.UvScrollSpeed;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.DragFloat2("UV Scroll/s##rmr", ref scroll, 0.001f))
                    mat.UvScrollSpeed = scroll;

                if (mat.Albedo is not null)
                {
                    var tex = mat.Albedo;
                    ImGui.TextDisabled($"Texture: {tex.Width}×{tex.Height}");
                    float avail  = ImGui.GetContentRegionAvail().X;
                    float aspect = tex.Height > 0 ? (float)tex.Width / tex.Height : 1f;
                    float th     = MathF.Min(avail / aspect, 160f);
                    float tw     = th * aspect;
                    ImGui.Image((nint)tex.GlId, new Vector2(tw, th), new Vector2(0f, 1f), new Vector2(1f, 0f));
                }
                else
                {
                    ImGui.TextColored(new Vector4(1f, 0.55f, 0.2f, 1f), "No texture (shows orange)");
                    if (ImGui.Button("Load Texture##rmr", new Vector2(-1f, 0f)))
                    {
                        ShowOpenFileDialog("Select Texture",
                            "Images\0*.png;*.jpg;*.jpeg;*.bmp;*.tga\0All Files\0*.*\0\0", tp =>
                        {
                            if (tp is not null)
                            {
                                var t = Texture2D.FromFile(Engine.Instance.GL, tp);
                                if (t is not null) mat.Albedo = t;
                            }
                        });
                    }
                }

                if (rmr.Mesh is not null)
                {
                    int faces = rmr.Mesh.IndexCount > 0
                        ? rmr.Mesh.IndexCount / 3 : rmr.Mesh.VertexCount / 3;
                    ImGui.TextDisabled($"Vertices:  {rmr.Mesh.VertexCount:N0}");
                    ImGui.TextDisabled($"Triangles: {faces:N0}");
                }
                else
                {
                    ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "Mesh: null!");
                }
            }
        }

        // Amedo 2026-09-19
        var dcr = e.Get<DirectCubeRenderer>();
        if (dcr is not null && !e.Has<CrashEngine.Importer.SceneryLightMarker>())
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Cube##dcr", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("Texture (PNG/JPG)");
                ImGui.SetNextItemWidth(-80f);
                ImGui.InputText("##texpath", ref _cubeTexPath, 512);
                ImGui.SameLine();
                if (ImGui.Button("...##browse", new Vector2(-1f, 0f)))
                {
                    ShowOpenFileDialog(
                        "Select Texture",
                        "Images\0*.png;*.jpg;*.jpeg;*.bmp;*.tga\0All Files\0*.*\0\0", picked =>
                    {
                        if (picked is not null)
                        {
                            _cubeTexPath = picked;
                            var gl  = Engine.Instance.GL;
                            var tex = Texture2D.FromFile(gl, picked);
                            if (tex is not null)
                            {
                                dcr.Albedo?.Dispose();
                                dcr.Albedo    = tex;
                                dcr.Mat.BaseColor = Vector4.One;
                            }
                        }
                    });
                }
                if (dcr.Albedo is not null)
                {
                    var tex = dcr.Albedo;
                    ImGui.TextDisabled($"{tex.Width}x{tex.Height}");
                    float avail  = ImGui.GetContentRegionAvail().X;
                    float aspect = tex.Height > 0 ? (float)tex.Width / tex.Height : 1f;
                    float th     = MathF.Min(avail / aspect, 128f);
                    float tw     = th * aspect;
                    ImGui.Image((nint)tex.GlId, new Vector2(tw, th), new Vector2(0f, 1f), new Vector2(1f, 0f));
                    if (ImGui.Button("Clear Texture##dcr", new Vector2(-1f, 0f)))
                    { dcr.Albedo.Dispose(); dcr.Albedo = null; }
                }
                else { ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1f), "No texture"); }

                ImGui.Separator();
                var cm = dcr.Mat;

                var col = cm.BaseColor;
                if (ImGui.ColorEdit4("Color##dcr", ref col)) cm.BaseColor = col;

                string[] cullLabels = { "Front", "Back", "Both" };
                int cullIdx = (int)cm.Culling;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.Combo("Cull Mode##dcr", ref cullIdx, cullLabels, cullLabels.Length))
                    cm.Culling = (Material.CullMode)cullIdx;

                bool unlit = cm.Unlit;
                if (ImGui.Checkbox("Unlit##dcr", ref unlit)) { cm.Unlit = unlit; cm.UnlitToggledByUser = true; }

                bool ab = cm.AlphaBlend;
                if (ImGui.Checkbox("Alpha Blend##dcr", ref ab)) cm.AlphaBlend = ab;
                if (cm.AlphaBlend)
                {
                    string[] blendLabels = { "Standard", "Additive", "Subtractive" };
                    int bi = (int)cm.Blend;
                    ImGui.SetNextItemWidth(-1f);
                    if (ImGui.Combo("Blend##dcr", ref bi, blendLabels, blendLabels.Length))
                        cm.Blend = (Material.BlendMode)bi;
                }

                bool fog = cm.FogEnabled;
                if (ImGui.Checkbox("Fog##dcr", ref fog)) cm.FogEnabled = fog;

                bool aot = cm.AlwaysOnTop;
                if (ImGui.Checkbox("Always on Top##dcr", ref aot)) cm.AlwaysOnTop = aot;

                var scroll = cm.UvScrollSpeed;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.DragFloat2("UV Scroll/s##dcr", ref scroll, 0.001f))
                    cm.UvScrollSpeed = scroll;

                ImGui.Separator();
                bool emitsCollision = e.Has<CollisionBoxMarker>();
                if (ImGui.Checkbox("Emit Collision (test box)##dcr", ref emitsCollision))
                {
                    if (emitsCollision)
                    {
                        e.Add(new CollisionBoxMarker());
                        dcr.Color = CollisionDebugColor;
                        dcr.Mat.AlphaBlend = true;
                    }
                    else if (e.Get<CollisionBoxMarker>() is { } marker)
                    {
                        var collRootForToggle = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
                        var collForToggle = collRootForToggle?.Get<ChunkSource>()?.Rm2
                            ?.GetItem<PS2AnyCollisionData>((uint)TwinConstants.LEVEL_COLLISION_ITEM);
                        if (collForToggle is not null)
                            foreach (var idx in marker.OwnedTriIndices)
                            {
                                if (idx < 0 || idx >= collForToggle.Triangles.Count) continue;
                                var orig = collForToggle.Triangles[idx];
                                collForToggle.Triangles[idx] = new TwinCollisionTriangle
                                {
                                    Vector1Index = orig.Vector1Index, Vector2Index = orig.Vector1Index,
                                    Vector3Index = orig.Vector1Index, SurfaceIndex = orig.SurfaceIndex,
                                };
                            }
                        e.RemoveComponent<CollisionBoxMarker>();
                    }
                }
                if (ImGui.IsItemHovered())
                    MaybeTooltip("When on, this cube's current position/scale is written as\n" +
                                      "real PS2 collision geometry on every Save Chunk (like Unity's\n" +
                                      "BoxCollider). Turning it off retracts any collision a previous\n" +
                                      "save already wrote for it.");

                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
                if (ImGui.Button("Delete Cube", new Vector2(-1f, 0f)))
                {
                    if (CubeFor(e) is { } cube && !_pendingDel.Contains(cube))
                        _pendingDel.Add(cube);
                }
                ImGui.PopStyleColor();
            }
        }

        var rp = e.Get<RenderPipeline>();
        if (rp is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Rendering##rp", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled($"Fog Color: ({rp.FogColor.X:F2}, {rp.FogColor.Y:F2}, {rp.FogColor.Z:F2})  " +
                                     "-- edit via World Lighting section to persist");

                ImGui.Separator();
                bool dbg = rp.DebugRed;
                if (ImGui.Checkbox("Debug: Diffuse Only##rp", ref dbg)) rp.DebugRed = dbg;
            }
        }

        var cam = e.Get<CameraComponent>();
        if (cam is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Camera##cam"))
            {
                ImGui.TextDisabled($"Far: {cam.Far}");
                var cp = cam.Transform.Position;
                ImGui.TextDisabled($"Pos: {cp.X:F1}  {cp.Y:F1}  {cp.Z:F1}");
            }
        }

        if (e.Name == "LinkedScenery")
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Linked Scenery (all links)##linkgroup", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("Add a real NEW link (trigger only, no scenery preview -- see\ntooltip) to a level this chunk doesn't already link to:");
                ImGui.SetNextItemWidth(-1f);
                ImGui.InputTextWithHint("##addLinkFilter", "Search levels...", ref _addLinkFilter, 128);
                if (ImGui.IsItemHovered())
                    MaybeTooltip("No Link_* scenery preview is built (that preview has caused\n" +
                                 "real visual distortion before when built away from a level's own\n" +
                                 "native position) -- just the real link + a draggable trigger\n" +
                                 "wall, positioned at the camera. Save Chunk to persist.");
                ImGui.BeginChild("##addLinkList", new Vector2(-1f, 140f), ImGuiChildFlags.Border);
                var existingTargets = new HashSet<string>(
                    e.Children.Select(c => c.Get<CrashEngine.Importer.LinkedSceneryLink>()?.Source.Path.Replace('/', '\\').TrimStart('\\'))
                              .Where(p => p is not null)!,
                    StringComparer.OrdinalIgnoreCase);
                foreach (var lvl in GetLinkRedirectTargets())
                {
                    if (!string.IsNullOrWhiteSpace(_addLinkFilter) &&
                        !lvl.Contains(_addLinkFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var basePath = lvl.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? lvl[..^4] : lvl;
                    if (existingTargets.Contains(basePath)) continue;
                    if (ImGui.Selectable(lvl))
                        AddNewLink(e, basePath);
                }
                ImGui.EndChild();
            }
        }

        var linkedScenery = e.Get<CrashEngine.Importer.LinkedSceneryLink>();
        if (linkedScenery is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Linked Scenery##link", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled($"Target: {linkedScenery.Source.Path}");
                if (ImGui.Button("Go to this scene##gotolinked", new Vector2(-1f, 0f)))
                {
                    var tgt = (linkedScenery.Source.Path ?? "").Replace('/', '\\').TrimStart('\\');
                    if (tgt.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ||
                        tgt.EndsWith(".sm2", StringComparison.OrdinalIgnoreCase)) tgt = tgt[..^4];
                    if (!string.IsNullOrWhiteSpace(tgt)) _pendingOpenLevelPath = tgt + ".rm2";
                }
                if (!IsLinkTargetKeptInNewGame(linkedScenery.Source.Path))
                    ImGui.TextColored(new Vector4(1f, 0.5f, 0.2f, 1f),
                        "⚠ Not claimed for New Game -- this link will be DISABLED\n(neutered) in the built ISO, even though it's still live here.");
                ImGui.TextDisabled($"Load Wall Active: {linkedScenery.Source.IsLoadWallActive}");
                if (!linkedScenery.Source.IsLoadWallActive)
                    ImGui.TextDisabled("(preview-only — the load wall isn't collidable, so\n" +
                                        "walking here won't actually transition, per this data)");

                bool isRendered = linkedScenery.Source.IsRendered;
                if (ImGui.Checkbox("Rendered (visible preview before crossing)##linkRendered", ref isRendered))
                {
                    linkedScenery.Source.IsRendered = isRendered;
                    if (isRendered)
                    {
                        if (e.Children.Count == 0)
                            RetargetLinkedScenery(e, linkedScenery.Source.Path);
                    }
                    else
                    {
                        foreach (var child in e.Children.ToList())
                            e.RemoveChild(child);
                    }
                }
                if (!isRendered)
                    ImGui.TextDisabled("(not rendered — real game skips the preview for this link;\n" +
                                        "toggle on to load+edit it here anyway)");

                ImGui.TextDisabled("Redirect to a different level's chunk:");
                if (GetGameProject() is { IsNewGame: true })
                    ImGui.TextColored(new Vector4(1f, 0.7f, 0.3f, 1f),
                        "New Game mode: list limited to levels that'll actually exist in the build.");
                ImGui.SetNextItemWidth(-1f);
                ImGui.InputTextWithHint("##linkFilter", "Search levels...", ref _linkTargetFilter, 128);
                ImGui.BeginChild("##linkList", new Vector2(-1f, 160f), ImGuiChildFlags.Border);
                foreach (var lvl in GetLinkRedirectTargets())
                {
                    if (!string.IsNullOrWhiteSpace(_linkTargetFilter) &&
                        !lvl.Contains(_linkTargetFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (ImGui.Selectable(lvl))
                        RetargetLinkedScenery(e, lvl);
                }
                ImGui.EndChild();
            }
        }

        if (e.Get<CrashEngine.Importer.SkydomeMarker>() is { } skyMarker)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Skydome##sky", ImGuiTreeNodeFlags.DefaultOpen))
            {
                float skyBrightness = skyMarker.Brightness;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.SliderFloat("Brightness##sky", ref skyBrightness, 0f, 3f))
                {
                    skyMarker.Brightness = skyBrightness;
                    var chunkRootForSky = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
                    if (chunkRootForSky is not null) ApplySkydomeBrightness(chunkRootForSky, skyMarker);
                }
                if (ImGui.IsItemHovered())
                    MaybeTooltip("Scales the skydome's OWN real, static per-vertex colours — same\n" +
                                      "idea as World Lighting's Scenery Brightness, scoped to just the\n" +
                                      "sky. Always scales from the ORIGINAL colours, never compounds\n" +
                                      "across repeated drags. Save Chunk to persist.");

                DrawSkydomeUniquePicker();
            }
        }

        var loadWall = e.Get<CrashEngine.Importer.LoadWallMarker>();
        if (loadWall is not null && e.Transform.LocalMatrix is { } wallLm)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Load Wall##loadwall", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled($"Target: {loadWall.Source.Path}");
                if (!IsLinkTargetKeptInNewGame(loadWall.Source.Path))
                    ImGui.TextColored(new Vector4(1f, 0.5f, 0.2f, 1f),
                        "⚠ Not claimed for New Game -- this wall will be DISABLED\n(neutered) in the built ISO, even though it's still live here.");

                var right = new Vector3(wallLm.M11, wallLm.M12, wallLm.M13);
                var up    = new Vector3(wallLm.M21, wallLm.M22, wallLm.M23);
                float width  = right.Length() * 2f;
                float height = up.Length() * 2f;

                ImGui.SetNextItemWidth(-1f);
                bool widthChanged  = ImGui.DragFloat("Width##loadwall",  ref width,  0.1f, 0.1f, 2000f);
                ImGui.SetNextItemWidth(-1f);
                bool heightChanged = ImGui.DragFloat("Height##loadwall", ref height, 0.1f, 0.1f, 2000f);
                if (widthChanged || heightChanged)
                {
                    var newRight = (right.LengthSquared() > 1e-9f ? Vector3.Normalize(right) : Vector3.UnitX) * (width * 0.5f);
                    var newUp    = (up.LengthSquared()    > 1e-9f ? Vector3.Normalize(up)    : Vector3.UnitY) * (height * 0.5f);
                    wallLm.M11 = newRight.X; wallLm.M12 = newRight.Y; wallLm.M13 = newRight.Z;
                    wallLm.M21 = newUp.X;    wallLm.M22 = newUp.Y;    wallLm.M23 = newUp.Z;
                    e.Transform.LocalMatrix = wallLm;
                }
                ImGui.TextDisabled("Move it with the gizmo. Saved with Save Chunk. Ctrl+D to duplicate\n" +
                                    "(a new independent entry/exit point to the same target level).");

                ImGui.Separator();
                if (ImGui.Button("Flip Load Wall Normal##flipwall", new Vector2(-1f, 0f)))
                {
                    wallLm.M11 = -wallLm.M11; wallLm.M12 = -wallLm.M12; wallLm.M13 = -wallLm.M13;
                    e.Transform.LocalMatrix = wallLm;
                    _browser.Log($"Flipped Load Wall normal for '{loadWall.Source.Path}'. Rectangle unchanged, only its facing reversed. Save Chunk + Build ISO to test the crossing.");
                }
                if (ImGui.IsItemHovered())
                    MaybeTooltip("Reverses which way the wall FACES (its normal). The game uses the\n" +
                                 "facing to decide crossing direction -- a wrong-facing wall freezes\n" +
                                 "you in place with no control. If a generated/added wall won't\n" +
                                 "transition, flip it. TEST: flip a REAL working wall -- if that\n" +
                                 "freezes too, the normal is confirmed as the cause. Save Chunk + Build.");

                ImGui.Separator();
                if (ImGui.Button("Add Bounding Box##addloadzonebox", new Vector2(-1f, 0f)))
                    AddLoadZoneBox(e, loadWall);
                if (ImGui.IsItemHovered())
                    MaybeTooltip("Adds a real pre-load trigger volume (separate from the wall\n" +
                                 "crossing plane above) to THIS SAME link — no new link entry, so\n" +
                                 "this is safe to add several of (chain into an L/U shape by\n" +
                                 "dragging each one with the Move gizmo). Save Chunk to persist.");
            }
        }

        var zoneBox = e.Get<CrashEngine.Importer.LoadZoneBoxMarker>();
        if (zoneBox is not null && e.Transform.LocalMatrix is { } boxLm)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Pre-Load Box##loadzonebox", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var bRight = new Vector3(boxLm.M11, boxLm.M12, boxLm.M13);
                var bUp    = new Vector3(boxLm.M21, boxLm.M22, boxLm.M23);
                var bFwd   = new Vector3(boxLm.M31, boxLm.M32, boxLm.M33);
                float sx = bRight.Length() * 2f, sy = bUp.Length() * 2f, sz = bFwd.Length() * 2f;

                ImGui.SetNextItemWidth(-1f);
                bool sxChanged = ImGui.DragFloat("Size X##loadzonebox", ref sx, 0.1f, 0.1f, 2000f);
                ImGui.SetNextItemWidth(-1f);
                bool syChanged = ImGui.DragFloat("Size Y##loadzonebox", ref sy, 0.1f, 0.1f, 2000f);
                ImGui.SetNextItemWidth(-1f);
                bool szChanged = ImGui.DragFloat("Size Z##loadzonebox", ref sz, 0.1f, 0.1f, 2000f);
                if (sxChanged || syChanged || szChanged)
                {
                    var nr = (bRight.LengthSquared() > 1e-9f ? Vector3.Normalize(bRight) : Vector3.UnitX) * (sx * 0.5f);
                    var nu = (bUp.LengthSquared()    > 1e-9f ? Vector3.Normalize(bUp)    : Vector3.UnitY) * (sy * 0.5f);
                    var nf = (bFwd.LengthSquared()   > 1e-9f ? Vector3.Normalize(bFwd)   : Vector3.UnitZ) * (sz * 0.5f);
                    boxLm.M11 = nr.X; boxLm.M12 = nr.Y; boxLm.M13 = nr.Z;
                    boxLm.M21 = nu.X; boxLm.M22 = nu.Y; boxLm.M23 = nu.Z;
                    boxLm.M31 = nf.X; boxLm.M32 = nf.Y; boxLm.M33 = nf.Z;
                    e.Transform.LocalMatrix = boxLm;
                }
                ImGui.TextDisabled("Move it with the gizmo. Saved with Save Chunk.");

                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
                if (ImGui.Button("Delete Box##delloadzonebox", new Vector2(-1f, 0f)))
                {
                    zoneBox.Link.ChunkLinksCollisionData.Remove(zoneBox.Box);
                    if (e.Parent is not null) e.Parent.RemoveChild(e);
                    _selectedSet.Remove(e);
                    if (_selected == e) _selected = null;
                }
                ImGui.PopStyleColor();
                if (ImGui.IsItemHovered())
                    MaybeTooltip("Removes this box from the link's pre-load zone. Not on the Undo\n" +
                                 "stack (unlike Add) — Reload Level from Disc undoes it before Save.");
            }
        }

        var camMarker = e.Get<CrashEngine.Importer.CameraMarker>();
        if (camMarker is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Camera Data##cam", ImGuiTreeNodeFlags.DefaultOpen))
            {
                var camData = camMarker.Source;
                ImGui.TextDisabled("Position/Rotation/Scale: see Transform above.");

                static string CamTypeName(ITwinCamera.CameraType t) => t switch
                {
                    ITwinCamera.CameraType.Null => "None",
                    ITwinCamera.CameraType.BossCamera => "Boss",
                    ITwinCamera.CameraType.CameraPoint => "Point",
                    ITwinCamera.CameraType.CameraLine => "Line",
                    ITwinCamera.CameraType.CameraPath => "Path",
                    ITwinCamera.CameraType.CameraSpline => "Spline",
                    ITwinCamera.CameraType.CameraSub1C09 => "Unk1C09",
                    ITwinCamera.CameraType.CameraPoint2 => "Point2",
                    ITwinCamera.CameraType.CameraSub1C0C => "Unk1C0C",
                    ITwinCamera.CameraType.CameraLine2 => "Line2",
                    ITwinCamera.CameraType.CameraZone => "Zone",
                    _ => $"Unk (0x{(uint)t:X})",
                };
                ImGui.TextDisabled($"Cam Item 1: {CamTypeName(camData.TypeIndex1)}    Cam Item 2: {CamTypeName(camData.TypeIndex2)}");

                ImGui.SetNextItemWidth(-1f);
                int camByte = camData.UnkByte;
                if (ImGui.DragInt("Unk Byte##camData", ref camByte, 1f, 0, 255)) camData.UnkByte = (byte)camByte;
                ImGui.SetNextItemWidth(-1f);
                int camShort = camData.UnkShort;
                if (ImGui.DragInt("Unk Short##camData", ref camShort, 1f, 0, ushort.MaxValue)) camData.UnkShort = (ushort)camShort;
                ImGui.SetNextItemWidth(-1f);
                float camFloat1 = camData.UnkFloat1;
                if (ImGui.DragFloat("Unk Float1##camData", ref camFloat1, 0.01f)) camData.UnkFloat1 = camFloat1;

                ImGui.Separator();
                ImGui.TextDisabled("Flags (CamHeader) -- labels confirmed real, everything else is a raw bit:");

                uint header = camData.CameraHeader;
                bool Bit(int i) => (header & (1u << i)) != 0;
                void SetBit(int i, bool v) { if (v) header |= (1u << i); else header &= ~(1u << i); }

                bool FlagBox(int i, string label)
                {
                    bool v = Bit(i);
                    if (ImGui.Checkbox($"{label}##camflag{i}", ref v)) { SetBit(i, v); camData.CameraHeader = header; return true; }
                    return false;
                }

                FlagBox(0, "Flag 0");
                ImGui.SameLine(); FlagBox(1, "Flag 1");
                ImGui.SameLine(); FlagBox(2, "Flag 2");
                if (Bit(2))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    int u3 = unchecked((int)camData.UnkInt3);
                    if (ImGui.DragInt("Unk Int3##camData", ref u3)) camData.UnkInt3 = unchecked((uint)u3);
                    ImGui.SetNextItemWidth(-1f);
                    int u4 = unchecked((int)camData.UnkInt4);
                    if (ImGui.DragInt("Unk Int4##camData", ref u4)) camData.UnkInt4 = unchecked((uint)u4);
                    ImGui.Unindent();
                }

                FlagBox(3, "Distance");
                if (Bit(3))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    float f4 = camData.UnkFloat4;
                    if (ImGui.DragFloat("Distance Min##camData", ref f4, 0.05f)) camData.UnkFloat4 = f4;
                    ImGui.SetNextItemWidth(-1f);
                    float f5 = camData.UnkFloat5;
                    if (ImGui.DragFloat("Distance Max##camData", ref f5, 0.05f)) camData.UnkFloat5 = f5;
                    ImGui.Unindent();
                }

                FlagBox(4, "Flag 4");
                ImGui.SameLine(); FlagBox(5, "Flag 5");

                FlagBox(6, "Angle Around Focus");
                if (Bit(6))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    int i5 = unchecked((int)camData.UnkInt5);
                    if (ImGui.DragInt("Angle Min##camData", ref i5)) camData.UnkInt5 = unchecked((uint)i5);
                    ImGui.SetNextItemWidth(-1f);
                    int i6 = unchecked((int)camData.UnkInt6);
                    if (ImGui.DragInt("Angle Max##camData", ref i6)) camData.UnkInt6 = unchecked((uint)i6);
                    ImGui.Unindent();
                }

                FlagBox(7, "Field of View");
                if (Bit(7))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    int i1 = unchecked((int)camData.UnkInt1);
                    if (ImGui.DragInt("FOV Min##camData", ref i1)) camData.UnkInt1 = unchecked((uint)i1);
                    ImGui.SetNextItemWidth(-1f);
                    int i2 = unchecked((int)camData.UnkInt2);
                    if (ImGui.DragInt("FOV Max##camData", ref i2)) camData.UnkInt2 = unchecked((uint)i2);
                    ImGui.Unindent();
                }

                FlagBox(8, "Local XYZ Offset From Focus");
                bool bit28 = Bit(28);
                if (Bit(8) || bit28)
                {
                    ImGui.Indent();
                    ImGui.SetNextItemWidth(-1f);
                    var v1 = new Vector3(camData.UnkVector1.X, camData.UnkVector1.Y, camData.UnkVector1.Z);
                    if (ImGui.DragFloat3("Offset 1 (XYZ)##camData", ref v1, 0.05f))
                    { camData.UnkVector1.X = v1.X; camData.UnkVector1.Y = v1.Y; camData.UnkVector1.Z = v1.Z; }
                    if (bit28)
                    {
                        ImGui.SetNextItemWidth(-1f);
                        float w1 = camData.UnkVector1.W;
                        if (ImGui.DragFloat("Offset 1 W##camData", ref w1, 0.05f)) camData.UnkVector1.W = w1;
                    }
                    ImGui.SetNextItemWidth(-1f);
                    var v2 = new Vector3(camData.UnkVector2.X, camData.UnkVector2.Y, camData.UnkVector2.Z);
                    if (ImGui.DragFloat3("Offset 2 (XYZ)##camData", ref v2, 0.05f))
                    { camData.UnkVector2.X = v2.X; camData.UnkVector2.Y = v2.Y; camData.UnkVector2.Z = v2.Z; }
                    if (bit28)
                    {
                        ImGui.SetNextItemWidth(-1f);
                        float w2 = camData.UnkVector2.W;
                        if (ImGui.DragFloat("Offset 2 W##camData", ref w2, 0.05f)) camData.UnkVector2.W = w2;
                    }
                    ImGui.Unindent();
                }

                bool bit9 = Bit(9), bit10 = Bit(10);
                FlagBox(9, "Flag 9");
                ImGui.SameLine(); FlagBox(10, "Flag 10");
                if (bit9 || bit10)
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    float f2 = camData.UnkFloat2;
                    if (ImGui.DragFloat("Unk Float2##camData", ref f2, 0.05f)) camData.UnkFloat2 = f2;
                    ImGui.SetNextItemWidth(-1f);
                    float f3 = camData.UnkFloat3;
                    if (ImGui.DragFloat("Unk Float3##camData", ref f3, 0.05f)) camData.UnkFloat3 = f3;
                    ImGui.Unindent();
                }

                FlagBox(11, "Lock Control");

                FlagBox(12, "Flag 12");
                if (Bit(12))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    float f6 = camData.UnkFloat6;
                    if (ImGui.DragFloat("Unk Float6##camData", ref f6, 0.05f)) camData.UnkFloat6 = f6;
                    ImGui.Unindent();
                }

                FlagBox(13, "Movement Speed");
                if (Bit(13))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    float f7 = camData.UnkFloat7;
                    if (ImGui.DragFloat("Speed##camData", ref f7, 0.05f)) camData.UnkFloat7 = f7;
                    ImGui.Unindent();
                }

                FlagBox(14, "Flag 14");

                FlagBox(15, "Flag 15");
                if (Bit(15))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    int i7 = unchecked((int)camData.UnkInt7);
                    if (ImGui.DragInt("Unk Int7##camData", ref i7)) camData.UnkInt7 = unchecked((uint)i7);
                    ImGui.Unindent();
                }

                FlagBox(16, "Flag 16");
                if (Bit(16))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    int i8 = unchecked((int)camData.UnkInt8);
                    if (ImGui.DragInt("Unk Int8##camData", ref i8)) camData.UnkInt8 = unchecked((uint)i8);
                    ImGui.Unindent();
                }

                FlagBox(17, "Flag 17");
                if (Bit(17))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    int i9 = unchecked((int)camData.UnkInt9);
                    if (ImGui.DragInt("Unk Int9##camData", ref i9)) camData.UnkInt9 = unchecked((uint)i9);
                    ImGui.Unindent();
                }

                FlagBox(18, "Flag 18");
                if (Bit(18))
                {
                    ImGui.Indent(); ImGui.SetNextItemWidth(-1f);
                    float f8 = camData.UnkFloat8;
                    if (ImGui.DragFloat("Unk Float8##camData", ref f8, 0.05f)) camData.UnkFloat8 = f8;
                    ImGui.Unindent();
                }

                FlagBox(19, "Lock Control And Look Up");
                FlagBox(20, "Lock Vertical Angle");
                FlagBox(21, "Unused Fixed Point? (uncertain)");

                if (ImGui.TreeNode("Other flags (no confirmed label)##camotherflags"))
                {
                    foreach (int i in new[] { 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 })
                    {
                        FlagBox(i, $"Flag {i}" + (i == 28 ? " (Offset W, see flag 8 above)" : ""));
                        if (i != 31) ImGui.SameLine();
                    }
                    ImGui.TreePop();
                }

                ImGui.Spacing();
                ImGui.TextDisabled($"CamHeader raw: 0x{camData.CameraHeader:X8}");
            }
        }

        var posMarker = e.Get<CrashEngine.Importer.PositionMarker>();
        if (posMarker is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Position Data##pos", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("XYZ: see Transform above.");
                ImGui.SetNextItemWidth(-1f);
                float w = posMarker.Source.Position.W;
                if (ImGui.DragFloat("W (unknown)##posData", ref w, 0.05f)) posMarker.Source.Position.W = w;
            }
        }

        var aiPosMarker = e.Get<CrashEngine.Importer.AiPositionMarker>();
        if (aiPosMarker is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("AI Position Data##aipos", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("XYZ: see Transform above.");
                ImGui.SetNextItemWidth(-1f);
                float w = aiPosMarker.Source.Position.W;
                if (ImGui.DragFloat("W (unknown)##aiPosData", ref w, 0.05f)) aiPosMarker.Source.Position.W = w;
                ImGui.SetNextItemWidth(-1f);
                int unkShort = aiPosMarker.Source.UnkShort;
                if (ImGui.DragInt("Unk Short##aiPosData", ref unkShort, 1f, 0, ushort.MaxValue))
                    aiPosMarker.Source.UnkShort = (ushort)unkShort;
            }
        }

        // Amedo 2026-09-19
        var lightMarker = e.Get<CrashEngine.Importer.SceneryLightMarker>();
        if (lightMarker is not null && lightMarker.Source is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader((lightMarker.IsNegative ? "Negative Light Data##lightdata" : "Point Light Data##lightdata"), ImGuiTreeNodeFlags.DefaultOpen))
            {
                var lt = lightMarker.Source;
                ImGui.TextDisabled("Position: drag the gizmo above to move (saved on Save Chunk).");
                var col = new Vector3(lt.Color.X, lt.Color.Y, lt.Color.Z);
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.ColorEdit3("Color##lmc", ref col)) { lt.Color.X = col.X; lt.Color.Y = col.Y; lt.Color.Z = col.Z; }
                float r = lt.Radius;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.DragFloat("Radius##lmr", ref r, 0.05f, 0f, float.MaxValue)) lt.Radius = r;
                if (lightMarker.IsNegative && lt is NegativeLight nlt)
                {
                    ImGui.SetNextItemWidth(-1f);
                    float nf1 = nlt.UnkFloat1;
                    if (ImGui.DragFloat("Unk Float 1##lmn1", ref nf1, 0.05f)) nlt.UnkFloat1 = nf1;
                    ImGui.SetNextItemWidth(-1f);
                    float nf2 = nlt.UnkFloat2;
                    if (ImGui.DragFloat("Unk Float 2##lmn2", ref nf2, 0.05f)) nlt.UnkFloat2 = nf2;
                }
                ImGui.TextDisabled("Affects the real game only (editor preview is ambient+directional).");
            }
        }

        var trigMarker = e.Get<CrashEngine.Importer.TriggerMarker>();
        if (trigMarker is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Trigger Settings##trig", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("Position/Rotation/Scale: see Transform above.");

                var twinTrig = trigMarker.Source.Trigger;
                if (ImGui.TreeNode("Activated By##trigActivator"))
                {
                    foreach (var flag in Enum.GetValues<Twinsanity.TwinsanityInterchange.Enumerations.Enums.TriggerActivatorObjects>())
                    {
                        bool on = twinTrig.ObjectActivatorMask.HasFlag(flag);
                        if (ImGui.Checkbox($"{flag}##trigActivator_{flag}", ref on))
                        {
                            twinTrig.ObjectActivatorMask = on
                                ? twinTrig.ObjectActivatorMask | flag
                                : twinTrig.ObjectActivatorMask & ~flag;
                        }
                    }
                    ImGui.TreePop();
                }

                ImGui.SetNextItemWidth(-1f);
                float trigUnkFloat = twinTrig.UnkFloat;
                if (ImGui.DragFloat("Unk Float##trigUnkFloat", ref trigUnkFloat, 0.05f)) twinTrig.UnkFloat = trigUnkFloat;

                if (ImGui.TreeNode($"Linked Instances ({twinTrig.Instances.Count})##trigInstances"))
                {
                    int removeIdx = -1;
                    for (int i = 0; i < twinTrig.Instances.Count; i++)
                    {
                        ImGui.TextDisabled($"0x{twinTrig.Instances[i]:X4}");
                        ImGui.SameLine();
                        if (ImGui.SmallButton($"Remove##trigInstRemove{i}")) removeIdx = i;
                    }
                    if (removeIdx >= 0) twinTrig.Instances.RemoveAt(removeIdx);

                    ImGui.SetNextItemWidth(120f);
                    ImGui.InputText("Instance id (hex)##trigInstAdd", ref _trigInstanceAddBuf, 8);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Add##trigInstAddBtn") &&
                        ushort.TryParse(_trigInstanceAddBuf, System.Globalization.NumberStyles.HexNumber, null, out var newInstId))
                    {
                        twinTrig.Instances.Add(newInstId);
                        _trigInstanceAddBuf = "";
                    }
                    ImGui.TreePop();
                }

                if (ImGui.TreeNode("AgentLab Messages (sent on trigger)##trigMessages"))
                {
                    for (int i = 0; i < trigMarker.Source.TriggerMessages.Length; i++)
                    {
                        ImGui.SetNextItemWidth(120f);
                        int msg = trigMarker.Source.TriggerMessages[i];
                        if (ImGui.DragInt($"Message {i}##trigMsg{i}", ref msg, 1f, 0, ushort.MaxValue))
                            trigMarker.Source.TriggerMessages[i] = (ushort)Math.Clamp(msg, 0, ushort.MaxValue);
                    }
                    ImGui.TreePop();
                }
            }
        }

        var se = e.Get<StealthEnemy>();
        if (se is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Enemy##se", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled($"State:      {se.State}");
                ImGui.TextDisabled($"Detection:  {se.DetectionPct:F1}%%");
            }
        }

        var inst = e.Get<InstanceData>();
        if (inst is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Instance##inst", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled($"Object ID:    0x{inst.ObjectId:X4}");
                ImGui.TextDisabled($"State Flags:  0x{inst.Source.StateFlags:X8}");

                if (ImGui.CollapsingHeader("Instance Flags (this placement only)##instflags"))
                {
                    var instFlagsEnum = (Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState)inst.Source.StateFlags;
                    foreach (var flag in Enum.GetValues<Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState>())
                    {
                        if (flag == 0) continue;
                        bool on = instFlagsEnum.HasFlag(flag);
                        if (ImGui.Checkbox($"{flag}##instflag_{flag}", ref on))
                        {
                            instFlagsEnum = on ? instFlagsEnum | flag : instFlagsEnum & ~flag;
                            inst.Source.StateFlags = (uint)instFlagsEnum;
                        }
                    }
                    ImGui.TextDisabled("Save Chunk to persist, then Build ISO + test.");
                }

                if (inst.Source.Positions.Count > 0 || inst.Source.Paths.Count > 0)
                {
                    if (ImGui.CollapsingHeader("Referenced Positions/Paths##instrefs", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        var chunkRootForRefs = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
                        Entity? FindPositionEntity(ushort pid) => chunkRootForRefs is null ? null :
                            AllEntities(chunkRootForRefs).FirstOrDefault(x =>
                                x.Get<CrashEngine.Importer.PositionMarker>()?.Source.GetID() == pid);

                        if (inst.Source.Positions.Count > 0)
                        {
                            ImGui.TextDisabled($"Positions ({inst.Source.Positions.Count}), in order:");
                            for (int i = 0; i < inst.Source.Positions.Count; i++)
                            {
                                ushort pid = inst.Source.Positions[i];
                                var target = FindPositionEntity(pid);
                                ImGui.TextUnformatted($"  [{i}] Position_{pid:X4}" + (target is null ? "  (not found in this chunk)" : ""));
                                if (target is not null)
                                {
                                    ImGui.SameLine();
                                    if (ImGui.SmallButton($"Select##posref_{i}")) SelectClicked(target, false);
                                }
                            }
                        }
                        if (inst.Source.Paths.Count > 0)
                        {
                            ImGui.TextDisabled($"Paths ({inst.Source.Paths.Count}), in order:");
                            for (int i = 0; i < inst.Source.Paths.Count; i++)
                                ImGui.TextUnformatted($"  [{i}] Path_{inst.Source.Paths[i]:X4}  (Path markers not loaded in the scene yet)");
                        }
                    }
                }

                if (inst.Source.ParamList2.Count > 0)
                {
                    if (ImGui.CollapsingHeader("Character Settings##paramlist2"))
                    {
                        if (ImGui.Button("Reset to Original##resetparamlist2"))
                            ResetParamList2ToOriginal(inst);
                        for (int p = 0; p < inst.Source.ParamList2.Count; p++)
                        {
                            string plabel = ParamList2Labels.TryGetValue(p, out var name) ? $"[{p}] {name}" : $"Param[{p}]";
                            float pval = inst.Source.ParamList2[p];
                            if (ImGui.InputFloat($"{plabel}##paramlist2_{p}", ref pval))
                                inst.Source.ParamList2[p] = pval;
                        }
                    }
                }

                if (inst.Source.ParamList1.Count > 0)
                {
                    if (ImGui.CollapsingHeader("Params (advanced)##paramlist13"))
                    {
                        ImGui.TextDisabled("Raw, mostly-unidentified per-instance parameters (hex).\n" +
                                            "Meaning varies by object type — edit only if you know\n" +
                                            "what a specific index does for this instance's own type.");
                        ImGui.Text("ParamList1");
                        for (int p = 0; p < inst.Source.ParamList1.Count; p++)
                        {
                            int pval1 = unchecked((int)inst.Source.ParamList1[p]);
                            if (ImGui.InputInt($"[{p}]##paramlist1_{p}", ref pval1, 0, 0, ImGuiInputTextFlags.CharsHexadecimal))
                                inst.Source.ParamList1[p] = unchecked((uint)pval1);
                        }
                    }
                }

                {
                    var scriptChunkSrc = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
                    var objSecForInsp = scriptChunkSrc?.Rm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                        ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
                    var behSecForInsp = scriptChunkSrc?.Rm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                        ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
                    var localObjForInsp = objSecForInsp?.GetItem<PS2AnyObject>(inst.ObjectId);
                    var globalObjSecForInsp = scriptChunkSrc?.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                        ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
                    var globalObjForInsp = globalObjSecForInsp?.GetItem<PS2AnyObject>(inst.ObjectId);
                    var globalBehSecForInsp = scriptChunkSrc?.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                        ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);

                    var thisObj = globalObjForInsp ?? localObjForInsp;
                    bool editingGlobal = globalObjForInsp is not null;
                    bool hasCollision = localObjForInsp is not null && globalObjForInsp is not null;

                    const ushort CrateOutlineStarterId = 0x0028;
                    ushort rawSpawnScriptId = inst.Source.OnSpawnHeaderScriptID;
                    bool spawnStateHandled = false;
                    if (thisObj?.Type == Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject.ObjectType.Crate &&
                        (rawSpawnScriptId == 0xFFFF || rawSpawnScriptId == CrateOutlineStarterId))
                    {
                        spawnStateHandled = true;
                        bool isOutline = rawSpawnScriptId == CrateOutlineStarterId;
                        if (ImGui.Checkbox("Starts as Outline (ghost, waits for a linked switch)##crateoutline", ref isOutline))
                        {
                            ushort newId = isOutline ? CrateOutlineStarterId : (ushort)0xFFFF;
                            inst.Source.OnSpawnHeaderScriptID = newId;
                            inst.OnSpawnScriptId = newId;
                        }
                        if (isOutline)
                            ImGui.TextDisabled("Raw Instance Script 0x0028 (COM_GENERIC_CRATE_OUTLINE, GLOBAL —\n" +
                                                "same constant works for any crate, in any level).");
                    }
                    if (!spawnStateHandled && rawSpawnScriptId != 0xFFFF)
                    {
                        if (!_spawnScriptInfoCache.TryGetValue(rawSpawnScriptId, out var spawnInfo))
                        {
                            spawnInfo = scriptChunkSrc is not null
                                ? ScriptDumper.ResolveSpawnScript(scriptChunkSrc.Rm2, scriptChunkSrc.GlobalRm2, rawSpawnScriptId)
                                : null;
                            _spawnScriptInfoCache[rawSpawnScriptId] = spawnInfo;
                        }

                        if (spawnInfo is null)
                        {
                            ImGui.TextColored(new Vector4(1f, 0.55f, 0.3f, 1f),
                                $"Spawn state: unresolved (raw id 0x{rawSpawnScriptId:X4}) — couldn't find or\n" +
                                "decode the real script this points to. Not necessarily the normal state.");
                        }
                        else
                        {
                            ImGui.TextColored(new Vector4(1f, 0.75f, 0.3f, 1f), $"Spawn state: {spawnInfo.FriendlyName}");
                            ImGui.TextDisabled($"({spawnInfo.GraphName}, id 0x{spawnInfo.ResolvedGraphId:X4} " +
                                               $"{(spawnInfo.IsGlobal ? "GLOBAL" : "LOCAL")}" +
                                               (spawnInfo.ChainDepth > 0 ? $", via {spawnInfo.ChainDepth} Starter hop(s)" : "") +
                                               $" — raw Instance Script 0x{rawSpawnScriptId:X4})");
                            if (ImGui.CollapsingHeader("Script##spawnscript"))
                            {
                                ImGui.BeginChild("##scriptText", new Vector2(-1f, 200f), ImGuiChildFlags.Border);
                                ImGui.TextUnformatted(spawnInfo.DecompiledText);
                                ImGui.EndChild();
                            }
                        }
                    }

                    if (thisObj is not null)
                    {
                        if (ImGui.CollapsingHeader("Object Type##objtype"))
                        {
                            ImGui.TextDisabled(editingGlobal
                                ? "Editing the GLOBAL (Startup\\Default.rm2) object definition."
                                : "Changes apply to EVERY placed instance of this exact object type.");
                            var typeValues = Enum.GetValues<Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject.ObjectType>();
                            int curIdx = Array.IndexOf(typeValues, thisObj.Type);
                            string[] typeNames = typeValues.Select(t => t.ToString()).ToArray();
                            if (ImGui.Combo("Type##objtypecombo", ref curIdx, typeNames, typeNames.Length))
                            {
                                thisObj.Type = typeValues[curIdx];
                                if (editingGlobal && scriptChunkSrc is not null)
                                    scriptChunkSrc.GlobalRm2Dirty = true;
                            }
                            ImGui.TextDisabled("Save Chunk to persist, then Build ISO + test.");
                        }
                    }

                    if (thisObj is not null && thisObj.HasInstanceProperties)
                    {
                        if (ImGui.CollapsingHeader("Object Flags (shared by every instance of this type)##objflags"))
                        {
                            ImGui.TextDisabled(editingGlobal
                                ? "Editing the GLOBAL (Startup\\Default.rm2) object definition —\n" +
                                  "affects EVERY level that uses this object type, not just this chunk."
                                : "Changes apply to EVERY placed instance of this exact\n" +
                                  "object type in this chunk, not just the one selected.");
                            if (hasCollision)
                                ImGui.TextColored(new Vector4(1f, 0.75f, 0.2f, 1f),
                                    $"Note: this ObjectId ALSO has a separate LOCAL entry ('{localObjForInsp!.Name}')\n" +
                                    "in this chunk — editing GLOBAL here since local edits were confirmed\n" +
                                    "to have no in-game effect for this kind of shared object.");
                            var flagNames = Enum.GetValues<Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState>();
                            foreach (var flag in flagNames)
                            {
                                if (flag == 0) continue;
                                bool on = thisObj.InstanceStateFlags.HasFlag(flag);
                                if (ImGui.Checkbox($"{flag}##objflag_{flag}", ref on))
                                {
                                    thisObj.InstanceStateFlags = on
                                        ? thisObj.InstanceStateFlags | flag
                                        : thisObj.InstanceStateFlags & ~flag;
                                    if (editingGlobal && scriptChunkSrc is not null)
                                        scriptChunkSrc.GlobalRm2Dirty = true;
                                }
                            }
                            ImGui.TextDisabled("Save Chunk to persist, then Build ISO + test.");
                        }
                    }

                    if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)TntCountdownBehaviourId) &&
                        scriptChunkSrc is not null)
                    {
                        var globalBehSecForTnt = scriptChunkSrc.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
                        if (ImGui.CollapsingHeader("TNT Countdown##tntcountdown", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            DrawTntCountdownSpeed(scriptChunkSrc, globalBehSecForTnt);
                        }
                    }

                    if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)NitroCrateBehaviourId) &&
                        scriptChunkSrc is not null)
                    {
                        var globalBehSecForNitro = scriptChunkSrc.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
                        if (ImGui.CollapsingHeader("Nitro Blast Radius##nitroblastradius", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            DrawNitroBlastRadius(scriptChunkSrc, globalBehSecForNitro);
                        }
                    }

                    if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)IronSpringCrateLandedOnId) &&
                        scriptChunkSrc is not null)
                    {
                        var globalBehSecForSpring = scriptChunkSrc.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
                        if (ImGui.CollapsingHeader("Spring Crate Bounce##springbouncehdr", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            DrawSpringCrateBounce(scriptChunkSrc, globalBehSecForSpring);
                        }
                    }

                    if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)EarthWormSquashLaunchId) &&
                        behSecForInsp is not null)
                    {
                        if (ImGui.CollapsingHeader("Earth Worm Squash Launch##wormlaunchhdr", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            DrawWormLaunchPower(behSecForInsp);
                        }
                    }

                    if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)RigidCannonActivatedId) &&
                        behSecForInsp is not null)
                    {
                        if (ImGui.CollapsingHeader("Cannon Shot Power##cannonpowerhdr", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            DrawCannonShotPower(behSecForInsp);
                        }
                    }

                    if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)GenericCreatureDamagedJumpedOnId) &&
                        behSecForInsp is not null)
                    {
                        if (ImGui.CollapsingHeader("Enemy Stomp##enemystomphdr", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            DrawEnemyStomp(behSecForInsp);
                        }
                    }

                    if (thisObj is not null && thisObj.RefBehaviours.Contains((ushort)DjDefaultBehaviourId))
                    {
                        if (ImGui.CollapsingHeader("DJ Music##djmusichdr", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            if (inst.Source.ParamList3.Count == 0)
                            {
                                ImGui.TextDisabled("This DJ instance has no ParamList3 (IVars) data at all — nothing to show.");
                            }
                            else
                            {
                                uint djCurrentId = inst.Source.ParamList3[0];
                                DrawLevelMusic(djCurrentId, "DJ instance's own IVars[0] (ParamList3)",
                                    newId =>
                                    {
                                        inst.Source.ParamList3[0] = newId;
                                        _musicLabel = MusicNames.TryGetValue(newId, out var name)
                                            ? $"{name} (id {newId})" : $"id {newId} (undocumented — not in the reference tool's own name table)";
                                        _browser.Log($"DJ Music: set this DJ instance's IVars[0] to {_musicLabel}. Save Chunk to persist.");
                                    },
                                    () => ResetDjMusicParamToOriginal(inst));
                            }
                        }
                    }

                    if (thisObj is not null && (thisObj.RefSounds.Count > 0 || thisObj.SoundSlots.Any(s => s != 0xFFFF)))
                    {
                        if (ImGui.CollapsingHeader("Sounds##soundshdr", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            // Amedo 2026-09-21
                            float previewVolPct = _previewVolume * 100f;
                            ImGui.SetNextItemWidth(-1f);
                            if (ImGui.SliderFloat("Preview Volume##previewvol", ref previewVolPct, 0f, 100f, "%.0f%%"))
                                _previewVolume = Math.Clamp(previewVolPct / 100f, 0f, 1f);
                            if (ImGui.IsItemHovered())
                                MaybeTooltip("Editor-only listening level for the Play buttons here and Music\n" +
                                                  "Preview — scales the previewed audio down before playback so it's\n" +
                                                  "comfortable on top of your system volume (does NOT change the\n" +
                                                  "sound's real in-game Volume/gain below). Lower it if previews are\n" +
                                                  "too loud.");

                            var codeSecLive = scriptChunkSrc?.Rm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION);
                            var soundIds = thisObj.RefSounds.Concat(thisObj.SoundSlots)
                                .Where(sid => sid != 0xFFFF).Distinct().ToList();
                            if (soundIds.Count == 0)
                            {
                                ImGui.TextDisabled("Referenced slot(s) are all empty (0xFFFF) — nothing to show.");
                            }
                            foreach (var sid in soundIds)
                            {
                                var (snd, sectionId, label) = FindSoundAnySection(codeSecLive, sid);
                                ImGui.PushID((int)sid);
                                if (snd is null)
                                {
                                    ImGui.TextDisabled($"Sound id 0x{sid:X} — not found in this level's own sound section (checked SFX + all language/dialogue sections).");
                                }
                                else
                                {
                                    double durationSeconds = snd.Sound.Length / 16.0 * 28.0 / snd.GetFreq();
                                    string tag = label == "SFX" ? "" : $" [{label} dialogue]";
                                    ImGui.Text($"Sound 0x{sid:X} (ID {sid}){tag} — {snd.GetFreq()}Hz, {FormatMinSec(durationSeconds)}");
                                    ImGui.SameLine();
                                    if (ImGui.SmallButton("Play##sndplay")) PlaySoundEffect(snd);
                                    ImGui.SameLine();
                                    if (ImGui.SmallButton("Replace...##sndreplace")) ReplaceSoundEffectAudio(snd, sid, _rm2, sectionId);
                                    ImGui.SameLine();
                                    if (ImGui.SmallButton("Reset Original##sndreset")) ResetSoundEffectToOriginal(snd, sid, _rm2, sectionId);

                                    float gain = GetPersistedSoundEffectGain(snd, sid, _rm2, sectionId);
                                    ImGui.SetNextItemWidth(150f);
                                    if (ImGui.SliderFloat("Volume (gain)##sndgain", ref gain, 0.1f, 3f, "%.2fx"))
                                        ApplySoundEffectGain(snd, sid, gain, _rm2, sectionId);
                                    if (ImGui.IsItemHovered())
                                        MaybeTooltip("Digital gain applied to this sound's own waveform before re-encoding — " +
                                                     "always computed from the ORIGINAL audio (cached the first time you touch " +
                                                     "this slider this session), never compounds. Save Chunk to persist.");
                                }
                                ImGui.PopID();
                            }
                            if (_musicLastError is not null)
                                ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f), $"Error: {_musicLastError}");
                        }
                    }

                    if (thisObj is not null && thisObj.RefOGIs.Count > 0)
                    {
                        if (ImGui.CollapsingHeader($"OGI ({thisObj.RefOGIs.Count})##objogihdr", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            var ogiChunkSrc = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
                            uint? defaultOgiId = ogiChunkSrc?.MeshTables is not null
                                ? MeshDecoder.ResolveDefaultOgiId(ogiChunkSrc.MeshTables, inst.ObjectId)
                                : null;
                            bool previewingThisInst = _ogiPreviewInstance == inst.Source.GetID();
                            if (!previewingThisInst)
                                ImGui.TextDisabled("\"(currently shown)\" = what this engine is rendering for\n" +
                                                    "this instance right now — its best available signal for\n" +
                                                    "what the real game shows, NOT a guaranteed match (see code\n" +
                                                    "comment). Preview never changes what's actually saved.");

                            foreach (var ogiId in thisObj.RefOGIs)
                            {
                                ImGui.PushID((int)ogiId);
                                var meshParts = ogiChunkSrc?.MeshTables is not null
                                    ? MeshDecoder.GetObjectMeshPartsByOgi(ogiChunkSrc.MeshTables, ogiId)
                                    : null;
                                string tag = ogiId == defaultOgiId ? " (currently shown)" : "";
                                if (meshParts is null || meshParts.Count == 0)
                                {
                                    ImGui.TextDisabled($"OGI 0x{ogiId:X4}{tag} — no renderable parts (script-only / manager OGI).");
                                }
                                else
                                {
                                    bool thisOneActive = previewingThisInst && _ogiPreviewOgiId == ogiId;
                                    ImGui.Text($"OGI 0x{ogiId:X4}{tag} — {meshParts.Count} part(s){(thisOneActive ? "  [PREVIEWING]" : "")}");
                                    ImGui.SameLine();
                                    if (ImGui.SmallButton("Preview##ogiprev") && ogiChunkSrc?.MeshTables is not null &&
                                        MeshDecoder.PreviewOgiOnInstance(Engine.Instance.GL, ogiChunkSrc.MeshTables, e, ogiId))
                                    {
                                        _ogiPreviewInstance = inst.Source.GetID();
                                        _ogiPreviewOgiId = ogiId;
                                        _browser.Log($"OGI preview: showing OGI 0x{ogiId:X4} on this instance only (in-memory, not saved).");
                                    }
                                }
                                ImGui.PopID();
                            }
                            if (previewingThisInst)
                            {
                                if (ImGui.Button("Reset to Currently-Shown##ogipreviewreset") && ogiChunkSrc?.MeshTables is not null)
                                {
                                    MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, ogiChunkSrc.MeshTables, e);
                                    _ogiPreviewInstance = null;
                                    _ogiPreviewOgiId = null;
                                }
                            }
                        }
                    }


                    uint matchingCrateBounceId = thisObj is not null
                        ? CrateLandedOnBehaviourIds.FirstOrDefault(id => thisObj.RefBehaviours.Contains((ushort)id))
                        : 0;
                    if (matchingCrateBounceId != 0 && scriptChunkSrc is not null)
                    {
                        var globalBehSecForCrateBounce = scriptChunkSrc.GlobalRm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                            ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
                        if (ImGui.CollapsingHeader("Crate Landing Bounce##cratebouncehdr", ImGuiTreeNodeFlags.DefaultOpen))
                        {
                            DrawCrateLandingBounceForId(scriptChunkSrc, globalBehSecForCrateBounce, matchingCrateBounceId);
                        }
                    }

                    var behSecForList = editingGlobal ? globalBehSecForInsp : behSecForInsp;
                    if (thisObj is not null && behSecForList is not null && thisObj.RefBehaviours.Count > 0)
                    {
                        if (ImGui.CollapsingHeader($"Behaviours ({thisObj.RefBehaviours.Count})##objbeh"))
                        {
                            DrawBehavioursSectionBody(thisObj, behSecForList, editingGlobal, scriptChunkSrc);
                        }
                    }
                }


                ImGui.Separator();
                if (ImGui.CollapsingHeader("Effects##fx"))
                    DrawEffectsBrowser();

                if (inst.LinkedPaths.Count > 0)
                    ImGui.TextDisabled($"Paths: {inst.LinkedPaths.Count}  ({string.Join(", ", inst.LinkedPaths.Select(p => $"0x{p:X4}"))})");

                ImGui.Separator();
                if (ImGui.CollapsingHeader("Conditions##cond", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    if (ImGui.IsItemHovered())
                        MaybeTooltip("Other Triggers/Instances in this level that must fire before\n" +
                                     "this one activates (they message it once their own condition\n" +
                                     "is met). Empty = no dependency -- this object runs its own\n" +
                                     "default behaviour on its own (e.g. opens on proximity).");
                    uint selfInstId = inst.Source.GetID();
                    var condSources = GetConditionSources(selfInstId, e);
                    if (condSources.Count == 0)
                    {
                        ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f),
                            "No condition set — activates on its own, unconditionally.");
                    }
                    else
                    {
                        for (int ci = 0; ci < condSources.Count; ci++)
                        {
                            var (srcEnt, kind, srcId) = condSources[ci];
                            ImGui.PushID(ci);
                            if (ImGui.Selectable($"[{kind}] {srcEnt.Name}  (0x{srcId:X4})", false,
                                    ImGuiSelectableFlags.AllowOverlap))
                            { _selected = srcEnt; _revealSelectionInTree = true; }
                            ImGui.SameLine();
                            if (ImGui.SmallButton("Remove##condrm"))
                            {
                                if (kind == "Instance") srcEnt.Get<InstanceData>()?.Source.Instances.Remove((ushort)selfInstId);
                                else srcEnt.Get<CrashEngine.Importer.TriggerMarker>()?.Source.Trigger.Instances.Remove((ushort)selfInstId);
                            }
                            ImGui.PopID();
                        }
                    }
                    if (ImGui.Button("+ Add Condition...##addcond"))
                    {
                        _conditionPickerTargetEntity = e;
                        _conditionPickerFilter = "";
                        _showConditionPicker = true;
                    }

                    if (inst.Source.ParamList3.Count > 2)
                    {
                        int required = unchecked((int)inst.Source.ParamList3[2]);
                        ImGui.SetNextItemWidth(80f);
                        if (ImGui.InputInt("Required count##condRequiredCount", ref required))
                            inst.Source.ParamList3[2] = unchecked((uint)Math.Max(0, required));
                        if (ImGui.IsItemHovered())
                            MaybeTooltip("How many of the conditions above must fire before this\n" +
                                         "object activates (ParamList3[2]) -- confirmed live: must\n" +
                                         "match the real number exactly, extra linked conditions\n" +
                                         "beyond this count are optional/alternative, not required.");

                        if (inst.ObjectId == 0x0000)
                        {
                            int startingMasks = Math.Max(0, required - 1);
                            ImGui.SetNextItemWidth(80f);
                            if (ImGui.InputInt("Starting Aku Aku Masks##crashStartMasks", ref startingMasks))
                                inst.Source.ParamList3[2] = unchecked((uint)Math.Max(1, startingMasks + 1));
                            if (ImGui.IsItemHovered())
                                MaybeTooltip("Crash-specific meaning of the SAME raw field above\n" +
                                             "(ParamList3[2] = this value + 1) -- confirmed live by\n" +
                                             "direct testing: 0 = no masks, dies in one hit; 1 = starts\n" +
                                             "with 1 Aku Aku mask; etc.");
                        }
                    }
                }

                int totalV = 0, totalF = 0, subCount = 0;
                var childRenderers = new List<CrashEngine.Importer.MeshRenderer>();
                foreach (var child in AllEntities(e).Skip(1))
                {
                    var cmr = child.Get<CrashEngine.Importer.MeshRenderer>();
                    if (cmr?.Mesh is null) continue;
                    totalV += cmr.Mesh.VertexCount;
                    totalF += cmr.Mesh.IndexCount > 0
                              ? cmr.Mesh.IndexCount / 3
                              : cmr.Mesh.VertexCount / 3;
                    childRenderers.Add(cmr);
                    subCount++;
                }

                var firstMat = childRenderers.FirstOrDefault()?.Material;

                ImGui.Separator();
                ImGui.Text("Meshes");
                if (subCount > 0)
                {
                    ImGui.TextDisabled($"Sub-meshes:  {subCount}");
                    ImGui.TextDisabled($"Vertices:    {totalV:N0}");
                    ImGui.TextDisabled($"Triangles:   {totalF:N0}");

                    var firstTex    = firstMat?.Albedo;
                    var firstCenter = firstMat?.LocalCenter;

                    if (firstTex is not null)
                    {
                        float avail  = ImGui.GetContentRegionAvail().X;
                        float aspect = firstTex.Height > 0 ? (float)firstTex.Width / firstTex.Height : 1f;
                        float th     = MathF.Min(avail / aspect, 128f);
                        float tw     = th * aspect;
                        ImGui.TextDisabled($"Texture: {firstTex.Width}x{firstTex.Height}");
                        ImGui.Image((nint)firstTex.GlId, new Vector2(tw, th), new Vector2(0f, 1f), new Vector2(1f, 0f));
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), "Texture: none");
                    }

                    if (firstCenter.HasValue)
                    {
                        var lc = firstCenter.Value;
                        ImGui.TextDisabled($"Local center: ({lc.X:F2}, {lc.Y:F2}, {lc.Z:F2})");
                    }
                }
                else
                {
                    ImGui.TextDisabled("  (no mesh decoded)");
                }

                if (firstMat is not null)
                {
                    ImGui.Separator();
                    if (ImGui.CollapsingHeader("Material##inst", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        if (!string.IsNullOrEmpty(firstMat.ShaderType))
                            ImGui.TextDisabled($"PS2 Shader: {firstMat.ShaderType}");

                        string[] cullLabels = { "Front", "Back", "Both" };
                        int cullIdx = (int)firstMat.Culling;
                        ImGui.SetNextItemWidth(-1f);
                        if (ImGui.Combo("Cull Mode##inst", ref cullIdx, cullLabels, cullLabels.Length))
                            foreach (var r in childRenderers)
                                if (r.Material is not null)
                                    r.Material.Culling = (Material.CullMode)cullIdx;

                        var bc = firstMat.BaseColor;
                        if (ImGui.ColorEdit4("Color##inst", ref bc))
                            foreach (var r in childRenderers)
                                if (r.Material is not null) r.Material.BaseColor = bc;

                        bool unlit = firstMat.Unlit;
                        if (ImGui.Checkbox("Unlit##inst", ref unlit))
                            foreach (var r in childRenderers)
                                if (r.Material is not null) { r.Material.Unlit = unlit; r.Material.UnlitToggledByUser = true; }

                        bool ab = firstMat.AlphaBlend;
                        if (ImGui.Checkbox("Alpha Blend##inst", ref ab))
                            foreach (var r in childRenderers)
                                if (r.Material is not null) r.Material.AlphaBlend = ab;

                        if (firstMat.AlphaBlend)
                        {
                            string[] blendLabels = { "Standard", "Additive", "Subtractive" };
                            int blendIdx = (int)firstMat.Blend;
                            ImGui.SetNextItemWidth(-1f);
                            if (ImGui.Combo("Blend##inst", ref blendIdx, blendLabels, blendLabels.Length))
                                foreach (var r in childRenderers)
                                    if (r.Material is not null)
                                        r.Material.Blend = (Material.BlendMode)blendIdx;
                        }

                        bool fog = firstMat.FogEnabled;
                        if (ImGui.Checkbox("Fog##inst", ref fog))
                            foreach (var r in childRenderers)
                                if (r.Material is not null) r.Material.FogEnabled = fog;

                        var spd = firstMat.UvScrollSpeed;
                        if (spd.X != 0f || spd.Y != 0f)
                            ImGui.TextDisabled($"UV Scroll: {spd.X:F4} , {spd.Y:F4} /s");
                    }
                }

                DrawSwapObjectAndCharacterSection(e, inst);
            }
        }

        var anim = e.Get<CrashEngine.Importer.AnimatedObject>();
        if (anim is not null && anim.Groups.Count > 0)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Animations##anim", ImGuiTreeNodeFlags.DefaultOpen))
            {
                foreach (var group in anim.Groups)
                {
                    bool isCurrent = anim.CurrentGroup == group;
                    bool altState  = anim.DefaultGroup is not null && group.MeshRoot != anim.DefaultGroup.MeshRoot;
                    ImGui.PushID(group.SlotIndex);
                    if (isCurrent) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 1f, 0.6f, 1f));
                    var label = $"Slot {group.SlotIndex}  —  Anim {group.AnimId:X4}  ({group.Anim.TotalFrames} frames)"
                              + (altState ? "  [alt state]" : "");
                    if (ImGui.Selectable(label, isCurrent))
                        anim.Play(group);
                    if (isCurrent) ImGui.PopStyleColor();

                    ImGui.Indent();
                    if (ImGui.SmallButton("Swap animation##swapbtn")) ImGui.OpenPopup("##swapAnim");
                    if (ImGui.IsItemHovered())
                        MaybeTooltip("Replace this slot's animation clip. Slots are called by\n" +
                                          "NUMBER from scripts, not by animation id — this only changes\n" +
                                          "WHICH clip plays for this slot, so scripts keep working.\n" +
                                          "Changes the shared GameObject definition — every instance of\n" +
                                          "this object in the level gets it, not just this one. Not\n" +
                                          "undoable.");
                    ImGui.Unindent();

                    if (ImGui.BeginPopup("##swapAnim"))
                    {
                        var swapChunkRoot2 = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
                        var animTables     = swapChunkRoot2?.Get<ChunkSource>()?.MeshTables;
                        if (animTables is null)
                        {
                            ImGui.TextDisabled("(chunk not loaded)");
                        }
                        else
                        {
                            ImGui.SetNextItemWidth(220f);
                            ImGui.InputTextWithHint("##animSwapFilter", "Search id...", ref _animSwapFilter, 64);
                            ImGui.BeginChild("##animSwapList", new Vector2(240f, 180f), ImGuiChildFlags.Border);
                            int myJoints = group.Anim.MainAnimation.JointSettings.Count;
                            foreach (var (animId, frames, joints) in MeshDecoder.GetAnimationCatalog(animTables))
                            {
                                if (!string.IsNullOrWhiteSpace(_animSwapFilter) &&
                                    !$"{animId:X4}".Contains(_animSwapFilter, StringComparison.OrdinalIgnoreCase))
                                    continue;
                                bool mismatch = joints != myJoints;
                                var itemLabel = $"Anim {animId:X4}  ({frames}f, {joints}j)" + (mismatch ? "  !" : "");
                                if (mismatch) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.2f, 1f));
                                bool picked = ImGui.Selectable($"{itemLabel}##pick{animId:X4}", animId == group.AnimId);
                                if (mismatch) ImGui.PopStyleColor();
                                if (mismatch && ImGui.IsItemHovered())
                                    MaybeTooltip($"Joint count differs from this slot's current clip\n" +
                                                      $"({joints} vs {myJoints}) — may look wrong or not match\n" +
                                                      "the object's actual skeleton. Not tested against a real\n" +
                                                      "PCSX2 run — verify in-game before trusting this.");
                                if (picked)
                                {
                                    SwapAnimationSlot(e, inst, group.SlotIndex, animId);
                                    ImGui.CloseCurrentPopup();
                                }
                            }
                            ImGui.EndChild();
                        }
                        ImGui.EndPopup();
                    }
                    ImGui.PopID();
                }

                if (anim.Current is not null)
                {
                    ImGui.Separator();
                    if (ImGui.Button(anim.Playing ? "Pause##anim" : "Play##anim")) anim.Playing = !anim.Playing;
                    ImGui.SameLine();
                    bool loop = anim.Loop;
                    if (ImGui.Checkbox("Loop##anim", ref loop)) anim.Loop = loop;
                    ImGui.SameLine();
                    if (ImGui.Button("Stop##anim")) anim.Stop();

                    if (anim.Current is not null)
                    {
                        int lastFrame = anim.Current.MainAnimation.AnimatedTransformations.Count - 1;
                        ImGui.Text($"Frame {anim.Frame:F1} / {MathF.Max(0, lastFrame)}");
                        float f = anim.Frame;
                        ImGui.SetNextItemWidth(-1f);
                        if (ImGui.SliderFloat("##animtimeline", ref f, 0f, MathF.Max(0, lastFrame)))
                        {
                            anim.Frame   = f;
                            anim.Playing = false;
                        }
                    }
                }
            }
        }

        bool hasSpecific = mr is not null || rmr is not null || dcr is not null || rp is not null
                        || cam is not null || se is not null || inst is not null || anim is not null
                        || linkedScenery is not null || loadWall is not null;
        if (!hasSpecific && e.Components.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextDisabled("Components:");
            foreach (var c in e.Components)
                ImGui.TextDisabled($"  {c.GetType().Name}");
        }
    }

    private void DrawBehavioursSectionBody(PS2AnyObject obj, BaseTwinSection behSecForList, bool editingGlobal, ChunkSource? scriptChunkSrc)
    {
        if (editingGlobal)
            ImGui.TextDisabled("Editing the GLOBAL (Startup\\Default.rm2) scripts — shared by\n" +
                                "EVERY placed instance of this exact object type, in every level.");
        ImGui.BeginChild("##objBehList", new Vector2(-1f, 200f), ImGuiChildFlags.Border);
        foreach (var bid in obj.RefBehaviours)
        {
            var bItem = behSecForList.GetItem<BaseTwinItem>(bid);
            string bLabel = bItem is PS2BehaviourGraph bg && !string.IsNullOrEmpty(bg.Name)
                ? bg.Name : (bItem is TwinBehaviourStarter ? "(starter, no name)" : "(missing)");
            string bType = bItem is TwinBehaviourStarter ? "Starter" : bItem is PS2BehaviourGraph ? "Graph" : "?";
            bool clicked = ImGui.Selectable($"0x{bid:X4}  [{bType,-7}]  {bLabel}");
            if (clicked && bItem is PS2BehaviourGraph clickedGraph)
            {
                _inspectedBehaviourId = bid;
                _inspectedBehaviourGraph = clickedGraph;
                _inspectedBehaviourManual = false;
                _inspectedBehaviourText = GraphToEditableText(clickedGraph);
                _inspectedBehaviourOriginalText =
                    GetPristineBehaviourGraphText(editingGlobal, bid) ?? _inspectedBehaviourText;
                _inspectedBehaviourApplyMsg = "";
            }
        }
        ImGui.EndChild();

        ImGui.SetNextItemWidth(90f);
        ImGui.InputText("##openBehIdInput", ref _openBehIdText, 8,
            ImGuiInputTextFlags.CharsHexadecimal);
        ImGui.SameLine();
        if (ImGui.Button("Open by ID (hex)##openbehid"))
        {
            if (uint.TryParse(_openBehIdText, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out uint manualId) &&
                behSecForList.GetItem<PS2BehaviourGraph>(manualId) is { } manualGraph)
            {
                _inspectedBehaviourId = manualId;
                _inspectedBehaviourGraph = manualGraph;
                _inspectedBehaviourManual = true;
                _inspectedBehaviourText = GraphToEditableText(manualGraph);
                _inspectedBehaviourOriginalText =
                    GetPristineBehaviourGraphText(editingGlobal, manualId) ?? _inspectedBehaviourText;
                _inspectedBehaviourApplyMsg = "";
            }
            else
            {
                _inspectedBehaviourApplyMsg = $"0x{_openBehIdText}: not found in this " +
                    $"{(editingGlobal ? "GLOBAL" : "LOCAL")} Behaviours section (or not a Graph).";
            }
        }
        if (ImGui.IsItemHovered())
            MaybeTooltip("Open ANY behaviour graph in this section by its raw hex id --\n" +
                         "not just ones listed above. Real use case: a script's own\n" +
                         "terminal state can hand off to a totally different graph by id\n" +
                         "(e.g. \"State_3(2635)\") that's never in RefBehaviours at all.");

        if (_inspectedBehaviourId is uint ibid &&
            (obj.RefBehaviours.Any(x => x == ibid) || _inspectedBehaviourManual) &&
            _inspectedBehaviourGraph is not null)
        {
            ImGui.TextDisabled($"0x{ibid:X4} — editable (uses WriteText/ReadText, the same " +
                                "round-trippable format the game's own tools save/load, NOT " +
                                "the pretty-print .lab decompile — command arguments are the " +
                                "raw hex values you're changing):");
            ImGui.InputTextMultiline("##objBehTextEdit", ref _inspectedBehaviourText, 65536,
                new Vector2(-1f, 220f));
            if (ImGui.Button("Apply Changes (live, in-memory)##applybeh"))
            {
                try
                {
                    using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_inspectedBehaviourText));
                    using var sr = new StreamReader(ms);
                    _inspectedBehaviourGraph.ReadText(sr);
                    _scriptTextCache.Remove(ibid);
                    if (editingGlobal && scriptChunkSrc is not null)
                        scriptChunkSrc.GlobalRm2Dirty = true;
                    _inspectedBehaviourApplyMsg = "Applied — in-memory only" +
                        (editingGlobal ? " (GLOBAL script — affects every instance of this " +
                                         "object type, every level)" : "") +
                        ". Build ISO to test live right now (no Save Chunk needed for " +
                        "that) — Save Chunk only when you're sure you want to keep it.";
                }
                catch (Exception ex)
                {
                    _inspectedBehaviourApplyMsg = $"Parse failed, NOT applied: {ex.Message}";
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Revert to Original##revertbeh"))
            {
                try
                {
                    using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_inspectedBehaviourOriginalText));
                    using var sr = new StreamReader(ms);
                    _inspectedBehaviourGraph.ReadText(sr);
                    _inspectedBehaviourText = _inspectedBehaviourOriginalText;
                    _scriptTextCache.Remove(ibid);
                    _inspectedBehaviourApplyMsg = "Reverted to the true original (disc) script, in-memory. Save Chunk to persist.";
                }
                catch (Exception ex)
                {
                    _inspectedBehaviourApplyMsg = $"Revert failed: {ex.Message}";
                }
            }
            if (!string.IsNullOrEmpty(_inspectedBehaviourApplyMsg))
                ImGui.TextDisabled(_inspectedBehaviourApplyMsg);
        }
    }

    private void SwapAnimationSlot(Entity e, InstanceData? data, int slotIndex, uint newAnimId)
    {
        if (data is null) return;
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource?.MeshTables is null) return;

        if (!MeshDecoder.SetAnimationSlot(chunkSource.MeshTables, data.ObjectId, slotIndex, newAnimId))
        {
            _browser.Log($"Swap Animation: slot {slotIndex} not found on ObjectId 0x{data.ObjectId:X4}.");
            return;
        }
        MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, chunkSource.MeshTables, e);
        _browser.Log($"Swapped Slot {slotIndex} -> Anim {newAnimId:X4} on ObjectId 0x{data.ObjectId:X4} " +
                      "(every instance of this object in the level shares this — not undoable). " +
                      "Save Chunk + Build ISO, then test in an emulator.");
    }


    private void RetargetLinkedScenery(Entity linkEnt, string newRm2Path)
    {
        var basePath = newRm2Path.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)
            ? newRm2Path[..^4] : newRm2Path;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            pkg.ShadowDir = SavedChunksDir;
            if (!ChunkImporter.RetargetLinkedScenery(Engine.Instance.GL, pkg, linkEnt, basePath, out string log))
            {
                _browser.Log($"Linked Scenery: {log}");
                return;
            }
            _browser.Log($"Linked Scenery: {log} Not undoable. Save Chunk + Build ISO, then test in an " +
                          "emulator — the game's own load-wall logic is what actually reads this data at " +
                          "runtime, not verified here against a real PCSX2 run.");
        }
        catch (Exception ex) { _browser.Log($"Linked Scenery: {ex.Message}"); }
    }

    private void SwapSkydome(string levelPath)
    {
        var basePath = levelPath.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase)
            ? levelPath[..^4] : levelPath;
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource?.Sm2 is null) { _browser.Log("Swap Skydome: current chunk has no scenery (sm2) data."); return; }

        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            pkg.ShadowDir = SavedChunksDir;
            using var stream = pkg.OpenByPath($"{basePath}.sm2");
            if (stream is null) { _browser.Log($"Swap Skydome: '{basePath}.sm2' not found."); return; }

            var sourceSm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
            using (var reader = new BinaryReader(stream))
                sourceSm2.Read(reader, (int)stream.Length);

            if (!MeshDecoder.TransplantSkydome(Engine.Instance.GL, chunkSource.Sm2, chunkSource.TexCache, sourceSm2, out string log))
            {
                _browser.Log($"Swap Skydome: {log}");
                return;
            }
            MeshDecoder.RebuildSkydomeDraws(Engine.Instance.GL, chunkSource.Sm2, chunkSource.TexCache);
            _browser.Log($"Swap Skydome: {log} Not undoable — Reload Level discards it. Save Chunk + Build ISO to keep it.");
        }
        catch (Exception ex) { _browser.Log($"Swap Skydome: {ex.Message}"); }
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



    private static TwinMat4 IdentityTwinMat4() => new TwinMat4
    {
        Column1 = new TwinVec4(1f, 0f, 0f, 0f),
        Column2 = new TwinVec4(0f, 1f, 0f, 0f),
        Column3 = new TwinVec4(0f, 0f, 1f, 0f),
        Column4 = new TwinVec4(0f, 0f, 0f, 1f),
    };

    private static TwinMat4 CloneMat(TwinMat4 m) => new TwinMat4
    {
        Column1 = new TwinVec4(m.Column1.X, m.Column1.Y, m.Column1.Z, m.Column1.W),
        Column2 = new TwinVec4(m.Column2.X, m.Column2.Y, m.Column2.Z, m.Column2.W),
        Column3 = new TwinVec4(m.Column3.X, m.Column3.Y, m.Column3.Z, m.Column3.W),
        Column4 = new TwinVec4(m.Column4.X, m.Column4.Y, m.Column4.Z, m.Column4.W),
    };

    private static Vector3 XformPoint(TwinMat4 m, Vector3 p) => new Vector3(
        m.Column1.X * p.X + m.Column2.X * p.Y + m.Column3.X * p.Z + m.Column4.X,
        m.Column1.Y * p.X + m.Column2.Y * p.Y + m.Column3.Y * p.Z + m.Column4.Y,
        m.Column1.Z * p.X + m.Column2.Z * p.Y + m.Column3.Z * p.Z + m.Column4.Z);

    private void AddNewLink(Entity linksRoot, string targetBasePath)
    {
        var chunkSource = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        var linkItem = chunkSource?.Sm2?.GetItem<PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM);
        if (linkItem is null || _loadScenesRoot is null)
        { _browser.Log("Add Link: chunk has no scenery link section."); return; }

        var thisSceneRel = (_rm2.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? _rm2[..^4] : _rm2)
            .Replace('/', '\\').TrimStart('\\');
        TwinChunkLink? counterpart = null;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            pkg.ShadowDir = SavedChunksDir;
            using var stream = pkg.OpenByPath($"{targetBasePath}.sm2");
            if (stream is not null)
            {
                var tgtSm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
                using (var reader = new BinaryReader(stream)) tgtSm2.Read(reader, (int)stream.Length);
                var tgtLinks = tgtSm2.GetItem<PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM);
                counterpart = tgtLinks?.LinksList.FirstOrDefault(l =>
                    string.Equals((l.Path ?? "").Replace('/', '\\').TrimStart('\\'), thisSceneRel, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch {  }
        bool aligned = counterpart is { ObjectMatrix: not null, ChunkMatrix: not null, LoadingWall: not null };

        TwinChunkLink newLink;
        if (aligned)
        {
            var cw = counterpart!.LoadingWall!;
            var a1 = XformPoint(counterpart.ObjectMatrix, new Vector3(cw.Column1.X, cw.Column1.Y, cw.Column1.Z));
            var a2 = XformPoint(counterpart.ObjectMatrix, new Vector3(cw.Column2.X, cw.Column2.Y, cw.Column2.Z));
            var a3 = XformPoint(counterpart.ObjectMatrix, new Vector3(cw.Column3.X, cw.Column3.Y, cw.Column3.Z));
            var a4 = XformPoint(counterpart.ObjectMatrix, new Vector3(cw.Column4.X, cw.Column4.Y, cw.Column4.Z));
            newLink = new TwinChunkLink
            {
                Path             = targetBasePath,
                IsRendered       = true,
                IsLoadWallActive = true,
                KeepLoaded       = false,
                ObjectMatrix     = CloneMat(counterpart.ChunkMatrix),
                ChunkMatrix      = CloneMat(counterpart.ObjectMatrix),
                LoadingWall      = new TwinMat4
                {
                    Column1 = new TwinVec4(a1.X, a1.Y, a1.Z, 1f),
                    Column2 = new TwinVec4(a4.X, a4.Y, a4.Z, 1f),
                    Column3 = new TwinVec4(a3.X, a3.Y, a3.Z, 1f),
                    Column4 = new TwinVec4(a2.X, a2.Y, a2.Z, 1f),
                },
            };
        }
        else
        {
            var camPos = _camera?.Transform.Position ?? Vector3.Zero;
            const float half = 3f;
            newLink = new TwinChunkLink
            {
                Path             = targetBasePath,
                IsRendered       = false,
                IsLoadWallActive = true,
                KeepLoaded       = false,
                ObjectMatrix     = IdentityTwinMat4(),
                ChunkMatrix      = IdentityTwinMat4(),
                LoadingWall      = new TwinMat4
                {
                    Column1 = new TwinVec4(camPos.X - half, camPos.Y - half, camPos.Z, 1f),
                    Column2 = new TwinVec4(camPos.X + half, camPos.Y - half, camPos.Z, 1f),
                    Column3 = new TwinVec4(camPos.X + half, camPos.Y + half, camPos.Z, 1f),
                    Column4 = new TwinVec4(camPos.X - half, camPos.Y + half, camPos.Z, 1f),
                },
            };
        }
        linkItem.LinksList.Add(newLink);

        var newLinkEnt = new Entity($"Link_{System.IO.Path.GetFileName(targetBasePath)}");
        newLinkEnt.Transform.LocalMatrix = CrashEngine.Importer.MeshDecoder.TwinMatToSys(newLink.ChunkMatrix);
        newLinkEnt.Add(new CrashEngine.Importer.LinkedSceneryLink { Source = newLink });
        linksRoot.AddChild(newLinkEnt);
        if (aligned)
            RetargetLinkedScenery(newLinkEnt, targetBasePath);

        var newWallEnt = MakeQuadMarker(_loadScenesRoot, $"LoadScene_{System.IO.Path.GetFileName(targetBasePath)}_new",
            newLink, new Vector4(1f, 0.4f, 0.1f, 0.6f));

        // Amedo 2026-09-20
        PushAddWithSelectionRestore(
            new LoadWallAddAction { Parent = _loadScenesRoot, Entity = newWallEnt, LinkItem = linkItem, Link = newLink },
            newWallEnt);

        _browser.Log(aligned
            ? $"Auto-aligned FULL return link -> '{targetBasePath}': it links back to '{thisSceneRel}', so matched its matrices (Object<->Chunk swap), built the aligned preview, AND auto-placed the LoadScene wall = the counterpart's real wall transformed here + normal flipped (matches the real reciprocal). Save Chunk + Build to test; if the crossing ever freezes, use 'Flip Load Wall Normal' on the LoadScene marker."
            : $"Added new link -> '{targetBasePath}' (target doesn't link back here -- plain trigger at camera, no preview). Save Chunk to persist.");
    }

    private void DeleteLinkedScenery()
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        var linkItem    = chunkSource?.Sm2?.GetItem<PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM);
        if (chunkRoot is null || linkItem is null)
        { _browser.Log("Delete Linked Scenery: chunk has no scenery link section."); return; }

        int count = linkItem.LinksList.Count;
        if (count == 0) { _browser.Log("Delete Linked Scenery: this chunk has no links to begin with."); return; }
        linkItem.LinksList.Clear();

        var linksRoot = AllEntities(chunkRoot).FirstOrDefault(x => x.Name == "LinkedScenery");
        if (linksRoot is not null)
            foreach (var child in linksRoot.Children.ToList())
                linksRoot.RemoveChild(child);
        if (_loadScenesRoot is not null)
            foreach (var child in _loadScenesRoot.Children.ToList())
                _loadScenesRoot.RemoveChild(child);

        _browser.Log($"Deleted all {count} linked-scenery entr{(count == 1 ? "y" : "ies")} (in-memory only). " +
                      "Reload Level from Disc to undo. Save Chunk + Build ISO to persist.");
    }

    private void DeleteSkydome()
    {
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        var gfx     = chunkSource?.Sm2?.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
        var skySec  = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_SKYDOMES_SECTION);
        if (chunkRoot is null || skySec is null)
        { _browser.Log("Delete Skydome: chunk has no skydome section."); return; }

        var ids = new List<uint>();
        for (int i = 0; i < skySec.GetItemsAmount(); i++)
            if (skySec.GetItem(i) is { } item) ids.Add(item.GetID());
        if (ids.Count == 0) { _browser.Log("Delete Skydome: this chunk has no skydome to begin with."); return; }
        foreach (var id in ids)
            skySec.RemoveItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnySkydome>(id);

        var scenery = chunkSource?.Sm2?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery>((uint)TwinConstants.SCENERY_SECENERY_ITEM);
        if (scenery is not null) scenery.SkydomeID = 0;

        RenderPipeline.SkydomeDraws.Clear();
        var skyEnt = AllEntities(chunkRoot).FirstOrDefault(x => x.Has<CrashEngine.Importer.SkydomeMarker>());
        if (skyEnt is not null)
            foreach (var child in skyEnt.Children.ToList())
                skyEnt.RemoveChild(child);

        _browser.Log($"Deleted {ids.Count} skydome item(s) (in-memory only). " +
                      "Reload Level from Disc to undo. Save Chunk + Build ISO to persist.");
    }

    private string GetPristineArchiveSource()
    {
        var backupBd = Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot, "Build",
                                     "original_backup_" + Path.GetFileName(_extractedRoot.TrimEnd('\\', '/')), "Crash.BD");
        return File.Exists(backupBd) ? backupBd : _extractedRoot;
    }

    private CrashProject? GetGameProject()
    {
        if (_gameProjectLoaded) return _gameProject;
        _gameProjectLoaded = true;
        var projectRoot = Path.GetDirectoryName(_scriptOut) ?? _extractedRoot;
        _gameProject = CrashProject.Open(projectRoot);
        return _gameProject;
    }

    // Amedo 2026-09-22
    private string ModeDir(string name) =>
        Path.Combine(Path.GetDirectoryName(_scriptOut) ?? _extractedRoot,
            GetGameProject() is { IsNewGame: true } ? name + "_NewGame" : name);

    private string SavedChunksDir => ModeDir("SavedChunks");

    private bool IsLinkTargetKeptInNewGame(string targetPath)
    {
        var project = GetGameProject();
        if (project is not { IsNewGame: true }) return true;
        var normalized = targetPath.Replace('/', '\\').TrimStart('\\');
        if (normalized.StartsWith(@"Levels\Custom\", StringComparison.OrdinalIgnoreCase)) return true;
        if (normalized.Equals(@"Levels\Earth\Hub\beach", StringComparison.OrdinalIgnoreCase)) return true;
        return project.ClaimedLevels.Any(p => p.Replace('/', '\\').TrimStart('\\').Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    // Amedo 2026-09-22
    private void StripDanglingLinksForNewGamePreview(Entity chunkRoot)
    {
        if (GetGameProject() is not { IsNewGame: true }) return;
        var sm2 = chunkRoot.Get<ChunkSource>()?.Sm2;
        var linkItem = sm2?.GetItem<PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM);
        if (linkItem is null || linkItem.LinksList.Count == 0) return;

        int removed = linkItem.LinksList.RemoveAll(l => !IsLinkTargetKeptInNewGame(l.Path));
        if (removed == 0) return;

        var linksRoot = chunkRoot.Children.FirstOrDefault(c => c.Name == "LinkedScenery");
        if (linksRoot is not null)
        {
            var stale = linksRoot.Children
                .Where(c => c.Get<CrashEngine.Importer.LinkedSceneryLink>() is { } ls
                            && !IsLinkTargetKeptInNewGame(ls.Source.Path))
                .ToList();
            foreach (var c in stale) linksRoot.RemoveChild(c);
        }
        _browser.Log($"New Game: removed {removed} upcoming-scene link(s) to excluded levels from this scene.");
    }

    private List<string> GetLinkRedirectTargets()
    {
        var full = GetSwapLevelList();
        var project = GetGameProject();
        if (project is not { IsNewGame: true }) return full;

        var keep = new HashSet<string>(project.ClaimedLevels.Select(p => p.Replace('/', '\\').TrimStart('\\')),
                                        StringComparer.OrdinalIgnoreCase)
        { @"Levels\Earth\Hub\beach" };
        return full.Where(rel =>
            rel.StartsWith(@"Levels\Custom\", StringComparison.OrdinalIgnoreCase) ||
            keep.Contains(rel.EndsWith(".rm2", StringComparison.OrdinalIgnoreCase) ? rel[..^4] : rel)
        ).ToList();
    }

    private List<string> GetSwapLevelList()
    {
        if (_swapLevelList is not null) return _swapLevelList;
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var pkg = PackageReader.Open(GetPristineArchiveSource());
            foreach (var rec in pkg.GetByExtension(".rm2"))
            {
                var rel = rec.Path.Replace('/', '\\');
                if (string.Equals(rel, _rm2, StringComparison.OrdinalIgnoreCase)) continue;
                if (rel.EndsWith(@"Startup\Default.rm2", StringComparison.OrdinalIgnoreCase)) continue;
                if (seen.Add(rel)) list.Add(rel);
            }
        }
        catch (Exception ex) { _browser.Log($"Swap Object: level scan failed: {ex.Message}"); }

        try
        {
            var savedChunksDir = SavedChunksDir;
            if (Directory.Exists(savedChunksDir))
            {
                foreach (var file in Directory.GetFiles(savedChunksDir, "*.rm2", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(savedChunksDir, file);
                    if (string.Equals(rel, _rm2, StringComparison.OrdinalIgnoreCase)) continue;
                    if (seen.Add(rel)) list.Add(rel);
                }
            }
        }
        catch (Exception ex) { _browser.Log($"Swap Object: SavedChunks scan failed: {ex.Message}"); }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        _swapLevelList = list;
        return list;
    }

    private PS2AnyTwinsanityRM2? GetForeignRm2(string rm2Path)
    {
        if (_foreignRm2Cache.TryGetValue(rm2Path, out var cached)) return cached;
        try
        {
            using var pkg = PackageReader.Open(_extractedRoot);
            pkg.ShadowDir = SavedChunksDir;
            var rm2 = ChunkImporter.ParseRm2Only(pkg, rm2Path);
            _foreignRm2Cache[rm2Path] = rm2;
            return rm2;
        }
        catch (Exception ex)
        {
            _browser.Log($"Swap Object: failed to open {rm2Path}: {ex.Message}");
            return null;
        }
    }

    private void ImportTextureOverMaterial(Material mat)
    {
        if (mat.Albedo is null) { _browser.Log("Import Texture: this material has no texture to replace."); return; }

        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource is null) return;

        if (!TraceTextureId(chunkSource, mat.Albedo, out uint id, out bool isScenery, out bool isGlobal))
        { _browser.Log("Import Texture: couldn't trace this texture back to a source id."); return; }

        var texObj = FindTextureById(chunkSource, id, isScenery, isGlobal, out var source);
        bool fromGlobal = source == TextureSource.GlobalRm2;
        if (texObj is null) { _browser.Log($"Import Texture: source PS2 texture {id:X8} not found in the chunk graph."); return; }

        ShowOpenFileDialog("Import Texture", "Images\0*.png;*.jpg;*.jpeg;*.bmp;*.tga\0All Files\0*.*\0\0", path =>
        {
        if (path is null) return;

        try
        {
            using var stream = File.OpenRead(path);
            var img = StbImageSharp.ImageResult.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);

            int dw, dh;
            if (texObj.TextureFormat == Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture.TexturePixelFormat.PSMT8)
            {
                var best = PS2AnyTexture.RegisteredPsmt8Sizes
                    .OrderBy(s => Math.Abs(s.Width - img.Width) + Math.Abs(s.Height - img.Height))
                    .First();
                (dw, dh) = best;
            }
            else
            {
                dw = NearestPowerOfTwo(img.Width);
                dh = NearestPowerOfTwo(img.Height);
            }
            var rgba = (dw == img.Width && dh == img.Height)
                ? img.Data
                : ResizeNearest(img.Data, img.Width, img.Height, dw, dh);

            var colors = new List<TwinColor>(dw * dh);
            for (int i = 0; i < dw * dh; i++)
                colors.Add(new TwinColor(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]));

            texObj.FromBitmap(colors, dw, texObj.TexFun, texObj.TextureFormat);

            if (fromGlobal) chunkSource.GlobalRm2Dirty = true;

            mat.Albedo.UpdateData(rgba, (uint)dw, (uint)dh);

            _browser.Log($"Imported texture → Texture {id:X8} ({img.Width}x{img.Height} → {dw}x{dh}), format {texObj.TextureFormat}" +
                         (dw != img.Width || dh != img.Height ? "  [resized to nearest power-of-two]" : "") +
                         "  [kept at the SOURCE image's own resolution, not clamped to the original texture's size — larger than the original slot, real VRAM-overflow risk at big resolutions]" +
                         (fromGlobal ? "  [shared/global object — will also write Startup\\Default.rm2 on Save Chunk]" : ""));
        }
        catch (Exception ex) { _browser.Log($"Import Texture failed: {ex.Message}"); }
        });
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
                var m = AddOneLightMarker(nl, true, $"NegLight_{scenery.NegativeLights.Count - 1}");
                if (m is not null) SelectClicked(m, false);
            }
            ImGui.EndDisabled();
            if (!scenery.HasLighting) { ImGui.SameLine(); ImGui.TextDisabled("(lighting disabled for this level)"); }
            ImGui.TreePop();
        }

        ImGui.TextDisabled("Point/Negative lights affect the real game only (the editor preview\nshows ambient+directional). Save Chunk to persist.");
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

    private void ResetTextureToOriginal(Material mat)
    {
        if (mat.Albedo is null) { _browser.Log("Reset Texture: nothing to reset (no texture bound)."); return; }

        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        if (chunkSource is null) return;

        if (!TraceTextureId(chunkSource, mat.Albedo, out uint id, out bool isScenery, out bool isGlobal))
        { _browser.Log("Reset Texture: couldn't trace this texture back to a source id."); return; }

        var texObj = FindTextureById(chunkSource, id, isScenery, isGlobal, out var source);
        bool fromGlobal = source == TextureSource.GlobalRm2;
        if (texObj is null) { _browser.Log($"Reset Texture: source PS2 texture {id:X8} not found in the chunk graph."); return; }

        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());

            PS2AnyTexture? freshTex;
            if (source == TextureSource.Sm2)
            {
                var path = _sm2;
                using var stream = pkg.OpenByPath(path)
                    ?? throw new FileNotFoundException($"Couldn't reopen {path} from the disc archive.");
                using var reader = new BinaryReader(stream);
                var freshSm2 = new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2();
                freshSm2.Read(reader, (int)stream.Length);
                var freshGfx    = freshSm2.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
                var freshTexSec = freshGfx?.GetItem<PS2AnyTexturesSection>((uint)TwinConstants.GRAPHICS_TEXTURES_SECTION);
                freshTex = freshTexSec?.GetItem<PS2AnyTexture>(id);
            }
            else
            {
                var path = fromGlobal ? @"Startup\Default.rm2" : _rm2;
                using var stream = pkg.OpenByPath(path)
                    ?? throw new FileNotFoundException($"Couldn't reopen {path} from the disc archive.");
                using var reader = new BinaryReader(stream);
                Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2 freshRm2 = fromGlobal
                    ? new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default()
                    : new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
                freshRm2.Read(reader, (int)stream.Length);
                var freshGfx    = freshRm2.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.LEVEL_GRAPHICS_SECTION);
                var freshTexSec = freshGfx?.GetItem<PS2AnyTexturesSection>((uint)TwinConstants.GRAPHICS_TEXTURES_SECTION);
                freshTex = freshTexSec?.GetItem<PS2AnyTexture>(id);
            }
            if (freshTex is null) { _browser.Log($"Reset Texture: original texture {id:X8} not found on disc."); return; }

            using (var buf = new MemoryStream())
            {
                using (var bw = new BinaryWriter(buf, System.Text.Encoding.Default, leaveOpen: true))
                    freshTex.Write(bw);
                buf.Position = 0;
                using var br = new BinaryReader(buf);
                texObj.Read(br, (int)buf.Length);
            }

            texObj.CalculateData();
            int w = texObj.ImageWidthPower  > 0 ? (1 << texObj.ImageWidthPower)  : 1;
            int h = texObj.ImageHeightPower > 0 ? (1 << texObj.ImageHeightPower) : 1;
            var rgba = new byte[Math.Max(texObj.Colors.Count, w * h) * 4];
            int idx = 0;
            foreach (TwinColor c in texObj.Colors)
            {
                rgba[idx++] = c.R; rgba[idx++] = c.G; rgba[idx++] = c.B; rgba[idx++] = c.A;
            }
            mat.Albedo.UpdateData(rgba, (uint)w, (uint)h);

            if (fromGlobal) chunkSource.GlobalRm2Dirty = true;
            _browser.Log($"Reset texture {id:X8} to its original disc data ({w}x{h}).");
        }
        catch (Exception ex) { _browser.Log($"Reset Texture failed: {ex.Message}"); }
    }

    private void ResetParamList2ToOriginal(InstanceData data)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            using var stream = pkg.OpenByPath(_rm2)
                ?? throw new FileNotFoundException($"Couldn't reopen {_rm2} from the disc archive.");
            using var reader = new BinaryReader(stream);
            var freshRm2 = new PS2AnyTwinsanityRM2();
            freshRm2.Read(reader, (int)stream.Length);

            PS2AnyInstance? original = null;
            for (int lid = 0; lid <= 7 && original is null; lid++)
            {
                var layout = freshRm2.GetItem<BaseTwinSection>((uint)lid);
                var instSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
                if (instSec is null) continue;
                for (int i = 0; i < instSec.GetItemsAmount(); i++)
                {
                    if (instSec.GetItem(i) is PS2AnyInstance cand &&
                        cand.GetID() == data.Source.GetID() && cand.ObjectId == data.ObjectId)
                    { original = cand; break; }
                }
            }
            if (original is null)
            {
                _browser.Log("Reset Character Settings: couldn't find this instance's original data on disc.");
                return;
            }

            data.Source.ParamList2.Clear();
            data.Source.ParamList2.AddRange(original.ParamList2);
            _browser.Log("Reset Character Settings to their original values (in-memory). Save Chunk to persist.");
        }
        catch (Exception ex) { _browser.Log($"Reset Character Settings failed: {ex.Message}"); }
    }

    private enum TextureSource { Rm2, GlobalRm2, Sm2 }

    private static bool TraceTextureId(ChunkSource src, Texture2D albedo, out uint id, out bool isScenery, out bool isGlobal)
    {
        foreach (var kv in src.GlobalTexCache)
            if (ReferenceEquals(kv.Value, albedo)) { id = kv.Key; isScenery = false; isGlobal = true; return true; }
        foreach (var kv in src.TexCache)
            if (ReferenceEquals(kv.Value, albedo)) { id = kv.Key; isScenery = false; isGlobal = false; return true; }
        foreach (var kv in src.SceneryTexCache)
            if (ReferenceEquals(kv.Value, albedo)) { id = kv.Key; isScenery = true; isGlobal = false; return true; }
        id = 0; isScenery = false; isGlobal = false;
        return false;
    }

    private static PS2AnyTexture? FindTextureById(ChunkSource src, uint id, bool isScenery, bool isGlobal, out TextureSource source)
    {
        if (isScenery)
        {
            var gfx    = src.Sm2?.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
            var texSec = gfx?.GetItem<PS2AnyTexturesSection>((uint)TwinConstants.GRAPHICS_TEXTURES_SECTION);
            source = TextureSource.Sm2;
            return texSec?.GetItem<PS2AnyTexture>(id);
        }

        PS2AnyTexture? Lookup(Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2? rm2)
        {
            var gfx    = rm2?.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.LEVEL_GRAPHICS_SECTION);
            var texSec = gfx?.GetItem<PS2AnyTexturesSection>((uint)TwinConstants.GRAPHICS_TEXTURES_SECTION);
            return texSec?.GetItem<PS2AnyTexture>(id);
        }
        source = isGlobal ? TextureSource.GlobalRm2 : TextureSource.Rm2;
        return isGlobal ? Lookup(src.GlobalRm2) : Lookup(src.Rm2);
    }

    private static int NearestPowerOfTwo(int v)
    {
        v = Math.Clamp(v, 4, 1024);
        int lower = 1 << (int)Math.Floor(Math.Log2(v));
        int upper = lower << 1;
        return (v - lower) < (upper - v) ? lower : upper;
    }

    private static byte[] ResizeNearest(byte[] src, int sw, int sh, int dw, int dh)
    {
        var dst = new byte[dw * dh * 4];
        for (int y = 0; y < dh; y++)
        {
            int sy = Math.Min(sh - 1, y * sh / dh);
            for (int x = 0; x < dw; x++)
            {
                int sx = Math.Min(sw - 1, x * sw / dw);
                int si = (sy * sw + sx) * 4;
                int di = (y * dw + x) * 4;
                dst[di] = src[si]; dst[di + 1] = src[si + 1]; dst[di + 2] = src[si + 2]; dst[di + 3] = src[si + 3];
            }
        }
        return dst;
    }

    private string? GetPristineBehaviourGraphText(bool isGlobal, uint bid)
    {
        try
        {
            using var pkg = PackageReader.Open(ResolvePristinePackageSource());
            string relPath = isGlobal ? @"Startup\Default.rm2" : _rm2;
            using var stream = pkg.OpenByPath(relPath);
            if (stream is null) return null;
            using var reader = new BinaryReader(stream);
            var freshRm2 = isGlobal
                ? (Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2)new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2Default()
                : new Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2();
            freshRm2.Read(reader, (int)stream.Length);

            var freshBehSec = freshRm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)
                ?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
            var freshGraph = freshBehSec?.GetItem<PS2BehaviourGraph>(bid);
            return freshGraph is null ? null : GraphToEditableText(freshGraph);
        }
        catch { return null; }
    }
}
