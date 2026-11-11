using CrashEngine.Core;
using CrashEngine.Renderer;

namespace CrashEngine.Importer;

public sealed class AnimatedObject : CrashEngine.Core.Component
{
    public sealed class SlotGroup
    {
        public ushort SlotIndex;
        public ushort AnimId;
        public Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyAnimation Anim { get; set; } = null!;
        public Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI Ogi { get; set; } = null!;
        public Entity MeshRoot { get; set; } = null!;
        public bool StaticMesh { get; set; } // Amedo 2026-09-28
        public List<(Entity Child, int JointIndex, System.Numerics.Matrix4x4 BindLocal)> AttachedRigidParts { get; } = new();
        public List<MeshDecoder.MorphMesh> MorphMeshes { get; } = new();
    }

    public List<SlotGroup> Groups { get; } = new();
    public List<SlotGroup> ModelGroups { get; } = new(); // Amedo 2026-09-28
    public SlotGroup? DefaultGroup { get; set; }
    public SlotGroup? CurrentGroup { get; private set; }
    private Entity? _modelRoot; // Amedo 2026-09-28
    public SlotGroup? ModelGroup { get; private set; } // Amedo 2026-10-01
    public Entity? VisibleRoot => _modelRoot ?? (CurrentGroup ?? DefaultGroup)?.MeshRoot; // Amedo 2026-09-28

    public Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyAnimation? Current => CurrentGroup?.Anim;
    public System.Numerics.Matrix4x4[] BoneMatrices { get; private set; } = RenderPipeline.IdentityBoneMatrices;
    public Dictionary<int, System.Numerics.Matrix4x4>? JointWorlds { get; private set; } // Amedo 2026-09-26
    public Dictionary<int, System.Numerics.Matrix4x4>? ShownJointWorlds { get; private set; } // Amedo 2026-10-01
    public MeshDecoder.IJointControllers? Controllers { get; set; } // Amedo 2026-09-28
    public int ReactSlots { get; set; } = 0xFF; // Amedo
    private readonly MeshDecoder.PoseBuffers _pose = new(); // Amedo 2026-09-29
    private readonly MeshDecoder.PoseBuffers _shownPose = new(); // Amedo 2026-10-01

    public static float LogicAlpha { get; set; } = 1f; // Amedo 2026-10-01
    public static int LogicNowTicks { get; set; } // Amedo 2026-10-02
    public static int LogicStepTicks { get; set; }
    public static bool InLogicCompose { get; private set; } // Amedo 2026-10-02
    private bool _logicDriven;
    private bool _rebase;
    private int _prevNow;
    private int _version, _shownVersion = -1, _shownTicks = int.MinValue;

    // Amedo 2026-10-01
    public bool LogicDriven
    {
        get => _logicDriven;
        set
        {
            if (_logicDriven == value) return;
            _logicDriven = value;
            if (value) _rebase = true;
            else _clock = _now * TickTime;
            _version++;
        }
    }

    public float Speed { get; set; } = 1f; // Amedo 2026-09-26

    // Amedo 2026-09-27
    public const uint LoopFlag = 0x1000, BlendFlag = 0x2000, StartPhaseFlag = 0x20000, SyncPhaseFlag = 0x40000, AppendFlag = 0x100000;

    private const float TickRate = 9000f;
    private const float TickTime = 1f / 9000f;

    // Amedo 2026-09-27
    private sealed class Layer
    {
        public SlotGroup? Group;
        public Layer? Twin;
        public uint Flags;
        public float Phase;
        public int Start, Duration, Elapsed, BlendStart, BlendTicks;
        public float Frac, Weight = 1f;
        public int F0, F1;
        public int PrevStart = int.MinValue; // Amedo 2026-10-02

        public int Frames => (int)((Flags >> 5) & 0xFFFF);
        public bool Loop => (Flags & 4) != 0;
        public bool HasBody => Group is { } g && g.Anim.HasAnimationData;
        public bool HasFace => Group is { } g && g.Anim.HasFacialAnimationData;
    }

    private readonly List<Layer> _body = new();
    private readonly List<Layer> _face = new();
    private double _clock;
    private int _now;
    private bool _paused;
    private bool _defaultLoop = true;

    public bool AllowBlend { get; set; } = true; // Amedo 2026-09-27

    private float[]? _faceOut;
    private float[]? _faceSrc;
    private int _faceCount;
    private bool _faceApplied;
    public IReadOnlyList<float>? FaceWeights => _faceCount > 0 ? _faceOut : null; // Amedo 2026-09-26

