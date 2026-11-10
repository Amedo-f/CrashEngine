using CrashEngine.Core;
using ImGuiNET;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
	private bool _showInspector = true;

	private void DrawInspectorPanel(int width, float y)
	{
		if (!_showInspector) { CommitInspectorEdit(); return; }
		ImGui.SetNextWindowPos(new System.Numerics.Vector2((float)width - 280f, 32f), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(new System.Numerics.Vector2(280f, y), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSizeConstraints(new System.Numerics.Vector2(180f, 120f), new System.Numerics.Vector2(float.MaxValue, float.MaxValue));
		if (!_windowChrome.Begin("Inspector##panel", ref _showInspector)) { CommitInspectorEdit(); return; }
		if (_workspace == Workspace.Scene) InspectorUndoFrame(_selected); // Amedo
		if (_workspace == Workspace.Animation) DrawAnimationInspector(); // Amedo 2026-10-04
		else if (_selected != null)
		{
			ImGui.PushID(_selected.GetHashCode());
			DrawInspector(_selected);
			ImGui.PopID();
		}
		else
		{
			ImGui.TextDisabled("No object selected - pick something from the Hierarchy.");
		}
		ImGui.End();
	}

}
