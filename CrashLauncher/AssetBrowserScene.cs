using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;
using System.Text.RegularExpressions;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using TwinShader = Twinsanity.TwinsanityInterchange.Common.TwinShader;
using TwinAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace CrashLauncher;

public sealed class AssetBrowserScene : Scene
{
    private sealed record AssetEntry(string Type, string Display, uint Id,
                                     string JsonPath, string Variation);

    private readonly string _assetsRoot;
    private readonly Scene  _back;

    private readonly Dictionary<string, List<AssetEntry>> _tree = new();
    private volatile bool _scanned;
    private string _search = "";

    private PreviewFbo? _fbo;
    private List<(GpuMesh Mesh, Material Mat)> _previewMeshes = new();
    private Texture2D? _previewTex;
    private AssetEntry? _selected;
    private readonly List<string> _info = new();
    private float _yaw = 30f, _pitch = 15f, _dist = 5f;
    private Vector3 _center = Vector3.Zero;

    private PS2AnyOGI? _previewOgi;
    private List<(GpuMesh Mesh, Material Mat, Matrix4x4 Local)> _skelParts = new();
    private TwinAnimation? _animData;
    private int  _animTotalFrames;
    private float _animFps = 30f;
    private float _animFrame;
    private bool _animPlaying;
    private bool _animLoop = true;
    private Matrix4x4[] _boneMatrices = RenderPipeline.IdentityBoneMatrices;

    public AssetBrowserScene(string projectPath, Scene back)
    {
        _assetsRoot = Path.Combine(projectPath, "assets");
        _back       = back;
    }

    protected override void Build()
    {
        var pipeEnt = new Entity("RenderPipeline");
        var pipe    = pipeEnt.Add(new RenderPipeline());
        pipe.FogColor = new Vector3(0.08f, 0.08f, 0.10f);
        AddRoot(pipeEnt);

        Task.Run(ScanAssets);
    }

    private static readonly Regex NameRx =
        new(@"^(?<name>.+?)[ ]?(?<id>[0-9A-Fa-f]{8})_(?<var>.*)$", RegexOptions.Compiled);

    private void ScanAssets()
    {
        if (!Directory.Exists(_assetsRoot)) { _scanned = true; return; }

        foreach (var dir in Directory.GetDirectories(_assetsRoot))
        {
            var type = Path.GetFileName(dir);
            if (type is "raw" or "disc") continue;

            var list = new List<AssetEntry>();
            foreach (var json in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                var stem = Path.GetFileNameWithoutExtension(json);
                var m    = NameRx.Match(stem);
                uint id  = 0; var name = stem; var variation = "";
                if (m.Success)
                {
                    name = m.Groups["name"].Value;
                    variation = m.Groups["var"].Value;
                    uint.TryParse(m.Groups["id"].Value,
                        System.Globalization.NumberStyles.HexNumber, null, out id);
                }
                list.Add(new AssetEntry(type, $"{name} [{id:X8}]", id, json, variation));
            }
            if (list.Count > 0)
                lock (_tree) _tree[type] = list.OrderBy(e => e.Display).ToList();
        }
        _scanned = true;
    }

    private AssetEntry? Find(string type, uint id, string preferVariation)
    {
        lock (_tree)
        {
            if (!_tree.TryGetValue(type, out var list)) return null;
            return list.FirstOrDefault(e => e.Id == id && e.Variation == preferVariation)
                ?? list.FirstOrDefault(e => e.Id == id && e.Variation.Contains("Default"))
                ?? list.FirstOrDefault(e => e.Id == id);
        }
    }

    private static string? DataPath(AssetEntry e, string ext)
    {
        var p = Path.ChangeExtension(e.JsonPath, ext);
        return File.Exists(p) ? p : null;
    }

    private static T? ParseTwin<T>(string dataPath) where T : class, Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem, new()
    {
        try
        {
            var bytes = File.ReadAllBytes(dataPath);
            using var ms = new MemoryStream(bytes);
            using var br = new BinaryReader(ms);
            var item = new T();
            item.Read(br, bytes.Length);
            return item;
        }
        catch { return null; }
    }

