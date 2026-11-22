using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashLauncher;

// Amedo 2026-11-15
public sealed record MovementMode(string Name, int Bit);

// Amedo 2026-11-15
public sealed record MovementInfo(IReadOnlyList<MovementMode> IdleModes, string DefaultIdle, bool UsesKeys, bool UsesAiNetwork)
{
    public static readonly MovementInfo None = new(Array.Empty<MovementMode>(), "", false, false);

    public int? PatrolBit => IdleModes.FirstOrDefault(m => m.Name == "Patrol")?.Bit;

    public uint ModeMask => IdleModes.Aggregate(0u, (mask, m) => mask | (1u << m.Bit));

    public MovementMode? Current(uint stateFlags) => IdleModes.FirstOrDefault(m => (stateFlags & (1u << m.Bit)) != 0);
}

// Amedo 2026-11-15
public static class MovementModes
{
    private const string IdleTag = "_IDLE_", SelectorName = "MAIN";
    private static readonly string[] AiNetworkConditions = { "GetRoute", "EdgeNeedsJump", "IsPathComplete", "InsideEdgeEndNode" };

    private static Dictionary<string, ushort>? _conditionIds;

    private static Dictionary<string, ushort> ConditionIds()
    {
        if (_conditionIds is not null) return _conditionIds;
        var ids = new Dictionary<string, ushort>(StringComparer.Ordinal);
        foreach (var (key, name) in PS2BehaviourGraph.GetAgentLabDefs().ConditionMap)
            if (ushort.TryParse(key, out var id)) ids.TryAdd(name, id);
        return _conditionIds = ids;
    }

    public static MovementInfo Of(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2, PS2AnyObject obj)
    {
        var ids = ConditionIds();
        if (!ids.TryGetValue("SoftFlagSet", out var softFlag)) return MovementInfo.None;
        ids.TryGetValue("GotKeys", out var gotKeys);
        var aiConditions = AiNetworkConditions.Where(ids.ContainsKey).Select(n => ids[n]).ToHashSet();

        var modes = new List<MovementMode>();
        string defaultIdle = "";
        bool usesKeys = false, usesAiNetwork = false;
        foreach (var id in MeshDecoder.ObjectBehaviourClosure(rm2, globalRm2, obj))
        {
            if (MeshDecoder.ResolveBehaviourGraph(rm2, globalRm2, id) is not { } graph) continue;
            foreach (var state in graph.ScriptStates.OfType<PS2BehaviourState>())
                foreach (var body in state.Bodies.OfType<PS2BehaviourStateBody>())
                {
                    if (body.Condition is not { } condition) continue;
                    if (gotKeys != 0 && condition.ConditionIndex == gotKeys) usesKeys = true;
                    if (aiConditions.Contains(condition.ConditionIndex)) usesAiNetwork = true;
                    if (condition.ConditionIndex != softFlag || !body.HasStateJump) continue;
                    if (IdleName(rm2, globalRm2, obj, graph, body.JumpToState) is not { } name) continue;
                    if (modes.All(m => m.Bit != condition.Parameter)) modes.Add(new MovementMode(name, condition.Parameter));
                    defaultIdle = defaultIdle.Length > 0 ? defaultIdle : DefaultIdleName(rm2, globalRm2, obj, graph, state);
                }
        }
        return new MovementInfo(modes, defaultIdle, usesKeys, usesAiNetwork);
    }

    private static string? IdleName(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2, PS2AnyObject obj, PS2BehaviourGraph graph, int stateIndex)
    {
        if (stateIndex < 0 || stateIndex >= graph.ScriptStates.Count || graph.ScriptStates[stateIndex] is not PS2BehaviourState target) return null;
        if (StateGraph(rm2, globalRm2, obj, target) is not { } runs) return null;
        int at = runs.Name.IndexOf(IdleTag, StringComparison.Ordinal);
        if (at < 0) return null;
        var mode = runs.Name[(at + IdleTag.Length)..];
        return mode == SelectorName ? null : Pretty(mode);
    }

    private static string DefaultIdleName(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2, PS2AnyObject obj, PS2BehaviourGraph graph, PS2BehaviourState selector)
    {
        if (!ConditionIds().TryGetValue("Else", out var elseId)) return "";
        var seen = new HashSet<int>();
        var state = selector;
        while (true)
        {
            var fallback = state.Bodies.OfType<PS2BehaviourStateBody>().FirstOrDefault(b => b.HasStateJump && b.Condition?.ConditionIndex == elseId);
            if (fallback is null || !seen.Add(fallback.JumpToState)) return "";
            if (IdleName(rm2, globalRm2, obj, graph, fallback.JumpToState) is { } name) return name;
            if (fallback.JumpToState < 0 || fallback.JumpToState >= graph.ScriptStates.Count || graph.ScriptStates[fallback.JumpToState] is not PS2BehaviourState next) return "";
            state = next;
        }
    }

    internal static PS2BehaviourGraph? StateGraph(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2, PS2AnyObject obj, PS2BehaviourState state)
    {
        if (state.BehaviourIndexOrSlot == -1) return null;
        if (!state.UsesObjectSlot) return MeshDecoder.ResolveBehaviourGraph(rm2, globalRm2, (ushort)state.BehaviourIndexOrSlot);
        int slot = state.BehaviourIndexOrSlot;
        return slot < obj.BehaviourSlots.Count && obj.BehaviourSlots[slot] != 0xFFFF
            ? MeshDecoder.ResolveBehaviourGraph(rm2, globalRm2, obj.BehaviourSlots[slot])
            : null;
    }

    private static string Pretty(string raw) =>
        string.Join(" ", raw.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(w => w[..1] + w[1..].ToLowerInvariant()));
}
