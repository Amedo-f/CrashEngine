using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Importer.AgentLabReadable;
using ImGuiNET;
using System.Numerics;
using System.Text;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashLauncher;

// Amedo 2026-09-25
public sealed partial class GameScene
{
    private bool _inspectedBehaviourIsGlobal;
    private bool _focusBehaviourTab;
    private bool _behaviourTabReadable = true;
    private readonly CodeEditor.CodeEditor _behaviourCode = new(); // Amedo 2026-11-04
    private string? _behaviourCodeSynced;
    private string _behaviourTabRawOriginal = "";

    private void OpenBehaviourInTab(uint id, PS2BehaviourGraph graph, bool isGlobal, bool manual)
    {
        _inspectedBehaviourId = id;
        _inspectedBehaviourGraph = graph;
        _inspectedBehaviourManual = manual;
        _inspectedBehaviourIsGlobal = isGlobal;
        var raw = GraphToEditableText(graph);
        _behaviourTabRawOriginal = GetPristineBehaviourGraphText(isGlobal, id) ?? raw;
        _inspectedBehaviourText = _behaviourTabReadable ? ReadableScript.ToReadable(raw) : raw;
        _inspectedBehaviourOriginalText = _behaviourTabRawOriginal;
        _inspectedBehaviourApplyMsg = "";
        _focusBehaviourTab = true;
    }

    private void DrawBehaviourTab()
    {
        if (_focusBehaviourTab) { ImGui.SetNextWindowFocus(); _focusBehaviourTab = false; }
        ImGui.SetNextWindowSize(new Vector2(900f, 500f), ImGuiCond.FirstUseEver);
        ImGui.Begin("Behaviour##panel", ImGuiWindowFlags.NoCollapse);

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
        bool readable = _behaviourTabReadable;
        if (ImGui.Checkbox("Readable##behtabreadable", ref readable)) SetBehaviourTabReadable(readable);
        ImGui.SameLine();
        // Amedo 2026-11-04: our coloured code editor (CodeEditor/) instead of the plain text box; the text it gives back is
        // exactly the text it got until the user types, so Apply / Revert / Readable work the same
        _behaviourCode.Language ??= CodeEditor.AgentLabLanguage.Instance;
        if (!ReferenceEquals(_behaviourCodeSynced, _inspectedBehaviourText))
        {
            _behaviourCode.SetText(_inspectedBehaviourText);
            _behaviourCodeSynced = _inspectedBehaviourText;
        }
        ImGui.TextDisabled($"Ln {_behaviourCode.CaretLine + 1}, Col {_behaviourCode.CaretColumn + 1}  |  {_behaviourCode.LineCount} lines");
        if (!string.IsNullOrEmpty(_inspectedBehaviourApplyMsg))
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"|  {_inspectedBehaviourApplyMsg}");
        }

        _behaviourCode.Render($"##behtabtext{bid}", ImGui.GetContentRegionAvail());
        if (_behaviourCode.Changed)
        {
            _inspectedBehaviourText = _behaviourCode.Text;
            _behaviourCodeSynced = _inspectedBehaviourText;
        }
        ImGui.End();
    }

    private void ApplyBehaviourTabText()
    {
        if (_inspectedBehaviourId is not uint bid || _inspectedBehaviourGraph is null) return;
        try
        {
            var rawText = _behaviourTabReadable ? ReadableScript.FromReadable(_inspectedBehaviourText) : _inspectedBehaviourText;
            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rawText));
            using var sr = new StreamReader(ms);
            _inspectedBehaviourGraph.ReadText(sr);
            CrashEngine.Runtime.AgentLab.Compiled.CompiledBehaviours.Forget(_inspectedBehaviourGraph); // Amedo 2026-11-04: an edited script runs on the interpreter
            _scriptTextCache.Remove(bid);
            var src = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
            if (_inspectedBehaviourIsGlobal && src is not null) src.GlobalRm2Dirty = true;
            _inspectedBehaviourApplyMsg = "Applied (in-memory)" + (_inspectedBehaviourIsGlobal ? ", GLOBAL script" : "") +
                ". Build ISO to test; Save Chunk to keep it.";
        }
        catch (Exception ex)
        {
            _inspectedBehaviourApplyMsg = $"Parse failed, NOT applied: {ex.Message}";
        }
    }

    private void RevertBehaviourTabText()
    {
        if (_inspectedBehaviourId is not uint bid || _inspectedBehaviourGraph is null) return;
        try
        {
            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_behaviourTabRawOriginal));
            using var sr = new StreamReader(ms);
            _inspectedBehaviourGraph.ReadText(sr);
            CrashEngine.Runtime.AgentLab.Compiled.CompiledBehaviours.Forget(_inspectedBehaviourGraph); // Amedo 2026-11-04
            _inspectedBehaviourText = _behaviourTabReadable ? ReadableScript.ToReadable(_behaviourTabRawOriginal) : _behaviourTabRawOriginal;
            _scriptTextCache.Remove(bid);
            _inspectedBehaviourApplyMsg = "Reverted to the original (disc) script, in-memory. Save Chunk to persist.";
        }
        catch (Exception ex)
        {
            _inspectedBehaviourApplyMsg = $"Revert failed: {ex.Message}";
        }
    }

    private void SetBehaviourTabReadable(bool readable)
    {
        if (readable == _behaviourTabReadable) return;
        try
        {
            _inspectedBehaviourText = readable
                ? ReadableScript.ToReadable(_inspectedBehaviourText)
                : ReadableScript.FromReadable(_inspectedBehaviourText);
            _behaviourTabReadable = readable;
            _inspectedBehaviourApplyMsg = "";
        }
        catch (Exception ex)
        {
            _inspectedBehaviourApplyMsg = $"Can't switch to raw: {ex.Message}";
        }
    }

	private static string GraphToEditableText(PS2BehaviourGraph g)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using (StreamWriter writer = new StreamWriter(memoryStream, Encoding.UTF8, 4096, leaveOpen: true))
		{
			g.WriteText(writer);
		}
		memoryStream.Position = 0L;
		using StreamReader streamReader = new StreamReader(memoryStream);
		return streamReader.ReadToEnd();
	}
}
