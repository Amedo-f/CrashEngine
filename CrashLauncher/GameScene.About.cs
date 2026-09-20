using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private bool _showAbout;

    private void DrawAbout()
    {
        if (!_showAbout) return;

        ImGui.SetNextWindowSize(new Vector2(440f, 260f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("About##about", ref _showAbout, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        ImGui.TextUnformatted("CrashEngine");
        ImGui.Spacing();
        ImGui.TextWrapped(
            "CrashEngine is a tool for creating mods for Crash Twinsanity. " +
            "It is in development by Yonko Amedo.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Version 0.0.6");
        ImGui.TextUnformatted("Developed by Yonko Amedo");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Special thanks");
        ImGui.Spacing();
        ImGui.BulletText("NeoKesha");
        ImGui.BulletText("Smartkin");

        ImGui.End();
    }
}
