using System.Text;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.AgentLab;

namespace CrashEngine.Importer;

public static class ScriptDumper
{
    public sealed class ChunkScripts
    {
        public Dictionary<string, string> Behaviours { get; } = new();

        public void SaveTo(string folder)
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
            Directory.CreateDirectory(folder);
            foreach (var (id, code) in Behaviours)
            {
                var path = Path.Combine(folder, $"{id}.lab");
                File.WriteAllText(path, code, Encoding.UTF8);
            }
        }
    }

    public static ChunkScripts DumpChunk(PS2AnyTwinsanityRM2 rm2)
    {
        var result = new ChunkScripts();

        var codeSection = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        if (codeSection is null) return result;

        var behaviourSection = codeSection.GetItem<BaseTwinSection>(1);
        if (behaviourSection is null) return result;

        for (int i = 0; i < behaviourSection.GetItemsAmount(); i++)
        {
            if (behaviourSection.GetItem(i) is not PS2BehaviourGraph graph) continue;

            try
            {
                var code = AgentLabDecompiler.Decompile(graph);
                result.Behaviours[$"{graph.GetID():X8}"] = code;
            }
            catch (Exception ex)
            {
                result.Behaviours[$"{graph.GetID():X8}"] =
                    $"// Decompile failed: {ex.Message}\n";
            }
        }

        return result;
    }

    public static string? DecompileOne(PS2AnyTwinsanityRM2 rm2, uint scriptId)
    {
        var codeSection = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var behaviourSection = codeSection?.GetItem<BaseTwinSection>(1);
        var graph = behaviourSection?.GetItem<PS2BehaviourGraph>(scriptId);
        if (graph is null) return null;
        try { return AgentLabDecompiler.Decompile(graph); }
        catch (Exception ex) { return $"// Decompile failed: {ex.Message}\n"; }
    }

    public sealed record SpawnScriptInfo(
        uint ResolvedGraphId, string GraphName, string DecompiledText, bool IsGlobal, int ChainDepth)
    {
        public string FriendlyName
        {
            get
            {
                var s = GraphName.StartsWith("COM_", StringComparison.OrdinalIgnoreCase) ? GraphName[4..] : GraphName;
                var words = s.Split('_', StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant() : w);
                var joined = string.Join(" ", words);
                return string.IsNullOrWhiteSpace(joined) ? GraphName : joined;
            }
        }
    }

    public static SpawnScriptInfo? ResolveSpawnScript(
        PS2AnyTwinsanityRM2 localRm2, PS2AnyTwinsanityRM2? globalRm2, uint scriptId, int maxDepth = 8)
    {
        var localBeh = localRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)Constants.CODE_BEHAVIOURS_SECTION);
        var globalBeh = globalRm2?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)Constants.CODE_BEHAVIOURS_SECTION);

        uint currentId = scriptId;
        for (int depth = 0; depth < maxDepth; depth++)
        {
            bool isGlobal = false;
            BaseTwinItem? item = localBeh?.GetItem<BaseTwinItem>(currentId);
            if (item is null) { item = globalBeh?.GetItem<BaseTwinItem>(currentId); isGlobal = true; }
            if (item is null) return null;

            if (item is PS2BehaviourGraph graph)
            {
                string name = string.IsNullOrEmpty(graph.Name) ? $"Script_{currentId:X4}" : graph.Name;
                try { return new SpawnScriptInfo(currentId, name, AgentLabDecompiler.Decompile(graph), isGlobal, depth); }
                catch (Exception ex) { return new SpawnScriptInfo(currentId, name, $"// Decompile failed: {ex.Message}\n", isGlobal, depth); }
            }
            if (item is TwinBehaviourStarter starter)
            {
                if (starter.Assigners.Count == 0) return null;
                currentId = (uint)(starter.Assigners[0].Behaviour - 1);
                continue;
            }
            return null;
        }
        return null;
    }

    public static PS2BehaviourGraph? ResolveSpawnScriptGraph(
        PS2AnyTwinsanityRM2 localRm2, PS2AnyTwinsanityRM2? globalRm2, uint scriptId, int maxDepth = 8)
    {
        var localBeh = localRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)Constants.CODE_BEHAVIOURS_SECTION);
        var globalBeh = globalRm2?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)Constants.CODE_BEHAVIOURS_SECTION);

        uint currentId = scriptId;
        for (int depth = 0; depth < maxDepth; depth++)
        {
            BaseTwinItem? item = localBeh?.GetItem<BaseTwinItem>(currentId);
            if (item is null) item = globalBeh?.GetItem<BaseTwinItem>(currentId);
            if (item is null) return null;

            if (item is PS2BehaviourGraph graph) return graph;
            if (item is TwinBehaviourStarter starter)
            {
                if (starter.Assigners.Count == 0) return null;
                currentId = (uint)(starter.Assigners[0].Behaviour - 1);
                continue;
            }
            return null;
        }
        return null;
    }

    public static void DumpFromPackage(PackageReader pkg, string rm2Path, string outputFolder)
    {
        var rm2 = new PS2AnyTwinsanityRM2();
        using var stream = pkg.OpenByPath(rm2Path)
            ?? throw new FileNotFoundException($"RM2 not found: {rm2Path}");
        using var reader = new BinaryReader(stream);
        rm2.Read(reader, (int)stream.Length);

        var scripts = DumpChunk(rm2);
        var chunkName = Path.GetFileNameWithoutExtension(rm2Path);
        scripts.SaveTo(Path.Combine(outputFolder, chunkName));

        Console.WriteLine($"[ScriptDumper] {rm2Path}: {scripts.Behaviours.Count} scripts dumped.");
    }

    public static void DumpAll(PackageReader pkg, string outputFolder)
    {
        foreach (var record in pkg.GetByExtension(".rm2"))
        {
            try   { DumpFromPackage(pkg, record.Path, outputFolder); }
            catch (Exception ex)
            { Console.WriteLine($"[ScriptDumper] SKIP {record.Path}: {ex.Message}"); }
        }
    }
}
