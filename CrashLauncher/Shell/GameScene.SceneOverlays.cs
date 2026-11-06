using CrashEngine.Core;
using ImGuiNET;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
	private void DrawSceneOverlays()
	{
		// Amedo 2026-09-21
		bool blockingUi = ImGui.IsPopupOpen("", ImGuiPopupFlags.AnyPopup);

		if (_workspace == Workspace.Scene && !blockingUi && _selected != null && _camera != null && !_choosingDuplicateDirection && _gizmoMode == GizmoMode.Move)
		{
			System.Numerics.Vector3 vector = GizmoPivot();
			float num = 2.25f * GizmoScale();
			System.Numerics.Vector3[] array = new System.Numerics.Vector3[3]
			{
				vector + new System.Numerics.Vector3(num, 0f, 0f),
				vector + new System.Numerics.Vector3(0f, num, 0f),
				vector + new System.Numerics.Vector3(0f, 0f, num)
			};
			uint[] array2 = new uint[3] { 4282401023u, 4282449728u, 4294918208u };
			uint[] array3 = new uint[3] { 1614823679u, 1614872384u, 1627340864u };
			ImDrawListPtr foregroundDrawList = ImGui.GetForegroundDrawList();
			if (WorldToScreen(vector, ViewW, ViewH, out var screen))
			{
				for (int i = 0; i < 3; i++)
				{
					if (WorldToScreen(array[i], ViewW, ViewH, out var screen2))
					{
						foregroundDrawList.AddLine(screen, screen2, array3[i], 1.5f);
						float num2 = ((_gizmoHover == i || (_gizmoDragging && _gizmoAxis == i)) ? 11f : 7f);
						foregroundDrawList.AddCircleFilled(screen2, num2, array2[i]);
						foregroundDrawList.AddCircle(screen2, num2 + 1.5f, 4278190080u, 12, 1.5f);
					}
				}
			}
		}
		if (_workspace == Workspace.Scene && !blockingUi && _selected != null && _camera != null && !_choosingDuplicateDirection && _gizmoMode == GizmoMode.Rotate)
		{
			System.Numerics.Vector3 vector2 = GizmoPivot();
			float num3 = 2.25f * GizmoScale();
			System.Numerics.Vector3 position = _camera.Transform.Position;
			uint[] array4 = new uint[4] { 4282401023u, 4282449728u, 4294918208u, 4292927712u };
			uint[] array5 = new uint[4] { 1614823679u, 1614872384u, 1627340864u, 2162221280u };
			ImDrawListPtr foregroundDrawList2 = ImGui.GetForegroundDrawList();
			System.Numerics.Vector2 valueOrDefault = default(System.Numerics.Vector2);
			for (int j = 0; j < 4; j++)
			{
				bool flag2 = _gizmoHover == j || (_gizmoDragging && _gizmoAxis == j);
				uint col = (flag2 ? array4[j] : array5[j]);
				float thickness = (flag2 ? 3f : ((j == 3) ? 2f : 1.5f));
				System.Numerics.Vector3[] array6 = RingPoints(vector2, RotateRingAxis(j), (j == 3) ? (num3 * 1.15f) : num3);
				System.Numerics.Vector2? vector3 = null;
				bool flag3 = false;
				for (int k = 0; k <= array6.Length; k++)
				{
					System.Numerics.Vector3 vector4 = array6[k % array6.Length];
					bool flag4 = j == 3 || System.Numerics.Vector3.Dot(vector4 - vector2, position - vector2) > 0f;
					if (!WorldToScreen(vector4, ViewW, ViewH, out var screen3))
					{
						vector3 = null;
						continue;
					}
					int num4;
					if (vector3.HasValue)
					{
						valueOrDefault = vector3.GetValueOrDefault();
						num4 = 1;
					}
					else
					{
						num4 = 0;
					}
					if (((uint)num4 & (flag3 ? 1u : 0u) & (flag4 ? 1u : 0u)) != 0)
					{
						foregroundDrawList2.AddLine(valueOrDefault, screen3, col, thickness);
					}
					vector3 = screen3;
					flag3 = flag4;
				}
			}
		}
		if (_workspace == Workspace.Scene && !blockingUi && _choosingDuplicateDirection && _camera != null)
		{
			float num5 = 2.25f * GizmoScale();
			ImDrawListPtr foregroundDrawList3 = ImGui.GetForegroundDrawList();
			if (WorldToScreen(_dupGizmoPivot, ViewW, ViewH, out var screen4))
			{
				for (int l = 0; l < SixDirs.Length; l++)
				{
					if (WorldToScreen(_dupGizmoPivot + SixDirs[l] * num5, ViewW, ViewH, out var screen5))
					{
						foregroundDrawList3.AddLine(screen4, screen5, 1627366694u, 1.5f);
						float num6 = ((_dupGizmoHover == l) ? 11f : 7f);
						foregroundDrawList3.AddCircleFilled(screen5, num6, 4280722943u);
						foregroundDrawList3.AddCircle(screen5, num6 + 1.5f, 4278190080u, 12, 1.5f);
					}
				}
			}
		}
		if (_camera != null)
		{
			ImDrawListPtr backgroundDrawList = ImGui.GetBackgroundDrawList();
			foreach (CubeEntry cube in _cubes)
			{
				System.Numerics.Vector3 world = cube.Ent.Transform.World.Translation + new System.Numerics.Vector3(0f, 2.3f, 0f);
				if (WorldToScreen(world, ViewW, ViewH, out var screen6))
				{
					uint col2 = (_selectedSet.Contains(cube.Ent) ? 4286644223u : 4289374890u);
					float x = ImGui.CalcTextSize(cube.Label).X;
					backgroundDrawList.AddText(screen6 - new System.Numerics.Vector2(x * 0.5f, 0f), col2, cube.Label);
				}
			}
		}
		if (_workspace == Workspace.Scene) UpdateAiPathLines(); // Amedo
		if (_workspace == Workspace.Scene && !blockingUi && _camera != null)
		{
			Entity camerasRoot = _camerasRoot;
			if (camerasRoot != null && camerasRoot.Active)
			{
				ImDrawListPtr foregroundDrawList4 = ImGui.GetForegroundDrawList();
				foreach (Entity child in _camerasRoot.Children)
				{
					if (WorldToScreen(child.Transform.World.Translation, ViewW, ViewH, out var screen7) && _cameraIconTexture is { } camIcon) // Amedo 2026-10-04
					{
						foregroundDrawList4.AddImage((nint)camIcon.GlId, screen7 + CameraIconMin, screen7 + CameraIconMax); // Amedo 2026-10-04
					}
					else if (WorldToScreen(child.Transform.World.Translation, ViewW, ViewH, out screen7)) // Amedo 2026-10-04
					{
						System.Numerics.Vector2 p_min = screen7 - new System.Numerics.Vector2(11f, 7.5f);
						System.Numerics.Vector2 p_max = screen7 + new System.Numerics.Vector2(11f, 7.5f);
						uint col3 = 4292878544u;
						uint col4 = 4278190080u;
						System.Numerics.Vector2 p_min2 = screen7 + new System.Numerics.Vector2(-4.4f, -12.5f);
						System.Numerics.Vector2 p_max2 = screen7 + new System.Numerics.Vector2(4.4f, -7.5f);
						foregroundDrawList4.AddRectFilled(p_min2, p_max2, col3, 1f);
						foregroundDrawList4.AddRect(p_min2, p_max2, col4, 1f, ImDrawFlags.None, 1f);
						foregroundDrawList4.AddRectFilled(p_min, p_max, col3, 2f);
						foregroundDrawList4.AddRect(p_min, p_max, col4, 2f, ImDrawFlags.None, 1.5f);
						foregroundDrawList4.AddCircleFilled(screen7, 6f, 4280295456u, 16);
						foregroundDrawList4.AddCircle(screen7, 6f, 4291611852u, 16, 1.5f);
						foregroundDrawList4.AddCircleFilled(screen7, 2.4f, 4286611584u, 12);
					}
				}
			}
		}
	}
}