    public (SlotGroup? From, float FromFrame, bool FromLoop, float Weight) BlendState => // Amedo 2026-09-27
        _body.Count >= 2 ? (_body[^2].Group, FramePosition(_body[^2]), _body[^2].Loop, _body[^1].Weight) : (null, 0f, false, 1f);

    public int LayerCount => _body.Count; // Amedo 2026-09-27

    public float Frame
    {
        get => _body.Count == 0 ? 0f : FramePosition(_body[^1]);
        set
        {
            if (_body.Count == 0) return;
            Seek(_body[^1], value);
            if (_body[^1].Twin is { } twin) Seek(twin, value);
        }
    }

    public bool Playing
    {
        get => !_paused && _body.Count > 0 && (_body[^1].Flags & 2) == 0;
        set => _paused = !value;
    }

    public bool Loop
    {
        get => _body.Count > 0 ? _body[^1].Loop : _defaultLoop;
        set
        {
            _defaultLoop = value;
            if (_body.Count == 0) return;
            SetLoop(_body[^1], value);
            if (_body[^1].Twin is { } twin) SetLoop(twin, value);
        }
    }

    private static void SetLoop(Layer l, bool loop) => l.Flags = loop ? l.Flags | 4u : l.Flags & ~4u;

    // Amedo 2026-09-26
    public void Play(SlotGroup group, float blendSeconds = 0f, float speed = 1f) =>
        Request(group, (_defaultLoop ? LoopFlag : 0u) | (0f < blendSeconds ? BlendFlag : 0u), blendSeconds, speed);

    // Amedo 2026-09-27
    public void Request(SlotGroup group, uint flags, float blendSeconds, float speed, float startPhase = 0f)
    {
        int blendTicks = (flags & BlendFlag) != 0 ? GameMath.Trunc(blendSeconds * TickRate) : 0;
        bool blend = AllowBlend && blendTicks != 0; // Amedo 2026-11-11
        uint req = ((flags >> 12) & 1) | (blendTicks != 0 ? 2u : 0u) | ((flags >> 17) & 4) | ((flags >> 15) & 0x20);
        if ((flags & SyncPhaseFlag) != 0) req |= 8;
        else if ((flags & StartPhaseFlag) != 0) req |= 0x10;
        int duration = DurationTicks(group, speed);
        var body = Insert(_body, group, req, duration, blendTicks, startPhase, blend);
        var face = Insert(_face, group, req, duration, blendTicks, startPhase, blend);
        if (body is not null && face is not null)
        {
            body.Twin = face;
            face.Twin = body;
        }

        if (_modelRoot is not null && _modelRoot != group.MeshRoot) _modelRoot.Active = false; // Amedo 2026-09-28
        _modelRoot = null;
        ModelGroup = null; // Amedo 2026-10-01
        var previouslyVisible = CurrentGroup ?? DefaultGroup;
        if (previouslyVisible is not null && previouslyVisible.MeshRoot != group.MeshRoot)
            previouslyVisible.MeshRoot.Active = false;
        group.MeshRoot.Active = true;

        CurrentGroup = group;
        Speed = speed;
        _version++; // Amedo 2026-10-01
    }

    // Amedo 2026-09-28
    public void ShowModel(SlotGroup group, float blendSeconds)
    {
        var visible = VisibleRoot;
        if (visible is not null && visible != group.MeshRoot) visible.Active = false;
        group.MeshRoot.Active = true;
        _modelRoot = group.MeshRoot;
        ModelGroup = group; // Amedo 2026-10-01
        RequestNone(blendSeconds);
    }

    // Amedo 2026-09-27
    public void RequestNone(float blendSeconds)
    {
        int blendTicks = GameMath.Trunc(blendSeconds * TickRate);
        var body = Insert(_body, null, 0u, 0, blendTicks, 0f, false);
        var face = Insert(_face, null, 0u, 0, blendTicks, 0f, false);
        _version++; // Amedo 2026-10-01
        if (body is null || face is null) return;
        body.Twin = face;
        face.Twin = body;
    }

