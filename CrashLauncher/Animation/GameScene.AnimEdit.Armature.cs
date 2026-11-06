using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using PS2AnyOGI = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI;

namespace CrashLauncher;

// Amedo 2026-10-04
public sealed partial class GameScene
{
	private readonly Dictionary<int, Matrix4x4> _armatureWorlds = new();
	private Vector2[] _armatureScreen = Array.Empty<Vector2>();
	private bool[] _armatureVisible = Array.Empty<bool>();
	private int[] _armatureChildren = Array.Empty<int>();
	private float[] _armatureLength = Array.Empty<float>();
	private const float ArmatureWidth = 0.1f, ArmatureJointRadius = 0.06f;

	private Dictionary<int, Matrix4x4>? PreviewJointWorlds(out PS2AnyOGI? ogi)
	{
		ogi = null;
		if (PreviewAnim is not { } anim || PreviewBody is not { } body) return null;
		ogi = _animGroup?.Ogi ?? SkeletonOf(anim);
		if (ogi is null) return null;
		var local = anim.JointWorlds ?? MeshDecoder.ComputeJointWorlds(ogi);
		var world = body.Transform.World;
		_armatureWorlds.Clear();
		foreach (var (k, m) in local) _armatureWorlds[k] = m * world;
		return _armatureWorlds;
	}

	private void DrawArmatureOverlay()
	{
		CheckPending();
		if (_camera is null) return;
		var worlds = PreviewJointWorlds(out var ogi);
		if (worlds is null || ogi is null) return;
		if (_showArmature) DrawArmature(worlds, ogi);
		DrawEventFlashes(worlds, ogi);
		DrawBoneGizmo(worlds, ogi);
	}

