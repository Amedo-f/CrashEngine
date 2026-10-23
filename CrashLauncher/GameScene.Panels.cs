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
        if (ImGui.IsItemHovered()) MaybeTooltip("Add a GameObject (same as the GameObject menu).");
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
        if (ImGui.IsItemHovered()) MaybeTooltip("Shows or hides this object in the editor.");
        ImGui.SameLine();
        float menuWidth = ImGui.GetFrameHeight();
        ImGui.SetNextItemWidth(-(menuWidth + ImGui.GetStyle().ItemSpacing.X));
        string name = e.Name;
        ImGui.InputText("##inspectorname", ref name, 256u, ImGuiInputTextFlags.ReadOnly);
        ImGui.SameLine();
        if (ImGui.Button("...##inspectormenu", new Vector2(menuWidth, 0f))) ImGui.OpenPopup("##inspectormenupopup");
        if (ImGui.IsItemHovered()) MaybeTooltip("More actions for this object.");
        if (ImGui.BeginPopup("##inspectormenupopup"))
        {
            if (ImGui.MenuItem("Export Object (OBJ + textures)...")) ExportSelectedObjectToObj();
            if (ImGui.IsItemHovered())
                MaybeTooltip("Exports this object (and every child part) as a real Wavefront .obj +\n.mtl + .png texture files, ready to open directly in Blender — full shape,\nUVs, and textures, not just raw positions like Export Collision to OBJ.");
            if (ImGui.MenuItem("Delete", "Del", false, _selectedSet.Count > 0)) DeleteSelected();
            ImGui.EndPopup();
        }
        ImGui.Separator();
    }
}