    public void Stop()
    {
        _body.Clear(); // Amedo 2026-09-27
        _face.Clear();
        _paused = false;
        _faceOut = null;
        _faceSrc = null;
        _faceCount = 0;
        _faceApplied = false;
        BoneMatrices = RenderPipeline.IdentityBoneMatrices;
        JointWorlds  = null; // Amedo 2026-09-26
        ShownJointWorlds = null; // Amedo 2026-10-01
        _version++;
        if (_modelRoot is not null) // Amedo 2026-09-28
        {
            _modelRoot.Active = false;
            if (DefaultGroup is not null) DefaultGroup.MeshRoot.Active = true;
            _modelRoot = null;
            ModelGroup = null; // Amedo 2026-10-01
        }
        if (CurrentGroup is not null)
        {
            foreach (var (child, _, bindLocal) in CurrentGroup.AttachedRigidParts)
                child.Transform.LocalMatrix = bindLocal;
            ResetFace(CurrentGroup);

            if (DefaultGroup is not null && CurrentGroup.MeshRoot != DefaultGroup.MeshRoot)
            {
                CurrentGroup.MeshRoot.Active = false;
                DefaultGroup.MeshRoot.Active = true;
            }
        }
        CurrentGroup = null;
    }

    private static void ResetFace(SlotGroup g)
    {
        if (g.MorphMeshes.Count == 0) return;
        var gl   = Engine.Instance.GL;
        var zero = new float[MeshDecoder.MaxMorphShapes];
        foreach (var mm in g.MorphMeshes) mm.ApplyWeights(gl, zero);
    }

    public override void OnUpdate()
    {
        if (_body.Count == 0 && _face.Count == 0) return; // Amedo 2026-09-27
        if (_logicDriven) // Amedo 2026-10-01
        {
            ShowLogicPose();
            return;
        }
        if (!_paused)
        {
            _clock += EngineTime.Delta;
            _now = (int)(_clock * TickRate);
        }
        foreach (var l in _body) Advance(l, true);
        Cleanup(_body, true);
        foreach (var l in _face) Advance(l, false);
        Cleanup(_face, false);
        if (CurrentGroup is not { } g) return;
        ComposeBody(g);
        UpdateFace(g);
    }

    // Amedo 2026-10-01
    public void AdvanceTo_294980(int ticks)
    {
        if (_rebase)
        {
            Rebase(ticks - _now);
            _rebase = false;
        }
        _prevNow = _now;
        if (!_paused) _now = ticks;
        foreach (var l in _body) Advance(l, true);
        Cleanup(_body, true);
        foreach (var l in _face) Advance(l, false);
        Cleanup(_face, false);
        _version++;
    }

    // Amedo 2026-10-02
    public void SkipLogicStep() { }

    // Amedo
    private System.Numerics.Matrix4x4[] _exitPoints = [];
    private ulong _exitPointsKept;

    // Amedo
    public void MarkExitPoints_295690() => _exitPointsKept = 0;

    // Amedo
    public bool KeptExitPoint(int exit, out System.Numerics.Matrix4x4 m)
    {
        if ((uint)exit < 64 && (_exitPointsKept & (1UL << exit)) != 0)
        {
            m = _exitPoints[exit];
            return true;
        }
        m = default;
        return false;
    }

    // Amedo
    public void KeepExitPoint(int exit, System.Numerics.Matrix4x4 m)
    {
        if ((uint)exit >= 64) return;
        if (_exitPoints.Length <= exit) Array.Resize(ref _exitPoints, exit + 1);
        _exitPoints[exit] = m;
        _exitPointsKept |= 1UL << exit;
    }

    // Amedo 2026-10-01
    public void Compose_294B18()
    {
        if (CurrentGroup is not { } g) return;
        if (_body.Count == 0 && Controllers is null) // Amedo
        {
            JointWorlds = null;
            return;
        }
        InLogicCompose = true; // Amedo 2026-10-02
        try { _logicBones = SamplePose(g, _pose, 0, false, out var worlds); JointWorlds = worlds; }
        finally { InLogicCompose = false; }
        _composedVersion = _version;
    }

    private System.Numerics.Matrix4x4[]? _logicBones; // Amedo 2026-10-01
    private int _composedVersion = -1;

    // Amedo 2026-10-01
    private void Rebase(int delta)
    {
        if (delta == 0) return;
        _now += delta;
        foreach (var l in _body) { l.Start += delta; l.BlendStart += delta; if (l.PrevStart != int.MinValue) l.PrevStart += delta; } // Amedo 2026-10-02
        foreach (var l in _face) { l.Start += delta; l.BlendStart += delta; }
    }

