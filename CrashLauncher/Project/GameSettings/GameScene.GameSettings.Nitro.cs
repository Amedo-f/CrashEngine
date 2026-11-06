using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private const uint NitroCrateBehaviourId       = 0x37;
    private const int  ApplyBlastRadiusCommandIndex = 117;

    private static PS2BehaviourCommand? FindBlastRadiusCommand(PS2BehaviourGraph graph)
    {
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex == ApplyBlastRadiusCommandIndex && cmd.Arguments.Count > 0)
                        return (PS2BehaviourCommand)cmd;
        return null;
    }

    private float? _nitroOriginalBlastRadius;
    private float  _nitroBlastRadiusMultiplier = 1f;
    private bool   _nitroBlastRadiusActive;

    private void DrawNitroNoiseRadius(Entity e, ChunkSource chunkSource, Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject obj) // Amedo
    {
        var nitroGraph = ObjectGraph(chunkSource, obj, NitroCrateBehaviourId, out bool own);
        if (nitroGraph is null)
        {
            ImGui.TextDisabled("Couldn't find COM_NITRO_CRATE_DEFAULT (0x37) in Startup\\Default.rm2.");
            return;
        }

        var cmd = FindBlastRadiusCommand(nitroGraph);
        if (cmd is null)
        {
            ImGui.TextDisabled("No ApplyBlastRadius (117) command found in COM_NITRO_CRATE_DEFAULT.");
            return;
        }

        _nitroOriginalBlastRadius ??= LoadNitroOriginalBlastRadius() ?? BitConverter.UInt32BitsToSingle(cmd.Arguments[0]);

        if (!_nitroBlastRadiusActive && _nitroOriginalBlastRadius is { } orig0 && orig0 != 0f)
        {
            float liveVal = BitConverter.UInt32BitsToSingle(cmd.Arguments[0]);
            _nitroBlastRadiusMultiplier = liveVal / orig0;
        }

        ImGui.Text("Nitro Hop Noise Radius"); // Amedo
        if (DrawScriptScope(e, own, "every Nitro crate")) return;
        bool changed = ImGui.SliderFloat("Radius Multiplier##nitroblast", ref _nitroBlastRadiusMultiplier, 0.1f, 5f, "%.2fx");
        _nitroBlastRadiusActive = ImGui.IsItemActive();
        if (changed)
        {
            cmd.Arguments[0] = BitConverter.SingleToUInt32Bits((_nitroOriginalBlastRadius ?? 10f) * _nitroBlastRadiusMultiplier);
            if (!own) chunkSource.GlobalRm2Dirty = true; // Amedo
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset##nitroblastreset"))
        {
            _nitroBlastRadiusMultiplier = 1f;
            cmd.Arguments[0] = BitConverter.SingleToUInt32Bits(_nitroOriginalBlastRadius ?? 10f);
            if (!own) chunkSource.GlobalRm2Dirty = true; // Amedo
        }

        float rawVal = BitConverter.UInt32BitsToSingle(cmd.Arguments[0]);
        ImGui.TextDisabled($"Current noise radius: {rawVal:F2} world units"); // Amedo
        DrawSelectedSphereRings(rawVal, new System.Numerics.Vector4(0.35f, 0.7f, 1f, 0.6f), 1.5f); // Amedo
    }

    private float? LoadNitroOriginalBlastRadius()
    {
        try
        {
            var freshBehSec = BehavioursOf(ReadPristineRm2(GlobalRm2Path));
            var freshGraph = freshBehSec?.GetItem<PS2BehaviourGraph>(NitroCrateBehaviourId);
            if (freshGraph is null) return null;

            var freshCmd = FindBlastRadiusCommand(freshGraph);
            return freshCmd is null ? null : BitConverter.UInt32BitsToSingle(freshCmd.Arguments[0]);
        }
        catch { return null; }
    }
}
