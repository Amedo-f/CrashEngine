using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private bool   _showConditionPicker;
    private Entity? _conditionPickerTargetEntity;
    private string _conditionPickerFilter = "";

    private List<(Entity Entity, string Kind, uint SourceId)> GetConditionSources(uint targetInstanceId, Entity excludeSelf)
    {
        var result = new List<(Entity, string, uint)>();
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (chunkRoot is null) return result;
        ushort target = (ushort)targetInstanceId;
        foreach (var cand in AllEntities(chunkRoot))
        {
            if (cand == excludeSelf) continue;
            if (cand.Get<InstanceData>() is { } candInst && candInst.Source.Instances.Contains(target))
                result.Add((cand, "Instance", candInst.Source.GetID()));
            else if (cand.Get<CrashEngine.Importer.TriggerMarker>() is { } candTrig &&
                     candTrig.Source.Trigger.Instances.Contains(target))
                result.Add((cand, "Trigger", candTrig.Source.GetID()));
        }
        return result;
    }

    private void DrawConditionPickerWindow()
    {
        if (!_showConditionPicker) return;
        var targetEnt = _conditionPickerTargetEntity;
        var targetInst = targetEnt?.Get<InstanceData>();
        if (targetEnt is null || targetInst is null) { _showConditionPicker = false; return; }
        uint targetId = targetInst.Source.GetID();

        ImGui.SetNextWindowSize(new Vector2(460f, 480f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin($"Add Condition For '{targetEnt.Name}'##condpicker", ref _showConditionPicker))
        {
            ImGui.End();
            return;
        }

        ImGui.TextDisabled($"Pick any Trigger or Instance in this level — it will message\n" +
                            $"'{targetEnt.Name}' once its own condition fires (adds this\n" +
                            "object's id to the PICKED one's own linked-instances list —\n" +
                            "same direction the real dock→door link uses). Yellow [ ] = not\n" +
                            "yet linked to this object; green [x] = already linked.");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##condPickerFilter", "Search...", ref _conditionPickerFilter, 128);

        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        ImGui.BeginChild("##condPickerList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        if (chunkRoot is not null)
        {
            int idx = 0;
            ushort targetU16 = (ushort)targetId;
            foreach (var cand in AllEntities(chunkRoot))
            {
                if (cand == targetEnt) continue;

                string kind; uint candId; bool already;
                if (cand.Get<InstanceData>() is { } candInst)
                {
                    kind = "Instance"; candId = candInst.Source.GetID();
                    already = candInst.Source.Instances.Contains(targetU16);
                }
                else if (cand.Get<CrashEngine.Importer.TriggerMarker>() is { } candTrig)
                {
                    kind = "Trigger"; candId = candTrig.Source.GetID();
                    already = candTrig.Source.Trigger.Instances.Contains(targetU16);
                }
                else continue;

                if (!string.IsNullOrWhiteSpace(_conditionPickerFilter) &&
                    !cand.Name.Contains(_conditionPickerFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                ImGui.PushID(idx++);
                ImGui.TextColored(already ? new Vector4(0.5f, 1f, 0.5f, 1f) : new Vector4(1f, 0.85f, 0.2f, 1f),
                                   already ? "[x]" : "[ ]");
                ImGui.SameLine();
                if (ImGui.Selectable($"[{kind}] {cand.Name}  (0x{candId:X4})"))
                {
                    if (!already)
                    {
                        if (cand.Get<InstanceData>() is { } ci) ci.Source.Instances.Add(targetU16);
                        else if (cand.Get<CrashEngine.Importer.TriggerMarker>() is { } ct) ct.Source.Trigger.Instances.Add(targetU16);
                        _browser.Log($"Conditions: '{cand.Name}' will now message '{targetEnt.Name}' once its " +
                                      "own condition fires (added to its own linked-instances list). " +
                                      "Save Chunk to persist, then test in an emulator — not verified live yet.");
                    }
                    _showConditionPicker = false;
                }
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
        ImGui.End();
    }
}
