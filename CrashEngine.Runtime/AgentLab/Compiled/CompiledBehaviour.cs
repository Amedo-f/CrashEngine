using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.Runtime.AgentLab.Compiled;

// Amedo 2026-11-04
public abstract partial class CompiledBehaviour : BehaviourScript
{
    public abstract string Name { get; }

    // Amedo 2026-11-18
    protected abstract void Define();

    // Amedo 2026-11-18
    private sealed class StateDef(Action method, bool skipFirst, bool nonBlocking, int sub, bool objectSlot, TwinBehaviourControlPacket? packet, ushort unused)
    {
        public readonly Action Method = method;
        public readonly bool SkipFirst = skipFirst, NonBlocking = nonBlocking, ObjectSlot = objectSlot;
        public readonly ushort Unused = unused;
        public readonly int Sub = sub;
        public readonly TwinBehaviourControlPacket? Packet = packet;
        public readonly List<BodyDef> Bodies = new();
    }

    // Amedo 2026-11-18
    private sealed class BodyDef(TwinBehaviourCondition? condition)
    {
        public readonly TwinBehaviourCondition? Condition = condition;
        public readonly List<PS2BehaviourCommand> Commands = new();
        public int Next = -1;
        public bool Reenter;
    }

    // Amedo 2026-11-18
    private sealed class Table
    {
        public readonly List<StateDef> States = new();
        public readonly Dictionary<MethodInfo, int> Index = new();
        public MethodInfo? Start;
        public int StartIndex;

        public int IndexOf(MethodInfo method) =>
            Index.TryGetValue(method, out int i) ? i : throw new InvalidOperationException($"{method.Name} is not a state (add State({method.Name}) to Define)");
    }

    [ThreadStatic] private static Table? _defining; // Amedo 2026-11-18
    [ThreadStatic] private static StateDef? _state; // Amedo 2026-11-18
    [ThreadStatic] private static BodyDef? _body; // Amedo 2026-11-18
    private Table? _table; // Amedo 2026-11-18
    private Table Tab => _table ??= Record(); // Amedo 2026-11-18

    // Amedo 2026-11-18
    private Table Record()
    {
        var table = new Table();
        var (outerTable, outerState, outerBody) = (_defining, _state, _body);
        (_defining, _state, _body) = (table, null, null);
        try
        {
            Define();
            for (int i = 0; i < table.States.Count; i++)
                if (!table.Index.TryAdd(table.States[i].Method.Method, i)) throw new InvalidOperationException($"{table.States[i].Method.Method.Name} is declared as a state twice");
            table.StartIndex = table.Start is null ? 0 : table.IndexOf(table.Start);
            foreach (var state in table.States)
            {
                (_state, _body) = (state, null);
                state.Method();
            }
        }
        finally
        {
            (_defining, _state, _body) = (outerTable, outerState, outerBody);
        }
        return table;
    }

    // Amedo 2026-11-18
    protected void Start(Action state) => Defining().Start = state.Method;

    // Amedo 2026-11-18
    protected void State(Action state, bool skipFirst = false, bool nonBlocking = false, int sub = -1, bool objectSlot = false, TwinBehaviourControlPacket? packet = null, ushort unused = 0) =>
        Defining().States.Add(new StateDef(state, skipFirst, nonBlocking, sub, objectSlot, packet, unused));

    // Amedo 2026-11-18
    protected static bool When(ConditionId condition, ushort parameter = 0, bool not = false, float interval = 0f, float returnCheck = 0.5f, float power = 2f) => Open(new()
    {
        ConditionIndex = (ushort)condition,
        Parameter = parameter,
        NotGate = not,
        CheckInterval = interval,
        ReturnCheck = returnCheck,
        ConditionPowerMultiplier = power,
    });

    protected static bool Always() => Open(null); // Amedo 2026-11-18
    protected static void GoTo(Action state) => CurrentBody().Next = _defining!.IndexOf(state.Method); // Amedo 2026-11-18
    protected static void Reenter() => CurrentBody().Reenter = true; // Amedo 2026-11-18

