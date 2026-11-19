using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-26
public sealed partial class AgentLabAgent : Component
{
    public sealed class Level
    {
        public PS2BehaviourGraph? Graph;
        public int Current = -1;
        public int Entered = -1;
        public PS2BehaviourStateBody? Else;
        public double EnterTime;
        public bool Stopped;
        public bool Reenter;
        public int Depth;
        public ushort Value_1A = 0xFFFF; // Amedo
    }

    public AgentLabWorld World { get; }
    public InstanceData Instance { get; }
    public PS2AnyObject? GameObject { get; }
    public AnimatedObject? Anim { get; private set; } // Amedo 2026-09-28

    public List<Level> Levels { get; private set; } = new(); // Amedo 2026-09-30
    public int ActiveDepth { get; private set; }
    public PS2BehaviourGraph? Behaviour { get; private set; }
    public TwinBehaviourControlPacket? ControlPacket { get; private set; }
    public int Priority { get; private set; } = -1;
    public bool Restartable { get; private set; } // Amedo 2026-09-27
    private object? _startedBy;
    private int _startedPriority;
    public int RunningPriority => Priority >= 0 ? Priority : _startedPriority;
    private uint _agentFlags; // Amedo 2026-10-01
    public uint AgentFlags
    {
        get => _agentFlags;
        private set
        {
            _agentFlags = value;
            if (ControllerType >= 0 && Instance is { } inst) inst.RuntimeFlags = value; // Amedo 2026-10-01
        }
    }
    public ushort Message { get; private set; } = NoMessage; // Amedo 2026-09-28
    public double MessageTime { get; private set; } = double.NegativeInfinity;
    public const ushort NoMessage = 0xFFFF;
    private (ushort Message, AgentLabAgent? Sender)? _inbox;
    public Dictionary<int, int> Counters { get; } = new();
    public List<AgentLabAgent?> Participants { get; private set; } = new(); // Amedo 2026-09-30

    private (PS2BehaviourGraph Graph, TwinBehaviourStarter? Starter, AgentLabAgent? Originator, AgentLabAgent? Requester)? _pendingBehaviour; // Amedo 2026-09-27
    private bool _pendingDefault;
    private bool _destroyed;
    private Vector3 _initPosition;

    public AgentLabAgent(AgentLabWorld world, InstanceData instance, PS2AnyObject? gameObject, AnimatedObject? anim)
    {
        LiveProbe.Track("agent", this);
        World      = world;
        Instance   = instance;
        GameObject = gameObject;
        Anim       = anim;
        Chunk      = world.RootChunk; // Amedo 2026-09-28
        anim?.MarkExitPoints_295690(); // Amedo
    }

    private WorldChunk _chunk = null!; // Amedo
    public WorldChunk Chunk // Amedo 2026-09-28
    {
        get => _chunk;
        set
        {
            if (ReferenceEquals(_chunk, value)) return;
            var from = _chunk;
            _chunk = value;
            if (from is not null) World.MoveToChunk_1ED188(this, from); // Amedo
        }
    }

    // Amedo 2026-09-28
    internal Vector3 RootPoint(Vector3 p) => ReferenceEquals(Chunk, World.RootChunk) ? p : Vector3.Transform(p, Chunk.ToRoot);
    internal Matrix4x4 RootMatrix(Matrix4x4 m) => ReferenceEquals(Chunk, World.RootChunk) ? m : m * Chunk.ToRoot;

    public string StateLabel =>
        Behaviour is null ? "(no behaviour)"
        : $"{(string.IsNullOrEmpty(Behaviour.Name) ? "?" : Behaviour.Name)} " +
          string.Join(" > ", Levels.Take(ActiveDepth + 1).Select(l => l.Current.ToString()));

    private Vector3 WorldPosition => Entity.Transform.World.Translation;

    public bool Destroyed => _destroyed;

    public void Begin()
    {
        _initPosition = WorldPosition;
        _initMatrix   = LocalMatrix;
        LoadKeys();
        LoadPaths();
        LoadLinks();
        LoadBox();
        InitController(); // Amedo 2026-09-27
        bool custom = CreateCustomAgent(); // Amedo 2026-09-27
        var p3 = Instance.Source.ParamList3;
        SetActivityRadius(p3.Count > 1 ? p3[1] & 0xFF : 0);
        _animExtra = _extra; // Amedo 2026-10-01
        _wakeOnce = true;
        if (!custom) RestartSpawnBehaviour();
        MarkMoved_197048(); // Amedo
    }

