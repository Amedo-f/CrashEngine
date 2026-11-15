using System.Numerics;
using CrashEngine.Core;
using CrashLauncher;
using ImGuiNET;

namespace CrashEngineHub;

// Amedo 2026-11-12
public sealed class HubScene : Scene
{
    private enum Page { Play, Projects }

    private const float SidebarWidth = 220f;

    private readonly ImGuiFileBrowser _browser = new();
    private readonly ProjectsPage _projects;
    private readonly PlayPage _play;
    private Page _page = Page.Play;

    public HubScene()
    {
        _projects = new ProjectsPage(_browser);
        _play = new PlayPage(_browser);
    }

    public override void OnImGuiRender()
    {
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGui.SetNextWindowSize(viewport.WorkSize);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.Begin("##hub", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus);
        ImGui.PopStyleVar();
        DrawSidebar();
        ImGui.SameLine(0f, 0f);
        ImGui.BeginChild("##page", Vector2.Zero, ImGuiChildFlags.None);
        ImGui.SetCursorPos(new Vector2(28f, 20f));
        ImGui.BeginGroup();
        if (_page == Page.Play) _play.Draw();
        else _projects.Draw();
        ImGui.EndGroup();
        ImGui.EndChild();
        ImGui.End();
        if (!_projects.DrawsBrowser) _browser.Draw();
    }

    private void DrawSidebar()
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, HubTheme.SidebarBg); // Amedo 2026-11-14
        ImGui.BeginChild("##sidebar", new Vector2(SidebarWidth, 0f), ImGuiChildFlags.None);
        var edge = ImGui.GetWindowPos() + new Vector2(SidebarWidth - 1f, 0f); // Amedo 2026-11-14
        ImGui.GetWindowDrawList().AddLine(edge, edge + new Vector2(0f, ImGui.GetWindowHeight()), ImGui.GetColorU32(ImGuiCol.Border));
        ImGui.SetCursorPos(new Vector2(20f, 22f));
        ImGui.TextDisabled("CrashEngine Hub");
        HubUi.Gap(14f);
        SidebarItem("Play", Page.Play);
        SidebarItem("Projects", Page.Projects);
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    // Amedo 2026-11-14
    private void SidebarItem(string label, Page page)
    {
        bool selected = _page == page;
        ImGui.SetCursorPosX(16f);
        var size = new Vector2(SidebarWidth - 32f, 38f);
        var min = ImGui.GetCursorScreenPos();
        bool pressed = ImGui.InvisibleButton($"##side{page}", size);
        var draw = ImGui.GetWindowDrawList();
        if (selected || ImGui.IsItemHovered())
            draw.AddRectFilled(min, min + size, ImGui.GetColorU32(selected ? HubTheme.SidebarSelected : HubTheme.SidebarHovered), 5f);
        float fontSize = ImGui.GetFontSize();
        draw.AddText(ImGui.GetFont(), fontSize, min + new Vector2(16f, (size.Y - fontSize) * 0.5f),
            ImGui.GetColorU32(selected ? new Vector4(1f, 1f, 1f, 1f) : ImGui.GetStyle().Colors[(int)ImGuiCol.Text]), label);
        if (!pressed) return;
        _page = page;
        if (page == Page.Projects) _projects.Refresh();
    }
}
