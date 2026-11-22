using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;

namespace CrashLauncher;

// Amedo 2026-11-21
public sealed record ScriptFlagUse(int Bit, string Script, int State, bool WhenOn, int JumpTo, string? HandsOverTo)
{
    public string Where => $"{Short(Script)} state {State} {(WhenOn ? "if on" : "if off")} -> " + (HandsOverTo is { } to ? Short(to) : $"state {JumpTo}");

    internal static string Short(string script) => script.StartsWith("COM_", StringComparison.Ordinal) ? script[4..] : script;
}

// Amedo 2026-11-21
public static class ScriptFlagUses
{
    private static readonly Dictionary<(string Script, int Bit), string> Known = new()
    {
        [("COM_EARTH_WORM_MOVE", 19)] = "Travels along the AI network (worm tunnel links) to its next key",
        [("COM_EARTH_WORM_IDLE", 20)] = "Turns around at random while idle",
        [("COM_EARTH_WORM_START", 21)] = "Hides underground when Crash is far",
        [("COM_CREATURE_BASIC_IDLE_PATROL", 20)] = "Walks to its keys along the AI network",
        [("COM_CREATURE_BASIC_IDLE_MAIN", 25)] = "Ignores its keys (no patrol)",
        [("COM_UTIL_TEXTMASTER_DEFAULT", 19)] = "Shows its text right away (no wait for message 87)",
        [("COM_GLOBAL_BAT_FLY_SPLINE", 31)] = "Flies its path once, then disappears",
    };

    public static string? KnownName(IEnumerable<ScriptFlagUse> uses) =>
        uses.Select(u => Known.GetValueOrDefault((u.Script, u.Bit))).FirstOrDefault(n => n is not null);

    // Amedo 2026-11-21
    public static Dictionary<int, string> Names(IReadOnlyDictionary<int, List<ScriptFlagUse>> uses)
    {
        var names = new Dictionary<int, string>();
        var auto = new Dictionary<int, string>();
        foreach (var (bit, list) in uses)
        {
            if (KnownName(list) is { } known)
            {
                bool more = list.Any(u => !Known.ContainsKey((u.Script, u.Bit))); // Amedo 2026-11-21
                names[bit] = more ? known + " (+ other uses)" : known;
                continue;
            }
            var targets = list.Select(u => u.HandsOverTo).Distinct().ToList();
            if (targets.Count == 1 && targets[0] is not null && list.All(u => u.WhenOn == list[0].WhenOn)) auto[bit] = HandOverName(list[0]);
        }
        foreach (var (bit, name) in auto)
            if (auto.Count(a => a.Value == name) == 1) names[bit] = name;
        return names;
    }

    // Amedo 2026-11-21
    private static string HandOverName(ScriptFlagUse use)
    {
        var from = use.Script.Split('_');
        var to = use.HandsOverTo!.Split('_');
        int same = 0;
        while (same < from.Length && same < to.Length - 1 && from[same] == to[same]) same++;
        var rest = string.Join(" ", to.Skip(same).Select(w => w.Length == 0 ? w : w[..1] + w[1..].ToLowerInvariant()));
        return use.WhenOn ? $"Goes to: {rest}" : $"Goes to: {rest} (when off)";
    }

    public static IReadOnlyDictionary<int, List<ScriptFlagUse>> Of(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2, PS2AnyObject obj)
    {
        var result = new Dictionary<int, List<ScriptFlagUse>>();
        ushort softFlag = 0;
        foreach (var (key, name) in PS2BehaviourGraph.GetAgentLabDefs().ConditionMap)
            if (name == "SoftFlagSet" && ushort.TryParse(key, out var id)) softFlag = id;
        if (softFlag == 0) return result;
        foreach (var behaviour in MeshDecoder.ObjectBehaviourClosure(rm2, globalRm2, obj))
        {
            if (MeshDecoder.ResolveBehaviourGraph(rm2, globalRm2, behaviour) is not { } graph) continue;
            for (int s = 0; s < graph.ScriptStates.Count; s++)
            {
                if (graph.ScriptStates[s] is not PS2BehaviourState state) continue;
                foreach (var body in state.Bodies.OfType<PS2BehaviourStateBody>())
                {
                    if (body.Condition is not { } c || c.ConditionIndex != softFlag || c.Parameter > 31) continue;
                    int to = body.HasStateJump ? body.JumpToState : -1;
                    string? hands = to >= 0 && to < graph.ScriptStates.Count && graph.ScriptStates[to] is PS2BehaviourState target
                        ? MovementModes.StateGraph(rm2, globalRm2, obj, target)?.Name : null;
                    var use = new ScriptFlagUse(c.Parameter, graph.Name, s, !c.NotGate, to, hands);
                    if (!result.TryGetValue(c.Parameter, out var list)) result[c.Parameter] = list = new List<ScriptFlagUse>();
                    if (!list.Contains(use)) list.Add(use);
                }
            }
        }
        return result;
    }
}