    // Amedo 2026-11-18
    internal static PS2BehaviourCommand Recorded(PS2BehaviourCommand command)
    {
        if (_defining is not null) CurrentBody().Commands.Add(command);
        return command;
    }

    private static Table Defining() => _defining is { } t && _state is null ? t : throw new InvalidOperationException("Start(...) and State(...) only work inside Define()"); // Amedo 2026-11-18

    // Amedo 2026-11-18
    private static bool Open(TwinBehaviourCondition? condition)
    {
        var state = _state ?? throw new InvalidOperationException("When(...) and Always() only work inside a state method");
        state.Bodies.Add(_body = new BodyDef(condition));
        return true;
    }

    private static BodyDef CurrentBody() => _body ?? throw new InvalidOperationException("commands, GoTo and Reenter go inside an if (When(...)) { } block of a state method"); // Amedo 2026-11-18

    private BodyDef BodyOf(int key) => Tab.States[key >> 16].Bodies[key & 0xFFFF];

    public string StateName(int state) => Tab.States[state].Method.Method.Name; // Amedo 2026-11-18
    public override int StartState => Tab.StartIndex;
    public override int StateCount => Tab.States.Count;
    public override int BodyCount(int state) => Tab.States[state].Bodies.Count;
    public override bool SkipsFirstBody(int state) => Tab.States[state].SkipFirst;
    public override bool NonBlocking(int state) => Tab.States[state].NonBlocking;
    public override int SubIndexOrSlot(int state) => Tab.States[state].Sub;
    public override bool UsesObjectSlot(int state) => Tab.States[state].ObjectSlot;
    public override TwinBehaviourControlPacket? Packet(int state) => Tab.States[state].Packet;
    public ushort UnusedStateBits(int state) => Tab.States[state].Unused; // Amedo 2026-11-18

    public override bool HasCondition(int key) => BodyOf(key).Condition is not null;
    public override bool NotGate(int key) => BodyOf(key).Condition!.NotGate;
    public override float ReturnCheck(int key) => BodyOf(key).Condition!.ReturnCheck;
    public override float Power(int key) => BodyOf(key).Condition!.ConditionPowerMultiplier;
    public override int Next(int key) => BodyOf(key).Next;
    public override bool Reenter(int key) => BodyOf(key).Reenter;

    // Amedo 2026-11-18
    public sealed override float Condition(AgentLabAgent agent, int key, AgentLabAgent.Level ctx) => agent.RunCondition(BodyOf(key).Condition!, ctx);

    // Amedo 2026-11-18
    public sealed override void Commands(AgentLabAgent agent, int key, AgentLabAgent.Level ctx)
    {
        foreach (var c in BodyOf(key).Commands) agent.RunCommand(c, ctx);
    }

    public TwinBehaviourCondition? ConditionOf(int key) => BodyOf(key).Condition; // Amedo 2026-11-18
    public IReadOnlyList<PS2BehaviourCommand> CommandsOf(int key) => BodyOf(key).Commands; // Amedo 2026-11-18

    public override string CommandsText(int key) =>
        string.Join(",", BodyOf(key).Commands.Select(c => $"{c.CommandIndex}:{AgentLabWorld.CommandName(c.CommandIndex)}"));

    public override string ConditionText(int key) =>
        BodyOf(key).Condition is { } c ? $"{c.ConditionIndex}:{AgentLabWorld.ConditionName(c.ConditionIndex)}({c.Parameter})" : "-";

    protected static float FloatBits(uint bits) => BitConverter.UInt32BitsToSingle(bits); // Amedo 2026-11-18

    protected static PS2BehaviourCommand Cmd(CommandId index, params uint[] args) // Amedo 2026-11-18
    {
        var c = new PS2BehaviourCommand { CommandIndex = (ushort)index };
        c.Arguments.AddRange(args);
        return Recorded(c);
    }

}

