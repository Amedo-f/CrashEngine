using System.Numerics;
using CrashEngine.Core;
using CrashEngine.Runtime;
using ImGuiNET;

namespace CrashLauncher;

// Amedo 2026-10-26
public sealed partial class GameScene
{
    private static readonly (string Name, float Ratio)[] GameAspects =
    [
        ("Free Aspect", 0f), ("16:9", 16f / 9f), ("16:10", 16f / 10f), ("4:3", 4f / 3f), ("21:9", 21f / 9f),
    ];

    private int _gameAspect;
    private bool _playMaximized = true;
    private float _gameControlsWidth;

    private float StripHeight => WorkspaceTabHeight * (_workspace == Workspace.Scene ? 2f : 1f);

    private void DrawGameViewToolbar(Vector2 origin, float width)
    {
        float rowY = origin.Y + WorkspaceTabHeight;
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(new Vector2(origin.X, rowY), new Vector2(origin.X + width, rowY + WorkspaceTabHeight), ImGui.GetColorU32(ImGuiCol.TabActive));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, 2f));
        float y = rowY + (WorkspaceTabHeight - ImGui.GetFrameHeight()) * 0.5f;

        ImGui.SetCursorScreenPos(new Vector2(origin.X + 6f, y));
        ImGui.SetNextItemWidth(104f);
        if (ImGui.BeginCombo("##viewmode", _simulatorOn ? "Simulator" : "Game"))
        {
            if (ImGui.Selectable("Game", !_simulatorOn)) _simulatorOn = false;
            if (ImGui.Selectable("Simulator", _simulatorOn)) _simulatorOn = SimDevice is not null;
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (SimulatorActive) DrawSimulatorToolbarItems();
        else
        {
            ImGui.SetNextItemWidth(120f);
            if (ImGui.BeginCombo("##gameaspect", GameAspects[_gameAspect].Name))
            {
                for (int i = 0; i < GameAspects.Length; i++)
                    if (ImGui.Selectable(GameAspects[i].Name, i == _gameAspect)) _gameAspect = i;
                ImGui.EndCombo();
            }
        }
        float leftEnd = ImGui.GetItemRectMax().X;
        if (_simulation is { } sim)
        {
            ImGui.SameLine(0f, 16f);
            DrawPlayStatus(sim);
        }

        ImGui.SetCursorScreenPos(new Vector2(MathF.Max(leftEnd + 16f, origin.X + width - _gameControlsWidth - 6f), y));
        ImGui.BeginGroup();
        DrawGameViewControls();
        ImGui.EndGroup();
        _gameControlsWidth = ImGui.GetItemRectSize().X;
        ImGui.PopStyleVar();
    }

    private void DrawGameViewControls()
    {
        ToolbarToggle("Play Maximized##playmax", ref _playMaximized, "Play starts with the view filling the whole window (Tab shows the panels again).");
        ImGui.SameLine();
        if (DrawMuteButton() && _simulation is { } playing) playing.Muted = !_simSound;
        ImGui.SameLine(0f, 2f);
        ImGui.SetNextItemWidth(70f);
        if (ImGui.SliderFloat("##simvolume", ref _simVolume, 0f, 1.5f, "%.2f") && _simulation is { } sim) sim.Volume = _simVolume;
        ImGui.SameLine();
        if (ToolbarToggle("Game Camera##simgamecam", ref _simGameCamera,
                "Views the scene through the game's own camera behind Crash (follow, camera zones, walls, right stick, L1/R1 recenter).\n" +
                "Off (or F7) flies the free camera: it starts just behind the game camera. Crash's controls then\n" +
                "follow this free camera (forward = where you look), while the game camera keeps working as in the game\n" +
                "and still decides what wakes up and where sounds are heard from."))
            SetSimGameCamera(_simGameCamera);
        ImGui.SameLine();
        ToolbarToggle("Camera Gizmo##simshowcam", ref _showGameCameraModel,
            "With the free camera: shows the game camera as a 3D model following Crash, its view cone (FOV),\n" +
            "the point it looks at, the camera zone it uses (green) and the other zones Crash is in (grey),\n" +
            "plus the path it took. Editor only: never in the game, the ISO or Build to PC.");
        ImGui.SameLine();
        ToolbarToggle("Stats##simperf", ref _simPerf,
            "Performance counters (top right): FPS, frame time, CPU time per part (simulation, camera, particles, sound, render, UI),\n" +
            "GPU time, draw calls and memory allocated per frame. Values are smoothed; peak = worst frame of the last second.");
    }

    private static bool ToolbarToggle(string label, ref bool value, string tip)
    {
        var colors = ImGui.GetStyle().Colors;
        ImGui.PushStyleColor(ImGuiCol.Button, value ? colors[(int)ImGuiCol.ButtonActive] : new Vector4(0f, 0f, 0f, 0f));
        bool clicked = ImGui.Button(label);
        ImGui.PopStyleColor();
        if (clicked) value = !value;
        return clicked;
    }

    private bool DrawMuteButton()
    {
        float h = ImGui.GetFrameHeight();
        var min = ImGui.GetCursorScreenPos();
        bool clicked = ImGui.InvisibleButton("##simmute", new Vector2(h, h));
        if (clicked) _simSound = !_simSound;
        var dl = ImGui.GetWindowDrawList();
        if (ImGui.IsItemHovered())
        {
            dl.AddRectFilled(min, min + new Vector2(h), ImGui.GetColorU32(ImGuiCol.ButtonHovered), 3f);
        }
        uint ink = ImGui.GetColorU32(_simSound ? ImGuiCol.Text : ImGuiCol.TextDisabled);
        var c = min + new Vector2(h * 0.42f, h * 0.5f);
        float s = h * 0.16f;
        dl.AddRectFilled(c + new Vector2(-2.4f * s, -s), c + new Vector2(-1.2f * s, s), ink);
        dl.AddTriangleFilled(c + new Vector2(-1.2f * s, -s), c + new Vector2(0.4f * s, -2.4f * s), c + new Vector2(0.4f * s, 2.4f * s), ink);
        dl.AddTriangleFilled(c + new Vector2(-1.2f * s, -s), c + new Vector2(0.4f * s, 2.4f * s), c + new Vector2(-1.2f * s, s), ink);
        if (_simSound)
        {
            dl.PathArcTo(c + new Vector2(0.6f * s, 0f), 1.9f * s, -0.9f, 0.9f, 8);
            dl.PathStroke(ink, ImDrawFlags.None, 1.6f);
        }
        else
        {
            dl.AddLine(c + new Vector2(1.2f * s, -1.4f * s), c + new Vector2(3.6f * s, 1.4f * s), ink, 1.6f);
            dl.AddLine(c + new Vector2(3.6f * s, -1.4f * s), c + new Vector2(1.2f * s, 1.4f * s), ink, 1.6f);
        }
        return clicked;
    }

    private static void DrawPlayStatus(SceneSimulation sim)
    {
        ImGui.TextDisabled($"AgentLab {sim.World.Agents.Count} agents, t = {sim.World.Now:0.0}s");
        if (sim.World.PlayerCharacter is { } pc) // Amedo 2026-09-28
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"| hp {pc.HitPoints} lives {sim.World.Lives}{(pc.Dead ? " (dead)" : pc.Invincible ? " (invincible)" : "")}");
        }
    }

    private void FitGameViewport()
    {
        if (SimulatorActive)
        {
            FitSimulatorViewport();
            return;
        }
        float ratio = GameAspects[_gameAspect].Ratio;
        if (_workspace != Workspace.Scene || ratio <= 0f) return;
        var engine = Engine.Instance;
        var area = engine.SceneViewport ?? (0, 0, engine.Width, engine.Height);
        int w = area.W, h = area.H;
        if (w > h * ratio) w = (int)MathF.Round(h * ratio);
        else h = (int)MathF.Round(w / ratio);
        engine.SceneViewport = (area.X + (area.W - w) / 2, area.Y + (area.H - h) / 2, Math.Max(16, w), Math.Max(16, h));
    }
}
