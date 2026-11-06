using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-10-04
public sealed partial class GameScene
{
	private bool _viewGizmoDragging;

	private enum ViewShading { Wire, Unlit, Lit }
	private ViewShading _animShading = ViewShading.Lit;
	private (bool Wire, bool Lit)? _sceneShadingBackup;

	private static ViewShading CurrentShading() =>
		RenderPipeline.Instance?.Wireframe == true ? ViewShading.Wire : GameLightingEnvironment.Enabled ? ViewShading.Lit : ViewShading.Unlit;

	private static void ApplyShading(ViewShading s)
	{
		if (RenderPipeline.Instance is { } rp) rp.Wireframe = s == ViewShading.Wire;
		if (s != ViewShading.Wire) GameLightingEnvironment.Enabled = s == ViewShading.Lit;
	}

	private void EnterAnimationShading()
	{
		_sceneShadingBackup ??= (RenderPipeline.Instance?.Wireframe ?? false, GameLightingEnvironment.Enabled);
		ApplyShading(_animShading);
	}

	private void LeaveAnimationShading()
	{
		_animShading = CurrentShading();
		if (_sceneShadingBackup is { } b)
		{
			if (RenderPipeline.Instance is { } rp) rp.Wireframe = b.Wire;
			GameLightingEnvironment.Enabled = b.Lit;
		}
		_sceneShadingBackup = null;
	}