    // Amedo 2026-10-01
    private void ShowLogicPose()
    {
        if (CurrentGroup is not { } g) return;
        if (_rebase && LogicStepTicks > 0) // Amedo 2026-10-02
        {
            Rebase(LogicNowTicks - _now);
            _prevNow = _now;
            _rebase = false;
        }
        int shown = _paused || _rebase || LogicStepTicks <= 0 || _now != LogicNowTicks ? _now // Amedo 2026-10-02
            : _now - GameMath.Trunc((1f - Math.Clamp(LogicAlpha, 0f, 1f)) * (_now - _prevNow));
        if (Controllers is null && shown == _shownTicks && _version == _shownVersion) return;
        _shownTicks = shown;
        _shownVersion = _version;
        if (_body.Count == 0 && Controllers is null) // Amedo
        {
            BoneMatrices = RenderPipeline.IdentityBoneMatrices;
            ShownJointWorlds = null;
            foreach (var (child, _, bindLocal) in g.AttachedRigidParts)
                child.Transform.LocalMatrix = bindLocal;
        }
        else
        {
            Dictionary<int, System.Numerics.Matrix4x4> worlds;
            if (Controllers is null && shown == _now && _composedVersion == _version && _logicBones is { } bones && JointWorlds is { } logicWorlds) // Amedo 2026-10-02
            {
                BoneMatrices = bones;
                worlds = logicWorlds;
            }
            else BoneMatrices = SamplePose(g, _shownPose, shown, true, out worlds);
            ShownJointWorlds = worlds;
            foreach (var (child, jointIndex, _) in g.AttachedRigidParts)
                if (worlds.TryGetValue(jointIndex, out var jw))
                    child.Transform.LocalMatrix = jw;
        }
        UpdateFace(g);
    }

    // Amedo 2026-10-01
    private System.Numerics.Matrix4x4[] SamplePose(SlotGroup g, MeshDecoder.PoseBuffers buffers, int ticks, bool shown,
                                                   out Dictionary<int, System.Numerics.Matrix4x4> worlds)
    {
        var layers = buffers.Layers;
        layers.Clear();
        foreach (var l in _body)
        {
            int f0 = l.F0, f1 = l.F1;
            float frac = l.Frac, weight = l.Weight;
            if (shown) ShownFrame(l, ticks, out f0, out f1, out frac, out weight);
            layers.Add(new MeshDecoder.PoseLayer(l.HasBody ? l.Group!.Anim.MainAnimation : null, f0, f1, frac, weight));
        }
        return MeshDecoder.SampleLayeredPose(g.Ogi, layers, out worlds, Controllers, buffers, ReactSlots);
    }

    // Amedo 2026-10-01
    private static void ShownFrame(Layer l, int ticks, out int f0, out int f1, out float frac, out float weight)
    {
        f0 = l.F0;
        f1 = l.F1;
        frac = l.Frac;
        weight = l.Weight;
        if ((l.Flags & 2) != 0) return;
        if ((l.Flags & 1) == 0) // Amedo 2026-10-02
        {
            if (l.HasBody) (f0, f1, frac) = FrameAt(l, 0);
            if ((l.Flags & 0x10) != 0) weight = 0f;
            return;
        }
        if (!l.HasBody) return;
        int e = ticks - l.Start;
        if (e < 0 && l.PrevStart != int.MinValue && l.PrevStart <= ticks) e = ticks - l.PrevStart; // Amedo 2026-10-02
        if (e < 0) e = 0;
        if (l.Duration <= e)
        {
            if (!l.Loop) e = l.Duration;
            else if (0 < l.Duration) e -= l.Duration * GameMath.Trunc(e * TickTime / (l.Duration * TickTime));
        }
        (f0, f1, frac) = FrameAt(l, e);
        if ((l.Flags & 0x10) == 0) return;
        int b = ticks - l.BlendStart;
        weight = b < 0 ? 0f : b < l.BlendTicks ? b * TickTime / (l.BlendTicks * TickTime) : 1f;
    }