    public void Step()
    {
        if (_destroyed || !Entity.Active) return;
        bool character = OwnCharacter is not null; // Amedo 2026-09-29
        if (character) BeginCharacterStep_22F758();
        UpdateLookAt(); // Amedo 2026-09-28
        UpdateSensors_2378C0(); // Amedo 2026-10-05
        StepBehaviour(); // Amedo 2026-09-27
        if (Pickup is not null || Projectile is not null || !HasScript_22FA24) TrajectoryFrame_23A6D8(); // Amedo
        if (character) EndCharacterStep_22F758(); // Amedo 2026-09-29
        if (!_destroyed && Entity.Active && Projectile is null && Pickup is null) UpdateTrails(); // Amedo 2026-11-16
    }

    private bool HasScript_22FA24 => Behaviour is not null || _otherChannel is { Behaviour: not null }; // Amedo

    private void StepBehaviour()
    {
        if (_inbox is { } received) { _inbox = null; Message = received.Message; _messageSource = received.Sender; MessageTime = World.Now; } // Amedo 2026-09-28
        if (Pickup is { } pickup) { pickup.Update(World.NowTicks); return; } // Amedo 2026-09-27
        if (Projectile is { } projectile) { projectile.Update_10A9C8(World.NowTicks, (int)Math.Round(World.Dt * 9000.0)); return; } // Amedo 2026-10-06
        StepChannel1(); // Amedo 2026-09-30
        RunContext();
    }

    // Amedo 2026-09-30
    private void RunContext()
    {
        if (_pendingBehaviour is { } next) { _pendingBehaviour = null; _pendingDefault = false; StartBehaviour(next.Graph, next.Starter, next.Originator, next.Requester); }
        if (Behaviour is null || Levels.Count == 0) return;

        if (NonBlockingPass())
        {
            if (ControlPacket is not null) StartPacket();
            TrajectoryFrame_23A6D8(); // Amedo
        }
        else if (_packetRunning && ControlPacket is not null)
        {
            UpdateMotion();
            TrajectoryFrame_23A6D8(); // Amedo
            if (PacketFinished() is { } done) RunStateStack(0, done);
        }
        else
        {
            _packetApplied = false;
            RunStateStack(0, null);
            if (_packetApplied && ControlPacket is not null) StartPacket();
            if (!Levels[0].Stopped) TrajectoryFrame_23A6D8(); // Amedo
        }

        if (_pendingBehaviour is { } switched) { _pendingBehaviour = null; _pendingDefault = false; StartBehaviour(switched.Graph, switched.Starter, switched.Originator, switched.Requester); }
        else if (_pendingDefault) { _pendingDefault = false; RestartSpawnBehaviour(); }
        else if (Levels[0].Stopped) Finish();
    }

    public void StartBehaviour(PS2BehaviourGraph graph, TwinBehaviourStarter? starter = null, AgentLabAgent? originator = null,
                               AgentLabAgent? requester = null)
    {
        Trace($"start behaviour '{graph.Name}' ({graph.ScriptStates.Count} states, start {graph.StartState}){(_channel != 0 ? $" on channel {_channel}" : "")}"); // Amedo 2026-09-30
        if (IsCharacterObject && !ReferenceEquals(this, World.Player) && graph.Name?.Contains("DEATH", StringComparison.Ordinal) == true)
            World.Log?.Invoke($"[AgentLab {World.Now:0.00}s] {Entity.Name} (not the player) starts '{graph.Name}' (by {(requester ?? originator)?.Entity.Name ?? "-"})");
        if (Behaviour is not null) Cleanup_23D820(); // Amedo 2026-09-30
        EndPacket();
        AssignParticipants(starter, originator ?? this);
        Requester = requester; // Amedo 2026-09-27
        Levels.Clear();
        Levels.Add(new Level { Graph = graph, Current = graph.StartState, EnterTime = World.Now });
        ActiveDepth = 0;
        _lockDepth  = 0xFF; // Amedo 2026-09-30
        Behaviour   = graph;
        _startedBy       = (object?)starter ?? graph; // Amedo 2026-09-27
        _startedPriority = (starter?.Priority ?? graph.Priority) & 0x7F;
        Priority         = -1;
        Restartable      = false;
    }

    public void Deliver(ushort message, AgentLabAgent? sender = null) => World.PostMessage_197A30(this, message, sender); // Amedo 2026-09-30

    internal void Receive_22E770(ushort message, AgentLabAgent? sender) // Amedo 2026-09-30
    {
        _inbox    = (message, sender); // Amedo 2026-09-28
        _wakeOnce = true;
        Trace($"message {message} from {sender?.Entity.Name ?? "-"}"); // Amedo
        if (GameObject is null) return;
        foreach (var tb in GameObject.TriggerBehaviours)
            if (tb.MessageID == (message & 0x3FF) && World.ResolveBehaviour(tb.TriggerBehaviour) is { Graph: { } g } r)
            {
                var entered = EnterChannel(tb.BehaviourCallerIndex & 1); // Amedo 2026-09-30
                if (CanRequest(g, r.Starter)) _pendingBehaviour = (g, r.Starter, sender, sender); // Amedo 2026-09-28
                LeaveChannel(entered);
                break;
            }
    }

