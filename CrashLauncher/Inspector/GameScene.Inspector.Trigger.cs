using CrashEngine.Core;
using ImGuiNET;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private string _trigInstanceAddBuf = "";

    private void DrawInspectorTrigger(Entity e)
    {
        var trigMarker = e.Get<CrashEngine.Importer.TriggerMarker>();
        if (trigMarker is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Trigger Settings##trig", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("Position/Rotation/Scale: see Transform above.");

                var twinTrig = trigMarker.Source.Trigger;
                if (ImGui.TreeNode("Activated By##trigActivator"))
                {
                    foreach (var flag in Enum.GetValues<Twinsanity.TwinsanityInterchange.Enumerations.Enums.TriggerActivatorObjects>())
                    {
                        bool on = twinTrig.ObjectActivatorMask.HasFlag(flag);
                        if (ImGui.Checkbox($"{flag}##trigActivator_{flag}", ref on))
                        {
                            twinTrig.ObjectActivatorMask = on
                                ? twinTrig.ObjectActivatorMask | flag
                                : twinTrig.ObjectActivatorMask & ~flag;
                        }
                    }
                    ImGui.TreePop();
                }

                ImGui.SetNextItemWidth(-1f);
                float trigUnkFloat = twinTrig.UnkFloat;
                if (ImGui.DragFloat("Unk Float##trigUnkFloat", ref trigUnkFloat, 0.05f)) twinTrig.UnkFloat = trigUnkFloat;

                if (ImGui.TreeNode($"Linked Instances ({twinTrig.Instances.Count})##trigInstances"))
                {
                    int removeIdx = -1;
                    for (int i = 0; i < twinTrig.Instances.Count; i++)
                    {
                        ImGui.TextDisabled($"0x{twinTrig.Instances[i]:X4}");
                        ImGui.SameLine();
                        if (ImGui.SmallButton($"Remove##trigInstRemove{i}")) removeIdx = i;
                    }
                    if (removeIdx >= 0) twinTrig.Instances.RemoveAt(removeIdx);

                    ImGui.SetNextItemWidth(120f);
                    ImGui.InputText("Instance id (hex)##trigInstAdd", ref _trigInstanceAddBuf, 8);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Add##trigInstAddBtn") &&
                        ushort.TryParse(_trigInstanceAddBuf, System.Globalization.NumberStyles.HexNumber, null, out var newInstId))
                    {
                        twinTrig.Instances.Add(newInstId);
                        _trigInstanceAddBuf = "";
                    }
                    ImGui.TreePop();
                }

                if (ImGui.TreeNode("AgentLab Messages (sent on trigger)##trigMessages"))
                {
                    for (int i = 0; i < trigMarker.Source.TriggerMessages.Length; i++)
                    {
                        ImGui.SetNextItemWidth(120f);
                        int msg = trigMarker.Source.TriggerMessages[i];
                        if (ImGui.DragInt($"Message {i}##trigMsg{i}", ref msg, 1f, 0, ushort.MaxValue))
                            trigMarker.Source.TriggerMessages[i] = (ushort)Math.Clamp(msg, 0, ushort.MaxValue);
                        DrawTriggerMessageTargets(trigMarker.Source, trigMarker.Source.TriggerMessages[i]); // Amedo 2026-09-25
                    }
                    ImGui.TreePop();
                }
            }
        }
    }
}
