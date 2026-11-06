using CrashEngine.Core;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo
public sealed partial class GameScene
{
    // Amedo
    private void DrawHierarchyHeader()
    {
        if (ImGui.Button("+##hierarchyadd")) ImGui.OpenPopup("##hierarchyaddmenu");
        if (ImGui.BeginPopup("##hierarchyaddmenu"))
        {
            DrawGameObjectMenu();
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##filter", "Search", ref _sceneFilter, 128u);
        ImGui.Separator();
    }

    // Amedo
    private void DrawInspectorHeader(Entity e)
    {
        bool active = e.Active;
        if (ImGui.Checkbox("##active", ref active)) e.Active = active;
        ImGui.SameLine();
        float menuWidth = ImGui.GetFrameHeight();
        ImGui.SetNextItemWidth(-(menuWidth + ImGui.GetStyle().ItemSpacing.X));
        string name = e.Name;
        ImGui.InputText("##inspectorname", ref name, 256u, ImGuiInputTextFlags.ReadOnly);
        ImGui.SameLine();
        if (ImGui.Button("...##inspectormenu", new Vector2(menuWidth, 0f))) ImGui.OpenPopup("##inspectormenupopup");
        if (ImGui.BeginPopup("##inspectormenupopup"))
        {
            if (ImGui.MenuItem("Export Object (OBJ + textures)...")) ExportSelectedObjectToObj();
            if (ImGui.MenuItem("Delete", "Del", false, _selectedSet.Count > 0)) DeleteSelected();
            ImGui.EndPopup();
        }
        ImGui.Separator();
    }
}
