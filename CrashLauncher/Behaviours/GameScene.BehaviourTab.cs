using CrashEngine.Importer;
using CrashEngine.Importer.AgentLabReadable;
using ImGuiNET;
using System.Numerics;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashLauncher;

// Amedo 2026-09-25
public sealed partial class GameScene
{
    private bool _inspectedBehaviourIsGlobal;
    private bool _focusBehaviourTab;
    private bool _showBehaviour;
    private const string BehaviourWindowName = "Behaviour##panel";
    private enum BehaviourView { CSharp, Readable, Raw } // Amedo 2026-11-18
    private static readonly string[] BehaviourViewNames = ["C#", "Readable", "Raw"]; // Amedo 2026-11-18
    private BehaviourView _behaviourView = BehaviourView.CSharp; // Amedo 2026-11-18
    private readonly CodeEditor.CodeEditor _behaviourCode = new(); // Amedo 2026-11-04
    private string? _behaviourCodeSynced;
    private PS2BehaviourGraph? _behaviourTabOriginal; // Amedo 2026-11-18
    private bool _behaviourTabTyped; // Amedo 2026-11-18

    private void OpenBehaviourInTab(uint id, PS2BehaviourGraph graph, bool isGlobal, bool manual)
    {
        _inspectedBehaviourId = id;
        _inspectedBehaviourGraph = graph;
        _inspectedBehaviourIsGlobal = isGlobal;
        _behaviourTabOriginal = GetPristineBehaviourGraph(isGlobal, id) ?? BehaviourGraphs.Clone(graph); // Amedo 2026-11-18
        SetBehaviourStatus(OutputKind.Info, ""); // Amedo 2026-11-18
        LoadBehaviourTabText(graph); // Amedo 2026-11-18
        _focusBehaviourTab = true;
    }

    // Amedo 2026-11-18
    private string ViewText(PS2BehaviourGraph graph, BehaviourView view) => view switch
    {
        BehaviourView.CSharp => GeneratedBehaviourCode(_inspectedBehaviourId ?? 0, graph, _inspectedBehaviourIsGlobal),
        BehaviourView.Readable => ReadableScript.ToReadable(BehaviourGraphs.ToText(graph)),
        _ => BehaviourGraphs.ToText(graph),
    };

    // Amedo 2026-11-18
    private void LoadBehaviourTabText(PS2BehaviourGraph graph)
    {
        _inspectedBehaviourText = TabText(graph);
        _behaviourTabTyped = false;
    }

    // Amedo 2026-11-18
    private void ReloadBehaviourTabText()
    {
        if (_behaviourTabTyped || _inspectedBehaviourGraph is null) return;
        string text = TabText(_inspectedBehaviourGraph);
        if (text != _inspectedBehaviourText) _inspectedBehaviourText = text;
    }

    // Amedo 2026-11-18
    private string TabText(PS2BehaviourGraph graph)
    {
        if (_behaviourView == BehaviourView.CSharp && ReferenceEquals(graph, _inspectedBehaviourGraph) && _inspectedBehaviourId is uint id
            && ReadBehaviourCode(id, _inspectedBehaviourIsGlobal) is { } file) return file;
        try { return ViewText(graph, _behaviourView); }
        catch (Exception ex) when (_behaviourView == BehaviourView.CSharp)
        {
            _behaviourView = BehaviourView.Readable;
            SetBehaviourStatus(OutputKind.Error, "Can't show as C#", ErrorRows(ex));
            return ViewText(graph, _behaviourView);
        }
    }

    // Amedo 2026-11-18
    private PS2BehaviourGraph? TabGraph(uint bid, List<OutputRow> errors)
    {
        if (_inspectedBehaviourText == ViewText(_inspectedBehaviourGraph!, _behaviourView)) return _inspectedBehaviourGraph;
        if (_behaviourView == BehaviourView.CSharp)
        {
            var compileErrors = new List<BehaviourCompiler.CompileError>();
            var compiled = BehaviourCompiler.ToGraph(_inspectedBehaviourText, bid, compileErrors);
            errors.AddRange(ErrorRows(compileErrors));
            return compiled;
        }
        return BehaviourGraphs.FromText(_behaviourView == BehaviourView.Readable ? ReadableScript.FromReadable(_inspectedBehaviourText) : _inspectedBehaviourText, bid);
    }

