using CrashEngine.Core;
using ImGuiNET;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
	private void DrawTopBar(int width)
	{
		ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(ImGui.GetStyle().FramePadding.X, MathF.Max(0f, (32f - ImGui.GetFontSize()) * 0.5f))); // Amedo
		bool topBar = ImGui.BeginMainMenuBar();
		ImGui.PopStyleVar();
		if (topBar)
		{
		DrawMainMenus(); // Amedo
		float value = ((EngineTime.Delta > 0f) ? (1f / EngineTime.Delta) : 0f);
		int drawCallsThisFrame = CrashEngine.Importer.MeshRenderer.DrawCallsThisFrame;
		Engine.Instance.SetTitleDetail($"{_rm2}  |  {_enemyCount} enemies  |  {_scriptCount} scripts" +
		                               (_musicLabel is not null ? $"  |  Music: {_musicLabel}" : "")); // Amedo
		if (_glueMode)
		{
			ImGui.SameLine();
			int glueAxisLock = _glueAxisLock;
			if (1 == 0)
			{
			}
			string text = glueAxisLock switch
			{
				0 => "X", 
				1 => "Y", 
				2 => "Z", 
				_ => "auto", 
			};
			if (1 == 0)
			{
			}
			string text2 = text;
			ImGui.TextColored(new System.Numerics.Vector4(1f, 0.85f, 0.2f, 1f), "  |  GLUE MODE (axis: " + text2 + ") — click target, X/Y/Z=lock axis, Esc=cancel");
		}
		ImGui.SameLine((float)width - 430f);
		bool animUndo = _workspace == Workspace.Animation; // Amedo 2026-10-04
		ImGui.BeginDisabled(animUndo ? !_animUndo.CanUndo : !_undoStack.CanUndo); // Amedo 2026-10-04
		if (ImGui.SmallButton("Undo"))
		{
			if (animUndo) AnimUndo(); // Amedo 2026-10-04
			else SelectAffected(_undoStack.Undo());
		}
		ImGui.EndDisabled();
		ImGui.SameLine();
		ImGui.BeginDisabled(animUndo ? !_animUndo.CanRedo : !_undoStack.CanRedo); // Amedo 2026-10-04
		if (ImGui.SmallButton("Redo"))
		{
			if (animUndo) AnimRedo(); // Amedo 2026-10-04
			else SelectAffected(_undoStack.Redo());
		}
		ImGui.EndDisabled();
		ImGui.SameLine((float)width - 320f);
		string camSpeedText = (_camera != null) ? $"Cam speed: {_camera.Speed:F1}  |  " : "";
		if (drawCallsThisFrame == 0)
		{
			ImGui.TextColored(new System.Numerics.Vector4(1f, 0.3f, 0.3f, 1f), $"{camSpeedText}draws: {drawCallsThisFrame}  FPS {value:F0}  ESC=back");
		}
		else
		{
			ImGui.TextDisabled($"{camSpeedText}draws: {drawCallsThisFrame}  FPS {value:F0}  ESC=back");
		}
		ImGui.EndMainMenuBar(); // Amedo
		}
	}
}
