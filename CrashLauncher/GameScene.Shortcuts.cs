using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-09-22
public sealed partial class GameScene
{
    private bool _showShortcuts;

    private void DrawShortcuts()
    {
        if (!_showShortcuts) return;

        ImGui.SetNextWindowSize(new Vector2(440f, 460f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("Shortcuts##shortcuts", ref _showShortcuts, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        static void Row(string key, string desc)
        {
            ImGui.TextUnformatted(key);
            ImGui.SameLine(150f);
            ImGui.TextDisabled(desc);
        }

        ImGui.TextUnformatted("Editing");
        ImGui.Separator();
        Row("Ctrl+Z",            "Undo");
        Row("Ctrl+Y  /  Ctrl+Shift+Z", "Redo");
        Row("Ctrl+D",            "Duplicate selected");
        Row("Ctrl+D + A",        "Duplicate with direction / array");
        Row("Delete",            "Delete selected");
        Row("Ctrl+G",            "Glue mode (snap to another object)");
        Row("X / Y / Z",         "Lock glue to an axis (while gluing)");
        Row("Esc",               "Cancel current mode / back to levels");

        ImGui.Spacing();
        ImGui.TextUnformatted("Selection");
        ImGui.Separator();
        Row("Click",             "Select an object / scenery tile");
        Row("Shift+Click",       "Add to selection (multi-select)");

        ImGui.Spacing();
        ImGui.TextUnformatted("Gizmo");
        ImGui.Separator();
        Row("Position / Rotation", "Switch gizmo mode (top of Scene panel)");

        ImGui.Spacing();
        ImGui.TextUnformatted("Camera");
        ImGui.Separator();
        Row("W A S D",           "Move the camera");
        Row("Mouse",             "Look around");
        Row("Shift",             "Move faster");

        ImGui.End();
    }
}
