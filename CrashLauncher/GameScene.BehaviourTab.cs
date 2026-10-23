using CrashEngine.Core;
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
    private int _behaviourTabCursor;
    private unsafe ImGuiInputTextCallback? _behaviourTabCallback;
    private bool _behaviourTabReadable = true;
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
        _behaviourTabCursor = 0;
        _focusBehaviourTab = true;
    }

    private unsafe int BehaviourTabTextCallback(ImGuiInputTextCallbackData* data)
    {
        _behaviourTabCursor = data->CursorPos;
        return 0;
    }

    private (int line, int col, int lines) BehaviourTabCursorInfo()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(_inspectedBehaviourText);
        int pos = Math.Clamp(_behaviourTabCursor, 0, bytes.Length);
        int line = 1, lineStart = 0, lines = 1;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] != (byte)'\n') continue;
            lines++;
            if (i < pos) { line++; lineStart = i + 1; }
        }
        return (line, pos - lineStart + 1, lines);
    }

    private unsafe void DrawBehaviourTab()
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
        var (line, col, lines) = BehaviourTabCursorInfo();
        ImGui.TextDisabled($"Ln {line}, Col {col}  |  {lines} lines");
        if (!string.IsNullOrEmpty(_inspectedBehaviourApplyMsg))
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"|  {_inspectedBehaviourApplyMsg}");
        }

        _behaviourTabCallback ??= BehaviourTabTextCallback;
        uint maxLen = (uint)Math.Max(65536, _inspectedBehaviourText.Length * 2);
        ImGui.InputTextMultiline($"##behtabtext{bid}", ref _inspectedBehaviourText, maxLen,
            ImGui.GetContentRegionAvail(),
            ImGuiInputTextFlags.AllowTabInput | ImGuiInputTextFlags.CallbackAlways,
            _behaviourTabCallback);
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
}
