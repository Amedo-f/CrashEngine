using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;

namespace CrashLauncher;

public sealed partial class GameScene
{
    // Amedo
    private List<float>? _crateExplodeOriginalRadii;
    private float _crateExplodeMultiplier = 1f;
    private bool  _crateExplodeActive;

    // Amedo
    private static bool IsLiteralFloatWord(uint w) => (w & 1) == 0 && ((w >> 1) & 3) == 2;
    private static float LiteralFloat(uint w) => BitConverter.Int32BitsToSingle((int)(w & ~7u));
    private static uint ToLiteralFloat(float f) => (BitConverter.SingleToUInt32Bits(f) & ~7u) | 4u;

    // Amedo
    private static List<PS2BehaviourCommand> FindCrateDamageCommands(PS2BehaviourGraph graph)
    {
        var list = new List<PS2BehaviourCommand>();
        foreach (var state in graph.ScriptStates)
            foreach (var body in state.Bodies)
                foreach (var cmd in body.Commands)
                    if (cmd.CommandIndex is 514 or 546 or 563 && cmd.Arguments.Count > 10 && IsLiteralFloatWord(cmd.Arguments[10]))
                        list.Add((PS2BehaviourCommand)cmd);
        return list;
    }

    // Amedo
    private void DrawCrateExplosionRadius(Entity e, ChunkSource chunkSource, Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject obj)
    {
        var graph = ObjectGraph(chunkSource, obj, CrateExplodeBehaviourId, out bool own);
        if (graph is null)
        {
            ImGui.TextDisabled("Couldn't find COM_GENERIC_CRATE_EXPLODE (47) in Startup\\Default.rm2.");
            return;
        }
        var cmds = FindCrateDamageCommands(graph);
        if (cmds.Count == 0)
        {
            ImGui.TextDisabled("No CreateDamage (514) command found in COM_GENERIC_CRATE_EXPLODE.");
            return;
        }

        if (_crateExplodeOriginalRadii is null || _crateExplodeOriginalRadii.Count != cmds.Count)
        {
            var orig = LoadCrateExplodeOriginalRadii();
            _crateExplodeOriginalRadii = orig is not null && orig.Count == cmds.Count
                ? orig
                : cmds.Select(c => LiteralFloat(c.Arguments[10])).ToList();
        }
        var origRadii = _crateExplodeOriginalRadii;

        int main = 0;
        for (int i = 1; i < origRadii.Count; i++) if (origRadii[i] > origRadii[main]) main = i;
        if (!_crateExplodeActive && origRadii[main] != 0f)
            _crateExplodeMultiplier = LiteralFloat(cmds[main].Arguments[10]) / origRadii[main];

        ImGui.Text("Crate Explosion Radius");
        if (DrawScriptScope(e, own, "every Nitro and TNT crate")) return;
        bool changed = ImGui.SliderFloat("Radius Multiplier##crateexplode", ref _crateExplodeMultiplier, 0.1f, 5f, "%.2fx");
        _crateExplodeActive = ImGui.IsItemActive();
        if (changed)
        {
            for (int i = 0; i < cmds.Count; i++)
                cmds[i].Arguments[10] = ToLiteralFloat(origRadii[i] * _crateExplodeMultiplier);
            if (!own) chunkSource.GlobalRm2Dirty = true;
        }
        ImGui.SameLine();
        if (ImGui.Button("Reset##crateexplodereset"))
        {
            _crateExplodeMultiplier = 1f;
            for (int i = 0; i < cmds.Count; i++) cmds[i].Arguments[10] = ToLiteralFloat(origRadii[i]);
            if (!own) chunkSource.GlobalRm2Dirty = true;
        }

        float mainVal = LiteralFloat(cmds[main].Arguments[10]);
        ImGui.TextDisabled($"Kill radius: {mainVal:F2} world units (original {origRadii[main]:F2})");
        DrawSelectedSphereRings(mainVal, new System.Numerics.Vector4(1f, 0.25f, 0.1f, 0.95f), 2.5f);
    }

    // Amedo
    private List<float>? LoadCrateExplodeOriginalRadii()
    {
        try
        {
            var freshGraph = BehavioursOf(ReadPristineRm2(GlobalRm2Path))?.GetItem<PS2BehaviourGraph>(CrateExplodeBehaviourId);
            return freshGraph is null ? null : FindCrateDamageCommands(freshGraph).Select(c => LiteralFloat(c.Arguments[10])).ToList();
        }
        catch { return null; }
    }
}
