using System.Numerics;
using ImGuiNET;

namespace CrashLauncher;

// Amedo
public static class EditorTheme
{
    private static Vector4 Gray(float v, float a = 1f) => new(v, v, v, a);
    private const float Darkness = 0.78f; // Amedo
    private static Vector4 Bg(float v, float a = 1f) => Gray(v * Darkness, a);

    // Amedo
    public static void Apply()
    {
        var style = ImGui.GetStyle();
        style.WindowRounding = 0f;
        style.ChildRounding = 0f;
        style.FrameRounding = 3f;
        style.PopupRounding = 2f;
        style.ScrollbarRounding = 3f;
        style.GrabRounding = 3f;
        style.TabRounding = 3f;
        style.WindowBorderSize = 1f;
        style.FrameBorderSize = 1f;
        style.PopupBorderSize = 1f;
        style.TabBorderSize = 0f;
        style.WindowPadding = new Vector2(6f, 6f);
        style.FramePadding = new Vector2(6f, 3f);
        style.ItemSpacing = new Vector2(6f, 4f);
        style.ItemInnerSpacing = new Vector2(4f, 4f);
        style.IndentSpacing = 14f;
        style.ScrollbarSize = 12f;
        style.GrabMinSize = 10f;
        style.WindowMenuButtonPosition = ImGuiDir.None;

        var c = style.Colors;
        c[(int)ImGuiCol.Text] = Gray(0.82f);
        c[(int)ImGuiCol.TextDisabled] = Gray(0.52f);
        c[(int)ImGuiCol.WindowBg] = Bg(0.22f);
        c[(int)ImGuiCol.ChildBg] = Bg(0.22f);
        c[(int)ImGuiCol.PopupBg] = Bg(0.20f);
        c[(int)ImGuiCol.Border] = Bg(0.10f);
        c[(int)ImGuiCol.BorderShadow] = Bg(0f, 0f);
        c[(int)ImGuiCol.FrameBg] = Bg(0.165f);
        c[(int)ImGuiCol.FrameBgHovered] = Bg(0.20f);
        c[(int)ImGuiCol.FrameBgActive] = Bg(0.24f);
        c[(int)ImGuiCol.TitleBg] = Bg(0.157f);
        c[(int)ImGuiCol.TitleBgActive] = Bg(0.19f);
        c[(int)ImGuiCol.TitleBgCollapsed] = Bg(0.157f);
        c[(int)ImGuiCol.MenuBarBg] = Bg(0.17f);
        c[(int)ImGuiCol.ScrollbarBg] = Bg(0.20f);
        c[(int)ImGuiCol.ScrollbarGrab] = Bg(0.37f);
        c[(int)ImGuiCol.ScrollbarGrabHovered] = Bg(0.43f);
        c[(int)ImGuiCol.ScrollbarGrabActive] = Bg(0.50f);
        c[(int)ImGuiCol.CheckMark] = Gray(0.85f);
        c[(int)ImGuiCol.SliderGrab] = Bg(0.55f);
        c[(int)ImGuiCol.SliderGrabActive] = Bg(0.70f);
        c[(int)ImGuiCol.Button] = Bg(0.345f);
        c[(int)ImGuiCol.ButtonHovered] = Bg(0.40f);
        c[(int)ImGuiCol.ButtonActive] = new Vector4(0.27f, 0.45f, 0.65f, 1f);
        c[(int)ImGuiCol.Header] = Bg(0.26f);
        c[(int)ImGuiCol.HeaderHovered] = Bg(0.30f);
        c[(int)ImGuiCol.HeaderActive] = new Vector4(0.173f, 0.365f, 0.53f, 1f);
        c[(int)ImGuiCol.Separator] = Bg(0.13f);
        c[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.27f, 0.45f, 0.65f, 1f);
        c[(int)ImGuiCol.SeparatorActive] = new Vector4(0.27f, 0.45f, 0.65f, 1f);
        c[(int)ImGuiCol.ResizeGrip] = Bg(0f, 0f);
        c[(int)ImGuiCol.ResizeGripHovered] = Bg(0.45f, 0.5f);
        c[(int)ImGuiCol.ResizeGripActive] = Bg(0.55f, 0.7f);
        c[(int)ImGuiCol.Tab] = Bg(0.17f);
        c[(int)ImGuiCol.TabHovered] = Bg(0.28f);
        c[(int)ImGuiCol.TabActive] = Bg(0.22f);
        c[(int)ImGuiCol.TabUnfocused] = Bg(0.17f);
        c[(int)ImGuiCol.TabUnfocusedActive] = Bg(0.22f);
        c[(int)ImGuiCol.DockingPreview] = new Vector4(0.27f, 0.45f, 0.65f, 0.6f);
        c[(int)ImGuiCol.DockingEmptyBg] = Bg(0.157f);
        c[(int)ImGuiCol.TextSelectedBg] = new Vector4(0.173f, 0.365f, 0.53f, 0.8f);
        c[(int)ImGuiCol.NavHighlight] = new Vector4(0.27f, 0.45f, 0.65f, 1f);
        c[(int)ImGuiCol.TableHeaderBg] = Bg(0.19f);
        c[(int)ImGuiCol.TableBorderStrong] = Bg(0.10f);
        c[(int)ImGuiCol.TableBorderLight] = Bg(0.16f);
    }
}
