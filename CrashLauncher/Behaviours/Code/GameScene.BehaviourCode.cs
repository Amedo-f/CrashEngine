using CrashEngine.Core;
using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

// Amedo 2026-11-18
public sealed partial class GameScene
{
    private const string GlobalCodeFolder = "Default";

    private string BehaviourCodeRoot => ModeDir("Code");

    private string BehaviourCodeDir(bool global) => Path.Combine(BehaviourCodeRoot, "Behaviours", BehaviourCodeLevel(global));

    private ChunkSource? CodeSource => Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();

    private void OpenBehaviourInIde(uint id, PS2BehaviourGraph graph, bool global)
    {
        if (!BehaviourCodeFiles(global, log: false).TryGetValue(id, out var path))
            path = SaveBehaviourCode(id, graph, global, GeneratedBehaviourCode(id, graph, global));
        OpenBehaviourFileInIde(path);
    }

    private void OpenBehaviourFileInIde(string path, int line = 0, int column = 0)
    {
        foreach (var changed in BehaviourIde.WriteProject(BehaviourCodeRoot)) RememberBehaviourCodeFile(changed);
        BehaviourIde.Open(BehaviourCodeRoot, path, line, column);
    }

    private string BehaviourCodeLevel(bool global) => global ? GlobalCodeFolder : GamePaths.FileNameWithoutExtension(_rm2);

    private List<(uint Id, PS2BehaviourGraph Graph)> LevelBehaviours(bool global)
    {
        var src = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        var list = new List<(uint, PS2BehaviourGraph)>();
        if (MeshDecoder.CodeSection(global ? src?.GlobalRm2 : src?.Rm2, TwinConstants.CODE_BEHAVIOURS_SECTION) is not BaseTwinSection section) return list;
        for (int i = 0; i < section.GetItemsAmount(); i++)
            if (section.GetItem(i) is PS2BehaviourGraph graph) list.Add((graph.GetID(), graph));
        return list;
    }

    private Dictionary<uint, string> BehaviourCodeFiles(bool global, bool log = true)
    {
        var files = new Dictionary<uint, string>();
        string dir = BehaviourCodeDir(global);
        if (!Directory.Exists(dir)) return files;
        foreach (var path in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
            if (BehaviourSource.IdOf(File.ReadAllText(path)) is { } id && !files.TryAdd(id, path) && log)
                _browser.Log($"Behaviour code: {Path.GetFileName(path)} and {Path.GetFileName(files[id])} are both script 0x{id:X4}; the first one is used.");
        return files;
    }

    private string GeneratedBehaviourCode(uint id, PS2BehaviourGraph graph, bool global) =>
        BehaviourSource.ToCSharp(graph, id, BehaviourCodeLevel(global), BehaviourClassName(id, graph, global));

    private string BehaviourClassName(uint id, PS2BehaviourGraph graph, bool global) =>
        BehaviourSource.ClassNames(LevelBehaviours(global)).GetValueOrDefault(id) ?? BehaviourSource.ClassName(graph, id);

    private string? ReadBehaviourCode(uint id, bool global) =>
        BehaviourCodeFiles(global).TryGetValue(id, out var path) ? File.ReadAllText(path) : null;

    private string SaveBehaviourCode(uint id, PS2BehaviourGraph graph, bool global, string code)
    {
        if (!BehaviourCodeFiles(global).TryGetValue(id, out var path))
            path = Path.Combine(BehaviourCodeDir(global), BehaviourSource.RelativePath(graph, BehaviourClassName(id, graph, global)));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, code);
        RememberBehaviourCodeFile(path);
        return path;
    }

    private Dictionary<string, long>? _behaviourCodeTimes;
    private object? _behaviourCodeRm2, _behaviourCodeGlobalRm2;

    private sealed record BehaviourCodeWork(object? Rm2, object? GlobalRm2, Dictionary<string, long> Times, List<(bool Global, Dictionary<string, string> Files)> Folders);
    private sealed record BehaviourCodeBuild(BehaviourCodeWork Work, List<(bool Global, Dictionary<uint, PS2BehaviourGraph>? Graphs)> Folders, List<BehaviourCompiler.CompileError> Errors);

