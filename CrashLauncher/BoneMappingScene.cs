using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace CrashLauncher;

public sealed class BoneMappingScene : Scene
{
    private readonly PS2AnyTwinsanityRM2 _rm2;
    private readonly object _tablesHandle;
    private readonly uint _targetObjectId, _sourceOgiId;
    private readonly ushort _cloneOgiId;
    private readonly Scene _back;
    private readonly Action _onApplied;

    private List<TwinJoint> _targetJoints = new();
    private List<TwinJoint> _sourceJoints = new();
    private float _sourceOffsetX;

    private readonly Dictionary<int, (int TargetIdx, int PairNumber)> _jointMap;
    private int _nextPairNumber;
    private int? _selectedSource;
    private int? _selectedTarget;
    private int? _copiedTarget;
    private int _manualTargetIdx = -1;
    private string _status = "";

    private PreviewFbo? _fbo;
    private float _yaw = 20f, _pitch = 10f, _dist = 8f;
    private Vector3 _center = Vector3.Zero;
    private float _jointRadius = 0.03f;

    private readonly Dictionary<uint, Texture2D> _texCache;

    public BoneMappingScene(PS2AnyTwinsanityRM2 rm2, object tablesHandle, Dictionary<uint, Texture2D> texCache,
        uint targetObjectId, uint sourceOgiId, ushort cloneOgiId,
        Dictionary<int, (int TargetIdx, int PairNumber)> jointMap, Scene back, Action onApplied)
    {
        _rm2 = rm2;
        _tablesHandle = tablesHandle;
        _texCache = texCache;
        _targetObjectId = targetObjectId;
        _sourceOgiId = sourceOgiId;
        _cloneOgiId = cloneOgiId;
        _jointMap = jointMap;
        _nextPairNumber = jointMap.Count > 0 ? jointMap.Values.Max(v => v.PairNumber) + 1 : 0;
        _back = back;
        _onApplied = onApplied;
    }

    protected override void Build()
    {
        var pipeEnt = new Entity("RenderPipeline");
        var pipe = pipeEnt.Add(new RenderPipeline());
        pipe.FogColor = new Vector3(0.09f, 0.09f, 0.11f);
        AddRoot(pipeEnt);

        _targetJoints = MeshDecoder.GetJointList(_tablesHandle, _targetObjectId) ?? new();
        _sourceJoints = MeshDecoder.GetJointListByOgi(_tablesHandle, _sourceOgiId) ?? new();

        float targetExtent = Extent(_targetJoints);
        float sourceExtent = Extent(_sourceJoints);
        _sourceOffsetX = (targetExtent + sourceExtent) * 0.75f + 1f;

        var allY = _targetJoints.Select(j => j.WorldTranslation.Y)
            .Concat(_sourceJoints.Select(j => j.WorldTranslation.Y)).ToList();
        _center = new Vector3(_sourceOffsetX * 0.5f, allY.Count > 0 ? allY.Average() : 0f, 0f);
        _dist = MathF.Max(targetExtent, sourceExtent) * 2.2f + 2f;

        _jointRadius = MathF.Max(MathF.Max(targetExtent, sourceExtent) * 0.012f, 0.01f);

        _status = $"Target ({_targetJoints.Count} joints) vs Source ({_sourceJoints.Count} joints). " +
                   "Click a target bone, press Ctrl+C, click a source bone, press Ctrl+V.";
    }

