using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void DrawBehavioursSectionBody(PS2AnyObject obj, BaseTwinSection behSecForList, bool editingGlobal, ChunkSource? scriptChunkSrc)
    {
        if (editingGlobal)
            ImGui.TextDisabled("Editing the GLOBAL (Startup\\Default.rm2) scripts — shared by\n" +
                                "EVERY placed instance of this exact object type, in every level.");
        ImGui.BeginChild("##objBehList", new Vector2(-1f, 200f), ImGuiChildFlags.Border);
        foreach (var bid in obj.RefBehaviours)
        {
            var bItem = behSecForList.GetItem<BaseTwinItem>(bid);
            string bLabel = bItem is PS2BehaviourGraph bg && !string.IsNullOrEmpty(bg.Name)
                ? bg.Name : (bItem is TwinBehaviourStarter ? "(starter, no name)" : "(missing)");
            string bType = bItem is TwinBehaviourStarter ? "Starter" : bItem is PS2BehaviourGraph ? "Graph" : "?";
            bool clicked = ImGui.Selectable($"0x{bid:X4}  [{bType,-7}]  {bLabel}", _inspectedBehaviourId == bid); // Amedo 2026-09-25
            if (clicked && bItem is PS2BehaviourGraph clickedGraph)
                OpenBehaviourInTab(bid, clickedGraph, editingGlobal, manual: false); // Amedo 2026-09-25
        }
        ImGui.EndChild();

        ImGui.SetNextItemWidth(90f);
        ImGui.InputText("##openBehIdInput", ref _openBehIdText, 8,
            ImGuiInputTextFlags.CharsHexadecimal);
        ImGui.SameLine();
        if (ImGui.Button("Open by ID (hex)##openbehid"))
        {
            if (uint.TryParse(_openBehIdText, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out uint manualId) &&
                behSecForList.GetItem<PS2BehaviourGraph>(manualId) is { } manualGraph)
            {
                OpenBehaviourInTab(manualId, manualGraph, editingGlobal, manual: true); // Amedo 2026-09-25
            }
            else
            {
                _browser.Log($"Open by ID: 0x{_openBehIdText} not found in this " +
                    $"{(editingGlobal ? "GLOBAL" : "LOCAL")} Behaviours section (or not a Graph).");
            }
        }

        if (_inspectedBehaviourId is uint ibid && _inspectedBehaviourGraph is not null) // Amedo 2026-09-25
        {
            ImGui.TextDisabled($"0x{ibid:X4} is open in the Behaviour tab.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Show##showbehtab")) _focusBehaviourTab = true;
        }
    }

    private string? GetPristineBehaviourGraphText(bool isGlobal, uint bid)
    {
        try
        {
            var freshRm2 = TryReadPristineRm2(isGlobal ? GlobalRm2Path : _rm2);
            if (freshRm2 is null) return null;

            var freshBehSec = BehavioursOf(freshRm2);
            var freshGraph = freshBehSec?.GetItem<PS2BehaviourGraph>(bid);
            return freshGraph is null ? null : GraphToEditableText(freshGraph);
        }
        catch { return null; }
    }
}