	private void DrawViewShading(float rightEdge)
	{
		var cur = CurrentShading();
		ImGui.SetNextWindowPos(new Vector2(rightEdge, ViewOrigin.Y + 8f), ImGuiCond.Always, new Vector2(1f, 0f)); // Amedo
		ImGui.SetNextWindowBgAlpha(0.55f);
		const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking |
		                               ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize;
		if (!ImGui.Begin("##viewshading", flags)) { ImGui.End(); return; }
		var active = ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive];
		foreach (var (mode, label, tip) in new[]
		{
			(ViewShading.Wire, "Wire", "Wireframe — only the mesh's edges."),
			(ViewShading.Unlit, "Unlit", "Unlit — textures and vertex colors, no lighting."),
			(ViewShading.Lit, "Lit", "Lit — the game's own lighting (ambient + the 3 strongest lights), like in game."),
		})
		{
			bool on = cur == mode;
			if (on) ImGui.PushStyleColor(ImGuiCol.Button, active);
			if (ImGui.SmallButton($"{label}##shade{mode}")) ApplyShading(mode);
			if (on) ImGui.PopStyleColor();
			ImGui.SameLine();
		}
		if (_workspace == Workspace.Animation)
		{
			ImGui.TextDisabled("|");
			ImGui.SameLine();
			if (_showArmature) ImGui.PushStyleColor(ImGuiCol.Button, active);
			if (ImGui.SmallButton("Bones##showarmature")) _showArmature = !_showArmature;
			if (_showArmature) ImGui.PopStyleColor();
			ImGui.SameLine();
		}
		ImGui.NewLine();
		ImGui.End();
	}

	private static readonly (Vector3 Axis, string Label, Vector4 Color)[] ViewAxes =
	{
		(Vector3.UnitX, "X", new Vector4(0.92f, 0.27f, 0.33f, 1f)),
		(Vector3.UnitY, "Y", new Vector4(0.55f, 0.82f, 0.22f, 1f)),
		(Vector3.UnitZ, "Z", new Vector4(0.27f, 0.52f, 0.93f, 1f)),
	};

	private Vector3 ViewPivot()
	{
		if (_camera?.OrbitTarget is { } orbit) return orbit;
		if (_workspace == Workspace.Scene && _selected is not null) return _selected.Transform.World.Translation;
		return (_camera?.Transform.Position ?? Vector3.Zero) + (_camera?.Forward ?? -Vector3.UnitZ) * 20f;
	}

	private void DrawViewGizmo()
	{
		if (_camera is null || Simulating || _camera.External) return;
		const float size = 96f, radius = 33f, dot = 9f;
		float right = ViewOrigin.X + ViewW; // Amedo
		float top = ViewOrigin.Y + 8f;
		DrawViewShading(right - size - 16f);
		ImGui.SetNextWindowPos(new Vector2(right - size - 10f, top));
		ImGui.SetNextWindowSize(new Vector2(size, size + 26f));
		ImGui.SetNextWindowBgAlpha(0f);
		const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking |
		                               ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoScrollbar;
		if (!ImGui.Begin("##viewgizmo", flags)) { ImGui.End(); return; }
		var dl = ImGui.GetWindowDrawList();
		var c = ImGui.GetWindowPos() + new Vector2(size * 0.5f, size * 0.5f);
		var mouse = ImGui.GetMousePos();
		bool inDisc = Vector2.Distance(mouse, c) <= radius + dot && ImGui.IsWindowHovered();

		var camRight = _camera.Right;
		var camUp = _camera.Up;
		var camFwd = _camera.Forward;
		var items = new List<(Vector2 P, float Depth, Vector3 Axis, string Label, Vector4 Color, bool Positive)>(6);
		foreach (var (axis, label, color) in ViewAxes)
			foreach (bool pos in new[] { true, false })
			{
				var a = pos ? axis : -axis;
				items.Add((c + new Vector2(Vector3.Dot(a, camRight), -Vector3.Dot(a, camUp)) * radius, Vector3.Dot(a, camFwd), a, label, color, pos));
			}
		items.Sort((x, y) => y.Depth.CompareTo(x.Depth));

		int hoverIndex = -1;
		for (int i = items.Count - 1; i >= 0; i--)
			if (ImGui.IsWindowHovered() && Vector2.Distance(mouse, items[i].P) <= dot) { hoverIndex = i; break; }

		if (inDisc || _viewGizmoDragging) dl.AddCircleFilled(c, radius + dot + 2f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.08f)), 32);
		for (int i = 0; i < items.Count; i++)
		{
			var it = items[i];
			if (it.Positive) dl.AddLine(c, it.P, ImGui.GetColorU32(it.Color), 2f);
		}
		for (int i = 0; i < items.Count; i++)
		{
			var it = items[i];
			var col = it.Color;
			bool hover = i == hoverIndex;
			if (it.Positive)
			{
				dl.AddCircleFilled(it.P, dot, ImGui.GetColorU32(hover ? Vector4.One : col), 20);
				var ts = ImGui.CalcTextSize(it.Label);
				dl.AddText(it.P - ts * 0.5f, ImGui.GetColorU32(new Vector4(0.08f, 0.08f, 0.08f, 1f)), it.Label);
			}
			else
			{
				dl.AddCircleFilled(it.P, dot - 1f, ImGui.GetColorU32(new Vector4(col.X, col.Y, col.Z, hover ? 0.75f : 0.35f)), 20);
				dl.AddCircle(it.P, dot - 1f, ImGui.GetColorU32(col), 20, 1.5f);
				if (hover) dl.AddText(it.P - ImGui.CalcTextSize("-" + it.Label) * 0.5f, ImGui.GetColorU32(Vector4.One), "-" + it.Label);
			}
		}

		if (hoverIndex >= 0)
		{
			var it = items[hoverIndex];
			ImGui.SetTooltip($"View from {(it.Positive ? "+" : "-")}{it.Label} (orthographic)");
			if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) _camera.SnapTo(-it.Axis, ViewPivot());
		}
		else if (inDisc && ImGui.IsMouseClicked(ImGuiMouseButton.Left)) _viewGizmoDragging = true;
		if (_viewGizmoDragging)
		{
			if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
			{
				var d = ImGui.GetIO().MouseDelta;
				if (d != Vector2.Zero) _camera.Rotate(-d.X * 0.4f, -d.Y * 0.4f);
			}
			else _viewGizmoDragging = false;
		}

		ImGui.SetCursorPos(new Vector2(4f, size + 2f));
		if (ImGui.SmallButton(_camera.Orthographic ? "Ortho##viewproj" : "Persp##viewproj"))
		{
			if (_camera.Orthographic) _camera.Orthographic = false;
			else _camera.SnapTo(_camera.Forward, ViewPivot());
		}
		ImGui.End();

		if (!ImGui.GetIO().WantTextInput && !TextTyping)
		{
			bool ctrl = ImGui.GetIO().KeyCtrl;
			if (ImGui.IsKeyPressed(ImGuiKey.Keypad1, false)) _camera.SnapTo(ctrl ? Vector3.UnitZ : -Vector3.UnitZ, ViewPivot());
			if (ImGui.IsKeyPressed(ImGuiKey.Keypad3, false)) _camera.SnapTo(ctrl ? Vector3.UnitX : -Vector3.UnitX, ViewPivot());
			if (ImGui.IsKeyPressed(ImGuiKey.Keypad7, false)) _camera.SnapTo(ctrl ? Vector3.UnitY : -Vector3.UnitY, ViewPivot());
			if (ImGui.IsKeyPressed(ImGuiKey.Keypad5, false))
			{
				if (_camera.Orthographic) _camera.Orthographic = false;
				else _camera.SnapTo(_camera.Forward, ViewPivot());
			}
		}
	}
}
