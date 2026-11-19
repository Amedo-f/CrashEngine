using CrashEngine.Assets.Modern;
using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Importer;

// Amedo 2026-11-18
public sealed class ModernCharacterRenderer : Component, ISunCaster
{
    private const uint BoneBinding = 4;
    private const float PlainVertexColor = 0.5f;
    public const string LookName = "ModernLook";
    private static readonly Vector3 UndoLevelMirror = new(-1f, 1f, 1f);
    public const int MaxSlots = 255;

    public ModernCharacter Data { get; }
    public string SourcePath { get; }
    public int Clip => _state.Clip;
    public float Time { get => _state.Time; set => Set(_state with { Time = value }); }
    public bool Playing { get => _state.Playing; set => Set(_state with { Playing = value }); }
    public bool Loop { get => _state.Loop; set => Set(_state with { Loop = value }); }
    public float Speed { get => _state.Speed; set => Set(_state with { Speed = value }); }
    public readonly List<int> Slots = new();

    private readonly List<(ModernGpuMesh Mesh, Material Material)> _parts = new();
    private readonly List<Texture2D> _textures = new();
    private readonly ModernPose _pose;
    private readonly ShaderStorage _bones;
    private readonly Vector3 _min, _max;
    private int _uploadedFrame = -1;

    private readonly record struct PlayState(int Clip, float Time, int FromClip, float FromTime, float BlendTime, float BlendLength, float Speed, bool Playing, bool Loop)
    {
        public float Weight => BlendLength > 0f ? Math.Clamp(BlendTime / BlendLength, 0f, 1f) : 1f;
    }

    private static readonly PlayState Stopped = new(-1, 0f, -1, 0f, 0f, 0f, 1f, false, true);
    private PlayState _state = Stopped, _stepStart = Stopped, _shown = Stopped;
    private PlayState? _posed;
    private int _clockTicks = -1, _stepTicks;

    public ModernCharacterRenderer(GL gl, ModernCharacter data, string sourcePath)
    {
        Data = data;
        SourcePath = sourcePath;
        _pose = new ModernPose(data);
        _bones = new ShaderStorage(gl);
        foreach (var t in data.Textures)
        {
            using var stream = new MemoryStream(t.Encoded);
            _textures.Add(t.Encoded.Length > 0 ? Texture2D.FromStream(gl, stream) : Texture2D.White(gl));
        }
        var materials = data.Materials.Select(ToMaterial).ToList();
        _min = new Vector3(float.MaxValue);
        _max = new Vector3(float.MinValue);
        foreach (var m in data.Meshes)
        {
            var mesh = new ModernGpuMesh(gl, Vertices(m), m.Indices);
            _min = Vector3.Min(_min, mesh.Min);
            _max = Vector3.Max(_max, mesh.Max);
            _parts.Add((mesh, m.Material >= 0 && m.Material < materials.Count ? materials[m.Material] : new Material { Unlit = false, GameLit = true }));
        }
    }

    public static ModernCharacterRenderer? AttachTo(Entity instance, GL gl, ModernCharacter data, string sourcePath, float scale = 1f, IReadOnlyList<string>? slots = null)
    {
        if (instance.Children.Any(c => c.Name == LookName)) return null;
        HideStill(instance);
        var look = new Entity(LookName);
        var renderer = look.Add(new ModernCharacterRenderer(gl, data, sourcePath));
        instance.AddChild(look);
        renderer.Scale = scale;
        if (slots is null) renderer.Slots.AddRange(Enumerable.Range(0, Math.Min(data.Clips.Count, MaxSlots)));
        else renderer.Slots.AddRange(slots.Take(MaxSlots).Select(n => data.Clips.FindIndex(c => c.Name == n)));
        return renderer;
    }

    private static void HideStill(Entity e)
    {
        if (e.Get<MeshRenderer>() is { } still) still.Enabled = false;
        foreach (var c in e.Children) HideStill(c);
    }

    public float Scale
    {
        get => Entity!.Transform.Scale.Y;
        set => Entity!.Transform.Scale = UndoLevelMirror * MathF.Max(value, 1e-3f);
    }

    public int ClipForSlot(int slot) => slot >= 0 && slot < Slots.Count ? Slots[slot] : -1;

    public void Play(int clip)
    {
        int c = clip >= 0 && clip < Data.Clips.Count ? clip : -1;
        Set(_state with { Clip = c, Time = 0f, Playing = c >= 0, FromClip = -1, BlendLength = 0f });
    }

