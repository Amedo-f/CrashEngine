using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashEngine.Runtime.AgentLab.Compiled;

// Amedo 2026-11-04: one behaviour script translated to C# (made by CrashEngine.ScriptGen). The state machine
// (AgentLabAgent.StateMachine) stays the same; it asks this class for a body's condition value and runs its commands.
// key = state << 16 | body.
public abstract class CompiledBehaviour
{
    public abstract string Name { get; }
    public abstract string Fingerprint { get; }
    public abstract float Condition(AgentLabAgent agent, int key, AgentLabAgent.Level ctx);
    public abstract void Commands(AgentLabAgent agent, int key, AgentLabAgent.Level ctx);

    protected static TwinBehaviourCondition Cond(ushort index, ushort parameter, bool notGate, uint interval, uint returnCheck, uint power) => new()
    {
        ConditionIndex = index,
        Parameter = parameter,
        NotGate = notGate,
        CheckInterval = BitConverter.UInt32BitsToSingle(interval),
        ReturnCheck = BitConverter.UInt32BitsToSingle(returnCheck),
        ConditionPowerMultiplier = BitConverter.UInt32BitsToSingle(power),
    };

    protected static PS2BehaviourCommand Cmd(ushort index, params uint[] args)
    {
        var c = new PS2BehaviourCommand { CommandIndex = index };
        c.Arguments.AddRange(args);
        return c;
    }
}

// Amedo 2026-11-04: which script has a C# translation. A script is matched by its fingerprint (its conditions and
// commands), so a script edited in the editor no longer matches and the interpreter runs it as before.
public static class CompiledBehaviours
{
    public sealed class Binding
    {
        public required CompiledBehaviour Code;
        public required Dictionary<PS2BehaviourStateBody, int> Keys;
    }

    private static readonly Dictionary<string, CompiledBehaviour> _byFingerprint = new();
    private static readonly ConditionalWeakTable<PS2BehaviourGraph, Binding?[]> _bound = new();

    public static int Count => _byFingerprint.Count;
    public static int BoundGraphs => _bound.Count(kv => kv.Value[0] is not null); // Amedo: graphs running on their translation

    public static int Register(Assembly assembly)
    {
        int n = 0;
        foreach (var t in assembly.GetTypes())
        {
            if (t.IsAbstract || !typeof(CompiledBehaviour).IsAssignableFrom(t)) continue;
            var code = (CompiledBehaviour)Activator.CreateInstance(t)!;
            _byFingerprint[code.Fingerprint] = code;
            n++;
        }
        _bound.Clear();
        return n;
    }

    public static void Clear()
    {
        _byFingerprint.Clear();
        _bound.Clear();
    }

    public static Binding? For(PS2BehaviourGraph? graph)
    {
        if (graph is null || _byFingerprint.Count == 0) return null;
        if (_bound.TryGetValue(graph, out var slot)) return slot[0];
        Binding? b = null;
        if (_byFingerprint.TryGetValue(Fingerprint(graph), out var code))
        {
            var keys = new Dictionary<PS2BehaviourStateBody, int>(ReferenceEqualityComparer.Instance);
            for (int s = 0; s < graph.ScriptStates.Count; s++)
            {
                var st = (PS2BehaviourState)graph.ScriptStates[s];
                for (int i = 0; i < st.Bodies.Count; i++) keys[(PS2BehaviourStateBody)st.Bodies[i]] = s << 16 | i;
            }
            b = new Binding { Code = code, Keys = keys };
        }
        _bound.AddOrUpdate(graph, new[] { b });
        return b;
    }

    // Amedo 2026-11-04: an edit made in the editor (ReadText) must drop the old match
    public static void Forget(PS2BehaviourGraph graph) => _bound.Remove(graph);

    // Amedo 2026-11-04: everything the translation replaces: per state and body, the condition's fields and the commands
    public static string Fingerprint(PS2BehaviourGraph graph)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
        {
            w.Write(graph.ScriptStates.Count);
            foreach (var s0 in graph.ScriptStates)
            {
                var st = (PS2BehaviourState)s0;
                w.Write(st.Bodies.Count);
                foreach (var b0 in st.Bodies)
                {
                    var body = (PS2BehaviourStateBody)b0;
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
