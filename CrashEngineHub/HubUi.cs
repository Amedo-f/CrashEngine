using System.Numerics;
using ImGuiNET;

namespace CrashEngineHub;

// Amedo 2026-11-12
public static class HubUi
{
    public const float LabelWidth = 120f;
    public const string DiscFilter = "Disc image\0*.iso\0All Files\0*.*\0\0";

    private static readonly Vector4 ErrorColor = new(1f, 0.45f, 0.4f, 1f);

    public static ImFontPtr? TitleFont { get; set; }

    public static void Title(string text)
    {
        if (TitleFont is { } font) ImGui.PushFont(font);
        ImGui.TextUnformatted(text);
        if (TitleFont is not null) ImGui.PopFont();
    }

    public static void Label(string text)
    {
        ImGui.TextDisabled(text);
        ImGui.SameLine(LabelWidth);
    }

    public static void Error(string text) => ImGui.TextColored(ErrorColor, text);

    public static void Gap(float height) => ImGui.Dummy(new Vector2(0f, height));

    public static bool PrimaryButton(string label, Vector2 size)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, CrashLauncher.EditorTheme.Accent); // Amedo 2026-11-14
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, CrashLauncher.EditorTheme.AccentHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, CrashLauncher.EditorTheme.AccentActive);
        ImGui.PushStyleColor(ImGuiCol.Border, CrashLauncher.EditorTheme.Accent);
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
        bool pressed = ImGui.Button(label, size);
        ImGui.PopStyleColor(5);
        return pressed;
    }

    public static bool Confirm(string title, ref bool open, string action, Action body, float actionWidth = 120f)
    {
        if (open) ImGui.OpenPopup(title);
        if (!ImGui.BeginPopupModal(title, ref open, ImGuiWindowFlags.AlwaysAutoResize)) return false;
        body();
        Gap(8f);
        bool yes = ImGui.Button(action, new Vector2(actionWidth, 28f));
        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(120f, 28f)) || yes)
        {
            open = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
        return yes;
    }
}