    // Amedo 2026-09-28
    private bool CanRequest(PS2BehaviourGraph g, TwinBehaviourStarter? starter)
    {
        object requested = (object?)starter ?? g;
        int priority = starter?.Priority ?? g.Priority;
        if (_pendingBehaviour is { } pending && priority < (pending.Starter?.Priority ?? pending.Graph.Priority)) return false;
        if (Behaviour is null) return true;
        int running = RunningPriority;
        if (priority < running) return false;
        return !(priority == running && ReferenceEquals(requested, _startedBy) && !Restartable);
    }

    // Amedo 2026-09-27
    public bool TriggerEventSlot(int slot) => TriggerEventSlot(slot, null);

    // Amedo 2026-09-27
    public bool TriggerEventSlot(int slot, AgentLabAgent? originator, bool force = false, int channel = 0)
    {
        if (EventsHeld_18) return false; // Amedo
        if (Pickup is not null) return RequestPickup(slot, originator); // Amedo 2026-09-27
        if (Projectile is not null) return RequestProjectile(slot, originator); // Amedo 2026-10-06
        if (Slot(slot) is not { Graph: not null }) return false;
        World.PostSlotRequest_260E68(this, slot, originator, force, channel); // Amedo
        return true;
    }

    // Amedo
    internal void RequestSlot_20E5A8(int slot, AgentLabAgent? originator, bool force, int channel = 0)
    {
        if (Slot(slot) is not { Graph: { } g } r) return;
        var entered = EnterChannel(channel);
        if (!force && !CanRequest(g, r.Starter))
            Trace($"event slot 0x{slot:X} -> '{g.Name}' refused (priority {r.Starter?.Priority ?? g.Priority}, running {RunningPriority}, pending {_pendingBehaviour?.Graph.Name ?? "-"})"); // Amedo 2026-10-01
        else
        {
            Trace($"event slot 0x{slot:X} -> '{g.Name}'{(channel != 0 ? $" on channel {channel}" : "")}");
            _pendingBehaviour = (g, r.Starter, originator ?? this, originator); // Amedo 2026-09-27
        }
        LeaveChannel(entered);
    }

    private (PS2BehaviourGraph? Graph, TwinBehaviourStarter? Starter) Slot(int slot) =>
        GameObject is not null && slot < GameObject.BehaviourSlots.Count && GameObject.BehaviourSlots[slot] != 0xFFFF
            ? World.ResolveBehaviour(GameObject.BehaviourSlots[slot])
            : (null, null);

    internal void RestartDefaultBehaviour() => RestartSpawnBehaviour(); // Amedo 2026-09-28

    private void RestartSpawnBehaviour()
    {
        var spawnId = Instance.Source.OnSpawnHeaderScriptID;
        var r = spawnId != 0xFFFF ? World.ResolveBehaviour(spawnId) : Slot(0);
        if (r.Graph is { } g) StartBehaviour(g, r.Starter, this);
        else Finish();
    }

    private void AssignParticipants(TwinBehaviourStarter? starter, AgentLabAgent originator)
    {
        Participants.Clear();
        if (starter is null) { Participants.Add(this); return; }
        foreach (var a in starter.Assigners)
        {
            switch ((int)a.AssignType)
            {
                case 0: Participants.Add(this); break;
                case 2: Participants.Add(a.GlobalObjectId != 0xFFFF ? LinkAt(a.GlobalObjectId) : null); break; // Amedo
                case 3: Participants.Add(a.GlobalObjectId != 0xFFFF ? World.GlobalAgent_3B5750(a.GlobalObjectId & 0xFF) : null); break; // Amedo
                case 4: Participants.Add(World.Player); break;
                case 8: Participants.Add(originator); break;
                default: Participants.Add(null); break; // Amedo
            }
            if (Participants.Count > 1 && a.Behaviour > 0)
                World.Unimplemented("participant", (int)a.AssignType, "controlled participant (own behaviour)");
        }
    }

    private void Finish()
    {
        Trace("behaviour finished");
        Stop_20C4A8(); // Amedo
        Cleanup_23D820(); // Amedo 2026-09-30
    }

    // Amedo
    private void Stop_20C4A8()
    {
        EndPacket();
        Behaviour = null;
        Levels.Clear();
    }

    private bool Tracing => World.TraceFilter is not null && World.TraceMatches(Entity.Name); // Amedo 2026-10-05

    private void Trace(string text)
    {
        if (Tracing) World.Log?.Invoke($"[AgentLab {World.Now:0.00}s] {Entity.Name}: {text}");
    }
}
