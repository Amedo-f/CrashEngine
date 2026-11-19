using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-11-15
public sealed partial class GameScene
{
    private static readonly Vector4 MovementWarningColor = new(1f, 0.6f, 0.2f, 1f);

    private void DrawInspectorMovement(Entity e, InstanceData inst)
    {
        var info = MovementOf(inst);
        bool hasPoints = inst.Source.Positions.Count > 0;
        if (info.IdleModes.Count == 0 && !info.UsesKeys && !info.UsesAiNetwork) return; // Amedo 2026-11-19
        if (!ImGui.CollapsingHeader("Movement##movement", ImGuiTreeNodeFlags.DefaultOpen)) return;

        var current = info.Current(inst.Source.StateFlags);
        if (info.IdleModes.Count > 0)
        {
            string shown = current?.Name ?? info.DefaultIdle;
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.BeginCombo("##idlemode", $"Idle mode: {shown}"))
            {
                if (ImGui.Selectable($"{info.DefaultIdle} (default)", current is null)) ChangeIdleMode(e, inst, info, null);
                foreach (var mode in info.IdleModes)
                    if (ImGui.Selectable($"{mode.Name}##mode{mode.Bit}", ReferenceEquals(mode, current))) ChangeIdleMode(e, inst, info, mode);
                ImGui.EndCombo();
            }
            if (current?.Name == "Patrol" && !hasPoints)
                ImGui.TextColored(MovementWarningColor, "Patrol needs points: without them it stands still.");
            if (current?.Name != "Patrol" && hasPoints && info.PatrolBit is not null)
                ImGui.TextColored(MovementWarningColor, "It has patrol points, but it only walks them in Patrol mode.");
        }

        ImGui.TextDisabled(info.UsesAiNetwork ? "Uses the AI network (AI points near it) when it moves around." : "Doesn't use the AI network.");
    }

    private void ChangeIdleMode(Entity e, InstanceData inst, MovementInfo info, MovementMode? mode) =>
        EditPatrol(e, rec => { rec.Instance(inst.Source); SetIdleMode(inst, info, mode); });

    private void EditPatrol(Entity focus, Action<LayoutEditRecorder> edit)
    {
        var rec = new LayoutEditRecorder();
        edit(rec);
        _undoStack.Push(rec.Finish(focus));
    }
}
