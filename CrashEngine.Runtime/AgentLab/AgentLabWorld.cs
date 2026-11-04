using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-26
public sealed partial class AgentLabWorld
{
    public PS2AnyTwinsanityRM2 Rm2 => RootChunk.Rm2; // Amedo 2026-09-28
    public WorldChunk RootChunk { get; set; }
    public List<WorldChunk> Chunks { get; } = new();
    public PS2AnyTwinsanityRM2? GlobalRm2 { get; }
    public double Now { get; private set; }
    public double Dt { get; private set; }
    private readonly List<AgentLabAgent> _awakeBodies = new(); // Amedo 2026-09-29
    private readonly List<(AgentLabAgent To, ushort Message, AgentLabAgent? From, int Touch, float Loudness)> _messages = new(), _dispatching = new(); // Amedo

    // Amedo 2026-09-29
    public AgentLabAgent? PlayerSlot_570(int n)
    {
        if (n == 0) return Crash; // Amedo
        foreach (var a in Agents)
        {
            if (a.Destroyed || a.ControllerType != 12 || ReferenceEquals(a, Crash)) continue; // Amedo
            var p3 = a.Instance.Source?.ParamList3;
            if (p3 is { Count: > 0 } && (int)p3[0] == n) return a;
        }
        return null;
    }
    public GameRandom Rng => GameRandom.Shared; // Amedo
    public Dictionary<int, int> GlobalCounters { get; } = new();
    public List<AgentLabAgent> Agents { get; } = new();
    public Dictionary<string, int> Unhandled { get; } = new();
    public Dictionary<string, Dictionary<string, int>> UnhandledBy { get; } = new();
    public AgentLabAgent? Current { get; private set; }
    public AgentLabAgent? Player { get; private set; }
    public Character.CharacterPad? Pad1 { get; set; } // Amedo
    public AgentLabAgent? Crash { get; private set; } // Amedo
    public Func<int, bool, bool>? SwitchCharacter_177A90 { get; set; } // Amedo

    // Amedo
    internal void SetPlayer_30A5A8(AgentLabAgent agent, Character.CharacterController character)
    {
        Player = agent;
        PlayerCharacter = character;
    }
    public Action<string>? Log { get; set; }
    // Amedo 2026-10-05
    public string? TraceFilter
    {
        get => _traceFilter;
        set
        {
            _traceFilter = value;
            _traceParts = value?.Split('|', StringSplitOptions.RemoveEmptyEntries) ?? [];
        }
    }
    private string? _traceFilter;
    private string[] _traceParts = [];

