using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private bool   _showScriptsBrowser;
    private string _scriptsFilter = "";

    private void DrawScriptsBrowserWindow()
    {
        if (!_showScriptsBrowser) return;
        var chunkRoot   = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        ImGui.SetNextWindowSize(new Vector2(560f, 480f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Scripts (CODE_BEHAVIOURS_SECTION)##scriptsbrowser", ref _showScriptsBrowser))
        {
            ImGui.End();
            return;
        }
        if (chunkSource?.Rm2 is null) { ImGui.TextDisabled("No level loaded."); ImGui.End(); return; }
        var code   = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION);
        var behSec = code?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
        if (behSec is null) { ImGui.TextDisabled("This chunk has no CODE_BEHAVIOURS_SECTION."); ImGui.End(); return; }

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##scriptsFilter", "Search by name or hex id...", ref _scriptsFilter, 128);
        ImGui.BeginChild("##scriptsList", new Vector2(-1f, -1f), ImGuiChildFlags.Border);
        for (int i = 0; i < behSec.GetItemsAmount(); i++)
        {
            var item = behSec.GetItem(i);
            var id = item.GetID();
            string label = item is PS2BehaviourGraph g && !string.IsNullOrEmpty(g.Name)
                ? g.Name : (item is TwinBehaviourStarter ? "(starter, no name)" : "(unnamed)");
            string typeStr = item is TwinBehaviourStarter ? "Starter" : item is PS2BehaviourGraph ? "Graph" : item.GetType().Name;
            if (!string.IsNullOrWhiteSpace(_scriptsFilter) &&
                !label.Contains(_scriptsFilter, StringComparison.OrdinalIgnoreCase) &&
                !$"{id:X4}".Contains(_scriptsFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            ImGui.Text($"0x{id:X4}  [{typeStr,-7}]  {label}");
        }
        ImGui.EndChild();
        ImGui.End();
    }
}