    public void Request(int clip, uint flags, float blendSeconds, float speed, float startPhase)
    {
        if (clip < 0 || clip >= Data.Clips.Count) return;
        var s = _state;
        bool loop = (flags & AnimatedObject.LoopFlag) != 0;
        float blend = (flags & AnimatedObject.BlendFlag) != 0 ? MathF.Max(blendSeconds, 0f) : 0f;
        if (blend > 0f && clip == s.Clip && s.Playing && loop == s.Loop && (flags & AnimatedObject.AppendFlag) == 0) return;
        float phase = (flags & AnimatedObject.SyncPhaseFlag) != 0 && s.Clip >= 0 && Data.Clips[s.Clip].Duration > 0f ? s.Time / Data.Clips[s.Clip].Duration
                    : (flags & AnimatedObject.StartPhaseFlag) != 0 ? startPhase : 0f;
        Set(new PlayState(clip, Math.Clamp(phase, 0f, 1f) * Data.Clips[clip].Duration, blend > 0f ? s.Clip : -1, s.Time, 0f, blend, speed, true, loop));
    }

    private void Set(PlayState s) => _state = _stepStart = _shown = s;

    public override void OnUpdate()
    {
        int step = AnimatedObject.LogicStepTicks;
        if (step <= 0)
        {
            _clockTicks = -1;
            Set(Advance(_state, EngineTime.Delta));
            return;
        }
        int now = AnimatedObject.LogicNowTicks;
        if (_clockTicks < 0 || now < _clockTicks)
        {
            _clockTicks = now;
            _stepTicks = 0;
            Set(_state);
            return;
        }
        if (now != _clockTicks)
        {
            int passed = now - _clockTicks;
            _stepTicks = Math.Min(step, passed);
            _stepStart = Advance(_state, (passed - _stepTicks) * AnimatedObject.TickTime);
            _state = Advance(_stepStart, _stepTicks * AnimatedObject.TickTime);
            _clockTicks = now;
        }
        _shown = Advance(_stepStart, Math.Clamp(AnimatedObject.LogicAlpha, 0f, 1f) * _stepTicks * AnimatedObject.TickTime);
    }

    private PlayState Advance(PlayState s, float seconds)
    {
        if (seconds <= 0f) return s;
        if (s.BlendLength > 0f)
        {
            float blended = s.BlendTime + seconds;
            float fromTime = s.FromClip >= 0 ? Wrap(s.FromClip, s.FromTime + seconds * s.Speed, true) : s.FromTime;
            s = blended >= s.BlendLength ? s with { FromClip = -1, BlendTime = 0f, BlendLength = 0f } : s with { BlendTime = blended, FromTime = fromTime };
        }
        if (!s.Playing || s.Clip < 0) return s;
        float time = Wrap(s.Clip, s.Time + seconds * s.Speed, s.Loop);
        return s with { Time = time, Playing = s.Loop || time < Data.Clips[s.Clip].Duration };
    }

    private float Wrap(int clip, float time, bool loop)
    {
        float duration = Data.Clips[clip].Duration;
        if (time <= duration) return time;
        return loop && duration > 0f ? time % duration : duration;
    }

    public override void OnRender(GL gl)
    {
        var pipeline = RenderPipeline.Instance;
        if (pipeline is null || !GlCaps.ModernSkin) return;
        var sh = pipeline.Shader;
        var u = sh.Cached(static s => new Uniforms(s));
        UploadBones();
        sh.Set(u.StartModel, Entity!.Transform.World);
        sh.Set(u.ShapeCenter, Vector4.Zero);
        sh.Set(u.ModernSkin, 1);
        foreach (var (mesh, mat) in _parts)
        {
            mat.Apply(gl, sh);
            bool lit = GameLightingDraw.Apply(sh, Entity!, mat);
            mesh.Draw(gl);
            if (lit) GameLightingDraw.Reset(sh);
            mat.Restore(gl, sh);
        }
        sh.Set(u.ModernSkin, 0);
    }