    protected override void OnUpdate()
    {
        bool ctrl = Input.KeyHeld(Key.ControlLeft) || Input.KeyHeld(Key.ControlRight);

        if (ctrl && Input.KeyDown(Key.C) && _selectedTarget is not null)
        {
            _copiedTarget = _selectedTarget;
            _status = "Bone copied — select the source bone, then press Ctrl+V.";
        }
        if (ctrl && Input.KeyDown(Key.V) && _copiedTarget is not null && _selectedSource is not null)
        {
            int pairNum = _nextPairNumber++;
            _jointMap[_selectedSource.Value] = (_copiedTarget.Value, pairNum);
            _status = $"Mapped pair #{pairNum}: bone {_selectedSource.Value} -> bone {_copiedTarget.Value}";
            _copiedTarget = null;
        }
        if (Input.KeyDown(Key.Escape) && _copiedTarget is not null)
        {
            _copiedTarget = null;
            _status = "Cancelled — pick a target bone and press Ctrl+C to try again.";
        }
    }

    private static float Extent(List<TwinJoint> joints)
    {
        if (joints.Count == 0) return 1f;
        float max = 0f;
        var origin = joints[0].WorldTranslation;
        foreach (var j in joints)
        {
            var d = new Vector3(j.WorldTranslation.X - origin.X, j.WorldTranslation.Y - origin.Y, j.WorldTranslation.Z - origin.Z);
            max = MathF.Max(max, d.Length());
        }
        return MathF.Max(max, 0.5f);
    }

    private Vector3 JointWorld(TwinJoint j, bool isSource) =>
        new(j.WorldTranslation.X + (isSource ? _sourceOffsetX : 0f), j.WorldTranslation.Y, j.WorldTranslation.Z);