    // Amedo 2026-10-05
    internal bool TraceMatches(string name)
    {
        foreach (var part in _traceParts)
            if (name.Contains(part, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public Action<float, float, Vector3>? CameraImpulse { get; set; } // Amedo 2026-09-28
    public Action<float>? Rumble_27B4F0 { get; set; } // Amedo
    public Action<float, float, float, float, Vector3>? CameraImpulseAxes { get; set; }

    private readonly Dictionary<(BaseTwinSection, uint), AgentLabAgent> _byInstance = new();
    private readonly Dictionary<(BaseTwinSection, ushort), PathSpline?> _paths = new();

    public PathSpline? Path(BaseTwinSection instances, ushort id) // Amedo 2026-09-27
    {
        if (_paths.TryGetValue((instances, id), out var path)) return path;
        var sec = LayoutItems(instances, Constants.LAYOUT_PATHS_SECTION);
        path = sec is not null && sec.ContainsItem(id) && sec.GetItem<BaseTwinItem>(id) is PS2AnyPath raw ? PathSpline.From(raw) : null;
        _paths[(instances, id)] = path;
        return path;
    }

    public NavGraph Nav => RootChunk.Nav; // Amedo 2026-09-28
    public GamePathFinder PathFinder { get; } = new(); // Amedo

    public const uint GateNear = 625;
    public const uint GateSleep = 9375;
    public const float GateScale = 0.00021331057f;
    public bool UpdateGate { get; set; } = true;
    public static bool ThrottleFarAgents { get; set; } = true; // Amedo
    public SceneryTree? Tree { get => RootChunk.Tree; set => RootChunk.Tree = value; } // Amedo 2026-09-28
    public Physics.CollisionWorld? Collision { get => RootChunk.Collision; set => RootChunk.Collision = value; } // Amedo 2026-09-27
    public Game.GameProgress Progress { get; } = new(); // Amedo
    public uint Progression { get => Progress.Word_04; set => Progress.Word_04 = value; } // Amedo
    public AgentLabAgent? GunHolder_30A5B0 { get; set; } // Amedo
    public const uint PlayerModeSingle = 1; // Amedo 2026-10-05
    public AgentLabAgent? PlayerSupport { get; set; } // Amedo 2026-09-27
    public Character.CharacterController? PlayerCharacter { get; set; } // Amedo 2026-09-27
    public bool RootLinksLoaded { get; set; } = true; // Amedo 2026-09-30
    public bool RootLinksQueued { get; set; } = true; // Amedo
    public Matrix4x4? ViewProjection { get; set; }
    public Vector3 ViewerPosition { get; set; }
    public Action? CameraGroup_172A48 { get; set; } // Amedo 2026-10-02
    public int Frame { get; private set; }
    public int SteppedAgents { get; private set; } // Amedo 2026-09-28
    public bool GateActive => UpdateGate && Tree?.Root is not null && ViewProjection is not null;
    private int _nextPhase;
    private int _nextRegister; // Amedo

    public const int MaxSpawnsPerAgent = 64;
    public const int MaxSpawnsTotal = 512;
    public Func<Entity, PS2AnyInstance, BaseTwinSection, Entity?>? SpawnHost { get; set; }
    public Func<WorldChunk, WorldChunk, Streaming.StreamLink?>? ChunkLink_1F1CC0 { get; set; } // Amedo 2026-10-05
    public Func<System.Numerics.Vector3, bool>? InCameraFrustum_227380 { get; set; } // Amedo 2026-10-05
    public Func<System.Numerics.Vector3?>? CameraObjectPosition { get; set; } // Amedo
    public Func<System.Numerics.Matrix4x4?>? CameraObjectMatrix { get; set; } // Amedo
    public List<AgentLabAgent> Spawned { get; } = new();
    public List<(string Object, string Spawner)> SpawnLog { get; } = new(); // Amedo 2026-09-28
    private readonly List<AgentLabAgent> _pendingAgents = new();
    private uint _nextSpawnId = 0xC000;

    public AgentLabAgent? CreateSpawned(AgentLabAgent spawner, PS2AnyInstance inst, Matrix4x4 local)
    {
        if (SpawnHost is null)
        {
            Unimplemented("spawn", 0, "no spawn host");
            return null;
        }
        if (Spawned.Count >= MaxSpawnsTotal)
        {
            Unimplemented("spawn", MaxSpawnsTotal, "total spawn cap reached");
            return null;
        }
        if (spawner.Entity.Parent is not { } parent) return null;

        inst.SetID(_nextSpawnId++);
        if (SpawnHost(parent, inst, spawner.Instance.Section) is not { } e) return null;
        var obj = GameObject(inst.ObjectId);
        e.Name = $"Spawned_{(obj?.Name ?? $"Object_{inst.ObjectId:X4}").Split('|').Last()}_{inst.GetID():X4}";
        e.Transform.LocalMatrix = local;
        if (e.Get<InstanceData>() is not { } data) return null;

        var agent = new AgentLabAgent(this, data, obj, e.Get<AnimatedObject>()) { Phase = _nextPhase++, RegisterOrder = _nextRegister++, Chunk = spawner.Chunk }; // Amedo 2026-09-30
        InstanceIdsAdd_199510(agent); // Amedo
        e.Add(agent);
        agent.ChunkAddInstance_1F1940(); // Amedo
        _byInstance[(data.Section, inst.GetID())] = agent;
        RegisterGlobalAgent_25D748(agent); // Amedo
        _pendingAgents.Add(agent);
        Spawned.Add(agent);
        SpawnLog.Add(((obj?.Name ?? inst.ObjectId.ToString()).Split('|').Last(), spawner.Entity.Name.Split("_act_").Last())); // Amedo 2026-09-28
        return agent;
    }

    // Amedo
    private readonly List<AgentLabAgent> _attachmentsGroup = new();
    private readonly List<AgentLabAgent> _attachmentsStep = new();
    internal object? LastAttachment_82F8;

    // Amedo
    internal void RegisterAttachments_197188(AgentLabAgent agent) => _attachmentsGroup.Insert(0, agent);

    // Amedo
    internal void RemoveAttachments_197480(AgentLabAgent agent)
    {
        _attachmentsGroup.Remove(agent);
        agent.AttachmentsRemoved();
    }

    // Amedo
    private void AttachmentsGroup_196DB8(WorldChunk chunk)
    {
        _attachmentsStep.Clear();
        _attachmentsStep.AddRange(_attachmentsGroup);
        foreach (var agent in _attachmentsStep)
            if (ReferenceEquals(agent.Chunk, chunk) && !agent.Destroyed) agent.AttachmentsUpdate_196DB8();
    }

    // Amedo 2026-09-29
    public void ReleaseSpawnedUnder(Entity root)
    {
        for (int i = Spawned.Count - 1; i >= 0; i--)
        {
            var s = Spawned[i];
            bool under = false;
            for (var e = s.Entity; e is not null && !under; e = e.Parent) under = ReferenceEquals(e, root);
            if (!under) continue;
            Spawned.RemoveAt(i);
            s.Released = true; // Amedo 2026-09-30
            Agents.Remove(s);
            _pendingAgents.Remove(s);
            _orderDirty = true; // Amedo 2026-10-01
            _byInstance.Remove((s.Instance.Section, s.Instance.Source.GetID()));
            if (s.InstanceId_154 != -1) InstanceIdsRemove_199548(s); // Amedo
            s.ReleaseSpawnSlot();
            s.LeaveTree();
            s.InChunk_A0 = false; // Amedo
            s.Entity.Parent?.RemoveChild(s.Entity);
        }
    }

    private readonly AgentLabAgent?[] _instanceIds = new AgentLabAgent?[0x100]; // Amedo
    private int _instanceIdCount; // Amedo
    public int InstanceIdCount => _instanceIdCount; // Amedo

    // Amedo
    private void InstanceIdsAdd_199510(AgentLabAgent agent)
    {
        if (0x100 <= _instanceIdCount) return;
        _instanceIds[_instanceIdCount] = agent;
        agent.InstanceId_154 = _instanceIdCount++;
    }

    // Amedo
    private void InstanceIdsRemove_199548(AgentLabAgent agent)
    {
        if (_instanceIdCount == 0) return;
        int last = --_instanceIdCount;
        int id = agent.InstanceId_154;
        if ((uint)id < (uint)last && _instanceIds[last] is { } moved)
        {
            _instanceIds[id] = moved;
            moved.InstanceId_154 = id;
        }
        _instanceIds[last] = null;
        agent.InstanceId_154 = -1;
    }

    // Amedo
    internal void ClearInstanceIds()
    {
        Array.Clear(_instanceIds);
        _instanceIdCount = 0;
    }

    // Amedo 2026-09-28
    private void RecycleSpawned()
    {
        for (int i = Spawned.Count - 1; i >= 0; i--)
        {
            var s = Spawned[i];
            if (!s.Destroyed) continue;
            Spawned.RemoveAt(i);
            Agents.Remove(s);
            _pendingAgents.Remove(s);
            _orderDirty = true; // Amedo 2026-10-01
            _byInstance.Remove((s.Instance.Section, s.Instance.Source.GetID()));
            if (s.InstanceId_154 != -1) InstanceIdsRemove_199548(s); // Amedo
            s.ReleaseSpawnSlot();
            s.LeaveTree();
            s.InChunk_A0 = false; // Amedo
            s.Entity.Parent?.RemoveChild(s.Entity);
        }
    }

    public AgentLabWorld(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2, string name = "") // Amedo 2026-09-28
    {
        GlobalRm2 = globalRm2;
        RootChunk = new WorldChunk(name, rm2, null);
        Chunks.Add(RootChunk);
        RememberStateChunk(RootChunk); // Amedo
        PathFinder.Link_22BB58(RootChunk.Nav); // Amedo
        Oleg = NewOleg(null); // Amedo
    }

    // Amedo 2026-09-28
    public IEnumerable<PS2AnyTwinsanityRM2> AllRm2()
    {
        yield return RootChunk.Rm2;
        foreach (var c in Chunks)
            if (!ReferenceEquals(c, RootChunk)) yield return c.Rm2;
        if (GlobalRm2 is not null) yield return GlobalRm2;
    }

    // Amedo 2026-09-28
    public void SetRoot(WorldChunk root)
    {
        RootChunk = root;
        Matrix4x4.Invert(root.ToAnchor, out var anchorToRoot);
        foreach (var c in Chunks) c.ToRoot = c.ToAnchor * anchorToRoot;
        _playerTriggers.Clear();
    }

    // Amedo 2026-09-28
    public void RemoveChunk(WorldChunk chunk)
    {
        if (ReferenceEquals(chunk, RootChunk) || !Chunks.Remove(chunk)) return;
        if (!_heldRm2.Contains(chunk.Rm2)) _heldRm2.Add(chunk.Rm2);
        ReleaseUnheldRm2_25F0E0();
        PathFinder.Unlink(chunk.Nav); // Amedo
        DropTriggersOf(chunk); // Amedo 2026-10-01
        RemoveDynamicScenery(chunk); // Amedo 2026-10-06
    }

    // Amedo 2026-09-28
    public void AddChunk(WorldChunk chunk)
    {
        RememberStateChunk(chunk); // Amedo
        if (!Chunks.Contains(chunk))
        {
            Chunks.Add(chunk);
            _heldRm2.Remove(chunk.Rm2);
            PathFinder.Link_22BB58(chunk.Nav); // Amedo
        }
    }

    public void Register(AgentLabAgent agent)
    {
        agent.Phase = _nextPhase++;
        agent.RegisterOrder = _nextRegister++; // Amedo
        Agents.Add(agent);
        _orderDirty = true; // Amedo 2026-10-01
        _byInstance[(agent.Instance.Section, agent.Instance.Source.GetID())] = agent;
        if (Player is null && agent.Instance.ObjectId == 0) Player = agent;
        if (Crash is null && agent.Instance.ObjectId == 0) Crash = agent; // Amedo
        RegisterGlobalAgent_25D748(agent); // Amedo
    }

    public void Unregister(AgentLabAgent agent)
    {
        var key = (agent.Instance.Section, agent.Instance.Source.GetID());
        if (_byInstance.TryGetValue(key, out var listed) && ReferenceEquals(listed, agent)) _byInstance.Remove(key);
        Places_13C.Remove(agent.Instance);
        Information_40.Remove(agent.Instance);
        ForgetVideoModel(agent);
        ForgetCheckpointAgent(agent);
        _pendingAgents.Remove(agent);
        if (_attachmentsGroup.Contains(agent)) RemoveAttachments_197480(agent);
        if (agent.InstanceId_154 != -1) InstanceIdsRemove_199548(agent);
        int slot = (ushort)agent.Instance.Source.RefListIndex & 0xFF;
        if (ReferenceEquals(_globalAgents[slot], agent)) _globalAgents[slot] = null;
    }

    private readonly AgentLabAgent?[] _globalAgents = new AgentLabAgent?[256]; // Amedo

    // Amedo
    private void RegisterGlobalAgent_25D748(AgentLabAgent agent)
    {
        ushort receiver = (ushort)agent.Instance.Source.RefListIndex;
        if (receiver != 0xFFFF) _globalAgents[receiver & 0xFF] = agent;
    }

    // Amedo
    public AgentLabAgent? GlobalAgent_3B5750(int index) => _globalAgents[index & 0xFF] is { Destroyed: false } a ? a : null;

    // Amedo 2026-09-28
    public WorldChunk ChunkOf(AgentLabAgent? asker) => asker?.Chunk ?? Current?.Chunk ?? RootChunk;

    public AgentLabAgent? Find(BaseTwinSection section, uint instanceId) =>
        _byInstance.TryGetValue((section, instanceId), out var a) ? a : null;

    private readonly List<Character.CharacterBody> _bodies = new(); // Amedo 2026-09-27
    private readonly List<object> _overlap = new();

    public List<DynamicSceneryInstance> DynamicScenery { get; } = new(); // Amedo 2026-10-06

    // Amedo 2026-10-06
    public void RemoveDynamicScenery(WorldChunk? chunk)
    {
        for (int i = DynamicScenery.Count - 1; i >= 0; i--)
        {
            if (chunk is not null && !ReferenceEquals(DynamicScenery[i].Chunk, chunk)) continue;
            CellsLeave(DynamicScenery[i].Cell); // Amedo
            DynamicScenery[i].Release();
            DynamicScenery.RemoveAt(i);
        }
    }

    private long _clockTicks; // Amedo
    public long ClockTicks => _clockTicks;
    public bool MoviePlaying { get; private set; } // Amedo
    private Action? _pauseSounds; // Amedo

    public void Step(double dt)
    {
        long ticks = (long)Math.Round(dt * 9000.0); // Amedo
        _gcClockTicks += ticks; // Amedo
        GcDtTicks = (int)ticks; // Amedo
        Oleg.EndFrame_259E98(); // Amedo
        StepRunState_17D298(); // Amedo
        Frame++;
        RecycleSpawned(); // Amedo 2026-09-28
        bool held = RunState_309718 == 3; // Amedo
        if (ChunkClocksRunning) // Amedo
        {
            _clockTicks += ticks;
            Now = _clockTicks / 9000.0;
            Dt = dt;
        }
        else Dt = 0.0;
        double worldDt = Dt; // Amedo
        MoviePlaying = MoviePlayer_309720?.Update_2AF0D8(ticks * (1.0 / 9000.0), _pauseSounds ??= () => Sounds?.PauseAll_1E5E50()) ?? false; // Amedo
        if (InputTape.Current is { } tape) // Amedo
        {
            bool want = tape.MovieGate(MoviePlaying);
            if (!want && MoviePlaying) MoviePlayer_309720?.Stop_2AFBC8();
            MoviePlaying = want;
        }
        Oleg.BeginFrame_259D58(); // Amedo
        if (!held && !MoviePlaying) // Amedo
        {
            InsertPendingAgents_198F90(); // Amedo 2026-10-01
            UpdateGlobalInstances_198F90(); // Amedo
            DispatchMessages_198430();
        }
        var order = UpdateOrder_1999F0();
        bool gate = GateActive;
        SteppedAgents = 0;
        if (!MoviePlaying) // Amedo
            foreach (var chunk in ChunkOrder_1EC350(order)) StepChunk_1ED448(chunk, gate, (float)worldDt, held); // Amedo
        Current = null;
        if (!held && !MoviePlaying) // Amedo
            using (CrashEngine.Core.PerfStats.Measure(CrashEngine.Core.PerfSection.SimPhysics)) StepPhysicsWorld_290170((float)worldDt); // Amedo
        SaveManager_309724?.Frame_2A13E0(GcNowTicks); // Amedo
        Update_177168(); // Amedo
        StepHud_177168((int)ticks); // Amedo
        if (GameMode is ModePlay or ModeCutscene) TickPickupClock(dt); // Amedo
        var perf = CrashEngine.Core.PerfStats.Measure(CrashEngine.Core.PerfSection.SimPhysics);
        foreach (var agent in Agents) if (!agent.Asleep) agent.MeasureTravel(); // Amedo
        perf.Dispose(); // Amedo 2026-09-28
    }

    private (Matrix4x4 View, int Fov, float Near, float Far)? _cellView; // Amedo
    private readonly Vector4[] _cellPlanes = new Vector4[6];
    private Vector3 _cellViewer;

    // Amedo
    public void DrawScene_1F11D8(CrashEngine.Runtime.Camera.RuntimeGameCamera? camera)
    {
        if (!GateActive) return;
        using var gatePerf = CrashEngine.Core.PerfStats.Measure(CrashEngine.Core.PerfSection.SimGate);
        bool useCamera = camera is { HasPose: true };
        foreach (var chunk in Chunks)
        {
            if (chunk.Tree?.Root is not { } root || chunk.DrawFrame != Frame) continue;
            var rootToChunk = Matrix4x4.Identity;
            if (!ReferenceEquals(chunk, RootChunk) && !Matrix4x4.Invert(chunk.ToRoot, out rootToChunk)) continue;
            if (useCamera)
            {
                var view = camera!.CellView_1F4E50(rootToChunk);
                _cellView = view;
                _cellViewer = view.View.Translation;
            }
            else
            {
                _cellView = null;
                _cellViewer = Vector3.Transform(ViewerPosition, rootToChunk);
                CrashEngine.Core.GameMath.FrustumPlanes(chunk.ToRoot * ViewProjection!.Value, _cellPlanes);
            }
            DrawCell_1ECB58(root, 2);
        }
        _cellView = null;
        DrawQueuedInstances_1FDBC8();
    }

    // Amedo
    private void DrawCell_1ECB58(SceneryTree.Node node, int mode)
    {
        if (mode == 2)
            mode = _cellView is { } v
                ? CrashEngine.Runtime.Camera.RuntimeGameCamera.BoxVisibility_1F5F90(v.View, v.Fov, v.Near, v.Far, node.Min, node.Max)
                : SceneryTree.Test(_cellPlanes, node);
        if (mode == 0) return;
        foreach (var body in node.Bodies)
        {
            body.UpdateMetric(_cellViewer);
            body.QueueDraw_1E9DA0(_drawFront_3D6BB0, _drawBack_3D6BB0);
        }
        foreach (var kid in node.Kids)
            if (kid is not null) DrawCell_1ECB58(kid, mode);
    }

    private readonly List<AgentLabAgent> _drawFront_3D6BB0 = new(); // Amedo
    private readonly List<AgentLabAgent> _drawBack_3D6BB0 = new();
    public (int Front, int Back) DrawQueued { get; private set; } // Amedo

    // Amedo
    private void DrawQueuedInstances_1FDBC8()
    {
        foreach (var a in Agents) a.Entity.DrawSlot = int.MaxValue;
        DrawQueued = (_drawFront_3D6BB0.Count, _drawBack_3D6BB0.Count);
        for (int i = 0; i < _drawFront_3D6BB0.Count; i++) _drawFront_3D6BB0[i].Entity.DrawSlot = i;
        for (int i = 0; i < _drawBack_3D6BB0.Count; i++) _drawBack_3D6BB0[i].Entity.DrawSlot = _drawFront_3D6BB0.Count + i;
        _drawFront_3D6BB0.Clear();
        _drawBack_3D6BB0.Clear();
    }

    // Amedo 2026-10-01
    private void InsertPendingAgents_198F90()
    {
        if (_pendingAgents.Count == 0) return;
        foreach (var pending in _pendingAgents) Agents.Insert(0, pending); // Amedo 2026-09-30
        _pendingAgents.Clear();
        _orderDirty = true;
    }

    private List<AgentLabAgent> _updateOrder = new(); // Amedo 2026-10-01
    private bool _orderDirty = true;
    private int _orderCount = -1;
    private static readonly Comparison<AgentLabAgent> NewestFirst = (a, b) => b.RegisterOrder.CompareTo(a.RegisterOrder); // Amedo

    // Amedo
    internal void Reregister_198730(AgentLabAgent agent)
    {
        agent.RegisterOrder = _nextRegister++;
        _orderDirty = true;
    }

    // Amedo 2026-10-01
    private List<AgentLabAgent> UpdateOrder_1999F0()
    {
        if (!_orderDirty && _orderCount == Agents.Count) return _updateOrder;
        _updateOrder = new List<AgentLabAgent>(Agents.Count); // Amedo
        foreach (var a in Agents) // Amedo
            if (!a.Asleep) _updateOrder.Add(a);
        _updateOrder.Sort(NewestFirst);
        _orderDirty = false;
        _orderCount = Agents.Count;
        return _updateOrder;
    }

    private readonly List<WorldChunk> _chunkOrder = new(); // Amedo 2026-10-01

    // Amedo 2026-10-01
    private List<WorldChunk> ChunkOrder_1EC350(List<AgentLabAgent> order)
    {
        _chunkOrder.Clear();
        var main = Player is { Destroyed: false } p ? p.Chunk : RootChunk;
        if (Chunks.Contains(main)) _chunkOrder.Add(main);
        for (int i = Chunks.Count - 1; i >= 0; i--) // Amedo
            if (!ReferenceEquals(Chunks[i], main)) _chunkOrder.Add(Chunks[i]);
        foreach (var agent in order)
            if (!_chunkOrder.Contains(agent.Chunk)) _chunkOrder.Add(agent.Chunk);
        return _chunkOrder;
    }

    // Amedo 2026-10-01
    private void StepChunk_1ED448(WorldChunk chunk, bool gate, float dt, bool held)
    {
        var perf = CrashEngine.Core.PerfStats.Measure(CrashEngine.Core.PerfSection.SimAgents);
        float tickDt = (int)Math.Round(dt * 9000.0) * (1f / 9000f); // Amedo 2026-10-06
        foreach (var agent in UpdateOrder_1999F0())
            if (ReferenceEquals(agent.Chunk, chunk)) agent.MotionGroup_192270();
        foreach (var d in DynamicScenery) // Amedo 2026-10-06
            if (ReferenceEquals(d.Chunk, chunk)) d.MotionGroup_192270(tickDt, ChunkClocksRunning);
        StepQueuedObjects_1985C0(); // Amedo
        foreach (var agent in UpdateOrder_1999F0())
        {
            if (!ReferenceEquals(agent.Chunk, chunk)) continue;
            Current = agent;
            if (!ChunkClocksRunning) // Amedo
            {
                agent.ClockStopped_22FC58();
                continue;
            }
            if (gate && agent.Pickup is null && !agent.ShouldRun(Frame)) // Amedo 2026-09-27
            {
                agent.GatedStep_22F758(); // Amedo 2026-09-30
                continue;
            }
            agent.MarkStepped();
            agent.ObjectComponent_114();
            agent.PreWobble(); // Amedo 2026-09-27
            agent.Step();
            agent.PhysicsPrestep();
            SteppedAgents++; // Amedo 2026-09-28
        }
        StepQueuedObjects_1985C0(); // Amedo
        foreach (var agent in UpdateOrder_1999F0()) // Amedo
            if (ReferenceEquals(agent.Chunk, chunk) && agent.SteppedNow && ChunkClocksRunning) agent.GrapleGroup_140FC0();
        StepQueuedObjects_1985C0(); // Amedo
        foreach (var agent in UpdateOrder_1999F0())
            if (ReferenceEquals(agent.Chunk, chunk)) agent.AnimGroup_19DE40();
        StepQueuedObjects_1985C0(); // Amedo
        foreach (var d in DynamicScenery) // Amedo 2026-10-06
        {
            if (!ReferenceEquals(d.Chunk, chunk)) continue;
            if (!ChunkClocksRunning) // Amedo
            {
                d.Hold();
                continue;
            }
            var before = d.GameMatrix; // Amedo
            d.Update_1FDEB0(tickDt);
            if (d.GameMatrix != before) MarkMoved_197048(d.Cell); // Amedo
        }
        StepQueuedObjects_1985C0(); // Amedo
        foreach (var agent in UpdateOrder_1999F0())
        {
            if (!ReferenceEquals(agent.Chunk, chunk) || !ChunkClocksRunning) continue; // Amedo
            Current = agent;
            agent.CrateGroup_17A9A0();
        }
        StepQueuedObjects_1985C0(); // Amedo
        foreach (var agent in UpdateOrder_1999F0()) // Amedo
        {
            if (!ReferenceEquals(agent.Chunk, chunk) || !ChunkClocksRunning) continue; // Amedo
            Current = agent;
            agent.CreatureGroup_17A9A0();
        }
        StepQueuedObjects_1985C0(); // Amedo
        perf.Dispose();
        perf = CrashEngine.Core.PerfStats.Measure(CrashEngine.Core.PerfSection.SimPlayer);
        foreach (var agent in UpdateOrder_1999F0())
        {
            if (!ReferenceEquals(agent.Chunk, chunk) || agent.Destroyed) continue;
            if (!ChunkClocksRunning) // Amedo
            {
                (ReferenceEquals(agent, Player) ? PlayerCharacter : agent.OwnCharacter)?.Bones.HoldSprings();
                continue;
            }
            if (ReferenceEquals(agent, Player))
            {
                if (PlayerCharacter is not { } pc) continue;
                Current = agent;
                pc.Step(dt);
                continue;
            }
            if (agent.OwnCharacter is not { } npc || !agent.SteppedNow) continue;
            Current = agent;
            npc.Step(dt);
        }
        StepQueuedObjects_1985C0(); // Amedo
        perf.Dispose();
        Current = null;
        perf = CrashEngine.Core.PerfStats.Measure(CrashEngine.Core.PerfSection.SimPhysics);
        if (ChunkClocksRunning) AttachmentsGroup_196DB8(chunk); // Amedo
        StepQueuedObjects_1985C0(); // Amedo
        perf.Dispose();
        if (ChunkClocksRunning) // Amedo
            using (CrashEngine.Core.PerfStats.Measure(CrashEngine.Core.PerfSection.SimTriggers)) StepTriggers(chunk);
        StepQueuedObjects_1985C0(); // Amedo
        if (ReferenceEquals(chunk, _chunkOrder[0])) CameraGroup_172A48?.Invoke(); // Amedo 2026-10-02
        StepQueuedObjects_1985C0(); // Amedo
        if (!held) // Amedo
            using (CrashEngine.Core.PerfStats.Measure(CrashEngine.Core.PerfSection.SimPhysics)) PairsAndTerrain_242C58(chunk, dt);
        Current = null;
    }

    private const int PhysicsSlots_290170 = 200; // Amedo
    private readonly Physics.RigidBody?[] _physicsBodies = new Physics.RigidBody?[PhysicsSlots_290170];
    private readonly AgentLabAgent?[] _physicsOwners = new AgentLabAgent?[PhysicsSlots_290170];
    private readonly int[] _physicsFree = Enumerable.Range(0, PhysicsSlots_290170).ToArray();
    private int _physicsUsed;
    private readonly HashSet<Physics.RigidBody> _physicsKnown = new();

    // Amedo
    private void AddPhysicsBody_290028(AgentLabAgent owner, Physics.RigidBody body)
    {
        if (_physicsUsed == PhysicsSlots_290170) return;
        int slot = _physicsFree[_physicsUsed];
        _physicsFree[_physicsUsed++] = -1;
        _physicsBodies[slot] = body;
        _physicsOwners[slot] = owner;
        _physicsKnown.Add(body);
    }

    // Amedo
    private void RemovePhysicsBody(int slot)
    {
        if (_physicsBodies[slot] is { } body) _physicsKnown.Remove(body);
        _physicsBodies[slot] = null;
        _physicsOwners[slot] = null;
        _physicsFree[--_physicsUsed] = slot;
    }

    // Amedo
    internal void ResetPhysicsBodies()
    {
        Array.Clear(_physicsBodies);
        Array.Clear(_physicsOwners);
        for (int i = 0; i < PhysicsSlots_290170; i++) _physicsFree[i] = i;
        _physicsUsed = 0;
        _physicsKnown.Clear();
    }

    // Amedo
    private void SyncPhysicsBodies()
    {
        for (int i = 0; i < PhysicsSlots_290170; i++)
            if (_physicsBodies[i] is { } body && (_physicsOwners[i] is not { } owner || owner.Destroyed || owner.Released || !ReferenceEquals(owner.PhysicsBody_290170, body)))
                RemovePhysicsBody(i);
        foreach (var agent in Agents)
            if (agent.PhysicsBody_290170 is { } body && !agent.Destroyed && !_physicsKnown.Contains(body)) AddPhysicsBody_290028(agent, body);
    }

    // Amedo
    private void StepPhysicsWorld_290170(float dt)
    {
        SyncPhysicsBodies();
        var bodies = _awakeBodies;
        bodies.Clear();
        for (int i = 0; i < PhysicsSlots_290170; i++)
            if (_physicsOwners[i] is { } owner && !owner.Asleep && owner.BodyAwake && owner.Chunk.Collision is not null) bodies.Add(owner);
        foreach (var agent in bodies) { Current = agent; agent.BodySubsteps(agent.Chunk.Collision!, dt); }
        foreach (var agent in bodies) { Current = agent; agent.BodyFinish(dt); }
        Current = null;
    }

    // Amedo 2026-10-01
    private void PairsAndTerrain_242C58(WorldChunk chunk, float dt)
    {
        if (chunk.RigidBodies_164 is not { } lists) return; // Amedo
        for (int i = 0; i < lists.FirstCount; i++)
        {
            if (lists.First[i] is not { } ctrl || !(ctrl.PairType < 9) || ctrl.Owner_94 is not AgentLabAgent agent) continue;
            if (!agent.RigidGate_242C58) continue;
            Current = agent;
            agent.PairsPass_246D00(dt);
        }
        if (chunk.Collision is not { } collision) return;
        for (int i = 0; i < lists.SecondCount; i++)
        {
            if (lists.Second[i] is not { } ctrl || ctrl.Owner_94 is not AgentLabAgent agent) continue;
            if (!agent.RigidGate_242C58) continue;
            Current = agent;
            agent.TerrainPass(collision, dt);
        }
    }

    // Amedo 2026-09-30
    internal void PostMessage_197A30(AgentLabAgent to, ushort message, AgentLabAgent? from) => Enqueue_197A30((to, message, from, -1, 0f));

    // Amedo
    private void Enqueue_197A30((AgentLabAgent To, ushort Message, AgentLabAgent? From, int Touch, float Loudness) m)
    {
        if (_handingOut && !_dispatchSeen.Contains(m.To) && Waiting(m.To)) _dispatching.Add(m);
        else _messages.Add(m);
    }

    // Amedo
    private bool Waiting(AgentLabAgent to)
    {
        for (int i = 0; i < _dispatching.Count; i++)
            if (ReferenceEquals(_dispatching[i].To, to) && _dispatching[i].Touch != Dropped) return true;
        return false;
    }

    // Amedo
    internal void ClearEvents_197B98(AgentLabAgent agent)
    {
        _messages.RemoveAll(m => ReferenceEquals(m.To, agent));
        if (!_handingOut || _dispatchSeen.Contains(agent)) return;
        for (int i = 0; i < _dispatching.Count; i++)
            if (ReferenceEquals(_dispatching[i].To, agent)) _dispatching[i] = _dispatching[i] with { Touch = Dropped };
    }

    // Amedo
    internal void PostNoise_209900(AgentLabAgent to, float loudness, AgentLabAgent? from) => Enqueue_197A30((to, 0, from, NoiseTouch, loudness));

    internal const int NoiseTouch = -2;
    internal const int SlotRequest = -3; // Amedo
    private const int Dropped = -4; // Amedo
    private bool _handingOut; // Amedo

    // Amedo
    internal void PostSlotRequest_260E68(AgentLabAgent to, int slot, AgentLabAgent? from, bool force, int channel = 0) =>
        Enqueue_197A30((to, (ushort)slot, from, SlotRequest, (force ? 1 : 0) + ((channel & 1) << 1)));

    // Amedo 2026-10-01
    internal void PostTouch_13E0A0(AgentLabAgent to, int type, AgentLabAgent? from) => Enqueue_197A30((to, 0, from, type, 0f));

    // Amedo 2026-09-30
    private void DispatchMessages_198430()
    {
        _handingOut = true; // Amedo
        while (_messages.Count > 0)
        {
            _dispatching.Clear();
            _dispatching.AddRange(_messages);
            _messages.Clear();
            _dispatchSeen.Clear(); // Amedo
            for (int i = 0; i < _dispatching.Count; i++)
            {
                var to = _dispatching[i].To;
                if (!_dispatchSeen.Add(to)) continue;
                for (int j = _dispatching.Count - 1; j >= i; j--) // Amedo
                {
                    var (jTo, message, from, touch, loudness) = _dispatching[j];
                    if (!ReferenceEquals(jTo, to)) continue;
                    if (to.Destroyed) break;
                    if (touch == Dropped) continue; // Amedo
                    if (touch >= 0) to.HandleMessage(touch, from); // Amedo 2026-10-01
                    else if (touch == NoiseTouch) to.NoiseEvent_209A30(loudness, from); // Amedo
                    else if (touch == SlotRequest) to.RequestSlot_20E5A8(message, from, ((int)loudness & 1) != 0, (int)loudness >> 1); // Amedo
                    else to.Receive_22E770(message, from);
                }
            }
        }
        _dispatching.Clear();
        _dispatchSeen.Clear(); // Amedo
        _handingOut = false; // Amedo
    }

    private readonly HashSet<AgentLabAgent> _dispatchSeen = new(); // Amedo

    public PS2BehaviourGraph? Behaviour(uint id) => ResolveBehaviour(id).Graph;

    public (PS2BehaviourGraph? Graph, TwinBehaviourStarter? Starter) ResolveBehaviour(uint id)
    {
        if (id == 0xFFFF) return (null, null);
        var r = Resolve(id);
        return r.Graph is not null ? r : Resolve(id & 0x7FFF);
    }

    private (PS2BehaviourGraph? Graph, TwinBehaviourStarter? Starter) Resolve(uint id)
    {
        TwinBehaviourStarter? starter = null;
        for (int depth = 0; depth < 8; depth++)
        {
            switch (CodeItem(id))
            {
                case PS2BehaviourGraph g: return (g, starter);
                case TwinBehaviourStarter s when s.Assigners.Count > 0:
                    starter = s;
                    id = (uint)(s.Assigners[0].Behaviour - 1);
                    continue;
            }
            break;
        }
        return (null, null);
    }

    private BaseTwinItem? CodeItem(uint id)
    {
        foreach (var rm in AllRm2()) // Amedo 2026-09-28
            if (BehaviourIn(rm, id) is { } item) return item;
        return id <= 0x7FFF ? HeldItem(HeldKind.Behaviour, id, Constants.CODE_BEHAVIOURS_SECTION) : null;
    }

    private static BaseTwinItem? BehaviourIn(PS2AnyTwinsanityRM2? rm, uint id) => CodeItemIn(rm, Constants.CODE_BEHAVIOURS_SECTION, id);

    private static BaseTwinItem? CodeItemIn(PS2AnyTwinsanityRM2? rm, int section, uint id)
    {
        var sec = rm?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
                    ?.GetItem<BaseTwinSection>((uint)section);
        return sec is not null && sec.ContainsItem(id) ? sec.GetItem<BaseTwinItem>(id) : null;
    }

    private enum HeldKind { Behaviour, Object, Sound }

    private readonly List<PS2AnyTwinsanityRM2> _heldRm2 = new();
    private readonly Dictionary<PS2AnyObject, HashSet<uint>>[] _objectResources =
        [new(ReferenceEqualityComparer.Instance), new(ReferenceEqualityComparer.Instance), new(ReferenceEqualityComparer.Instance)];

    private BaseTwinItem? HeldItem(HeldKind kind, uint id, int section)
    {
        if (_heldRm2.Count == 0 || !ObjectResourcesHold_25F0E0(kind, id)) return null;
        foreach (var rm in _heldRm2)
            if (CodeItemIn(rm, section, id) is { } item) return item;
        return null;
    }

    internal PS2AnySound? HeldSound(ushort id, int section) => HeldItem(HeldKind.Sound, id, section) as PS2AnySound;

    private bool ObjectResourcesHold_25F0E0(HeldKind kind, uint id)
    {
        foreach (var list in (ReadOnlySpan<List<AgentLabAgent>>)[Agents, _pendingAgents])
            foreach (var agent in list)
                if (ResourcesOf(kind, agent) is { } held && held.Contains(id)) return true;
        return false;
    }

    private HashSet<uint>? ResourcesOf(HeldKind kind, AgentLabAgent agent)
    {
        if (agent.Destroyed || agent.Released || agent.GameObject is not { } go) return null;
        var cache = _objectResources[(int)kind];
        if (!cache.TryGetValue(go, out var held))
        {
            var (refs, slots) = kind switch
            {
                HeldKind.Behaviour => (go.RefBehaviours, go.BehaviourSlots),
                HeldKind.Object => (go.RefObjects, go.ObjectSlots),
                _ => (go.RefSounds, go.SoundSlots),
            };
            held = new HashSet<uint>();
            foreach (var r in refs) held.Add(r);
            foreach (var s in slots) if (s != 0xFFFF) held.Add(s);
            cache[go] = held;
        }
        return held;
    }

    private void ReleaseUnheldRm2_25F0E0()
    {
        foreach (var cache in _objectResources) cache.Clear();
        if (_heldRm2.Count == 0) return;
        var wanted = new Dictionary<int, HashSet<uint>>();
        for (int section = Constants.CODE_SOUND_EFFECTS_SECTION; section <= Constants.CODE_LANG_JPN_SECTION; section++)
            wanted[section] = new HashSet<uint>();
        wanted[Constants.CODE_BEHAVIOURS_SECTION] = new HashSet<uint>();
        wanted[Constants.CODE_GAME_OBJECTS_SECTION] = new HashSet<uint>();
        foreach (var list in (ReadOnlySpan<List<AgentLabAgent>>)[Agents, _pendingAgents])
            foreach (var agent in list)
            {
                if (ResourcesOf(HeldKind.Behaviour, agent) is { } behaviours) wanted[Constants.CODE_BEHAVIOURS_SECTION].UnionWith(behaviours);
                if (ResourcesOf(HeldKind.Object, agent) is { } objects) wanted[Constants.CODE_GAME_OBJECTS_SECTION].UnionWith(objects);
                if (ResourcesOf(HeldKind.Sound, agent) is { } sounds)
                    for (int section = Constants.CODE_SOUND_EFFECTS_SECTION; section <= Constants.CODE_LANG_JPN_SECTION; section++)
                        wanted[section].UnionWith(sounds);
            }
        foreach (var rm in AllRm2()) Supplies(rm, wanted);
        _heldRm2.RemoveAll(rm => !Supplies(rm, wanted));
        foreach (var cache in _objectResources) cache.Clear();
    }

    private static bool Supplies(PS2AnyTwinsanityRM2? rm, Dictionary<int, HashSet<uint>> wanted)
    {
        var code = rm?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        if (code is null) return false;
        bool any = false;
        foreach (var (section, ids) in wanted)
        {
            var sec = code.GetItem<BaseTwinSection>((uint)section);
            if (sec is null) continue;
            for (int i = 0, n = sec.GetItemsAmount(); i < n; i++) any |= ids.Remove(sec.GetItem(i).GetID());
        }
        return any;
    }

    public BaseTwinSection? LayoutItems(BaseTwinSection instances, int kind)
    {
        foreach (var chunk in Chunks) // Amedo 2026-09-28
            for (int lid = Constants.LEVEL_LAYOUT_1_SECTION; lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
            {
                var layout = chunk.Rm2.GetItem<BaseTwinSection>((uint)lid);
                if (layout is not null && ReferenceEquals(layout.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_INSTANCES_SECTION), instances))
                    return layout.GetItem<BaseTwinSection>((uint)kind);
            }
        return null;
    }

    public PS2AnyObject? GameObject(ushort objectId)
    {
        foreach (var rm in AllRm2()) // Amedo 2026-09-28
        {
            var objs = rm?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
                         ?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
            if (objs is not null && objs.ContainsItem(objectId) && objs.GetItem<BaseTwinItem>(objectId) is PS2AnyObject o)
                return o;
        }
        return HeldItem(HeldKind.Object, objectId, Constants.CODE_GAME_OBJECTS_SECTION) as PS2AnyObject;
    }

    private readonly Dictionary<uint, PS2AnyOGI?> _defaultOgi = new(); // Amedo 2026-09-27

    public PS2AnyOGI? DefaultOgi(PS2AnyObject obj)
    {
        if (_defaultOgi.TryGetValue(obj.GetID(), out var cached)) return cached;
        PS2AnyOGI? found = null;
        foreach (var rm in AllRm2()) // Amedo 2026-09-28
        {
            var ogis = rm?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
                         ?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
            if (ogis is null) continue;
            foreach (var id in obj.OGISlots)
                if (id != 0xFFFF && ogis.ContainsItem(id) && ogis.GetItem<BaseTwinItem>(id) is PS2AnyOGI ogi && (ogi.SkinID != 0 || ogi.RigidModelIds.Count > 0))
                {
                    found = ogi;
                    break;
                }
            if (found is not null) break;
        }
        _defaultOgi[obj.GetID()] = found;
        return found;
    }

    // Amedo
    private readonly Dictionary<uint, float> _objectScale = new();

    // Amedo
    public float ObjectScale(PS2AnyObject obj)
    {
        uint id = obj.GetID();
        if (_objectScale.TryGetValue(id, out float scale)) return scale;
        scale = 1f;
        if (id >= 0x8000 && DefaultOgi(obj) is { } own)
            foreach (var rm in AllRm2())
            {
                var objs = rm?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
                             ?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
                if (objs is null) continue;
                PS2AnyObject? original = null;
                for (int i = 0; i < objs.GetItemsAmount() && original is null; i++)
                    if (objs.GetItem(i) is PS2AnyObject o && o.GetID() < 0x8000 && o.Name == obj.Name) original = o;
                if (original is null || DefaultOgi(original) is not { } baseOgi) continue;
                scale = BoxScale(baseOgi, own);
                break;
            }
        _objectScale[id] = scale;
        return scale;
    }

    // Amedo
    private static float BoxScale(PS2AnyOGI from, PS2AnyOGI to)
    {
        if (from.BoundingBox is not { Length: >= 2 } || to.BoundingBox is not { Length: >= 2 }) return 1f;
        float a = from.BoundingBox[1].X - from.BoundingBox[0].X, b = from.BoundingBox[1].Y - from.BoundingBox[0].Y, c = from.BoundingBox[1].Z - from.BoundingBox[0].Z;
        float x = to.BoundingBox[1].X - to.BoundingBox[0].X, y = to.BoundingBox[1].Y - to.BoundingBox[0].Y, z = to.BoundingBox[1].Z - to.BoundingBox[0].Z;
        double product = 1d;
        int n = 0;
        if (MathF.Abs(a) > 1e-4f) { product *= MathF.Abs(x / a); n++; }
        if (MathF.Abs(b) > 1e-4f) { product *= MathF.Abs(y / b); n++; }
        if (MathF.Abs(c) > 1e-4f) { product *= MathF.Abs(z / c); n++; }
        if (n == 0 || product <= 0d) return 1f;
        float s = (float)Math.Pow(product, 1d / n);
        return MathF.Abs(s - 1f) < 0.01f ? 1f : Math.Clamp(s, 0.05f, 50f);
    }

    public void Unimplemented(string kind, int index, string name)
    {
        string key = $"{kind} {index} {name}";
        if (Current is { } who)
        {
            if (!UnhandledBy.TryGetValue(key, out var by)) UnhandledBy[key] = by = new();
            by[who.Entity.Name] = by.GetValueOrDefault(who.Entity.Name) + 1;
        }
        if (Unhandled.TryGetValue(key, out int n)) { Unhandled[key] = n + 1; return; }
        Unhandled[key] = 1;
        Log?.Invoke($"[AgentLab] not implemented yet: {key}");
    }

    public static string CommandName(int op) =>
        PS2BehaviourGraph.GetAgentLabDefs().CommandMap.TryGetValue(op.ToString(), out var d) ? d.Name : "?";

    public static string ConditionName(int id) =>
        PS2BehaviourGraph.GetAgentLabDefs().ConditionMap.TryGetValue(id.ToString(), out var d) ? d : "?";
}