    private (Texture2D? Tex, TwinShader? Shader) ResolveMaterial(uint matId, string variation)
    {
        var matEntry = Find("Material", matId, variation);
        if (matEntry is null) return (null, null);
        var dp = DataPath(matEntry, ".data");
        if (dp is null) return (null, null);
        var mat = ParseTwin<PS2AnyMaterial>(dp);
        if (mat is null || mat.Shaders.Count == 0) return (null, null);

        var shader = mat.Shaders[0];
        foreach (var sh in mat.Shaders)
        {
            var texEntry = Find("Texture", sh.TextureId, variation);
            var png = texEntry is null ? null : DataPath(texEntry, ".png");
            if (png is not null)
                return (Texture2D.FromFile(Engine.Instance.GL, png), sh);
        }
        return (null, shader);
    }

    private void Select(AssetEntry e)
    {
        _selected = e;
        foreach (var (mesh, mat) in _previewMeshes) { mesh.Dispose(); mat.Albedo?.Dispose(); }
        _previewMeshes.Clear();
        foreach (var (mesh, mat, _) in _skelParts) { mesh.Dispose(); mat.Albedo?.Dispose(); }
        _skelParts.Clear();
        _previewOgi   = null;
        _animData     = null;
        _animPlaying  = false;
        _animFrame    = 0f;
        _boneMatrices = RenderPipeline.IdentityBoneMatrices;
        _previewTex?.Dispose();
        _previewTex = null;
        _info.Clear();
        _yaw = 30f; _pitch = 15f; _dist = 5f; _center = Vector3.Zero;

        var gl = Engine.Instance.GL;
        _info.Add($"Type: {e.Type}");
        _info.Add($"ID: {e.Id:X8}");
        _info.Add($"Source: {e.Variation}");

        try
        {
            switch (e.Type)
            {
                case "Texture":
                {
                    var png = DataPath(e, ".png");
                    if (png is not null) _previewTex = Texture2D.FromFile(gl, png);
                    if (_previewTex is not null)
                        _info.Add($"Size: {_previewTex.Width}x{_previewTex.Height}");
                    break;
                }
                case "Material":
                {
                    var dp = DataPath(e, ".data");
                    var mat = dp is null ? null : ParseTwin<PS2AnyMaterial>(dp);
                    if (mat is null) { _info.Add("(failed to parse)"); break; }
                    _info.Add($"Name: {mat.Name}");
                    _info.Add($"Shaders: {mat.Shaders.Count}");
                    foreach (var sh in mat.Shaders)
                    {
                        _info.Add($"  {sh.ShaderType}  tex:{sh.TextureId:X8}");
                        _info.Add($"    blend:{sh.ABlending} fog:{sh.Fog}");
                    }
                    var (tex, _) = ResolveMaterial(e.Id, e.Variation);
                    _previewTex = tex;
                    break;
                }
                case "Model" or "Mesh":
                {
                    var dp = DataPath(e, ".data");
                    var model = dp is null ? null : ParseTwin<PS2AnyModel>(dp);
                    if (model is null) { _info.Add("(failed to parse)"); break; }
                    _previewMeshes = MeshDecoder.DecodeModel(gl, model);
                    FrameMeshes();
                    _info.Add($"SubModels: {model.SubModels.Count}");
                    break;
                }
                case "RigidModel":
                {
                    var dp = DataPath(e, ".data");
                    var rm = dp is null ? null : ParseTwin<PS2AnyRigidModel>(dp);
                    if (rm is null) { _info.Add("(failed to parse)"); break; }
                    _info.Add($"Model: {rm.Model:X8}  Materials: {rm.Materials.Count}");

                    var modelEntry = Find("Model", rm.Model, e.Variation);
                    var mdp = modelEntry is null ? null : DataPath(modelEntry, ".data");
                    var model = mdp is null ? null : ParseTwin<PS2AnyModel>(mdp);
                    if (model is null) { _info.Add("(model not found)"); break; }

                    var subMats = rm.Materials
                        .Select(mid => ResolveMaterial(mid, e.Variation)).ToList();
                    _previewMeshes = MeshDecoder.DecodeModel(gl, model, subMats);
                    FrameMeshes();
                    break;
                }
                case "Skin" or "BlendSkin" when e.Type == "Skin":
                {
                    var dp = DataPath(e, ".data");
                    var skin = dp is null ? null : ParseTwin<PS2AnySkin>(dp);
                    if (skin is null) { _info.Add("(failed to parse)"); break; }
                    _info.Add($"SubSkins: {skin.SubSkins.Count}");

                    var subMats = skin.SubSkins
                        .Select(ss => ResolveMaterial(ss.Material, e.Variation)).ToList();
                    _previewMeshes = MeshDecoder.DecodeSkin(gl, skin, subMats);
                    FrameMeshes();
                    break;
                }
                case "OGI":
                {
                    var dp = DataPath(e, ".data");
                    var ogi = dp is null ? null : ParseTwin<PS2AnyOGI>(dp);
                    if (ogi is null) { _info.Add("(failed to parse)"); break; }
                    _info.Add($"Joints: {ogi.Joints.Count}");
                    _info.Add($"RigidModels: {ogi.RigidModelIds.Count}");
                    _info.Add($"Skin: {(ogi.SkinID == 0 ? "-" : $"{ogi.SkinID:X8}")}   BlendSkin: {(ogi.BlendSkinID == 0 ? "-" : $"{ogi.BlendSkinID:X8}")}");
                    _previewOgi = ogi;
                    _skelParts  = BuildOgiParts(ogi, e.Variation);
                    FrameMeshes();
                    break;
                }
                case "Animation":
                {
                    var dp = DataPath(e, ".data");
                    var anim = dp is null ? null : ParseTwin<PS2AnyAnimation>(dp);
                    if (anim is null) { _info.Add("(failed to parse)"); break; }
                    _info.Add($"Total Frames: {anim.TotalFrames}");
                    _info.Add($"FPS: {anim.DefaultFPS}");
                    _info.Add($"Has facial data: {anim.HasFacialAnimationData}");

                    if (!anim.HasAnimationData) { _info.Add("(no skeletal animation data)"); break; }
                    _info.Add($"Joint tracks: {anim.MainAnimation.JointSettings.Count}");

                    _animData         = anim.MainAnimation;
                    _animTotalFrames  = anim.TotalFrames;
                    _animFrame        = 0f;
                    _animPlaying      = false;
                    _animFps          = anim.DefaultFPS > 0 ? anim.DefaultFPS : 30f;

                    var best = FindBestFitOgi(anim.MainAnimation.JointSettings.Count, e.Variation);
                    if (best is { } b)
                    {
                        _previewOgi = b.Ogi;
                        _skelParts  = BuildOgiParts(b.Ogi, b.Entry.Variation);
                        FrameMeshes();
                        _info.Add($"Preview skeleton: {b.Entry.Display}"
                                  + (b.Ogi.Joints.Count != anim.MainAnimation.JointSettings.Count
                                     ? $"  (joint count mismatch: {b.Ogi.Joints.Count} vs {anim.MainAnimation.JointSettings.Count})"
                                     : ""));
                    }
                    else _info.Add("(no OGI found to preview against)");
                    break;
                }
                case "GameObject":
                {
                    var dp  = DataPath(e, ".data");
                    var obj = dp is null ? null : ParseTwin<PS2AnyObject>(dp);
                    if (obj is null) { _info.Add("(failed to parse)"); break; }
                    _info.Add($"Name: {obj.Name}");
                    _info.Add($"Type: {obj.Type}");
                    _info.Add($"OGI/Animation Slots ({obj.OGISlots.Count}):");
                    for (int i = 0; i < obj.OGISlots.Count; i++)
                    {
                        var ogiId  = obj.OGISlots[i];
                        var animId = i < obj.AnimationSlots.Count ? obj.AnimationSlots[i] : (ushort)0xFFFF;
                        _info.Add($"  [{i}] OGI:{(ogiId == 0xFFFF ? "-" : $"{ogiId:X4}")}"
                                  + $"  Anim:{(animId == 0xFFFF ? "-" : $"{animId:X4}")}");
                    }

                    var firstOgi = obj.OGISlots.FirstOrDefault(s => s != 0xFFFF);
                    if (firstOgi != 0)
                    {
                        var ogiEntry = Find("OGI", firstOgi, e.Variation);
                        var odp = ogiEntry is null ? null : DataPath(ogiEntry, ".data");
                        var ogi = odp is null ? null : ParseTwin<PS2AnyOGI>(odp);
                        if (ogi is not null)
                        {
                            _previewOgi = ogi;
                            _skelParts  = BuildOgiParts(ogi, ogiEntry!.Variation);
                            FrameMeshes();
                        }
                    }
                    break;
                }
                default:
                {
                    var dp = DataPath(e, ".data");
                    if (dp is not null) _info.Add($"Data: {new FileInfo(dp).Length:N0} bytes");
                    _info.Add("(no 3D preview for this type yet)");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _info.Add($"ERROR: {ex.Message}");
        }
    }

    private void FrameMeshes()
    {
        var centers = _previewMeshes.Where(pm => pm.Mat.LocalCenter.HasValue).Select(pm => pm.Mat.LocalCenter!.Value)
            .Concat(_skelParts.Where(p => p.Mat.LocalCenter.HasValue).Select(p => p.Mat.LocalCenter!.Value))
            .ToList();
        if (centers.Count > 0)
        {
            _center = centers.Aggregate(Vector3.Zero, (a, b) => a + b) / centers.Count;
            _dist = MathF.Max(2f, _center.Length() * 2f + 3f);
        }
    }

    private List<(GpuMesh Mesh, Material Mat, Matrix4x4 Local)> BuildOgiParts(PS2AnyOGI ogi, string variation)
    {
        var result = new List<(GpuMesh, Material, Matrix4x4)>();
        var gl = Engine.Instance.GL;
        var jointWorld = MeshDecoder.ComputeJointWorlds(ogi);

        for (int i = 0; i < ogi.RigidModelIds.Count; i++)
        {
            var rmEntry = Find("RigidModel", ogi.RigidModelIds[i], variation);
            var rmDp = rmEntry is null ? null : DataPath(rmEntry, ".data");
            var rm = rmDp is null ? null : ParseTwin<PS2AnyRigidModel>(rmDp);
            if (rm is null) continue;

            var modelEntry = Find("Model", rm.Model, variation);
            var mdp = modelEntry is null ? null : DataPath(modelEntry, ".data");
            var model = mdp is null ? null : ParseTwin<PS2AnyModel>(mdp);
            if (model is null) continue;

            var subMats = rm.Materials.Select(mid => ResolveMaterial(mid, variation)).ToList();
            int jointIdx = i < ogi.JointIndices.Count ? ogi.JointIndices[i] : 0;
            var local = jointWorld.TryGetValue(jointIdx, out var jw) ? jw : Matrix4x4.Identity;

            foreach (var (mesh, mat) in MeshDecoder.DecodeModel(gl, model, subMats))
                result.Add((mesh, mat, local));
        }

        if (ogi.SkinID != 0)
        {
            var skinEntry = Find("Skin", ogi.SkinID, variation);
            var sdp = skinEntry is null ? null : DataPath(skinEntry, ".data");
            var skin = sdp is null ? null : ParseTwin<PS2AnySkin>(sdp);
            if (skin is not null)
            {
                var subMats = skin.SubSkins.Select(ss => ResolveMaterial(ss.Material, variation)).ToList();
                foreach (var (mesh, mat) in MeshDecoder.DecodeSkin(gl, skin, subMats))
                    result.Add((mesh, mat, Matrix4x4.Identity));
            }
        }

        if (ogi.BlendSkinID != 0)
        {
            var blendEntry = Find("BlendSkin", ogi.BlendSkinID, variation);
            var bdp = blendEntry is null ? null : DataPath(blendEntry, ".data");
            var blend = bdp is null ? null : ParseTwin<PS2AnyBlendSkin>(bdp);
            if (blend is not null)
            {
                var subMats = blend.SubBlends.Select(sb => ResolveMaterial(sb.Material, variation)).ToList();
                foreach (var (mesh, mat) in MeshDecoder.DecodeBlendSkin(gl, blend, subMats))
                {
                    mat.AlwaysOnTop = true;
                    result.Add((mesh, mat, Matrix4x4.Identity));
                }
            }
        }

        return result;
    }

    private (PS2AnyOGI Ogi, AssetEntry Entry)? FindBestFitOgi(int jointCount, string variation)
    {
        List<AssetEntry> list;
        lock (_tree)
        {
            if (!_tree.TryGetValue("OGI", out var l)) return null;
            list = l;
        }

        PS2AnyOGI? best = null; AssetEntry? bestEntry = null; int bestScore = int.MaxValue;
        foreach (var entry in list)
        {
            var dp = DataPath(entry, ".data");
            if (dp is null) continue;
            var ogi = ParseTwin<PS2AnyOGI>(dp);
            if (ogi is null) continue;

            int diff = Math.Abs(ogi.Joints.Count - jointCount);
            bool sameVar = entry.Variation == variation;
            if (diff == 0 && sameVar) return (ogi, entry);

            int score = diff * 2 - (sameVar ? 1 : 0);
            if (score < bestScore) { bestScore = score; best = ogi; bestEntry = entry; }
        }
        return best is null ? null : (best, bestEntry!);
    }

    private void RenderPreview(int w, int h)
    {
        var gl = Engine.Instance.GL;
        _fbo ??= new PreviewFbo(gl, w, h);
        _fbo.Resize(w, h);

        _fbo.Begin(new Vector3(0.13f, 0.13f, 0.16f));

        var pipeline = RenderPipeline.Instance;
        if (pipeline is not null && (_previewMeshes.Count > 0 || _skelParts.Count > 0))
        {
            var sh = pipeline.Shader;
            sh.Use();

            float yr = _yaw * MathF.PI / 180f, pr = _pitch * MathF.PI / 180f;
            var eye = _center + new Vector3(
                MathF.Cos(pr) * MathF.Sin(yr),
                MathF.Sin(pr),
                MathF.Cos(pr) * MathF.Cos(yr)) * _dist;

            var view = CameraComponent.CreateLookAtRH(eye, _center, Vector3.UnitY);
            var proj = CameraComponent.CreatePerspectiveRH(
                50f * MathF.PI / 180f, (float)w / h, 0.05f, 20000f);

            sh.Set("StartView", view);
            sh.Set("StartProjection", proj);
            sh.Set("StartModel", Matrix4x4.Identity);
            sh.Set("EyePosition", eye);
            sh.Set("EyeDirection", Vector3.Normalize(_center - eye));
            sh.Set("Resolution", new Vector2(w, h));
            sh.Set("Time", 0f);
            sh.Set("FogColor", new Vector3(0.13f, 0.13f, 0.16f));
            sh.Set("Diffuse", Vector4.One);
            sh.Set("Opacity", 1f);
            sh.Set("FlipY", 0f);
            sh.Set("DiffuseOnly", 0f);
            sh.SetMatrixArray("BoneMatrices", _boneMatrices);

            gl.Disable(Silk.NET.OpenGL.EnableCap.Blend);
            foreach (var (mesh, mat) in _previewMeshes)
            {
                mat.FogEnabled = false;
                mat.Apply(gl, sh);
                mesh.Draw(gl);
                mat.Restore(gl, sh);
            }
            foreach (var (mesh, mat, local) in _skelParts)
            {
                sh.Set("StartModel", local);
                mat.FogEnabled = false;
                mat.Apply(gl, sh);
                mesh.Draw(gl);
                mat.Restore(gl, sh);
            }
        }

        _fbo.End(Engine.Instance.Width, Engine.Instance.Height);
    }

    public override void OnImGuiRender()
    {
        if (_animData is not null)
        {
            if (_animPlaying)
            {
                var dt = ImGui.GetIO().DeltaTime;
                _animFrame += dt * _animFps;
                if (_animFrame > _animTotalFrames)
                {
                    if (_animLoop) _animFrame %= MathF.Max(1f, _animTotalFrames);
                    else { _animFrame = _animTotalFrames; _animPlaying = false; }
                }
            }
            if (_previewOgi is not null)
                _boneMatrices = MeshDecoder.SampleAnimationPose(_previewOgi, _animData, _animFrame);
        }

        int sw = Engine.Instance.Width, sh = Engine.Instance.Height;
        float treeW = 340f, infoW = 300f;

        ImGui.SetNextWindowPos(new Vector2(0, 0));
        ImGui.SetNextWindowSize(new Vector2(treeW, sh));
        ImGui.Begin("All Assets",
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

        if (ImGui.Button("< Back to Levels")) { Engine.Instance.ActiveScene = _back; ImGui.End(); return; }
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##search", "Search assets...", ref _search, 128);
        ImGui.Separator();

        if (!_scanned)
            ImGui.TextDisabled("Scanning asset database...");
        else
        {
            lock (_tree)
            {
                foreach (var (type, list) in _tree.OrderBy(t => t.Key))
                {
                    var filtered = _search.Length == 0 ? list
                        : list.Where(e => e.Display.Contains(_search, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (filtered.Count == 0) continue;

                    if (ImGui.TreeNodeEx($"{type} ({filtered.Count})"))
                    {
                        int shown = 0;
                        foreach (var e in filtered)
                        {
                            if (++shown > 500) { ImGui.TextDisabled("... (refine search)"); break; }
                            if (ImGui.Selectable($"  {e.Display}##{e.JsonPath.GetHashCode()}", _selected == e))
                                Select(e);
                        }
                        ImGui.TreePop();
                    }
                }
            }
        }
        ImGui.End();

        float previewW = sw - treeW - infoW;
        ImGui.SetNextWindowPos(new Vector2(treeW, 0));
        ImGui.SetNextWindowSize(new Vector2(previewW, sh));
        ImGui.Begin("Preview",
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

        if (_previewMeshes.Count > 0 || _skelParts.Count > 0)
        {
            float timelineH = _animData is not null ? 58f : 0f;
            var avail = ImGui.GetContentRegionAvail();
            int pw = Math.Max(64, (int)avail.X), ph = Math.Max(64, (int)(avail.Y - timelineH));
            RenderPreview(pw, ph);

            ImGui.Image((nint)_fbo!.ColorTexture, new Vector2(pw, ph),
                        new Vector2(1, 1), new Vector2(0, 0));

            if (ImGui.IsItemHovered())
            {
                var io = ImGui.GetIO();
                if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    _yaw   += io.MouseDelta.X * 0.4f;
                    _pitch  = Math.Clamp(_pitch + io.MouseDelta.Y * 0.4f, -89f, 89f);
                }
                if (io.MouseWheel != 0)
                    _dist = Math.Clamp(_dist * (1f - io.MouseWheel * 0.12f), 0.2f, 500f);
            }

            if (_animData is not null)
            {
                ImGui.Spacing();
                if (ImGui.Button(_animPlaying ? "Pause" : "Play")) _animPlaying = !_animPlaying;
                ImGui.SameLine();
                ImGui.Checkbox("Loop", ref _animLoop);
                ImGui.SameLine();
                ImGui.Text($"Frame {_animFrame:F1} / {_animTotalFrames}   ({_animFps:F0} fps)");

                float f = _animFrame;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.SliderFloat("##timeline", ref f, 0f, _animTotalFrames))
                {
                    _animFrame   = f;
                    _animPlaying = false;
                }
            }
        }
        else if (_previewTex is not null)
        {
            float avail  = ImGui.GetContentRegionAvail().X;
            float aspect = _previewTex.Height > 0 ? (float)_previewTex.Width / _previewTex.Height : 1f;
            float tw = MathF.Min(avail, 512f), th = tw / aspect;
            ImGui.Image((nint)_previewTex.GlId, new Vector2(tw, th));
        }
        else
        {
            ImGui.TextDisabled(_selected is null
                ? "Select an asset to preview"
                : "No visual preview for this asset");
        }
        ImGui.End();

        ImGui.SetNextWindowPos(new Vector2(sw - infoW, 0));
        ImGui.SetNextWindowSize(new Vector2(infoW, sh));
        ImGui.Begin("Info",
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);
        if (_selected is not null)
        {
            ImGui.TextWrapped(_selected.Display);
            ImGui.Separator();
        }
        foreach (var line in _info) ImGui.TextWrapped(line);
        ImGui.End();
    }
}