// Amedo 2026-11-04
public static class CompiledBehaviours
{
    private static readonly Dictionary<string, Type> _byFingerprint = new(); // Amedo 2026-11-18
    private static readonly ConditionalWeakTable<PS2BehaviourGraph, BehaviourScript> _bound = new(); // Amedo 2026-11-18

    public static int Count => _byFingerprint.Count;
    public static int BoundGraphs => _bound.Count(kv => kv.Value is CompiledBehaviour); // Amedo
    public static int Version { get; private set; } // Amedo 2026-11-18

    public static int Register(Assembly assembly)
    {
        int n = 0;
        foreach (var t in assembly.GetTypes())
        {
            if (t.IsAbstract || !typeof(CompiledBehaviour).IsAssignableFrom(t)) continue;
            var code = (CompiledBehaviour)Activator.CreateInstance(t)!;
            _byFingerprint[Fingerprint(code.ToGraph(0))] = t; // Amedo 2026-11-18
            n++;
        }
        _bound.Clear();
        Version++; // Amedo 2026-11-18
        return n;
    }

    public static void Clear()
    {
        _byFingerprint.Clear();
        _bound.Clear();
        Version++; // Amedo 2026-11-18
    }

    // Amedo 2026-11-18
    public static BehaviourScript? For(PS2BehaviourGraph? graph)
    {
        if (graph is null) return null;
        if (_bound.TryGetValue(graph, out var script)) return script;
        script = _byFingerprint.Count > 0 && _byFingerprint.TryGetValue(Fingerprint(graph), out var type)
            ? (CompiledBehaviour)Activator.CreateInstance(type)!
            : new DataScript(graph);
        _bound.AddOrUpdate(graph, script);
        return script;
    }

    // Amedo 2026-11-04
    public static void Forget(PS2BehaviourGraph graph)
    {
        _bound.Remove(graph);
        Version++; // Amedo 2026-11-18
    }

    // Amedo 2026-11-04
    public static string Fingerprint(PS2BehaviourGraph graph)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
        {
            w.Write(graph.Name ?? ""); // Amedo 2026-11-18
            w.Write(graph.Priority); // Amedo 2026-11-18
            w.Write(graph.StartState); // Amedo 2026-11-18
            w.Write(graph.ScriptStates.Count);
            foreach (var s0 in graph.ScriptStates)
            {
                var st = (PS2BehaviourState)s0;
                w.Write(st.SkipsFirstStateBody); // Amedo 2026-11-18
                w.Write(st.NoneBlocking);
                w.Write(st.BehaviourIndexOrSlot);
                w.Write(st.UsesObjectSlot);
                w.Write(st.Unknown); // Amedo 2026-11-18
                w.Write(st.ControlPacket is not null);
                st.ControlPacket?.Write(w);
                w.Write(st.Bodies.Count);
                foreach (var b0 in st.Bodies)
                {
                    var body = (PS2BehaviourStateBody)b0;
                    w.Write(body.HasStateJump ? body.JumpToState : -1); // Amedo 2026-11-18
                    w.Write(body.Unknown); // Amedo 2026-11-18
                    var c = body.Condition;
                    w.Write(c is not null);
                    if (c is not null)
                    {
                        w.Write(c.ConditionIndex);
                        w.Write(c.Parameter);
                        w.Write(c.NotGate);
                        w.Write(BitConverter.SingleToUInt32Bits(c.CheckInterval));
                        w.Write(BitConverter.SingleToUInt32Bits(c.ReturnCheck));
                        w.Write(BitConverter.SingleToUInt32Bits(c.ConditionPowerMultiplier));
                    }
                    w.Write(body.Commands.Count);
                    foreach (var m0 in body.Commands)
                    {
                        var m = (PS2BehaviourCommand)m0;
                        w.Write(m.CommandIndex);
                        w.Write(m.Arguments.Count);
                        foreach (var a in m.Arguments) w.Write(a);
                    }
                }
            }
        }
        return Convert.ToHexString(SHA1.HashData(ms.ToArray()));
    }
}
