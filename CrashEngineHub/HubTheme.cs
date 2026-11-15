using System.Numerics;
using CrashLauncher;
using ImGuiNET;

namespace CrashEngineHub;

// Amedo 2026-11-14
public static class HubTheme
{
    public static readonly Vector4 SidebarBg = EditorTheme.BarBg;
    public static readonly Vector4 SidebarSelected = new(0.227f, 0.227f, 0.227f, 1f);
    public static readonly Vector4 SidebarHovered = new(0.18f, 0.18f, 0.18f, 1f);

    public static void Apply()
    {
        EditorTheme.Apply();
        var style = ImGui.GetStyle();
        style.WindowBorderSize = 0f;
        style.ChildBorderSize = 0f;
        style.WindowPadding = new Vector2(10f, 10f);
        style.FramePadding = new Vector2(10f, 6f);
        style.CellPadding = new Vector2(8f, 6f);
        style.ItemSpacing = new Vector2(8f, 6f);
        style.ItemInnerSpacing = new Vector2(6f, 4f);
        style.IndentSpacing = 16f;
        var c = style.Colors;
        c[(int)ImGuiCol.ChildBg] = new Vector4(0f, 0f, 0f, 0f);
    }
}
