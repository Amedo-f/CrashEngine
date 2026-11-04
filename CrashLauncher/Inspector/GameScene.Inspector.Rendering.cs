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
    private void DrawInspectorMeshRenderer(Entity e, CrashEngine.Importer.MeshRenderer? mr)
    {
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
                        ImGui.TextDisabled($"Texture: {tex.Width}x{tex.Height}{tex.HdLabel}"); // Amedo 2026-10-02
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
    }

    private void DrawInspectorRemoveImportedModel(Entity e)
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

    private void DrawInspectorRendererMesh(Entity e, CrashEngine.Renderer.MeshRenderer? rmr)
    {
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
                    ImGui.TextDisabled($"Texture: {tex.Width}×{tex.Height}{tex.HdLabel}"); // Amedo 2026-10-02
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
    }

    private void DrawInspectorCube(Entity e, DirectCubeRenderer? dcr)
    {
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
    }

    private void DrawInspectorRenderPipeline(Entity e, RenderPipeline? rp)
    {
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
    }

    private void DrawInspectorCamera(Entity e, CameraComponent? cam)
    {
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
    }
}
