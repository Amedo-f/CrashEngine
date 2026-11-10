using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene
{
    private enum BuildTarget { WindowsPc, Ps2Iso }

    private bool _showBuildWindow;
    private BuildTarget _buildTarget;

    private void DrawBuildWindow()
    {
        if (!_showBuildWindow) return;
        EnsurePcOutputPathLoaded();
        EnsureIsoOutputPathLoaded();
        ImGui.SetNextWindowSize(new Vector2(680f, 260f), ImGuiCond.FirstUseEver);
        if (!_windowChrome.Begin("Build##buildwindow", ref _showBuildWindow)) return;

        ImGui.BeginChild("##buildtargets", new Vector2(170f, -1f), ImGuiChildFlags.Border);
        if (ImGui.Selectable("PC (Windows)", _buildTarget == BuildTarget.WindowsPc)) _buildTarget = BuildTarget.WindowsPc;
        ImGui.BeginDisabled();
        ImGui.Selectable("PC (Linux) - soon");
        ImGui.EndDisabled();
        if (ImGui.Selectable("PS2 (ISO)", _buildTarget == BuildTarget.Ps2Iso)) _buildTarget = BuildTarget.Ps2Iso;
        ImGui.EndChild();
        ImGui.SameLine();

        ImGui.BeginChild("##buildtarget", new Vector2(-1f, -1f));
        if (_buildTarget == BuildTarget.Ps2Iso) DrawIsoBuildTarget();
        else DrawPcBuildTarget();
        ImGui.EndChild();
        ImGui.End();
    }

    private void DrawPcBuildTarget()
    {
        ImGui.TextUnformatted("PC (Windows)");
        ImGui.Separator();
        ImGui.TextUnformatted("Output folder:");
        ImGui.SetNextItemWidth(-40f);
        if (ImGui.InputTextWithHint("##buildPcOut", "not set — Build asks for it", ref _pcOutputPath, 512u)) SavePcOutputPath();
        ImGui.SameLine();
        if (ImGui.Button("...##buildPcBrowse")) ChoosePcOutputPath(null);
        if (!string.IsNullOrWhiteSpace(_pcOutputPath))
            ImGui.TextDisabled("-> " + Path.Combine(_pcOutputPath.Trim(), BuiltGameName, BuiltGameName + ".exe"));
        DrawBuildStartLevel();
        var (build, run) = DrawBuildButtons(withRun: true);
        if (build || run) BuildPc(run);
    }

    private void DrawIsoBuildTarget()
    {
        ImGui.TextUnformatted("PS2 (ISO)");
        ImGui.Separator();
        ImGui.TextUnformatted("ISO file (overwritten every build):");
        ImGui.SetNextItemWidth(-40f);
        if (ImGui.InputTextWithHint("##buildIsoOut", "not set — Build asks for it", ref _isoOutputPath, 512u)) SaveIsoOutputPath();
        ImGui.SameLine();
        if (ImGui.Button("...##buildIsoBrowse")) ChooseIsoOutputPath(null);
        DrawBuildStartLevel();
        if (DrawBuildButtons(withRun: false).Build) BuildIso();
    }

    private void DrawBuildStartLevel()
    {
        var level = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2Path;
        ImGui.Spacing();
        ImGui.TextDisabled("Starts in: " + (string.IsNullOrEmpty(level) ? "the default level" : Path.ChangeExtension(level, null) + " (the open level)"));
    }

    private (bool Build, bool Run) DrawBuildButtons(bool withRun)
    {
        const float width = 120f, gap = 8f;
        float x = ImGui.GetWindowWidth() - width - 8f - (withRun ? width + gap : 0f);
        ImGui.SetCursorPos(new Vector2(x, ImGui.GetWindowHeight() - ImGui.GetFrameHeight() - 8f));
        ImGui.BeginDisabled(_building || _buildingPc);
        bool build = ImGui.Button((_buildingPc ? "Building..." : "Build") + "##buildgo", new Vector2(width, 0f));
        bool run = false;
        if (withRun)
        {
            ImGui.SameLine(0f, gap);
            run = ImGui.Button("Build And Run##buildrun", new Vector2(width, 0f));
        }
        ImGui.EndDisabled();
        return (build, run);
    }
}
