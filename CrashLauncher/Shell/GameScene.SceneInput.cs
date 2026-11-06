using CrashEngine.Core;
using ImGuiNET;
using Silk.NET.Input;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
	private void UpdateEscapeAndGlueKeys()
	{
		if (!TextTyping && Input.KeyDown(Key.Escape)) // Amedo 2026-10-01
		{
			if (_choosingDuplicateDirection)
			{
				_choosingDuplicateDirection = false;
			}
			else if (_glueMode)
			{
				_glueMode = false;
				_glueSource = null;
				_glueAxisLock = -1;
			}
			else
			{
				LeaveScene(); // Amedo 2026-09-28
			}
		}
		if (_glueMode && !TextTyping) // Amedo 2026-10-01
		{
			if (Input.KeyDown(Key.X))
			{
				_glueAxisLock = 0;
			}
			else if (Input.KeyDown(Key.Y))
			{
				_glueAxisLock = 1;
			}
			else if (Input.KeyDown(Key.Z))
			{
				_glueAxisLock = 2;
			}
		}
		else if ((Input.KeyHeld(Key.ControlLeft) || Input.KeyHeld(Key.ControlRight)) && Input.KeyDown(Key.G) && _selected != null && !_gizmoDragging)
		{
			_glueMode = true;
			_glueSource = _selected;
			_glueAxisLock = -1;
		}
	}

	private void UpdateSceneMouse()
	{
		bool flag = Input.Mouse(MouseButton.Right);
		bool flag2 = Input.Mouse(MouseButton.Left);
		bool flag3 = flag2 && !_mouseWasDown;
		bool flag4 = !flag2 && _mouseWasDown;
		_mouseWasDown = flag2;
		int width = ViewW; // Amedo
		int height = ViewH;
		System.Numerics.Vector2 mousePosition = Input.MousePosition;
		bool flag5 = !flag && !ImGui.GetIO().WantCaptureMouse && !PlayFullView && MouseInView(mousePosition); // Amedo
		if (_gizmoRenderer != null)
		{
			_gizmoRenderer.Visible = _selected != null && !flag && !_choosingDuplicateDirection && _gizmoMode == GizmoMode.Move && !PlayFullView; // Amedo 2026-09-28
			if (_selected != null)
			{
				_gizmoRenderer.GizmoPos = GizmoPivot();
				_gizmoRenderer.GizmoScale = GizmoScale();
			}
		}
		if (_dupGizmoRenderer != null)
		{
			_dupGizmoRenderer.Visible = _choosingDuplicateDirection && !flag;
			if (_choosingDuplicateDirection)
			{
				_dupGizmoRenderer.GizmoPos = _dupGizmoPivot;
				_dupGizmoRenderer.GizmoScale = GizmoScale();
			}
		}
		WorldLightingSettings worldLightingSettings = _selected?.Get<WorldLightingSettings>();
		if (worldLightingSettings != null)
		{
			System.Numerics.Vector3 translation = _selected.Transform.World.Translation;
			float gizmoScale = GizmoScale() * 1.6f;
			for (int i = 0; i < worldLightingSettings.ArrowRenderers.Count && i < worldLightingSettings.Directional.Count; i++)
			{
				GizmoRenderer gizmoRenderer = worldLightingSettings.ArrowRenderers[i];
				gizmoRenderer.Visible = !flag && !PlayFullView; // Amedo 2026-09-28
				gizmoRenderer.GizmoPos = translation;
				gizmoRenderer.GizmoScale = gizmoScale;
				gizmoRenderer.GizmoRot = RotationBetween(System.Numerics.Vector3.UnitX, worldLightingSettings.Directional[i].Direction);
			}
			_lastLightingSelected = worldLightingSettings;
		}
		else if (_lastLightingSelected != null)
		{
			foreach (GizmoRenderer arrowRenderer in _lastLightingSelected.ArrowRenderers)
			{
				arrowRenderer.Visible = false;
			}
			_lastLightingSelected = null;
		}
		_gizmoHover = ((!(_selected != null && flag5) || _choosingDuplicateDirection) ? (-1) : ((_gizmoMode == GizmoMode.Move) ? GetGizmoHoverAxis(mousePosition, width, height) : GetRotateHoverAxis(mousePosition, width, height)));
		_dupGizmoHover = ((_choosingDuplicateDirection && flag5) ? GetDuplicateHoverDir(mousePosition, width, height) : (-1));
		if (flag3 && flag5)
		{
			if (_choosingDuplicateDirection)
			{
				if (_dupGizmoHover >= 0)
				{
					_lastDuplicateDirection = SixDirs[_dupGizmoHover];
					DuplicateSelected(_lastDuplicateDirection);
				}
				_choosingDuplicateDirection = false;
			}
			else if (_glueMode)
			{
				Entity entity = ((_camera != null) ? RaycastScene(mousePosition, width, height) : null);
				if (entity != null && entity != _glueSource && _glueSource != null)
				{
					ExecuteGlue(_glueSource, entity);
					_glueMode = false;
					_glueSource = null;
					_glueAxisLock = -1;
				}
			}
			else
			{
				int num = ((_selected == null) ? (-1) : ((_gizmoMode == GizmoMode.Move) ? GetGizmoHoverAxis(mousePosition, width, height) : GetRotateHoverAxis(mousePosition, width, height)));
				if (num >= 0)
				{
					_gizmoDragging = true;
					_gizmoAxis = num;
					_gizmoPrevMouse = mousePosition;
					_gizmoDragBefore.Clear();
					foreach (Entity item2 in _selectedSet)
					{
						_gizmoDragBefore[item2] = new TransformSnapshot(item2.Transform);
					}
				}
				else
				{
					_gizmoDragging = false;
					_gizmoAxis = -1;
					Entity hit = HitTestCameraIcons(mousePosition, width, height)
						?? ((_camera != null) ? RaycastScene(mousePosition, width, height) : null);
					bool additive = Input.KeyHeld(Key.ShiftLeft) || Input.KeyHeld(Key.ShiftRight);
					SelectClicked(hit, additive);
					if (_selected != null)
					{
						_revealSelectionInTree = true;
					}
				}
			}
		}
		if (_gizmoDragging && flag2 && _selected != null)
		{
			if (_gizmoMode == GizmoMode.Move)
			{
				UpdateGizmoDrag(mousePosition, width, height);
			}
			else
			{
				UpdateRotateDrag(mousePosition, width, height);
			}
		}
		if (flag4)
		{
			if (_gizmoDragging)
			{
				List<IEditAction> list = new List<IEditAction>();
				foreach (KeyValuePair<Entity, TransformSnapshot> item3 in _gizmoDragBefore)
				{
					item3.Deconstruct(out var key, out var value);
					Entity entity2 = key;
					TransformSnapshot other = value;
					TransformSnapshot after = new TransformSnapshot(entity2.Transform);
					if (!after.Equals(in other))
					{
						list.Add(new TransformEditAction
						{
							Entity = entity2,
							Before = other,
							After = after
						});
					}
				}
				if (list.Count == 1)
				{
					_undoStack.Push(list[0]);
				}
				else if (list.Count > 1)
				{
					_undoStack.Push(new CompositeEditAction
					{
						Actions = list
					});
				}
			}
			_gizmoDragging = false;
			_gizmoAxis = -1;
		}
	}

	private void UpdateShortcutKeys()
	{
		if (!TextTyping && (Input.KeyHeld(Key.ControlLeft) || Input.KeyHeld(Key.ControlRight))) // Amedo 2026-10-01
		{
			if (Input.KeyDown(Key.Z) && (Input.KeyHeld(Key.ShiftLeft) || Input.KeyHeld(Key.ShiftRight)))
			{
				SelectAffected(_undoStack.Redo());
			}
			else if (Input.KeyDown(Key.Z))
			{
				CommitInspectorEdit(force: true); // Amedo
				SelectAffected(_undoStack.Undo());
			}
			else if (Input.KeyDown(Key.Y))
			{
				SelectAffected(_undoStack.Redo());
			}
			else if (!_choosingDuplicateDirection && ((Input.KeyDown(Key.D) && Input.KeyHeld(Key.A)) || (Input.KeyDown(Key.A) && Input.KeyHeld(Key.D))))
			{
				BeginDuplicatePick();
			}
			else if (!_choosingDuplicateDirection && Input.KeyDown(Key.D) && !Input.KeyHeld(Key.A))
			{
				DuplicateSelected(_lastDuplicateDirection);
			}
		}
		if (!TextTyping && Input.KeyDown(Key.Delete)) // Amedo 2026-10-01
		{
			DeleteSelected();
		}
	}
}
