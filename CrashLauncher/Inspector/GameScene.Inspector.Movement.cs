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
        if (info.IdleModes.Count == 0 && !info.UsesKeys && !info.UsesAiNetwork && !hasPoints) return;
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

        if (info.UsesKeys || hasPoints)
        {
            ImGui.TextUnformatted($"Patrol points ({inst.Source.Positions.Count}):");
            var points = PatrolPoints(inst.Source);
            for (int i = 0; i < points.Count; i++)
            {
                var (id, _, marker) = points[i];
                ImGui.TextUnformatted($"  [{i}] Position_{id:X4}" + (marker is null ? "  (not in this layout)" : ""));
                ImGui.SameLine();
                if (marker is not null && ImGui.SmallButton($"Select##patsel{i}")) SelectClicked(marker, false);
                ImGui.SameLine();
                if (ImGui.SmallButton($"^##patup{i}")) EditPatrol(e, rec => MovePatrolPoint(rec, inst, i, -1));
                ImGui.SameLine();
                if (ImGui.SmallButton($"v##patdn{i}")) EditPatrol(e, rec => MovePatrolPoint(rec, inst, i, +1));
                ImGui.SameLine();
                if (ImGui.SmallButton($"x##patdel{i}")) { EditPatrol(e, rec => RemovePatrolPoint(rec, inst, i)); break; }
            }
            if (ImGui.Button("+ Add patrol points in the scene##patadd", new Vector2(-1f, 0f)))
            {
                SelectClicked(e, false);
                SetAiTool(true);
            }
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
