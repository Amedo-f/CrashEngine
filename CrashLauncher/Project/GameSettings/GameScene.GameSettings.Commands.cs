using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

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