    private void DrawBehaviourTab()
    {
        if (_focusBehaviourTab)
        {
            _showBehaviour = true;
            _windowChrome.Reveal(BehaviourWindowName);
            ImGui.SetNextWindowFocus();
            _focusBehaviourTab = false;
        }
        if (!_showBehaviour) return;
        ImGui.SetNextWindowSize(new Vector2(900f, 500f), ImGuiCond.FirstUseEver);
        if (!_windowChrome.Begin(BehaviourWindowName, ref _showBehaviour)) return;

        if (_inspectedBehaviourId is not uint bid || _inspectedBehaviourGraph is null)
        {
            ImGui.TextDisabled("No behaviour open. Pick one in the Inspector (Behaviours, Messages -> Behaviours,");
            ImGui.TextDisabled("Behaviour Slots or Open by ID) to edit it here.");
            ImGui.End();
            return;
        }

        var graph = _inspectedBehaviourGraph;
        string name = string.IsNullOrEmpty(graph.Name) ? $"Script_{bid:X4}" : graph.Name;
        ImGui.TextUnformatted($"0x{bid:X4}  {name}");
        if (_inspectedBehaviourIsGlobal)
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1f, 0.75f, 0.3f, 1f), "[GLOBAL: Startup\\Default.rm2, shared by every instance of this object type in every level]");
        }

        if (ImGui.Button("Apply##behtabapply")) ApplyBehaviourTabText();
        ImGui.SameLine();
        if (ImGui.Button("Revert to Original##behtabrevert")) RevertBehaviourTabText();
        ImGui.SameLine();
        int view = (int)_behaviourView; // Amedo 2026-11-18
        ImGui.SetNextItemWidth(ImGui.CalcTextSize("Readable").X + ImGui.GetFrameHeight() * 2f);
        if (ImGui.Combo("View##behtabview", ref view, BehaviourViewNames, BehaviourViewNames.Length)) SetBehaviourView((BehaviourView)view);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("C#: the script as a C# class (Apply turns it into the game's data, same bytes when nothing changed).\nReadable / Raw: the old text forms.");
        ImGui.SameLine();
        if (ImGui.Button("Export Level Code##behtabexport")) // Amedo 2026-11-18
        {
            int written = ExportBehaviourCode(_inspectedBehaviourIsGlobal);
            SetBehaviourStatus(OutputKind.Info, $"Wrote {written} new script file(s) to Code\\Behaviours\\{BehaviourCodeLevel(_inspectedBehaviourIsGlobal)} (files already there are kept).");
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Write every script of this level as a C# file in the project's Code\\Behaviours folder,\nso you can edit them here or in VS Code. Existing files are not overwritten.");
        ImGui.SameLine();
        if (ImGui.Button("Open Code Folder##behtabfolder")) // Amedo 2026-11-18
        {
            Directory.CreateDirectory(BehaviourCodeDir(_inspectedBehaviourIsGlobal));
            ShowInFileBrowser(BehaviourCodeDir(_inspectedBehaviourIsGlobal));
        }
        ImGui.SameLine();
        if (ImGui.Button("Open in Visual Studio##behtabide")) OpenBehaviourInIde(bid, graph, _inspectedBehaviourIsGlobal); // Amedo 2026-11-18
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Edit this script in Visual Studio: it knows the engine (autocomplete, errors as you type)\nand CrashEngine's rules. Saving there updates the level here.");
        // Amedo 2026-11-04: our coloured code editor (CodeEditor/) instead of the plain text box; the text it gives back is
        // exactly the text it got until the user types, so Apply / Revert / Readable work the same
        _behaviourCode.Language = _behaviourView == BehaviourView.CSharp ? CodeEditor.CSharpLanguage.Instance : CodeEditor.AgentLabLanguage.Instance; // Amedo 2026-11-18
        if (!ReferenceEquals(_behaviourCodeSynced, _inspectedBehaviourText))
        {
            _behaviourCode.SetText(_inspectedBehaviourText);
            _behaviourCodeSynced = _inspectedBehaviourText;
            ShowBehaviourSquiggles(); // Amedo 2026-11-18
        }
        ImGui.TextDisabled($"Ln {_behaviourCode.CaretLine + 1}, Col {_behaviourCode.CaretColumn + 1}  |  {_behaviourCode.LineCount} lines");

        var avail = ImGui.GetContentRegionAvail(); // Amedo 2026-11-18
        const float splitH = 5f;
        _behaviourOutputHeight = Math.Clamp(_behaviourOutputHeight, 60f, Math.Max(60f, avail.Y - 120f));
        float codeH = Math.Max(40f, avail.Y - _behaviourOutputHeight - splitH - ImGui.GetStyle().ItemSpacing.Y * 2f);
        _behaviourCode.Render($"##behtabtext{bid}", new Vector2(avail.X, codeH)); // Amedo 2026-11-18
        if (_behaviourCode.Changed)
        {
            _inspectedBehaviourText = _behaviourCode.Text;
            _behaviourCodeSynced = _inspectedBehaviourText;
            _behaviourTabTyped = true; // Amedo 2026-11-18
        }
        ImGui.InvisibleButton("##behoutsplit", new Vector2(avail.X, splitH)); // Amedo 2026-11-18
        if (ImGui.IsItemActive()) _behaviourOutputHeight -= ImGui.GetIO().MouseDelta.Y;
        if (ImGui.IsItemHovered() || ImGui.IsItemActive()) ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNS);
        DrawBehaviourOutput(new Vector2(avail.X, _behaviourOutputHeight));
        ImGui.End();
    }

    private void ApplyBehaviourTabText()
    {
        if (_inspectedBehaviourId is not uint bid || _inspectedBehaviourGraph is null) return;
        try
        {
            var errors = new List<OutputRow>(); // Amedo 2026-11-18
            var edited = TabGraph(bid, errors);
            if (edited is null) { SetBehaviourStatus(OutputKind.Error, $"NOT applied: {ErrorCount(errors.Count)}", errors); return; }
            if (!ReferenceEquals(edited, _inspectedBehaviourGraph)) BehaviourGraphs.CopyInto(edited, _inspectedBehaviourGraph);
            if (_behaviourView == BehaviourView.CSharp) SaveBehaviourCode(bid, _inspectedBehaviourGraph, _inspectedBehaviourIsGlobal, _inspectedBehaviourText); // Amedo 2026-11-18
            else if (ReadBehaviourCode(bid, _inspectedBehaviourIsGlobal) is not null) // Amedo 2026-11-18
                SaveBehaviourCode(bid, _inspectedBehaviourGraph, _inspectedBehaviourIsGlobal, GeneratedBehaviourCode(bid, _inspectedBehaviourGraph, _inspectedBehaviourIsGlobal));
            CrashEngine.Runtime.AgentLab.Compiled.CompiledBehaviours.Forget(_inspectedBehaviourGraph); // Amedo 2026-11-04: an edited script runs on the interpreter
            _scriptTextCache.Remove(bid);
            var src = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
            if (_inspectedBehaviourIsGlobal && src is not null) src.GlobalRm2Dirty = true;
            _behaviourTabTyped = false; // Amedo 2026-11-18
            SetBehaviourStatus(OutputKind.Success, "No issues found"); // Amedo 2026-11-18
        }
        catch (Exception ex)
        {
            SetBehaviourStatus(OutputKind.Error, "NOT applied: 1 error", ErrorRows(ex)); // Amedo 2026-11-18
        }
    }

    private void RevertBehaviourTabText()
    {
        if (_inspectedBehaviourId is not uint bid || _inspectedBehaviourGraph is null) return;
        try
        {
            if (_behaviourTabOriginal is not null) BehaviourGraphs.CopyInto(_behaviourTabOriginal, _inspectedBehaviourGraph); // Amedo 2026-11-18
            if (ReadBehaviourCode(bid, _inspectedBehaviourIsGlobal) is not null) // Amedo 2026-11-18
                SaveBehaviourCode(bid, _inspectedBehaviourGraph, _inspectedBehaviourIsGlobal, GeneratedBehaviourCode(bid, _inspectedBehaviourGraph, _inspectedBehaviourIsGlobal));
            CrashEngine.Runtime.AgentLab.Compiled.CompiledBehaviours.Forget(_inspectedBehaviourGraph); // Amedo 2026-11-04
            LoadBehaviourTabText(_inspectedBehaviourGraph); // Amedo 2026-11-18
            _scriptTextCache.Remove(bid);
            SetBehaviourStatus(OutputKind.Success, "Reverted to the original (disc) script, in-memory. Save Chunk to persist."); // Amedo 2026-11-18
        }
        catch (Exception ex)
        {
            SetBehaviourStatus(OutputKind.Error, "Revert failed", ErrorRows(ex)); // Amedo 2026-11-18
        }
    }

    // Amedo 2026-11-18
    private void SetBehaviourView(BehaviourView view)
    {
        if (view == _behaviourView || _inspectedBehaviourId is not uint bid || _inspectedBehaviourGraph is null) return;
        try
        {
            var errors = new List<OutputRow>();
            var graph = TabGraph(bid, errors);
            if (graph is null) { SetBehaviourStatus(OutputKind.Error, $"Can't switch view: {ErrorCount(errors.Count)}", errors); return; }
            _behaviourView = view;
            SetBehaviourStatus(OutputKind.Info, "");
            LoadBehaviourTabText(graph);
        }
        catch (Exception ex)
        {
            SetBehaviourStatus(OutputKind.Error, "Can't switch view", ErrorRows(ex));
        }
    }
}