	private void DrawArmature(Dictionary<int, Matrix4x4> worlds, PS2AnyOGI ogi)
	{
		if (_camera is null) return;
		int sw = ViewW, sh = ViewH; // Amedo
		int n = ogi.Joints.Count;
		if (_armatureScreen.Length < n)
		{
			_armatureScreen = new Vector2[n];
			_armatureVisible = new bool[n];
			_armatureChildren = new int[n];
			_armatureLength = new float[n];
		}
		Array.Clear(_armatureChildren, 0, n);
		Array.Clear(_armatureLength, 0, n);
		for (int i = 0; i < n; i++)
			_armatureVisible[i] = worlds.TryGetValue(i, out var m) && WorldToScreen(m.Translation, sw, sh, out _armatureScreen[i]);
		for (int i = 1; i < n; i++)
		{
			int p = ogi.Joints[i].ParentIndex;
			if (p < 0 || p >= n || p == i || !worlds.TryGetValue(p, out var pm) || !worlds.TryGetValue(i, out var cm)) continue;
			_armatureChildren[p]++;
			float len = Vector3.Distance(pm.Translation, cm.Translation);
			_armatureLength[p] = MathF.Max(_armatureLength[p], len);
			if (_armatureLength[i] <= 0f) _armatureLength[i] = -len;
		}
		var dl = ImGui.GetBackgroundDrawList();
		uint boneCol = ImGui.GetColorU32(new Vector4(0.75f, 0.78f, 0.85f, 0.9f));
		uint selCol = ImGui.GetColorU32(new Vector4(1f, 0.6f, 0.15f, 1f));
		var mouse = ImGui.GetMousePos();
		bool canPick = !ImGui.GetIO().WantCaptureMouse && _gizmoDrag is null && _boneGizmoHover < 0;
		var camPos = _camera.Transform.Position;
		int hover = -1;
		float hoverDist = float.MaxValue;
		Span<Vector3> ring = stackalloc Vector3[4];
		Span<Vector2> ringScreen = stackalloc Vector2[4];

		for (int i = 1; i < n; i++)
		{
			int p = ogi.Joints[i].ParentIndex;
			if (p < 0 || p >= n || p == i || !worlds.TryGetValue(p, out var pm) || !worlds.TryGetValue(i, out var cm)) continue;
			Vector3 head = pm.Translation, tail = cm.Translation, d = tail - head;
			float len = d.Length();
			if (len < 1e-5f) continue;
			var dir = d / len;
			var roll = Vector3.TransformNormal(Vector3.UnitX, pm);
			var alt = Vector3.TransformNormal(Vector3.UnitY, pm);
			var alt2 = Vector3.TransformNormal(Vector3.UnitZ, pm);
			if (MathF.Abs(Vector3.Dot(Vector3.Normalize(alt), dir)) < MathF.Abs(Vector3.Dot(Vector3.Normalize(roll), dir))) roll = alt;
			if (MathF.Abs(Vector3.Dot(Vector3.Normalize(alt2), dir)) < MathF.Abs(Vector3.Dot(Vector3.Normalize(roll), dir))) roll = alt2;
			var u = roll - dir * Vector3.Dot(roll, dir);
			if (!(u.LengthSquared() > 1e-10f)) u = Vector3.Cross(dir, MathF.Abs(dir.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX);
			u = Vector3.Normalize(u);
			var v = Vector3.Cross(dir, u);
			float r = len * ArmatureWidth;
			var mid = head + d * ArmatureWidth;
			ring[0] = mid + u * r;
			ring[1] = mid + v * r;
			ring[2] = mid - u * r;
			ring[3] = mid - v * r;
			if (!WorldToScreen(head, sw, sh, out var sHead) || !WorldToScreen(tail, sw, sh, out var sTail)) continue;
			bool ok = true;
			for (int k = 0; k < 4 && ok; k++) ok = WorldToScreen(ring[k], sw, sh, out ringScreen[k]);
			if (!ok) continue;
			bool sel = p == _animJoint;
			var baseCol = sel ? new Vector4(1f, 0.6f, 0.15f, 1f) : new Vector4(0.55f, 0.6f, 0.7f, 1f);
			var toViewer = _camera.Orthographic ? -_camera.Forward : Vector3.Normalize(camPos - mid);
			for (int k = 0; k < 4; k++)
			{
				int k2 = (k + 1) & 3;
				for (int side = 0; side < 2; side++)
				{
					var apex = side == 0 ? head : tail;
					var sApex = side == 0 ? sHead : sTail;
					var nrm = Vector3.Cross(ring[k] - apex, ring[k2] - apex);
					float facing = nrm.LengthSquared() > 1e-12f ? MathF.Abs(Vector3.Dot(Vector3.Normalize(nrm), toViewer)) : 0f;
					var fill = baseCol with { W = 0.12f + 0.28f * facing };
					dl.AddTriangleFilled(sApex, ringScreen[k], ringScreen[k2], ImGui.GetColorU32(fill));
				}
			}
			uint edge = sel ? selCol : boneCol;
			float thick = sel ? 1.8f : 1.1f;
			for (int k = 0; k < 4; k++)
			{
				dl.AddLine(sHead, ringScreen[k], edge, thick);
				dl.AddLine(sTail, ringScreen[k], edge, thick);
				dl.AddLine(ringScreen[k], ringScreen[(k + 1) & 3], edge, thick);
			}
			if (canPick)
			{
				float px = r / MathF.Max(_camera.WorldPerPixel(Vector3.Distance(camPos, mid), sh), 1e-9f);
				float dist = SegmentDistance(mouse, sHead, sTail);
				if (dist < MathF.Max(6f, px) && dist < hoverDist) { hoverDist = dist; hover = p; }
			}
		}
		for (int i = 0; i < n; i++)
		{
			if (!_armatureVisible[i] || !worlds.TryGetValue(i, out var m)) continue;
			bool sel = i == _animJoint;
			float len = MathF.Abs(_armatureLength[i]);
			float wpp = _camera.WorldPerPixel(Vector3.Distance(camPos, m.Translation), sh);
			float radius = MathF.Max(2.5f, len * ArmatureJointRadius / MathF.Max(wpp, 1e-9f));
			dl.AddCircleFilled(_armatureScreen[i], radius, sel ? selCol : boneCol, 16);
			float dist = Vector2.Distance(mouse, _armatureScreen[i]);
			if (canPick && _armatureChildren[i] == 0 && dist < MathF.Max(8f, radius) && dist < hoverDist)
			{
				hoverDist = dist;
				hover = i;
			}
		}
		if (hover >= 0)
		{
			ImGui.SetTooltip(JointLabel(ogi.Joints[hover]));
			if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) _animJoint = hover;
		}
	}

	private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
	{
		var d = b - a;
		float len2 = d.LengthSquared();
		float t = len2 > 1e-6f ? Math.Clamp(Vector2.Dot(p - a, d) / len2, 0f, 1f) : 0f;
		return Vector2.Distance(p, a + d * t);
	}
}