    public override void OnRenderDepth(GL gl)
    {
        if (!SunShadowPass.InDepthPass || !GlCaps.ModernSkin) return;
        var world = Entity!.Transform.World;
        float scale = MathF.Sqrt(MathF.Max(GameMath.LengthSquared(new Vector3(world.M11, world.M12, world.M13)),
                      MathF.Max(GameMath.LengthSquared(new Vector3(world.M21, world.M22, world.M23)), GameMath.LengthSquared(new Vector3(world.M31, world.M32, world.M33)))));
        float radius = GameMath.Length(_max - _min) * 0.55f * scale * 1.5f + 1f;
        SunShadowPass.AddCaster(this, world, Vector3.Transform((_min + _max) * 0.5f, world), radius, character: true);
    }

    public void DrawSunDepth(GL gl, in Matrix4x4 world)
    {
        UploadBones();
        foreach (var (mesh, mat) in _parts)
        {
            var sh = SunShadowPass.UseDepthProgram(mat.AlphaTest > 0f);
            var u = sh.Cached(static s => new Uniforms(s));
            sh.Set(u.StartModel, world);
            sh.Set(u.ModernSkin, 1);
            mat.Apply(gl, sh);
            GlState.DepthMask(gl, true);
            GlState.Culling(gl, false);
            mesh.Draw(gl);
            mat.Restore(gl, sh);
            sh.Set(u.ModernSkin, 0);
            SunShadowPass.Draws++;
        }
    }

    public override void OnDestroy()
    {
        foreach (var (mesh, _) in _parts) mesh.Dispose();
        foreach (var t in _textures) t.Dispose();
        _bones.Dispose();
    }

    private void UploadBones()
    {
        var shown = _shown;
        if (_posed != shown)
        {
            float weight = shown.Weight;
            if (weight < 1f) _pose.Blend(shown.FromClip >= 0 ? Data.Clips[shown.FromClip] : null, shown.FromTime, shown.Clip >= 0 ? Data.Clips[shown.Clip] : null, shown.Time, weight);
            else if (shown.Clip >= 0) _pose.Sample(Data.Clips[shown.Clip], shown.Time);
            else _pose.Rest();
            _posed = shown;
            _uploadedFrame = -1;
        }
        if (_uploadedFrame == RenderPipeline.Frame) { _bones.Bind(BoneBinding); return; }
        _bones.Upload(_pose.Skin, BoneBinding);
        _uploadedFrame = RenderPipeline.Frame;
    }

    private Material ToMaterial(ModernMaterial m) => new()
    {
        Name = m.Name,
        Albedo = m.BaseColorTexture >= 0 && m.BaseColorTexture < _textures.Count ? _textures[m.BaseColorTexture] : null,
        BaseColor = m.BaseColor,
        Culling = Material.CullMode.Both,
        AlphaTest = m.Alpha == ModernAlpha.Mask ? m.AlphaCutoff : 0f,
        AlphaBlend = m.Alpha == ModernAlpha.Blend,
        Unlit = false,
        GameLit = true,
    };

    private static ModernGpuMesh.Vertex[] Vertices(Assets.Modern.ModernMesh m)
    {
        const int n = ModernCharacterImporter.MaxInfluences;
        var verts = new ModernGpuMesh.Vertex[m.Positions.Length];
        for (int v = 0; v < verts.Length; v++)
        {
            int at = v * n;
            verts[v] = new ModernGpuMesh.Vertex
            {
                Position = m.Positions[v],
                Normal = m.Normals[v],
                UV = m.Uv[v],
                Color = m.Colors is { } c ? c[v] * PlainVertexColor : new Vector4(PlainVertexColor, PlainVertexColor, PlainVertexColor, 1f),
                Joints01 = Pair(m.Joints, at), Joints23 = Pair(m.Joints, at + 2), Joints45 = Pair(m.Joints, at + 4), Joints67 = Pair(m.Joints, at + 6),
                Weights0 = new Vector4(m.Weights[at], m.Weights[at + 1], m.Weights[at + 2], m.Weights[at + 3]),
                Weights1 = new Vector4(m.Weights[at + 4], m.Weights[at + 5], m.Weights[at + 6], m.Weights[at + 7]),
            };
        }
        return verts;
    }

    private static uint Pair(ushort[] joints, int at) => joints[at] | ((uint)joints[at + 1] << 16);

    private sealed class Uniforms(TwinShaderProgram sh)
    {
        public readonly TwinShaderProgram.Slot StartModel = sh.Uniform("StartModel");
        public readonly TwinShaderProgram.Slot ShapeCenter = sh.Uniform("ShapeCenter");
        public readonly TwinShaderProgram.Slot ModernSkin = sh.Uniform("ModernSkin");
    }
}