    private void RenderPreview(int w, int h, out Matrix4x4 view, out Matrix4x4 proj, out Vector3 camRight, out Vector3 camUp)
    {
        var gl = Engine.Instance.GL;
        _fbo ??= new PreviewFbo(gl, w, h);
        _fbo.Resize(w, h);
        _fbo.Begin(new Vector3(0.10f, 0.10f, 0.13f));

        float yr = _yaw * MathF.PI / 180f, pr = _pitch * MathF.PI / 180f;
        var eye = _center + new Vector3(
            MathF.Cos(pr) * MathF.Sin(yr), MathF.Sin(pr), MathF.Cos(pr) * MathF.Cos(yr)) * _dist;
        view = CameraComponent.CreateLookAtRH(eye, _center, Vector3.UnitY);
        proj = CameraComponent.CreatePerspectiveRH(50f * MathF.PI / 180f, (float)w / h, 0.05f, 20000f);

        var forward = Vector3.Normalize(_center - eye);
        camRight = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, forward));
        camUp    = Vector3.Cross(forward, camRight);

        var pipeline = RenderPipeline.Instance;
        if (pipeline is not null)
        {
            var sh = pipeline.Shader;
            sh.Use();
            sh.Set("StartView", view);
            sh.Set("StartProjection", proj);
            sh.Set("EyePosition", eye);
            sh.Set("EyeDirection", forward);
            sh.Set("Resolution", new Vector2(w, h));
            sh.Set("Time", 0f);
            sh.Set("FogColor", new Vector3(0.10f, 0.10f, 0.13f));
            sh.Set("FlipY", 0f);
            sh.Set("DiffuseOnly", 0f);
            sh.SetMatrixArray("BoneMatrices", RenderPipeline.IdentityBoneMatrices);

            sh.Set("StartModel", Matrix4x4.Identity);
            sh.Set("Diffuse", Vector4.One);
            sh.Set("Opacity", 1f);
            gl.Disable(EnableCap.Blend);
            gl.Disable(EnableCap.CullFace);
            gl.DepthMask(true);
            gl.LineWidth(2f);

            using var lines = BuildSkeletonLines(gl);
            using var circles = BuildJointCircles(gl, camRight, camUp);
            var mat = new Material { FogEnabled = false, Culling = Material.CullMode.Both };
            mat.Apply(gl, sh);
            lines.Draw(gl);
            circles.Draw(gl);
            mat.Restore(gl, sh);
        }

        _fbo.End(Engine.Instance.Width, Engine.Instance.Height);
    }

    private GpuMesh BuildSkeletonLines(GL gl)
    {
        var verts = new List<GpuMesh.Vertex>();

        void AddLine(Vector3 a, Vector3 b, Vector4 color)
        {
            verts.Add(new GpuMesh.Vertex { Position = a, Color = color });
            verts.Add(new GpuMesh.Vertex { Position = b, Color = color });
        }

        void AddSkeleton(List<TwinJoint> joints, bool isSource)
        {
            var boneColor = isSource ? new Vector4(0.9f, 0.55f, 0.15f, 1f) : new Vector4(0.35f, 0.45f, 0.55f, 1f);
            for (int i = 0; i < joints.Count; i++)
            {
                var j = joints[i];
                var pos = JointWorld(j, isSource);
                if (j.ParentIndex >= 0 && j.ParentIndex < joints.Count && j.ParentIndex != i)
                    AddLine(JointWorld(joints[j.ParentIndex], isSource), pos, boneColor);
            }
        }

        AddSkeleton(_targetJoints, false);
        AddSkeleton(_sourceJoints, true);


        return new GpuMesh(gl, verts.ToArray(), PrimitiveType.Lines);
    }

    private GpuMesh BuildJointCircles(GL gl, Vector3 right, Vector3 up)
    {
        var verts = new List<GpuMesh.Vertex>();
        const int Segments = 12;

        void AddCircle(Vector3 center, Vector4 color, float radius)
        {
            for (int i = 0; i < Segments; i++)
            {
                float a0 = i / (float)Segments * MathF.Tau;
                float a1 = (i + 1) / (float)Segments * MathF.Tau;
                var p0 = center;
                var p1 = center + right * (MathF.Cos(a0) * radius) + up * (MathF.Sin(a0) * radius);
                var p2 = center + right * (MathF.Cos(a1) * radius) + up * (MathF.Sin(a1) * radius);
                verts.Add(new GpuMesh.Vertex { Position = p0, Color = color });
                verts.Add(new GpuMesh.Vertex { Position = p1, Color = color });
                verts.Add(new GpuMesh.Vertex { Position = p2, Color = color });
            }
        }

        bool blinkOn = MathF.Sin(EngineTime.Total * 10f) > 0f;
        var boundColor  = new Vector4(0.2f, 0.7f, 1f, 1f);
        var selColor    = new Vector4(1f, 1f, 0.2f, 1f);
        var blinkColor  = new Vector4(1f, 1f, 1f, 1f);
        bool SourceBound(int i) => _jointMap.ContainsKey(i);
        bool TargetBound(int i) => _jointMap.Values.Any(v => v.TargetIdx == i);

        void AddSkeleton(List<TwinJoint> joints, bool isSource, int? selected)
        {
            var baseColor = isSource ? new Vector4(0.9f, 0.55f, 0.15f, 1f) : new Vector4(0.35f, 0.45f, 0.55f, 1f);
            for (int i = 0; i < joints.Count; i++)
            {
                bool isCopied = !isSource && _copiedTarget == i;
                bool isBound  = isSource ? SourceBound(i) : TargetBound(i);
                var color = isCopied ? (blinkOn ? blinkColor : baseColor)
                          : isBound ? boundColor
                          : selected == i ? selColor
                          : baseColor;
                float radius = (selected == i || isCopied) ? _jointRadius * 1.8f : _jointRadius;
                AddCircle(JointWorld(joints[i], isSource), color, radius);
            }
        }

        AddSkeleton(_targetJoints, false, _selectedTarget);
        AddSkeleton(_sourceJoints, true, _selectedSource);

        return new GpuMesh(gl, verts.ToArray(), PrimitiveType.Triangles);
    }

    private static Vector2? WorldToScreen(Vector3 world, Matrix4x4 vp, Vector2 imageSize)
    {
        var clip = Vector4.Transform(new Vector4(world, 1f), vp);
        if (clip.W <= 0.0001f) return null;
        var ndc = new Vector2(clip.X / clip.W, clip.Y / clip.W);
        return new Vector2((ndc.X * 0.5f + 0.5f) * imageSize.X, (1f - (ndc.Y * 0.5f + 0.5f)) * imageSize.Y);
    }

    private bool TryPick(Vector2 mouseInImage, Vector2 imageSize, Matrix4x4 view, Matrix4x4 proj,
        out bool isSource, out int jointIndex)
    {
        float bestDist = 18f;
        bool bestIsSource = false;
        int bestIndex = -1;
        var vp = view * proj;

        void Check(List<TwinJoint> joints, bool src)
        {
            for (int i = 0; i < joints.Count; i++)
            {
                var screen = WorldToScreen(JointWorld(joints[i], src), vp, imageSize);
                if (screen is null) continue;
                float d = Vector2.Distance(screen.Value, mouseInImage);
                if (d < bestDist) { bestDist = d; bestIsSource = src; bestIndex = i; }
            }
        }
        Check(_targetJoints, false);
        Check(_sourceJoints, true);
        isSource = bestIsSource;
        jointIndex = bestIndex;
        return jointIndex >= 0;
    }

    private static string JointInfo(List<TwinJoint> joints, int? idx)
    {
        if (idx is null || idx < 0 || idx >= joints.Count) return "(none selected)";
        var j = joints[idx.Value];
        var children = joints.Count(x => x.ParentIndex == idx.Value);
        return $"Joint #{j.Index}  (parent #{j.ParentIndex}, {children} child bone(s))";
    }

    public override void OnImGuiRender()
    {
        int sw = Engine.Instance.Width, sh_ = Engine.Instance.Height;
        float sideW = 340f;

        ImGui.SetNextWindowPos(new Vector2(0, 0));
        ImGui.SetNextWindowSize(new Vector2(sw - sideW, sh_));
        ImGui.Begin("Bone Mapping — 3D Preview",
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

        var avail = ImGui.GetContentRegionAvail();
        int pw = Math.Max(64, (int)avail.X), ph = Math.Max(64, (int)avail.Y);
        RenderPreview(pw, ph, out var view, out var proj, out _, out _);

        var cursorBefore = ImGui.GetCursorScreenPos();
        ImGui.Image((nint)_fbo!.ColorTexture, new Vector2(pw, ph), new Vector2(0, 1), new Vector2(1, 0));

        var vpMat = view * proj;
        var dl = ImGui.GetWindowDrawList();

        if (_copiedTarget is not null)
        {
            var msg = "Select the source bone, then press Ctrl+V";
            var pos = cursorBefore + new Vector2(12, 12);
            var textSize = ImGui.CalcTextSize(msg);
            dl.AddRectFilled(pos - new Vector2(6, 4), pos + textSize + new Vector2(6, 4), 0xCC000000);
            dl.AddText(pos, 0xFF40FFFF, msg);
        }


        if (ImGui.IsItemHovered())
        {
            var io = ImGui.GetIO();
            if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                _yaw += io.MouseDelta.X * 0.4f;
                _pitch = Math.Clamp(_pitch + io.MouseDelta.Y * 0.4f, -89f, 89f);
            }
            if (io.MouseWheel != 0)
                _dist = Math.Clamp(_dist * (1f - io.MouseWheel * 0.12f), 0.2f, 2000f);

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                var mouseInImage = ImGui.GetMousePos() - cursorBefore;
                if (TryPick(mouseInImage, new Vector2(pw, ph), view, proj, out bool isSrc, out int idx))
                {
                    if (isSrc)
                    {
                        _selectedSource = idx;
                        _manualTargetIdx = _jointMap.TryGetValue(idx, out var existingPair) ? existingPair.TargetIdx : idx;
                    }
                    else { _selectedTarget = idx; }
                }
                else
                {
                    _selectedSource = null;
                    _selectedTarget = null;
                    _manualTargetIdx = -1;
                }
            }
        }

        ImGui.End();

        ImGui.SetNextWindowPos(new Vector2(sw - sideW, 0));
        ImGui.SetNextWindowSize(new Vector2(sideW, sh_));
        ImGui.Begin("Bone Mapping", ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse);

        ImGui.TextWrapped("Orange = source bones, Blue-grey = target bones. Bound pairs turn " +
                           "bright BLUE on both sides. Yellow = selected. Right-drag to orbit, scroll to zoom.");
        ImGui.Separator();
        ImGui.TextWrapped("Just Copy Bone: click a target bone, press Ctrl+C (it blinks), click a " +
                           "source bone, press Ctrl+V to bind. Esc cancels a pending copy. Or use " +
                           "Change Number below — click a Coco bone, edit its number, Save. No " +
                           "need to touch Crash's skeleton at all for that.");
        ImGui.Spacing();
        ImGui.TextWrapped(_status);
        ImGui.Separator();

        ImGui.TextWrapped("Target: " + JointInfo(_targetJoints, _selectedTarget));
        ImGui.TextWrapped("Source: " + JointInfo(_sourceJoints, _selectedSource));
        ImGui.Separator();

        ImGui.TextWrapped("Change Number (click a Coco bone above, edit its number, Save):");
        ImGui.SetNextItemWidth(90f);
        ImGui.InputInt("New number ###manualtgt", ref _manualTargetIdx);
        if (ImGui.Button("Save##manualset"))
        {
            if (_selectedSource is null)
                _status = "Click a Coco bone first.";
            else if (_manualTargetIdx < 0 || _manualTargetIdx >= _targetJoints.Count)
                _status = $"New number {_manualTargetIdx} out of range (0..{_targetJoints.Count - 1}).";
            else
            {
                int srcIdx = _selectedSource.Value;
                int pairNum = _jointMap.TryGetValue(srcIdx, out var existing) ? existing.PairNumber : _nextPairNumber++;
                _jointMap[srcIdx] = (_manualTargetIdx, pairNum);
                _status = $"Saved: Coco bone {srcIdx} is now numbered {_manualTargetIdx}.";
            }
        }
        ImGui.Separator();

        ImGui.Text($"Mapped: {_jointMap.Count} / {_sourceJoints.Count} source bone(s)");
        ImGui.BeginChild("##maplist", new Vector2(-1f, sh_ - 400f), ImGuiChildFlags.Border);
        foreach (var (src, val) in _jointMap.OrderBy(kv => kv.Value.PairNumber).ToList())
        {
            ImGui.Text($"#{val.PairNumber}: source {src}  ->  target {val.TargetIdx}");
            ImGui.SameLine();
            if (ImGui.SmallButton($"x##unmap{src}")) _jointMap.Remove(src);
        }
        ImGui.EndChild();

        ImGui.Spacing();
        bool canApply = _jointMap.Count > 0;
        if (!canApply) ImGui.BeginDisabled();
        if (ImGui.Button("Apply##bonemapapply", new Vector2(-1f, 32f)))
        {
            var plainMap = _jointMap.ToDictionary(kv => kv.Key, kv => kv.Value.TargetIdx);
            try
            {
                if (MeshDecoder.RemapReskinJoints(Engine.Instance.GL, _rm2, _tablesHandle, _texCache, _cloneOgiId, _sourceOgiId, plainMap, out string log))
                {
                    _onApplied();
                    Engine.Instance.ActiveScene = _back;
                    ImGui.End();
                    return;
                }
                _status = $"Apply failed: {log}";
            }
            catch (Exception ex) { _status = $"Apply failed: {ex.Message}"; }
        }
        if (!canApply)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Map at least one bone first.");
        }
        if (ImGui.Button("Cancel##bonemapcancel", new Vector2(-1f, 0f)))
        {
            Engine.Instance.ActiveScene = _back;
            ImGui.End();
            return;
        }
        ImGui.End();
    }
}
