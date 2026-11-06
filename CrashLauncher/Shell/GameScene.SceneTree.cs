using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using Silk.NET.Input;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void DrawEntityNode(Entity e)
    {
        if (e.Name is "__Gizmo" or "Meter" or "Player" or "__AiPathLines") return; // Amedo

        bool isSel    = _selectedSet.Contains(e);
        bool isSystem = e.Name is "RenderPipeline" or "Camera" or "Grid";

        string hint = e switch
        {
            _ when e.Has<StealthEnemy>()                              => " [npc]",
            _ when e.Has<DirectCubeRenderer>()                        => " [cube]",
            _ when e.Get<CrashEngine.Importer.AnimatedObject>() is { Groups.Count: > 0 } => " [anim]", // Amedo 2026-09-28
            _ when e.Has<CrashEngine.Importer.DynamicSceneryAnimator>() => " [anim]", // Amedo 2026-09-26
            _ when e.Has<CrashEngine.Importer.MeshRenderer>()        => " [mesh]",
            _ when e.Has<CrashEngine.Renderer.MeshRenderer>()        => " [mesh]",
            _ when e.Has<InstanceData>()                              => " [inst]",
            _ when e.Has<CrashEngine.Importer.CollisionMesh>()        => " [collision]",
            _ when e.Has<CameraComponent>()                           => " [cam]",
            _ when e.Has<GridRenderer>()                              => " [grid]",
            _ when e.Has<RenderPipeline>()                            => " [rp]",
            _ => e.Components.Count > 0 ? " [•]" : "",
        };

        bool hasChildren = e.Children.Count > 0;
        var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
        if (!hasChildren) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
        if (isSel)        flags |= ImGuiTreeNodeFlags.Selected;

        _treeVisibleOrder.Add(e);

        ImGui.PushID(RuntimeHelpers.GetHashCode(e));

        if (_revealSelectionInTree && hasChildren && _treeRevealAncestors.Contains(e))
            ImGui.SetNextItemOpen(true);

        if (isSystem) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1f));
        bool open = ImGui.TreeNodeEx(e.Name + hint, flags);
        if (isSystem) ImGui.PopStyleColor();

        if (_revealSelectionInTree && isSel)
            ImGui.SetScrollHereY(0.5f);

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            bool shift = Input.KeyHeld(Key.ShiftLeft) || Input.KeyHeld(Key.ShiftRight);
            bool ctrl  = Input.KeyHeld(Key.ControlLeft) || Input.KeyHeld(Key.ControlRight);
            if (shift && ctrl && _rangeSelectAnchor is not null)
            {
                SelectRange(_rangeSelectAnchor, e);
            }
            else
            {
                SelectClicked(e, shift);
                _rangeSelectAnchor = e;
            }
        }
        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            _camera?.FocusOn(e.Transform.World.Translation);

        if (e.Get<SceneryTile>() is { } ctxTile && ImGui.BeginPopupContextItem())
        {
            if (ImGui.IsWindowAppearing())
                _editMeshIdBuf = ctxTile.SourceId.ToString("X8");

            ImGui.TextDisabled(ctxTile.IsLod ? "LOD group -- edits its Meshes[0] entry" : "Mesh ID");
            ImGui.SetNextItemWidth(140f);
            ImGui.InputText("##editmeshid", ref _editMeshIdBuf, 8, ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.CharsUppercase);
            ImGui.SameLine();
            if (ImGui.Button("Apply##editmeshid"))
            {
                if (uint.TryParse(_editMeshIdBuf, System.Globalization.NumberStyles.HexNumber, null, out var newId))
                    ApplyManualMeshIdChange(e, ctxTile, newId);
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        if (open && hasChildren)
        {
            foreach (var child in e.Children)
                DrawEntityNode(child);
            ImGui.TreePop();
        }

        ImGui.PopID();
    }

	private void DrawHierarchyPanel(float y)
	{
		if (_workspace == Workspace.Scene) // Amedo 2026-10-04
		{
		ImGui.SetNextWindowPos(new System.Numerics.Vector2(0f, 32f), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSize(new System.Numerics.Vector2(280f, y), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSizeConstraints(new System.Numerics.Vector2(180f, 120f), new System.Numerics.Vector2(float.MaxValue, float.MaxValue));
		ImGui.Begin("Hierarchy##panel", ImGuiWindowFlags.NoCollapse); // Amedo
		if (_importError != null) ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), _importError); // Amedo
		DrawHierarchyHeader(); // Amedo
		ImGui.Separator();
		ImGui.BeginChild("##tree", new System.Numerics.Vector2(-1f, -1f), ImGuiChildFlags.None);
		if (!string.IsNullOrWhiteSpace(_sceneFilter))
		{
			foreach (Entity item in FlatEntities())
			{
				bool flag;
				switch (item.Name)
				{
				case "__Gizmo":
				case "Meter":
				case "Player":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				if (!flag && item.Name.Contains(_sceneFilter, StringComparison.OrdinalIgnoreCase))
				{
					ImGui.PushID(RuntimeHelpers.GetHashCode(item));
					if (ImGui.Selectable(item.Name, _selectedSet.Contains(item)))
					{
						SelectClicked(item, Input.KeyHeld(Key.ShiftLeft) || Input.KeyHeld(Key.ShiftRight));
					}
					ImGui.PopID();
				}
			}
		}
		else
		{
			_treeRevealAncestors.Clear();
			if (_revealSelectionInTree && _selected != null)
			{
				for (Entity parent = _selected.Parent; parent != null; parent = parent.Parent)
				{
					_treeRevealAncestors.Add(parent);
				}
			}
			_treeVisibleOrder.Clear();
			foreach (Entity root in base.Roots)
			{
				DrawEntityNode(root);
			}
			_revealSelectionInTree = false;
		}
		ImGui.EndChild();
		ImGui.End();
		}
	}
}
