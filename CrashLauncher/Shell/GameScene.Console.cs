using CrashEngine.Core;
using ImGuiNET;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
	private void DrawConsolePanel(int width, int height)
	{
		ImGui.SetNextWindowPos(new System.Numerics.Vector2(0f, (float)height - 180f), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(new System.Numerics.Vector2(width, 180f), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSizeConstraints(new System.Numerics.Vector2(200f, 60f), new System.Numerics.Vector2(float.MaxValue, float.MaxValue));
		ImGui.Begin("Console##panel", ImGuiWindowFlags.NoCollapse); // Amedo
		foreach (string item2 in _browser.GetLog())
		{
			ImGui.TextUnformatted(item2);
		}
		if (ImGui.GetScrollY() >= ImGui.GetScrollMaxY())
		{
			ImGui.SetScrollHereY(1f);
		}
		ImGui.End();
	}
}