    // Amedo 2026-09-27
    private Layer? Insert(List<Layer> list, SlotGroup? group, uint req, int duration, int blendTicks, float phase, bool blend)
    {
        var layer = new Layer { Group = group, Duration = duration, BlendTicks = blendTicks };
        layer.Flags = blendTicks != 0 ? 0x10u : 0u;
        if (group is null) layer.Flags |= blendTicks == 0 ? 2u : 0u;
        else layer.Flags |= (uint)(FrameCount(group) & 0xFFFF) << 5;
        layer.Flags = (layer.Flags & ~4u) | ((req & 1) << 2);
        layer.Flags = (layer.Flags & ~8u) | ((req << 1) & 8);
        if ((req & 0x10) != 0)
        {
            layer.Phase = phase;
            layer.Flags |= 0x200000;
        }
        if (!blend)
        {
            list.Clear();
            list.Add(layer);
            return layer;
        }
        if (list.Count == 0)
        {
            list.Add(layer);
            return layer;
        }
        var last = list[^1];
        bool append = (req & 0x20) != 0 || ((last.Flags >> 2) & 1) != (req & 1) ||
                      ((last.Flags >> 3) & 1) != ((req >> 2) & 1) || !SameAnimation(last, layer);
        if (!append) return null;
        list.Add(layer);
        if ((req & 8) != 0) SyncPhase(layer, last);
        return layer;
    }

    private static bool SameAnimation(Layer a, Layer b)
    {
        if (a.HasBody != b.HasBody || a.HasFace != b.HasFace) return false;
        return !(a.HasBody || a.HasFace) || ReferenceEquals(a.Group!.Anim, b.Group!.Anim);
    }

    private static void SyncPhase(Layer layer, Layer last)
    {
        if ((last.Flags & 1) == 0) return;
        float phase = last.Duration > 0 ? last.Elapsed * TickTime / (last.Duration * TickTime) : 0f;
        if (((layer.Flags >> 3) & 1) != ((last.Flags >> 3) & 1)) phase = 1f - phase;
        layer.Phase = phase;
        layer.Flags = (layer.Flags & ~1u) | 0x200000;
    }

    // Amedo 2026-09-27
    private void Advance(Layer l, bool body)
    {
        uint f = l.Flags;
        if ((f & 2) == 0)
        {
            if ((f & 1) == 0)
            {
                l.Start = (f & 0x200000) == 0 ? _now : _now - GameMath.Trunc(l.Duration * TickTime * l.Phase * TickRate);
                l.Flags |= 1;
                if ((l.Flags & 0x10) != 0) l.BlendStart = _now;
            }
            else
            {
                int e = _now - l.Start;
                l.Elapsed = e;
                if (l.Duration <= e)
                {
                    if ((f & 4) == 0)
                    {
                        l.Elapsed = l.Duration;
                        l.Flags |= 2;
                    }
                    else if (0 < l.Duration)
                    {
                        int k = GameMath.Trunc(e * TickTime / (l.Duration * TickTime));
                        l.PrevStart = l.Start; // Amedo 2026-10-02
                        l.Start  += l.Duration * k;
                        l.Elapsed = e - l.Duration * k;
                    }
                }
            }
        }
        FramePair(l, body);
        if ((l.Flags & 0x10) == 0) return;
        int b = _now - l.BlendStart;
        if (b < l.BlendTicks) l.Weight = b * TickTime / (l.BlendTicks * TickTime);
        else
        {
            l.Flags &= ~0x10u;
            l.Weight = 1f;
        }
    }

    private static void FramePair(Layer l, bool body)
    {
        if (!(body ? l.HasBody : l.HasFace)) return;
        (l.F0, l.F1, l.Frac) = FrameAt(l, l.Elapsed); // Amedo 2026-10-01
    }

    // Amedo 2026-10-01
    private static (int F0, int F1, float Frac) FrameAt(Layer l, int elapsed)
    {
        int n = l.Frames, last = n - 1;
        int span = l.Loop ? n : last;
        float t = 0 < l.Duration ? elapsed * TickTime / (l.Duration * TickTime) : 1f;
        float pos = span * t;
        int f0 = GameMath.Trunc(pos);
        float frac = pos - f0;
        int f1 = f0 == last ? (l.Loop ? 0 : f0) : f0 + 1;
        if ((l.Flags & 8) != 0)
        {
            int r0 = last - f1;
            f1 = last - f0;
            f0 = r0;
            frac = 1f - frac;
        }
        return (Math.Clamp(f0, 0, Math.Max(0, last)), Math.Clamp(f1, 0, Math.Max(0, last)), frac);
    }

    private static void Cleanup(List<Layer> list, bool body)
    {
        for (int i = 0; i < list.Count;)
        {
            bool remove = i == list.Count - 1
                ? (list[i].Flags & 0x10) == 0 && !(body ? list[i].HasBody : list[i].HasFace)
                : (list[i + 1].Flags & 0x10) == 0;
            if (remove) list.RemoveAt(i);
            else i++;
        }
    }

