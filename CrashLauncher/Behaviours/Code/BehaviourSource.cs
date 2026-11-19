using System.Text.RegularExpressions;
using CrashEngine.ScriptGen;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashLauncher;

// Amedo 2026-11-18
public static class BehaviourSource
{
    private static readonly Regex IdAttribute = new(@"\[BehaviourId\(0x([0-9A-Fa-f]+)\)\]", RegexOptions.Compiled);

    public static string ToCSharp(PS2BehaviourGraph graph, uint id, string level, string? className = null) =>
        BehaviourCodeGen.Generate(graph, className ?? ClassName(graph, id), [level], id, Namespace(level)).Source;

    public static string Namespace(string level) => $"{BehaviourCodeGen.Namespace}.{Naming.ClassName(level)}";

    public static string ClassName(PS2BehaviourGraph graph, uint id) =>
        Naming.ClassName(string.IsNullOrEmpty(graph.Name) ? $"Script_{id:X4}" : graph.Name);

    public static Dictionary<uint, string> ClassNames(IEnumerable<(uint Id, PS2BehaviourGraph Graph)> scripts)
    {
        var names = new Dictionary<uint, string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, graph) in scripts.OrderBy(s => s.Id))
        {
            string name = ClassName(graph, id);
            if (!used.Add(name)) used.Add(name = $"{name}_{id:X4}");
            names[id] = name;
        }
        return names;
    }

    public static string RelativePath(PS2BehaviourGraph graph, string className) =>
        Path.Combine(Naming.Folder(graph.Name ?? ""), className + ".cs");

    public static uint? IdOf(string code) =>
        IdAttribute.Match(code) is { Success: true } m ? Convert.ToUInt32(m.Groups[1].Value, 16) : null;
}
