using CrashEngine.Core;
using CrashEngine.Stealth;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void DrawInspectorStealthEnemy(Entity e, StealthEnemy? se)
    {
        if (se is not null)
        {
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Enemy##se", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled($"State:      {se.State}");
                ImGui.TextDisabled($"Detection:  {se.DetectionPct:F1}%%");
            }
        }
    }

    private void DrawInspectorAgentLabState(Entity e)
    {
        if (e.Get<CrashEngine.Runtime.AgentLab.AgentLabAgent>() is { } agent) // Amedo 2026-09-26
        {
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.4f, 1f, 0.6f, 1f), "AgentLab (running)");
            ImGui.TextDisabled($"Behaviour / state: {agent.StateLabel}");
            ImGui.TextDisabled($"Message: {(agent.Message == CrashEngine.Runtime.AgentLab.AgentLabAgent.NoMessage ? "-" : $"{agent.Message} @ {agent.MessageTime:0.00}s")}"); // Amedo 2026-09-28
        }
    }
}