    // Amedo 2026-09-27
    private void ComposeBody(SlotGroup g)
    {
        if (_body.Count == 0)
        {
            BoneMatrices = RenderPipeline.IdentityBoneMatrices;
            JointWorlds  = null;
            ShownJointWorlds = null; // Amedo 2026-10-01
            foreach (var (child, _, bindLocal) in g.AttachedRigidParts)
                child.Transform.LocalMatrix = bindLocal;
            return;
        }
        BoneMatrices = SamplePose(g, _pose, 0, false, out var jointWorlds); // Amedo 2026-10-01
        JointWorlds  = jointWorlds;
        ShownJointWorlds = jointWorlds;
        foreach (var (child, jointIndex, _) in g.AttachedRigidParts)
            if (jointWorlds.TryGetValue(jointIndex, out var jw))
                child.Transform.LocalMatrix = jw;
    }

    // Amedo 2026-09-27
    private void UpdateFace(SlotGroup g)
    {
        _faceCount = 0;
        float[]? current = null;
        foreach (var l in _face)
        {
            float w = l.Weight;
            if (l.HasFace)
            {
                var anim  = l.Group!.Anim.FacialAnimation;
                int count = anim.JointSettings.Count > 0 ? Math.Min((int)anim.JointSettings[0].FacialShapesAmount, MeshDecoder.MaxMorphShapes) : 0;
                var value = MeshDecoder.SampleFacialWeights(anim, l.F0, l.F1, l.Frac);
                _faceSrc = _faceOut;
                if (_faceCount < count)
                {
                    current = new float[MeshDecoder.MaxMorphShapes];
                    _faceCount = count;
                }
                _faceOut = current;
                for (int i = 0; i < count; i++)
                    _faceOut![i] = _faceSrc is null ? value[i] * w : value[i] * w + _faceSrc[i] * (1f - w);
            }
            else
                for (int i = 0; i < _faceCount; i++)
                    _faceOut![i] = _faceSrc is null ? 0f : _faceSrc[i] * (1f - w);
        }
        if (g.MorphMeshes.Count == 0) return;
        if (_faceCount == 0)
        {
            if (!_faceApplied) return;
            _faceApplied = false;
            ResetFace(g);
            return;
        }
        var gl = Engine.Instance.GL;
        var weights = new float[MeshDecoder.MaxMorphShapes];
        Array.Copy(_faceOut!, weights, _faceCount);
        foreach (var mm in g.MorphMeshes) mm.ApplyWeights(gl, weights);
        _faceApplied = true;
    }

    private void Seek(Layer l, float frame)
    {
        int n = l.Frames, span = l.Loop ? n : n - 1;
        if (span <= 0 || l.Duration <= 0) return;
        int e = (int)(Math.Clamp(frame / span, 0f, 1f) * l.Duration);
        l.Start   = _now - e;
        l.Elapsed = e;
        l.Flags   = (l.Flags | 1u) & ~(2u | 0x200000u);
        if (!l.Loop && l.Duration <= e) l.Flags |= 2;
        FramePair(l, l.HasBody);
        _version++; // Amedo 2026-10-01
    }

    private static float FramePosition(Layer l) => l.F0 + l.Frac;

    private static int FrameCount(SlotGroup g) => g.Anim.HasAnimationData
        ? g.Anim.MainAnimation.AnimatedTransformations.Count
        : g.Anim.FacialAnimation.AnimatedTransformations.Count;

    private static int DurationTicks(SlotGroup g, float speed)
    {
        int frames = FrameCount(g);
        float fps  = g.Anim.DefaultFPS > 0 ? g.Anim.DefaultFPS : 30f;
        return GameMath.Trunc((float)frames / fps / speed * TickRate);
    }

    // Amedo 2026-09-26
    public static float NaturalDuration(SlotGroup g)
    {
        int frames = g.Anim.MainAnimation.AnimatedTransformations.Count;
        float fps  = g.Anim.DefaultFPS > 0 ? g.Anim.DefaultFPS : 30f;
        return frames / fps;
    }

    // Amedo 2026-09-27
    public float RemainingSeconds
    {
        get
        {
            float best = 0f;
            foreach (var l in _body)
            {
                float s = GameMath.Trunc((l.Duration * TickTime - l.Elapsed * TickTime) * TickRate) * TickTime;
                if (best < s) best = s;
            }
            return best;
        }
    }
}
