using System.Numerics;
using ImGuiNET;

namespace CrashLauncher;

// Amedo
public static class EditorTheme
{
    private static Vector4 Gray(float v, float a = 1f) => new(v, v, v, a);

    // Amedo 2026-11-14
    public static readonly Vector4 Accent = new(0.18f, 0.49f, 0.87f, 1f);
    public static readonly Vector4 AccentHovered = new(0.24f, 0.55f, 0.93f, 1f);
    public static readonly Vector4 AccentActive = new(0.15f, 0.43f, 0.78f, 1f);
    public static readonly Vector4 Selection = new(0.17f, 0.33f, 0.50f, 1f);
    public static readonly Vector4 SelectionHovered = new(0.20f, 0.38f, 0.57f, 1f);
    public static readonly Vector4 PanelBg = Gray(0.098f);
    public static readonly Vector4 BarBg = Gray(0.129f);

    // Amedo
    public static void Apply()
    {
        var style = ImGui.GetStyle();
        style.WindowRounding = 0f;
        style.ChildRounding = 0f;
        style.FrameRounding = 3f;
        style.PopupRounding = 4f;
        style.ScrollbarRounding = 6f;
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
        style.ScrollbarSize = 10f;
        style.GrabMinSize = 10f;
        style.WindowMenuButtonPosition = ImGuiDir.None;

        var c = style.Colors;
        c[(int)ImGuiCol.Text] = Gray(0.90f);
        c[(int)ImGuiCol.TextDisabled] = Gray(0.62f);
        c[(int)ImGuiCol.WindowBg] = PanelBg;
        c[(int)ImGuiCol.ChildBg] = PanelBg;
        c[(int)ImGuiCol.PopupBg] = Gray(0.15f);
        c[(int)ImGuiCol.Border] = Gray(0.19f);
        c[(int)ImGuiCol.BorderShadow] = Gray(0f, 0f);
        c[(int)ImGuiCol.FrameBg] = Gray(0.12f);
        c[(int)ImGuiCol.FrameBgHovered] = Gray(0.15f);
        c[(int)ImGuiCol.FrameBgActive] = Gray(0.17f);
        c[(int)ImGuiCol.TitleBg] = BarBg;
        c[(int)ImGuiCol.TitleBgActive] = Gray(0.15f);
        c[(int)ImGuiCol.TitleBgCollapsed] = BarBg;
        c[(int)ImGuiCol.MenuBarBg] = BarBg;
        c[(int)ImGuiCol.ScrollbarBg] = PanelBg;
        c[(int)ImGuiCol.ScrollbarGrab] = Gray(0.27f);
        c[(int)ImGuiCol.ScrollbarGrabHovered] = Gray(0.33f);
        c[(int)ImGuiCol.ScrollbarGrabActive] = Gray(0.40f);
        c[(int)ImGuiCol.CheckMark] = Accent;
        c[(int)ImGuiCol.SliderGrab] = Accent;
        c[(int)ImGuiCol.SliderGrabActive] = AccentHovered;
        c[(int)ImGuiCol.Button] = Gray(0.235f);
        c[(int)ImGuiCol.ButtonHovered] = Gray(0.29f);
        c[(int)ImGuiCol.ButtonActive] = Gray(0.33f);
        c[(int)ImGuiCol.Header] = Gray(0.165f);
        c[(int)ImGuiCol.HeaderHovered] = Gray(0.20f);
        c[(int)ImGuiCol.HeaderActive] = Gray(0.235f);
        c[(int)ImGuiCol.Separator] = Gray(0.19f);
        c[(int)ImGuiCol.SeparatorHovered] = Accent;
        c[(int)ImGuiCol.SeparatorActive] = Accent;
        c[(int)ImGuiCol.ResizeGrip] = Gray(0f, 0f);
        c[(int)ImGuiCol.ResizeGripHovered] = Gray(0.4f, 0.5f);
        c[(int)ImGuiCol.ResizeGripActive] = Gray(0.5f, 0.7f);
        c[(int)ImGuiCol.Tab] = Gray(0.11f);
        c[(int)ImGuiCol.TabHovered] = Gray(0.20f);
        c[(int)ImGuiCol.TabActive] = Gray(0.16f);
        c[(int)ImGuiCol.TabUnfocused] = Gray(0.11f);
        c[(int)ImGuiCol.TabUnfocusedActive] = Gray(0.16f);
        c[(int)ImGuiCol.DockingPreview] = new Vector4(Accent.X, Accent.Y, Accent.Z, 0.6f);
        c[(int)ImGuiCol.DockingEmptyBg] = Gray(0.07f);
        c[(int)ImGuiCol.TableHeaderBg] = Gray(0.137f);
        c[(int)ImGuiCol.TableBorderStrong] = Gray(0.19f);
        c[(int)ImGuiCol.TableBorderLight] = Gray(0.16f);
        c[(int)ImGuiCol.TableRowBg] = Gray(0f, 0f);
        c[(int)ImGuiCol.TableRowBgAlt] = Gray(0f, 0f);
        c[(int)ImGuiCol.TextSelectedBg] = new Vector4(Accent.X, Accent.Y, Accent.Z, 0.45f);
        c[(int)ImGuiCol.NavHighlight] = Accent;
        c[(int)ImGuiCol.ModalWindowDimBg] = Gray(0f, 0.55f);
    }
}
