using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;

namespace CrashLauncher;

// Amedo 2026-09-25
public sealed partial class GameScene
{
    private readonly record struct ResolvedTriggerBehaviour(uint GraphId, PS2BehaviourGraph Graph, bool IsGlobal);

    private static ResolvedTriggerBehaviour? ResolveTriggerBehaviour(uint id, BaseTwinSection? localBeh, BaseTwinSection? globalBeh)
    {
        uint current = id;
        for (int depth = 0; depth < 8; depth++)
        {
            bool isGlobal = false;
            var item = localBeh?.GetItem<BaseTwinItem>(current);
            if (item is null) { item = globalBeh?.GetItem<BaseTwinItem>(current); isGlobal = true; }
            if (item is PS2BehaviourGraph graph) return new ResolvedTriggerBehaviour(current, graph, isGlobal);
            if (item is TwinBehaviourStarter starter && starter.Assigners.Count > 0)
            {
                current = (uint)(starter.Assigners[0].Behaviour - 1);
                continue;
            }
            return null;
        }
        return null;
    }

    private static string TriggerBehaviourLabel(ResolvedTriggerBehaviour? r, uint rawId) =>
        r is { } v
            ? (string.IsNullOrEmpty(v.Graph.Name) ? $"Script_{v.GraphId:X4}" : v.Graph.Name) + (v.IsGlobal ? "  [global]" : "")
            : $"(unresolved 0x{rawId:X4})";

    private (BaseTwinSection? localBeh, BaseTwinSection? globalBeh, BaseTwinSection? localObjs, BaseTwinSection? globalObjs) GetCodeSections()
    {
        var src = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        BaseTwinSection? Sec(Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2? rm2, int sub) =>
            rm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)?.GetItem<BaseTwinSection>((uint)sub);
        return (Sec(src?.Rm2, TwinConstants.CODE_BEHAVIOURS_SECTION), Sec(src?.GlobalRm2, TwinConstants.CODE_BEHAVIOURS_SECTION),
                Sec(src?.Rm2, TwinConstants.CODE_GAME_OBJECTS_SECTION), Sec(src?.GlobalRm2, TwinConstants.CODE_GAME_OBJECTS_SECTION));
    }

    private void DrawMessageBehaviours(PS2AnyObject obj)
    {
        var (localBeh, globalBeh, _, _) = GetCodeSections();
        if (!ImGui.BeginTable("##objMsgBeh", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit))
            return;
        ImGui.TableSetupColumn("Message");
        ImGui.TableSetupColumn("Behaviour", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Caller");
        ImGui.TableSetupColumn("");
        ImGui.TableHeadersRow();
        for (int i = 0; i < obj.TriggerBehaviours.Count; i++)
        {
            var tb = obj.TriggerBehaviours[i];
            var r = ResolveTriggerBehaviour(tb.TriggerBehaviour, localBeh, globalBeh);
            ImGui.PushID(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.TextUnformatted(tb.MessageID.ToString());
            ImGui.TableNextColumn(); ImGui.TextUnformatted(TriggerBehaviourLabel(r, tb.TriggerBehaviour));
            ImGui.TableNextColumn(); ImGui.TextUnformatted(tb.BehaviourCallerIndex.ToString());
            ImGui.TableNextColumn();
            ImGui.BeginDisabled(r is null);
            if (ImGui.SmallButton("Open") && r is { } rr) OpenBehaviourInTab(rr.GraphId, rr.Graph, rr.IsGlobal, manual: true);
            ImGui.EndDisabled();
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private void DrawTriggerMessageTargets(PS2AnyTrigger trigger, ushort message)
    {
        var (localBeh, globalBeh, localObjs, globalObjs) = GetCodeSections();
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        if (chunkRoot is null) return;
        int shown = 0;
        var wanted = trigger.Trigger.Instances.Select(i => (uint)i).ToHashSet();
        var targets = AllEntities(chunkRoot)
            .Where(x => x.Get<InstanceData>() is { } d && wanted.Contains(d.Source.GetID()))
            .ToDictionary(x => x.Get<InstanceData>()!.Source.GetID(), x => x);
        foreach (var instId in trigger.Trigger.Instances)
        {
            if (!targets.TryGetValue(instId, out var target)) continue;
            var data = target.Get<InstanceData>();
            if (data is null) continue;
            var obj = globalObjs?.GetItem<PS2AnyObject>(data.ObjectId) ?? localObjs?.GetItem<PS2AnyObject>(data.ObjectId);
            if (obj is null) continue;
            foreach (var tb in obj.TriggerBehaviours.Where(t => t.MessageID == (message & 0x3FF)))
            {
                var r = ResolveTriggerBehaviour(tb.TriggerBehaviour, localBeh, globalBeh);
                ImGui.PushID($"{instId}_{tb.TriggerBehaviour}");
                ImGui.TextDisabled($"   -> {target.Name}: {TriggerBehaviourLabel(r, tb.TriggerBehaviour)}");
                ImGui.SameLine();
                if (ImGui.SmallButton("Select")) SelectClicked(target, false);
                ImGui.PopID();
                shown++;
            }
        }
        if (shown == 0)
            ImGui.TextDisabled("   -> no linked instance reacts to this message");
    }
}