    private Dictionary<string, long> BehaviourCodeTimes()
    {
        var times = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (bool global in new[] { false, true })
        {
            string dir = BehaviourCodeDir(global);
            if (!Directory.Exists(dir)) continue;
            foreach (var path in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
                times[path] = File.GetLastWriteTimeUtc(path).Ticks;
        }
        return times;
    }

    private bool BehaviourCodeKnown(object? rm2, object? globalRm2) =>
        _behaviourCodeTimes is not null && ReferenceEquals(_behaviourCodeRm2, rm2) && ReferenceEquals(_behaviourCodeGlobalRm2, globalRm2);

    private bool BehaviourCodeApplied(Dictionary<string, long> times, object? rm2, object? globalRm2) =>
        BehaviourCodeKnown(rm2, globalRm2) && times.All(t => _behaviourCodeTimes!.TryGetValue(t.Key, out var at) && at == t.Value);

    private void RememberBehaviourCodeFile(string path)
    {
        _behaviourAssetsDirty = true;
        var src = CodeSource;
        if (BehaviourCodeKnown(src?.Rm2, src?.GlobalRm2) && File.Exists(path)) _behaviourCodeTimes![path] = File.GetLastWriteTimeUtc(path).Ticks;
    }

    private BehaviourCodeWork? PendingBehaviourCode()
    {
        var src = CodeSource;
        var times = BehaviourCodeTimes();
        if (times.Count == 0 || BehaviourCodeApplied(times, src?.Rm2, src?.GlobalRm2)) return null;
        var folders = new List<(bool, Dictionary<string, string>)>();
        foreach (bool global in new[] { false, true })
        {
            string dir = BehaviourCodeDir(global) + Path.DirectorySeparatorChar;
            var files = times.Keys.Where(p => p.StartsWith(dir, StringComparison.OrdinalIgnoreCase)).ToDictionary(p => p, File.ReadAllText);
            if (files.Count > 0) folders.Add((global, files));
        }
        return new BehaviourCodeWork(src?.Rm2, src?.GlobalRm2, times, folders);
    }

    private static BehaviourCodeBuild CompileBehaviourCode(BehaviourCodeWork work)
    {
        var errors = new List<BehaviourCompiler.CompileError>();
        var folders = new List<(bool, Dictionary<uint, PS2BehaviourGraph>?)>();
        foreach (var (global, files) in work.Folders)
        {
            var folderErrors = new List<BehaviourCompiler.CompileError>();
            folders.Add((global, BehaviourCompiler.ToGraphs(files, folderErrors)));
            errors.AddRange(folderErrors);
        }
        return new BehaviourCodeBuild(work, folders, errors);
    }

    private bool SyncBehaviourCode()
    {
        var work = PendingBehaviourCode();
        return work is null || FinishBehaviourCode(CompileBehaviourCode(work), fromWatch: false);
    }

    private bool FinishBehaviourCode(BehaviourCodeBuild build, bool fromWatch)
    {
        string? tabFile = _inspectedBehaviourId is uint tabId ? BehaviourCodeFiles(_inspectedBehaviourIsGlobal, log: false).GetValueOrDefault(tabId) : null;
        if (build.Errors.Count > 0)
        {
            foreach (var e in build.Errors.Take(20))
                _browser.Log($"Behaviour code: {Path.GetFileName(e.File)}({e.Line},{e.Column}): {(e.Code.Length > 0 ? e.Code + ": " : "")}{e.Message}");
            _browser.Log($"Behaviour code: {ErrorCount(build.Errors.Count)} in Code\\Behaviours; no script was changed.");
            ReloadBehaviourTabText();
            SetBehaviourStatus(OutputKind.Error, $"NOT applied: {ErrorCount(build.Errors.Count)} in Code\\Behaviours",
                build.Errors.Select(e => new OutputRow(e.Message, e.Line, e.Column, e.Code, string.Equals(e.File, tabFile, StringComparison.OrdinalIgnoreCase) ? "" : e.File)));
            _behaviourOutputFromCode = true;
            return false;
        }
        var src = CodeSource;
        int changed = 0;
        foreach (var (global, graphs) in build.Folders)
        {
            var scripts = LevelBehaviours(global).ToDictionary(s => s.Id, s => s.Graph);
            foreach (var (id, built) in graphs!)
            {
                if (!scripts.TryGetValue(id, out var script))
                {
                    _browser.Log($"Behaviour code: script 0x{id:X4} is not in {BehaviourCodeLevel(global)}; its file is skipped.");
                    continue;
                }
                if (BehaviourGraphs.Bytes(built).AsSpan().SequenceEqual(BehaviourGraphs.Bytes(script))) continue;
                BehaviourGraphs.CopyInto(built, script);
                CrashEngine.Runtime.AgentLab.Compiled.CompiledBehaviours.Forget(script);
                _scriptTextCache.Remove(id);
                if (global && src is not null) src.GlobalRm2Dirty = true;
                changed++;
            }
        }
        _behaviourCodeTimes = build.Work.Times;
        _behaviourCodeRm2 = build.Work.Rm2;
        _behaviourCodeGlobalRm2 = build.Work.GlobalRm2;
        ReloadBehaviourTabText();
        if (changed > 0) _browser.Log($"Behaviour code: {changed} script(s) updated from Code\\Behaviours.");
        if (_behaviourOutputFromCode || (fromWatch && changed > 0))
            SetBehaviourStatus(OutputKind.Success, changed > 0 ? $"No issues found: {changed} script(s) updated from Code\\Behaviours" : "No issues found");
        return true;
    }

    private int ExportBehaviourCode(bool global)
    {
        var existing = BehaviourCodeFiles(global);
        var scripts = LevelBehaviours(global);
        var names = BehaviourSource.ClassNames(scripts);
        int written = 0;
        foreach (var (id, graph) in scripts)
        {
            if (existing.ContainsKey(id)) continue;
            string path = Path.Combine(BehaviourCodeDir(global), BehaviourSource.RelativePath(graph, names[id]));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, BehaviourSource.ToCSharp(graph, id, BehaviourCodeLevel(global), names[id]));
            RememberBehaviourCodeFile(path);
            written++;
        }
        return written;
    }
}
