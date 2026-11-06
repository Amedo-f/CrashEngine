using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void DrawInspectorConditions(Entity e, InstanceData inst)
    {
        ImGui.Separator();
        if (ImGui.CollapsingHeader("Conditions##cond", ImGuiTreeNodeFlags.DefaultOpen))
        {
            uint selfInstId = inst.Source.GetID();
            var condSources = GetConditionSources(selfInstId, e);
            if (condSources.Count == 0)
            {
                ImGui.TextColored(new Vector4(1f, 0.85f, 0.2f, 1f),
                    "No condition set — activates on its own, unconditionally.");
            }
            else
            {
                for (int ci = 0; ci < condSources.Count; ci++)
                {
                    var (srcEnt, kind, srcId) = condSources[ci];
                    ImGui.PushID(ci);
                    if (ImGui.Selectable($"[{kind}] {srcEnt.Name}  (0x{srcId:X4})", false,
                            ImGuiSelectableFlags.AllowOverlap))
                    { _selected = srcEnt; _revealSelectionInTree = true; }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Remove##condrm"))
                    {
                        if (kind == "Instance") srcEnt.Get<InstanceData>()?.Source.Instances.Remove((ushort)selfInstId);
                        else srcEnt.Get<CrashEngine.Importer.TriggerMarker>()?.Source.Trigger.Instances.Remove((ushort)selfInstId);
                    }
                    ImGui.PopID();
                }
            }
            if (ImGui.Button("+ Add Condition...##addcond"))
            {
                _conditionPickerTargetEntity = e;
                _conditionPickerFilter = "";
                _showConditionPicker = true;
            }

            if (inst.Source.ParamList3.Count > 2)
            {
                int required = unchecked((int)inst.Source.ParamList3[2]);
                ImGui.SetNextItemWidth(80f);
                if (ImGui.InputInt("Required count##condRequiredCount", ref required))
                    inst.Source.ParamList3[2] = unchecked((uint)Math.Max(0, required));

                if (inst.ObjectId == 0x0000)
                {
                    int startingMasks = Math.Max(0, required - 1);
                    ImGui.SetNextItemWidth(80f);
                    if (ImGui.InputInt("Starting Aku Aku Masks##crashStartMasks", ref startingMasks))
                        inst.Source.ParamList3[2] = unchecked((uint)Math.Max(1, startingMasks + 1));
                }
            }
        }
    }
}
