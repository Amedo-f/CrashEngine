using CrashEngine.Core;
using CrashEngine.Importer;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using ImGuiNET;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private const uint ApplyVelocityCommandIndex   = 523;
    private const int  SpringVelocityArgIndex1     = 5;
    private const int  SpringVelocityArgIndex2     = 6;

    private static float GetArgFloat(ITwinBehaviourCommand cmd, int idx) =>
        idx < cmd.Arguments.Count ? BitConverter.Int32BitsToSingle((int)cmd.Arguments[idx]) : 0f;

    private static void SetArgFloat(ITwinBehaviourCommand cmd, int idx, float value)
    {
        if (idx < cmd.Arguments.Count)
            cmd.Arguments[idx] = unchecked((uint)BitConverter.SingleToInt32Bits(value));
    }

    private static List<ITwinBehaviourCommand> FindApplyVelocityCommands(PS2BehaviourGraph? graph)
    {
        var found = new List<ITwinBehaviourCommand>();
        if (graph is null) return found;
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex == ApplyVelocityCommandIndex && cmd.Arguments.Count > SpringVelocityArgIndex2)
                        found.Add(cmd);
        return found;
    }

    private sealed class MultiplierSetting
    {
        public required string Name, Missing, Header, Slider, Row1, Row2, ResetButton;
        public string? Note;
        public required Func<BaseTwinSection?, List<ITwinBehaviourCommand>> Find;
        public required int Arg1, Arg2;
        public bool Global;
        public List<float>? Originals;
        public float Multiplier = 1f;
        public bool Active;
    }

    private static BaseTwinSection? BehavioursOf(PS2AnyTwinsanityRM2 rm2) =>
        rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);

    private void DrawMultiplierSetting(MultiplierSetting s, BaseTwinSection? behSec, ChunkSource? chunkSource)
    {
        var cmds = s.Find(behSec);
        if (cmds.Count == 0)
        {
            ImGui.TextDisabled(s.Missing);
            return;
        }

        if (s.Originals is null || s.Originals.Count != cmds.Count * 2)
        {
            s.Originals = LoadMultiplierOriginals(s, cmds.Count)
                ?? cmds.SelectMany(c => new[] { GetArgFloat(c, s.Arg1), GetArgFloat(c, s.Arg2) }).ToList();
        }

        if (!s.Active && s.Originals.Count > 0 && s.Originals[0] != 0f)
            s.Multiplier = GetArgFloat(cmds[0], s.Arg1) / s.Originals[0];

        ImGui.Text(s.Header);
        if (s.Note is not null) ImGui.TextDisabled(s.Note);
        bool multChanged = ImGui.SliderFloat(s.Slider, ref s.Multiplier, 0.2f, 4f, "%.2fx");
        s.Active = ImGui.IsItemActive();
        if (multChanged)
        {
            ApplyMultiplier(s, cmds, s.Originals, s.Multiplier);
            if (s.Global) chunkSource!.GlobalRm2Dirty = true;
        }

        bool anyChanged = false;
        for (int i = 0; i < cmds.Count; i++)
        {
            float v1 = GetArgFloat(cmds[i], s.Arg1);
            if (ImGui.InputFloat(string.Format(s.Row1, i), ref v1, 0.5f, 5f, "%.3f"))
            {
                SetArgFloat(cmds[i], s.Arg1, v1);
                anyChanged = true;
            }
            float v2 = GetArgFloat(cmds[i], s.Arg2);
            if (ImGui.InputFloat(string.Format(s.Row2, i), ref v2, 0.5f, 5f, "%.3f"))
            {
                SetArgFloat(cmds[i], s.Arg2, v2);
                anyChanged = true;
            }
        }
        if (anyChanged && s.Global) chunkSource!.GlobalRm2Dirty = true;

        if (ImGui.Button(s.ResetButton))
        {
            s.Multiplier = 1f;
            ResetMultiplierSetting(s, behSec, chunkSource);
        }
    }

    private static void ApplyMultiplier(MultiplierSetting s, List<ITwinBehaviourCommand> cmds, List<float> originals, float multiplier)
    {
        for (int i = 0; i < cmds.Count; i++)
        {
            SetArgFloat(cmds[i], s.Arg1, originals[i * 2] * multiplier);
            SetArgFloat(cmds[i], s.Arg2, originals[i * 2 + 1] * multiplier);
        }
    }

    private List<float>? LoadMultiplierOriginals(MultiplierSetting s, int expectedCount)
    {
        try
        {
            var freshCmds = s.Find(BehavioursOf(ReadPristineRm2(s.Global ? GlobalRm2Path : _rm2)));
            var values = freshCmds.SelectMany(c => new[] { GetArgFloat(c, s.Arg1), GetArgFloat(c, s.Arg2) }).ToList();
            return values.Count == expectedCount * 2 ? values : null;
        }
        catch { return null; }
    }

    private void ResetMultiplierSetting(MultiplierSetting s, BaseTwinSection? behSec, ChunkSource? chunkSource)
    {
        try
        {
            var freshCmds = s.Find(BehavioursOf(ReadPristineRm2(s.Global ? GlobalRm2Path : _rm2)));
            var liveCmds = s.Find(behSec);
            for (int i = 0; i < liveCmds.Count && i < freshCmds.Count; i++)
            {
                SetArgFloat(liveCmds[i], s.Arg1, GetArgFloat(freshCmds[i], s.Arg1));
                SetArgFloat(liveCmds[i], s.Arg2, GetArgFloat(freshCmds[i], s.Arg2));
            }
            if (s.Global) chunkSource!.GlobalRm2Dirty = true;
            _browser.Log($"Reset {s.Name} to its original values (in-memory). Save Chunk to persist.");
        }
        catch (Exception ex) { _browser.Log($"Reset {s.Name} failed: {ex.Message}"); }
    }

    // Amedo 2026-09-21
    private void DrawSelectedSphereRings(float radius, System.Numerics.Vector4 color, float thickness)
    {
        if (_selected is null || _camera is null || ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopup)) return;
        var center = _selected.Transform.World.Translation;
        int sw = ViewW, sh = ViewH; // Amedo
        var dl = ImGui.GetForegroundDrawList();
        uint ringColor = ImGui.ColorConvertFloat4ToU32(color);
        foreach (var axis in new[] { System.Numerics.Vector3.UnitX, System.Numerics.Vector3.UnitY, System.Numerics.Vector3.UnitZ })
        {
            var pts = RingPoints(center, axis, radius);
            System.Numerics.Vector2? prev = null;
            for (int i = 0; i <= pts.Length; i++)
            {
                if (!WorldToScreen(pts[i % pts.Length], sw, sh, out var sp)) { prev = null; continue; }
                if (prev is { } pv) dl.AddLine(pv, sp, ringColor, thickness);
                prev = sp;
            }
        }
    }

    // Amedo
    private bool DrawScriptScope(Entity e, bool own, string sharedBy)
    {
        if (own)
        {
            ImGui.TextColored(new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f), "Applies to: this object only (own script)");
            return false;
        }
        ImGui.TextColored(new System.Numerics.Vector4(1f, 0.75f, 0.3f, 1f), $"Applies to: {sharedBy} (shared script)");
        ImGui.SameLine();
        if (!ImGui.SmallButton("Only this object##ownscriptscope")) return false;
        return MakeSelectionOwnScripts(e);
    }
}
