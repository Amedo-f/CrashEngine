using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using PS2AnyAnimation = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyAnimation;
using PS2AnyOGI = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI;
using TwinAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation;
using TwinJointSettings = Twinsanity.TwinsanityInterchange.Common.Animation.JointSettings;
using TwinTransformation = Twinsanity.TwinsanityInterchange.Common.Animation.Transformation;
using TwinAnimatedTransformation = Twinsanity.TwinsanityInterchange.Common.Animation.AnimatedTransformation;
using TransformType = Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType;
using static CrashLauncher.AnimChannelOps;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2BehaviourCommand = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourCommand;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using CrashEngine.Runtime.AgentLab;
using CrashEngine.Runtime.Physics;

namespace CrashLauncher;

// Amedo 2026-10-04
public sealed partial class GameScene
{
	private readonly Dictionary<PS2AnyAnimation, PS2AnyAnimation> _animClones = new(ReferenceEqualityComparer.Instance);
	private readonly HashSet<PS2AnyAnimation> _animCloneSet = new(ReferenceEqualityComparer.Instance);
	private readonly HashSet<PS2AnyAnimation> _animEdited = new(ReferenceEqualityComparer.Instance);
	private readonly Dictionary<(TwinAnimation Anim, int Joint, int Ch), List<int>> _userKeys = new();
	private readonly Dictionary<(TwinAnimation Anim, int Joint, int Ch), ChannelKeys> _keyCache = new();
	private bool _showArmature = true;
	private bool _timelineAllFrames;

	private sealed class ChannelKeys
	{
		public required int[] Values;
		public required List<int> Keys;
		public required bool[] IsKey;
	}

	private PS2AnyAnimation? EditableAnim(AnimatedObject.SlotGroup g)
	{
		if (_animCloneSet.Contains(g.Anim)) return g.Anim;
		if (!_animClones.TryGetValue(g.Anim, out var clone))
		{
			var main = CloneTwinAnimation(g.Anim.MainAnimation);
			if (!SameChannels(g.Anim.MainAnimation, main))
			{
				_browser.Log($"Animation 0x{g.AnimId:X4}: the editable copy didn't match the original — editing is off for it.");
				return null;
			}
			clone = new PS2AnyAnimation
			{
				HasAnimationData = g.Anim.HasAnimationData,
				HasFacialAnimationData = g.Anim.HasFacialAnimationData,
				TotalFrames = g.Anim.TotalFrames,
				DefaultFPS = g.Anim.DefaultFPS,
				MainAnimation = main,
				FacialAnimation = g.Anim.FacialAnimation,
			};
			_animClones[g.Anim] = clone;
			_animCloneSet.Add(clone);
			_cloneOriginal[clone] = g.Anim;
			_animPristine.TryAdd(g.Anim, CloneTwinAnimation(g.Anim.MainAnimation));
		}
		SwapToClone(g, clone);
		return clone;
	}

	private void UseCloneIfAny(AnimatedObject.SlotGroup g)
	{
		if (!_animCloneSet.Contains(g.Anim) && _animClones.TryGetValue(g.Anim, out var clone)) g.Anim = clone;
	}

	private void SwapToClone(AnimatedObject.SlotGroup g, PS2AnyAnimation clone)
	{
		if (ReferenceEquals(g.Anim, clone)) return;
		g.Anim = clone;
		if (PreviewAnim is { } anim && _animGroup == g)
		{
			float f = anim.Frame;
			bool loop = anim.Loop;
			anim.Stop();
			anim.Loop = loop;
			anim.Play(g);
			anim.Frame = f;
			anim.Playing = false;
		}
		ChannelsChanged();
	}

	private void ChannelsChanged()
	{
		_channelCache = null;
		_keyCache.Clear();
	}

	private ChannelKeys Channel(TwinAnimation a, int joint, int ch)
	{
		if (_keyCache.TryGetValue((a, joint, ch), out var c)) return c;
		var v = RawChannel(a, joint, ch);
		var keys = _userKeys.TryGetValue((a, joint, ch), out var user) ? user
			: TypesOf(a.JointSettings[joint])[ch] == TransformType.Animated ? KeysOf(v)
			: v.Length > 1 ? new List<int> { 0, v.Length - 1 } : new List<int> { 0 };
		var mask = new bool[v.Length];
		foreach (var k in keys) if (k >= 0 && k < mask.Length) mask[k] = true;
		_keyCache[(a, joint, ch)] = c = new ChannelKeys { Values = v, Keys = keys, IsKey = mask };
		return c;
	}

	private bool SetKey(AnimatedObject.SlotGroup g, int joint, int ch, int frame, int raw)
	{
		if (EditableAnim(g) is not { } clone) return false;
		var a = clone.MainAnimation;
		if (joint < 0 || joint >= a.JointSettings.Count || a.AnimatedTransformations.Count == 0) return false;
		frame = Math.Clamp(frame, 0, a.AnimatedTransformations.Count - 1);
		bool wasStatic = TypesOf(a.JointSettings[joint])[ch] != TransformType.Animated;
		var keys = new List<int>(Channel(a, joint, ch).Keys);
		var v = RawChannel(a, joint, ch);
		if (!keys.Contains(frame)) { keys.Add(frame); keys.Sort(); }
		if (wasStatic) MakeAnimated(a, joint, ch);
		v[frame] = raw;
		int i = keys.IndexOf(frame);
		int lo = i > 0 ? keys[i - 1] : frame, hi = i < keys.Count - 1 ? keys[i + 1] : frame;
		for (int f = lo + 1; f < frame; f++) v[f] = Lerp(v[lo], v[frame], f - lo, frame - lo);
		for (int f = frame + 1; f < hi; f++) v[f] = Lerp(v[frame], v[hi], f - frame, hi - frame);
		WriteChannel(a, joint, ch, v, Math.Min(lo + 1, frame), Math.Max(hi - 1, frame));
		_userKeys[(a, joint, ch)] = keys;
		_animEdited.Add(clone);
		ChannelsChanged();
		return true;
	}

	private bool DeleteKey(AnimatedObject.SlotGroup g, int joint, int ch, int frame)
	{
		if (EditableAnim(g) is not { } clone) return false;
		var a = clone.MainAnimation;
		if (TypesOf(a.JointSettings[joint])[ch] != TransformType.Animated) return false;
		var keys = new List<int>(Channel(a, joint, ch).Keys);
		var v = RawChannel(a, joint, ch);
		int i = keys.IndexOf(frame);
		if (i <= 0 || i >= keys.Count - 1) return false;
		int lo = keys[i - 1], hi = keys[i + 1];
		keys.RemoveAt(i);
		for (int f = lo + 1; f < hi; f++) v[f] = Lerp(v[lo], v[hi], f - lo, hi - lo);
		WriteChannel(a, joint, ch, v, lo + 1, hi - 1);
		_userKeys[(a, joint, ch)] = keys;
		_animEdited.Add(clone);
		ChannelsChanged();
		return true;
	}

	private static readonly string[] TransformRows = { "Location X", "Y", "Z", "Rotation X", "Y", "Z", "Scale X", "Y", "Z" };

	private void DrawBoneTransformPanel(AnimatedObject anim, AnimatedObject.SlotGroup g, int joint)
	{
		var a = g.Anim.MainAnimation;
		ImGui.SeparatorText("Transform (this frame)");
		if (joint < 0 || joint >= a.JointSettings.Count)
		{
			ImGui.TextDisabled("This bone isn't in this animation — it stays in its bind pose.");
			return;
		}
		int frames = Math.Max(1, a.AnimatedTransformations.Count);
		int frame = Math.Clamp((int)MathF.Round(anim.Frame), 0, frames - 1);
		var types = TypesOf(a.JointSettings[joint]);
		for (int ch = 0; ch < 9; ch++)
		{
			if (ch == 3 || ch == 6) ImGui.Spacing();
			var c = Channel(a, joint, ch);
			var v = c.Values;
			bool animated = types[ch] == TransformType.Animated;
			bool isKey = animated && frame < c.IsKey.Length && c.IsKey[frame];
			ImGui.PushID(ch);
			ImGui.TextUnformatted(TransformRows[ch]);
			ImGui.SameLine(90f);
			float value = RawToValue(v[Math.Min(frame, v.Length - 1)], ch);
			ImGui.SetNextItemWidth(-36f);
			string fmt = ch is >= 3 and <= 5 ? "%.2f°" : ch < 3 ? "%.4f" : "%.3f";
			if (ImGui.DragFloat("##v", ref value, ch is >= 3 and <= 5 ? 0.5f : 0.005f, 0f, 0f, fmt))
			{
				anim.Playing = false;
				anim.Frame = frame;
				BeginPanelEdit(g, joint, frame);
				SetKey(g, joint, ch, frame, ValueToRaw(value, ch));
			}
			if (ImGui.IsItemDeactivated()) EndPanelEdit();
			ImGui.SameLine();
			var keyCol = ImGui.GetColorU32(isKey || animated ? new Vector4(0.95f, 0.75f, 0.25f, 1f) : new Vector4(0.6f, 0.6f, 0.6f, 0.7f));
			var p0 = ImGui.GetCursorScreenPos();
			bool clicked = ImGui.InvisibleButton("##key", new Vector2(20f, ImGui.GetFrameHeight()));
			var kc = p0 + new Vector2(10f, ImGui.GetFrameHeight() * 0.5f);
			var kdl = ImGui.GetWindowDrawList();
			if (animated)
			{
				Vector2 top = kc + new Vector2(0f, -6f), right = kc + new Vector2(6f, 0f), bottom = kc + new Vector2(0f, 6f), left = kc + new Vector2(-6f, 0f);
				if (isKey) kdl.AddQuadFilled(top, right, bottom, left, keyCol);
				else kdl.AddQuad(top, right, bottom, left, keyCol, 1.5f);
			}
			else kdl.AddLine(kc + new Vector2(-5f, 0f), kc + new Vector2(5f, 0f), keyCol, 2f);
			if (clicked && EditableAnim(g) is { } editClone)
			{
				anim.Playing = false;
				anim.Frame = frame;
				CommitPending();
				var before = CaptureJoint(editClone, joint, frame);
				bool done = isKey ? DeleteKey(g, joint, ch, frame) : SetKey(g, joint, ch, frame, v[Math.Min(frame, v.Length - 1)]);
				if (done) PushAnimEdit(before);
			}
			if (ImGui.IsItemHovered())
				MaybeTooltip(isKey ? "A key at this frame — click to delete it (the frames around it get filled in between its neighbours)."
					: animated ? "Between keys — click to set a key here with this value."
					: "Fixed for the whole animation — click to make it animated with a key here.");
			ImGui.PopID();
		}
		ImGui.Spacing();
		ImGui.TextDisabled("Mode: XYZ Euler (the game's order: X, then Y, then Z)");
		if (_animEdited.Contains(g.Anim))
			ImGui.TextColored(new Vector4(0.95f, 0.75f, 0.25f, 1f), "Edited — Save (in the Timeline) puts it in the level.");
	}

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

	private int _boneGizmoHover = -1;
	private GizmoDrag? _gizmoDrag;
	private GizmoRenderer? _boneGizmoRenderer;
	private Quaternion _gizmoAdd = Quaternion.Identity;

	private const float GizmoRingPickPx = 10f, GizmoTipPickPx = 18f, GizmoShaftPickPx = 6f;
	private const int RingSegments = 48;
	private static readonly float GizmoScreenScale = 0.055f / (2f * MathF.Tan(MathF.PI / 6f));
	private readonly Vector3[] _gizmoDirs = new Vector3[4];
	private readonly float[] _gizmoScales = new float[3];
	private readonly Vector2[] _gizmoTips = new Vector2[3];
	private readonly Vector2[] _gizmoTangents = new Vector2[4];
	private readonly Vector2[] _ringPts = new Vector2[RingSegments + 1];
	private readonly bool[] _ringOk = new bool[RingSegments + 1];

	private static readonly uint[] GizmoSolid = { 4282401023u, 4282449728u, 4294918208u, 4292927712u };
	private static readonly uint[] GizmoFaint = { 1614823679u, 1614872384u, 1627340864u, 2162221280u };

	private static readonly (GizmoMode Mode, string Id, string Label, string Tip)[] GizmoModeButtons =
	{
		(GizmoMode.Move, "##movegizmo", "Position",
			"Position (W) — arrows on the picked bone, along its parent bone's axes: each arrow is one Location channel.\nDrag the ball at an arrow's tip to set a key at this frame. Ctrl snaps 0.05, Esc / right-click cancels."),
		(GizmoMode.Rotate, "##rotgizmo", "Rotation",
			"Rotation (E) — rings on the picked bone, one per Rotation channel (Gimbal: the game turns X, then Y, then Z).\nThe white ring turns it toward the screen (sets X, Y and Z together). Ctrl snaps 5°, Esc / right-click cancels."),
	};

	private sealed class GizmoDrag
	{
		public required PS2AnyAnimation Anim;
		public required int[] Channels;
		public required JointState Before;
		public required int[] StartRaw;
		public required int[] LastRaw;
		public int Joint, Axis, Frame;
		public Vector2 StartMouse, Center, Tangent;
		public float UnitsPerPixel, RingPx, Sign, LastAngle, Angle;
		public bool Changed;
		public Quaternion Add, Rot0;
		public Vector3 AxisParent, LastEuler;
	}

	private void DrawGizmoModeButtons()
	{
		var activeCol = ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive];
		const float icon = 26f; // Amedo
		ImGui.SetNextWindowPos(ViewOrigin + new Vector2(8f, 8f));
		ImGui.SetNextWindowBgAlpha(0.55f);
		const ImGuiWindowFlags flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking |
		                               ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.AlwaysAutoResize;
		ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(4f, 4f));
		bool open = ImGui.Begin("##gizmotools", flags);
		ImGui.PopStyleVar();
		if (!open) { ImGui.End(); return; }
		ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4f, 4f));
		for (int i = 0; i < GizmoModeButtons.Length; i++)
		{
			var (mode, id, label, tip) = GizmoModeButtons[i];
			var tex = mode == GizmoMode.Move ? _moveIconTexture : _rotateIconTexture;
			bool on = _gizmoMode == mode;
			if (on) ImGui.PushStyleColor(ImGuiCol.Button, activeCol);
			bool clicked = tex != null
				? ImGui.ImageButton(id, (nint)tex.GlId, new Vector2(icon, icon), Vector2.Zero, Vector2.One, Vector4.Zero, Vector4.One)
				: ImGui.SmallButton(label + id);
			if (on) ImGui.PopStyleColor();
			if (ImGui.IsItemHovered())
			{
				if (_workspace == Workspace.Animation) MaybeTooltip(tip);
				else ImGui.SetTooltip(label);
			}
			if (clicked) _gizmoMode = mode;
		}
		ImGui.PopStyleVar();
		ImGui.End();
	}

	private GizmoRenderer? BoneGizmoRenderer()
	{
		if (_animStage is null || _gizmoMesh is null) return null;
		if (_boneGizmoRenderer is null || _boneGizmoRenderer.Entity != _animStage)
			_boneGizmoRenderer = _animStage.Add(new GizmoRenderer { Mesh = _gizmoMesh });
		return _boneGizmoRenderer;
	}

	private Vector3 ToViewer(Vector3 p) =>
		_camera is null ? Vector3.UnitZ : _camera.Orthographic ? -_camera.Forward : Vector3.Normalize(_camera.Transform.Position - p);

	private bool GizmoAxes(AnimatedObject anim, TwinAnimation a, Dictionary<int, Matrix4x4> worlds, PS2AnyOGI ogi, int joint,
		out Vector3 origin, out Matrix4x4 parent, out int frame)
	{
		origin = default;
		parent = Matrix4x4.Identity;
		frame = 0;
		if (PreviewBody is not { } body || _camera is null || joint < 0 || joint >= a.JointSettings.Count || joint >= ogi.Joints.Count
		    || a.AnimatedTransformations.Count == 0 || !worlds.TryGetValue(joint, out var self)) return false;
		var j = ogi.Joints[joint];
		parent = joint > 0 && worlds.TryGetValue(j.ParentIndex, out var pw) ? pw : body.Transform.World;
		origin = self.Translation;
		frame = Math.Clamp((int)MathF.Round(anim.Frame), 0, a.AnimatedTransformations.Count - 1);
		_gizmoAdd = a.JointSettings[joint].UseAdditionalRotation
			? Quaternion.Normalize(new Quaternion(j.AdditionalAnimationRotation.X, j.AdditionalAnimationRotation.Y, j.AdditionalAnimationRotation.Z, j.AdditionalAnimationRotation.W))
			: Quaternion.Identity;
		Vector3 x = Vector3.UnitX, y = Vector3.UnitY, z = Vector3.UnitZ;
		if (_gizmoMode == GizmoMode.Rotate)
		{
			const float rawToRad = MathF.Tau / RotTurnRaw;
			float ry = Channel(a, joint, 4).Values[frame] * rawToRad, rz = Channel(a, joint, 5).Values[frame] * rawToRad;
			x = Vector3.Transform(Vector3.UnitX, _gizmoAdd * MeshDecoder.GameEulerToQuaternion(0f, ry, rz));
			y = Vector3.Transform(Vector3.UnitY, _gizmoAdd * MeshDecoder.GameEulerToQuaternion(0f, 0f, rz));
			z = Vector3.Transform(Vector3.UnitZ, _gizmoAdd);
		}
		Span<Vector3> local = stackalloc Vector3[] { x, y, z };
		for (int k = 0; k < 3; k++)
		{
			var w = Vector3.TransformNormal(local[k], parent);
			_gizmoScales[k] = w.Length();
			_gizmoDirs[k] = _gizmoScales[k] > 1e-6f ? w / _gizmoScales[k] : Vector3.UnitY;
		}
		_gizmoDirs[3] = _camera.Forward;
		return true;
	}

	private void DrawBoneGizmo(Dictionary<int, Matrix4x4> worlds, PS2AnyOGI ogi)
	{
		int lastHover = _boneGizmoHover;
		_boneGizmoHover = -1;
		var renderer = BoneGizmoRenderer();
		if (renderer is not null) renderer.Visible = false;
		if (PreviewAnim is not { } anim || _animGroup is not { } g || _animJoint < 0 || _camera is null)
		{
			_gizmoDrag = null;
			return;
		}
		var a = g.Anim.MainAnimation;
		if (!GizmoAxes(anim, a, worlds, ogi, _animJoint, out var o, out var parent, out int frame)) return;
		int sw = ViewW, sh = ViewH; // Amedo
		if (!WorldToScreen(o, sw, sh, out var c)) return;
		float wpp = _camera.WorldPerPixel(Vector3.Distance(_camera.Transform.Position, o), sh);
		float len = TipLen * wpp * sh * GizmoScreenScale;
		var dl = ImGui.GetForegroundDrawList();
		var mouse = ImGui.GetMousePos();
		var drag = _gizmoDrag;
		bool canHover = drag is null && !ImGui.GetIO().WantCaptureMouse;
		bool move = _gizmoMode == GizmoMode.Move;
		int hot = drag?.Axis ?? lastHover;
		float best = float.MaxValue;

		if (move)
		{
			if (renderer is not null)
			{
				Vector3 d0 = _gizmoDirs[0], d1 = _gizmoDirs[1], d2 = _gizmoDirs[2];
				renderer.Visible = true;
				renderer.GizmoPos = o;
				renderer.GizmoScale = len / TipLen;
				renderer.GizmoBasis = new Matrix4x4(d0.X, d0.Y, d0.Z, 0f, d1.X, d1.Y, d1.Z, 0f, d2.X, d2.Y, d2.Z, 0f, 0f, 0f, 0f, 1f);
			}
			for (int k = 0; k < 3; k++)
			{
				_gizmoTips[k] = c;
				if (!WorldToScreen(o + _gizmoDirs[k] * len, sw, sh, out var tip)) continue;
				_gizmoTips[k] = tip;
				dl.AddLine(c, tip, GizmoFaint[k], 1.5f);
				float r = k == hot ? 11f : 7f;
				dl.AddCircleFilled(tip, r, GizmoSolid[k]);
				dl.AddCircle(tip, r + 1.5f, 4278190080u, 12, 1.5f);
				if (!canHover) continue;
				float dTip = Vector2.Distance(mouse, tip), dShaft = SegmentDistance(mouse, c, tip);
				float score = dTip < GizmoTipPickPx ? dTip : dShaft < GizmoShaftPickPx ? GizmoTipPickPx + dShaft : float.MaxValue;
				if (score < best)
				{
					best = score;
					_boneGizmoHover = k;
				}
			}
		}
		else
		{
			var toViewer = ToViewer(o);
			for (int k = 0; k < 4; k++)
			{
				var axis = _gizmoDirs[k];
				float radius = k == 3 ? len * ViewRingScale : len;
				bool on = k == hot;
				uint col = on ? GizmoSolid[k] : GizmoFaint[k];
				float thick = on ? 3f : k == 3 ? 2f : 1.5f;
				var helper = MathF.Abs(axis.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
				var b1 = Vector3.Normalize(Vector3.Cross(axis, helper));
				var b2 = Vector3.Cross(axis, b1);
				for (int i = 0; i <= RingSegments; i++)
				{
					float t = i * MathF.Tau / RingSegments;
					var offset = (b1 * MathF.Cos(t) + b2 * MathF.Sin(t)) * radius;
					_ringOk[i] = WorldToScreen(o + offset, sw, sh, out _ringPts[i]) && (k == 3 || Vector3.Dot(offset, toViewer) > 0f);
				}
				float ringBest = float.MaxValue;
				for (int i = 0; i < RingSegments; i++)
				{
					if (!_ringOk[i] || !_ringOk[i + 1]) continue;
					dl.AddLine(_ringPts[i], _ringPts[i + 1], col, thick);
					float sd = SegmentDistance(mouse, _ringPts[i], _ringPts[i + 1]);
					if (sd < ringBest)
					{
						ringBest = sd;
						var seg = _ringPts[i + 1] - _ringPts[i];
						_gizmoTangents[k] = seg.LengthSquared() > 1e-6f ? Vector2.Normalize(seg) : Vector2.Zero;
					}
				}
				if (canHover && ringBest < GizmoRingPickPx && ringBest < best)
				{
					best = ringBest;
					_boneGizmoHover = k;
				}
			}
		}

		if (drag is not null)
		{
			if (!move) dl.AddLine(c, mouse, 2162221280u, 1f);
			UpdateGizmoDrag(anim, g, drag, mouse);
			return;
		}
		if (_boneGizmoHover >= 0 && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
			BeginGizmoDrag(anim, g, _boneGizmoHover, frame, o, c, parent, wpp, len, mouse);
	}

	private void BeginGizmoDrag(AnimatedObject anim, AnimatedObject.SlotGroup g, int axis, int frame, Vector3 o, Vector2 c,
		Matrix4x4 parent, float wpp, float len, Vector2 mouse)
	{
		if (_camera is null) return;
		if (EditableAnim(g) is not { } clone) return;
		var a = clone.MainAnimation;
		bool move = _gizmoMode == GizmoMode.Move;
		int[] chans = move ? new[] { axis } : axis == 3 ? new[] { 3, 4, 5 } : new[] { 3 + axis };
		var start = new int[chans.Length];
		for (int i = 0; i < chans.Length; i++) start[i] = Channel(a, _animJoint, chans[i]).Values[frame];
		var d = new GizmoDrag
		{
			Anim = clone,
			Channels = chans,
			Before = CaptureJoint(clone, _animJoint, frame),
			StartRaw = start,
			LastRaw = (int[])start.Clone(),
			Joint = _animJoint,
			Axis = axis,
			Frame = frame,
			StartMouse = mouse,
			Center = c,
			RingPx = MathF.Max(len / MathF.Max(wpp, 1e-9f), 1f),
		};
		float detSign = parent.GetDeterminant() < 0f ? -1f : 1f;
		if (move)
		{
			var screen = _gizmoTips[axis] - c;
			float px = MathF.Max(screen.Length(), 1f);
			d.Tangent = screen / px;
			d.UnitsPerPixel = len / px / MathF.Max(_gizmoScales[axis], 1e-6f);
		}
		else
		{
			float facing = Vector3.Dot(_gizmoDirs[axis], ToViewer(o));
			if (MathF.Abs(facing) >= 0.25f)
			{
				d.Sign = detSign * MathF.Sign(facing);
				var r = mouse - c;
				d.LastAngle = MathF.Atan2(-r.Y, r.X);
			}
			else
			{
				d.Sign = detSign;
				d.Tangent = _gizmoTangents[axis];
			}
			if (axis == 3)
			{
				Matrix4x4.Invert(parent, out var inv);
				const float rawToRad = MathF.Tau / RotTurnRaw;
				d.AxisParent = Vector3.Normalize(Vector3.TransformNormal(_gizmoDirs[3], inv));
				d.Add = _gizmoAdd;
				d.LastEuler = new Vector3(start[0], start[1], start[2]) * rawToRad;
				d.Rot0 = _gizmoAdd * MeshDecoder.GameEulerToQuaternion(d.LastEuler.X, d.LastEuler.Y, d.LastEuler.Z);
			}
		}
		anim.Playing = false;
		anim.Frame = frame;
		_gizmoDrag = d;
	}

	private void UpdateGizmoDrag(AnimatedObject anim, AnimatedObject.SlotGroup g, GizmoDrag d, Vector2 mouse)
	{
		if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
		{
			CancelGizmoDrag();
			return;
		}
		if (!ImGui.IsMouseDown(ImGuiMouseButton.Left) || !ReferenceEquals(g.Anim, d.Anim) || _animJoint != d.Joint)
		{
			_gizmoDrag = null;
			if (d.Changed) RecordEdit(d.Before);
			return;
		}
		bool snap = ImGui.GetIO().KeyCtrl;
		Span<int> want = stackalloc int[3];
		float deg = 0f;
		if (d.Channels[0] < 3)
		{
			float value = Vector2.Dot(mouse - d.StartMouse, d.Tangent) * d.UnitsPerPixel;
			if (snap) value = MathF.Round(value / 0.05f) * 0.05f;
			want[0] = d.StartRaw[0] + ValueToRaw(value, d.Channels[0]);
		}
		else
		{
			if (d.Tangent != Vector2.Zero) d.Angle = Vector2.Dot(mouse - d.StartMouse, d.Tangent) / d.RingPx;
			else
			{
				var r = mouse - d.Center;
				if (r.LengthSquared() > 4f)
				{
					float ang = MathF.Atan2(-r.Y, r.X), step = ang - d.LastAngle;
					if (step > MathF.PI) step -= MathF.Tau;
					else if (step < -MathF.PI) step += MathF.Tau;
					d.Angle += step;
					d.LastAngle = ang;
				}
			}
			deg = d.Angle * d.Sign * 180f / MathF.PI;
			if (snap) deg = MathF.Round(deg / 5f) * 5f;
			if (d.Axis < 3) want[0] = d.StartRaw[0] + ValueToRaw(deg, d.Channels[0]);
			else
			{
				var q = Quaternion.CreateFromAxisAngle(d.AxisParent, deg * MathF.PI / 180f) * d.Rot0;
				d.LastEuler = GameEulerFrom(Quaternion.Conjugate(d.Add) * q, d.LastEuler);
				want[0] = EulerRaw(d.LastEuler.X);
				want[1] = EulerRaw(d.LastEuler.Y);
				want[2] = EulerRaw(d.LastEuler.Z);
			}
		}
		for (int i = 0; i < d.Channels.Length; i++)
			if (want[i] != d.LastRaw[i] && SetKey(g, d.Joint, d.Channels[i], d.Frame, want[i]))
			{
				d.LastRaw[i] = want[i];
				d.Changed = true;
			}
		anim.Frame = d.Frame;
		if (d.Channels.Length == 3)
			ImGui.SetTooltip($"Rotation (view)  {deg:+0.00;-0.00;0}°  ->  X {RawToValue(d.LastRaw[0], 3):0.00}°  Y {RawToValue(d.LastRaw[1], 4):0.00}°  Z {RawToValue(d.LastRaw[2], 5):0.00}°  (frame {d.Frame})");
		else
		{
			float delta = RawToValue(d.LastRaw[0] - d.StartRaw[0], d.Channels[0]);
			ImGui.SetTooltip(d.Channels[0] < 3
				? $"{ChannelNames[d.Channels[0]]}  {delta:+0.0000;-0.0000;0}  (frame {d.Frame})"
				: $"{ChannelNames[d.Channels[0]]}  {delta:+0.00;-0.00;0}°  (frame {d.Frame})");
		}
	}

	private void CancelGizmoDrag()
	{
		if (_gizmoDrag is not { } d) return;
		_gizmoDrag = null;
		ApplyJoint(d.Before, false);
	}

	private readonly EditorUndoStack _animUndo = new();
	private JointState? _panelUndo;

	private sealed class JointState
	{
		public required PS2AnyAnimation Clone;
		public required int Joint, Frame;
		public required TransformType[] Types;
		public required ushort StaticIndex, AnimatedIndex;
		public required byte Counts;
		public required TwinTransformation[]?[] Slots;
		public required List<int>?[] Keys;
		public required bool Edited;
	}

	private sealed class AnimEditAction : IEditAction
	{
		public required JointState Before, After;
		public required Action<JointState, bool> Apply;
		public void Undo() => Apply(Before, true);
		public void Redo() => Apply(After, true);
		public IEnumerable<Entity> AffectedEntities => Array.Empty<Entity>();
	}

	private JointState CaptureJoint(PS2AnyAnimation clone, int joint, int frame)
	{
		var a = clone.MainAnimation;
		var js = a.JointSettings[joint];
		var types = TypesOf(js);
		var slots = new TwinTransformation[]?[9];
		var keys = new List<int>?[9];
		for (int ch = 0; ch < 9; ch++)
		{
			if (types[ch] == TransformType.Animated)
			{
				int slot = ChannelSlot(js, ch);
				slots[ch] = a.AnimatedTransformations.Select(f => f.Transforms[slot]).ToArray();
			}
			keys[ch] = _userKeys.TryGetValue((a, joint, ch), out var k) ? new List<int>(k) : null;
		}
		return new JointState
		{
			Clone = clone,
			Joint = joint,
			Frame = frame,
			Types = types,
			StaticIndex = js.TransformationIndex,
			AnimatedIndex = js.AnimationTransformationIndex,
			Counts = js.ChannelCounts,
			Slots = slots,
			Keys = keys,
			Edited = _animEdited.Contains(clone),
		};
	}

	private void ApplyJoint(JointState s, bool focus)
	{
		var a = s.Clone.MainAnimation;
		var js = a.JointSettings[s.Joint];
		for (int ch = 0; ch < 9; ch++) SetType(js, ch, s.Types[ch]);
		js.TransformationIndex = s.StaticIndex;
		js.AnimationTransformationIndex = s.AnimatedIndex;
		js.ChannelCounts = s.Counts;
		for (int ch = 0; ch < 9; ch++)
		{
			if (s.Slots[ch] is { } saved)
			{
				int slot = ChannelSlot(js, ch);
				for (int f = 0; f < saved.Length && f < a.AnimatedTransformations.Count; f++)
					a.AnimatedTransformations[f].Transforms[slot] = saved[f];
			}
			if (s.Keys[ch] is { } keys) _userKeys[(a, s.Joint, ch)] = new List<int>(keys);
			else _userKeys.Remove((a, s.Joint, ch));
		}
		if (s.Edited) _animEdited.Add(s.Clone);
		else _animEdited.Remove(s.Clone);
		ChannelsChanged();
		if (focus) FocusAnimEdit(s);
	}

	private void FocusAnimEdit(JointState s)
	{
		if (PreviewAnim is not { } anim) return;
		var g = anim.Groups.FirstOrDefault(x => ReferenceEquals(x.Anim, s.Clone)
			|| (_animClones.TryGetValue(x.Anim, out var c) && ReferenceEquals(c, s.Clone)));
		if (g is null)
		{
			_browser.Log("Animation undo: that edit was on another character's animation - it was undone there.");
			return;
		}
		if (g != _animGroup)
		{
			_animGroup = g;
			UseCloneIfAny(g);
			anim.Loop = true;
			anim.Play(g);
		}
		_animJoint = s.Joint;
		anim.Playing = false;
		anim.Frame = s.Frame;
	}

	private void PushAnimEdit(JointState before)
	{
		var after = CaptureJoint(before.Clone, before.Joint, before.Frame);
		_animUndo.Push(new AnimEditAction { Before = before, After = after, Apply = ApplyJoint });
	}

	private void BeginPanelEdit(AnimatedObject.SlotGroup g, int joint, int frame)
	{
		if (_panelUndo is null && EditableAnim(g) is { } clone) _panelUndo = CaptureJoint(clone, joint, frame);
	}

	private void EndPanelEdit()
	{
		if (_panelUndo is not { } before) return;
		_panelUndo = null;
		RecordEdit(before);
	}

	private void AnimUndo()
	{
		if (_panelUndo is not null && !ImGui.IsAnyItemActive()) EndPanelEdit();
		if (_gizmoDrag is not null || _panelUndo is not null) return;
		if (_pendingPose.Count > 0)
		{
			DropPending();
			return;
		}
		_animUndo.Undo();
	}

	private void AnimRedo()
	{
		if (_panelUndo is not null && !ImGui.IsAnyItemActive()) EndPanelEdit();
		if (_gizmoDrag is not null || _panelUndo is not null) return;
		_animUndo.Redo();
	}
	private readonly Dictionary<PS2AnyAnimation, TwinAnimation> _animPristine = new(ReferenceEqualityComparer.Instance);
	private readonly Dictionary<PS2AnyAnimation, PS2AnyAnimation> _cloneOriginal = new(ReferenceEqualityComparer.Instance);
	private readonly HashSet<PS2AnyAnimation> _animApplied = new(ReferenceEqualityComparer.Instance);
	private readonly Dictionary<int, JointState> _pendingPose = new();
	private PS2AnyAnimation? _pendingClone;
	private int _pendingFrame;
	private bool _autoKey;

	private sealed class AnimSwapAction : IEditAction
	{
		public required PS2AnyAnimation Clone;
		public required TwinAnimation Before, After;
		public required bool EditedBefore, EditedAfter;
		public required Action<PS2AnyAnimation, TwinAnimation, bool> Apply;
		public void Undo() => Apply(Clone, Before, EditedBefore);
		public void Redo() => Apply(Clone, After, EditedAfter);
		public IEnumerable<Entity> AffectedEntities => Array.Empty<Entity>();
	}

	private void RecordEdit(JointState before)
	{
		if (_autoKey) PushAnimEdit(before);
		else AddPending(before);
	}

	private void AddPending(JointState before)
	{
		if (!ReferenceEquals(_pendingClone, before.Clone) || _pendingFrame != before.Frame) DropPending();
		_pendingClone = before.Clone;
		_pendingFrame = before.Frame;
		_pendingPose.TryAdd(before.Joint, before);
	}

	private void ClearPending()
	{
		_pendingPose.Clear();
		_pendingClone = null;
	}

	private void DropPending()
	{
		foreach (var b in _pendingPose.Values) ApplyJoint(b, false);
		ClearPending();
	}

	private void CheckPending()
	{
		if (_pendingPose.Count == 0 || _gizmoDrag is not null || _panelUndo is not null) return;
		int frame = PreviewAnim is { } anim ? (int)MathF.Round(anim.Frame) : -1;
		if (_animGroup is null || !ReferenceEquals(_animGroup.Anim, _pendingClone) || frame != _pendingFrame) DropPending();
	}

	private void PushActions(List<IEditAction> actions)
	{
		if (actions.Count == 0) return;
		_animUndo.Push(actions.Count == 1 ? actions[0] : new CompositeEditAction { Actions = actions, HandlesSelectionItself = true });
	}

	private void CommitPending()
	{
		if (_pendingPose.Count == 0 || _pendingClone is not { } clone) return;
		var actions = new List<IEditAction>();
		foreach (var b in _pendingPose.Values)
			actions.Add(new AnimEditAction { Before = b, After = CaptureJoint(clone, b.Joint, b.Frame), Apply = ApplyJoint });
		ClearPending();
		PushActions(actions);
	}

	private void MarkKey(TwinAnimation a, int joint, int ch, int frame)
	{
		var keys = new List<int>(Channel(a, joint, ch).Keys);
		if (keys.Contains(frame)) return;
		keys.Add(frame);
		keys.Sort();
		_userKeys[(a, joint, ch)] = keys;
		ChannelsChanged();
	}

	private void InsertKeyframe()
	{
		if (PreviewAnim is not { } anim || _animGroup is not { } g || _animJoint < 0 || _gizmoDrag is not null || _panelUndo is not null) return;
		CheckPending();
		if (EditableAnim(g) is not { } clone) return;
		var a = clone.MainAnimation;
		if (_animJoint >= a.JointSettings.Count || a.AnimatedTransformations.Count == 0) return;
		int frame = Math.Clamp((int)MathF.Round(anim.Frame), 0, a.AnimatedTransformations.Count - 1);
		anim.Playing = false;
		anim.Frame = frame;
		var befores = new List<JointState>(_pendingPose.Values);
		if (!_pendingPose.ContainsKey(_animJoint)) befores.Add(CaptureJoint(clone, _animJoint, frame));
		var types = TypesOf(a.JointSettings[_animJoint]);
		for (int ch = 0; ch < 9; ch++)
			if (types[ch] == TransformType.Animated) MarkKey(a, _animJoint, ch, frame);
		ClearPending();
		var actions = new List<IEditAction>();
		foreach (var b in befores)
			actions.Add(new AnimEditAction { Before = b, After = CaptureJoint(clone, b.Joint, frame), Apply = ApplyJoint });
		PushActions(actions);
		_browser.Log($"Animation 0x{g.AnimId:X4}: keyed Bone {_animJoint} at frame {frame}" + (befores.Count > 1 ? $" (+ {befores.Count - 1} more posed bone(s))." : "."));
	}

	private void SetCloneMain(PS2AnyAnimation clone, TwinAnimation main, bool edited)
	{
		clone.MainAnimation = main;
		if (edited) _animEdited.Add(clone);
		else _animEdited.Remove(clone);
		ChannelsChanged();
	}

	private bool CanResetAnim(AnimatedObject.SlotGroup g) =>
		_animCloneSet.Contains(g.Anim) && _cloneOriginal.TryGetValue(g.Anim, out var original)
		&& (_animEdited.Contains(g.Anim) || _animApplied.Contains(original) || _pendingPose.Count > 0);

	private void ResetToOriginal(AnimatedObject.SlotGroup g)
	{
		DropPending();
		if (!_animCloneSet.Contains(g.Anim) || !_cloneOriginal.TryGetValue(g.Anim, out var original) || !_animPristine.TryGetValue(original, out var pristine)) return;
		var clone = g.Anim;
		var fresh = CloneTwinAnimation(pristine);
		bool editedAfter = _animApplied.Contains(original);
		_animUndo.Push(new AnimSwapAction
		{
			Clone = clone, Before = clone.MainAnimation, After = fresh,
			EditedBefore = _animEdited.Contains(clone), EditedAfter = editedAfter, Apply = SetCloneMain,
		});
		SetCloneMain(clone, fresh, editedAfter);
		_browser.Log($"Animation 0x{g.AnimId:X4}: back to the original from the disc" + (editedAfter ? " — press Save to put the original back in the level too." : "."));
	}

	private static bool HoldsAnimation(PS2AnyTwinsanityRM2? rm2, PS2AnyAnimation anim)
	{
		var sec = rm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_ANIMATIONS_SECTION);
		if (sec is null) return false;
		for (int i = 0; i < sec.GetItemsAmount(); i++)
			if (ReferenceEquals(sec.GetItem(i), anim)) return true;
		return false;
	}

	private bool AnimDirty(AnimatedObject.SlotGroup g) => _animCloneSet.Contains(g.Anim) && _animEdited.Contains(g.Anim);

	private bool CanSaveAnim(AnimatedObject.SlotGroup g) => (AnimDirty(g) || _scriptEdited.Count > 0) && _pendingPose.Count == 0 && !Simulating;

	private void SaveAnimToLevel(AnimatedObject.SlotGroup g)
	{
		if (!CanSaveAnim(g)) return;
		string name = $"Animation 0x{g.AnimId:X4}";
		var root = _animTarget;
		while (root?.Parent is not null) root = root.Parent;
		var source = root?.Get<ChunkSource>();
		if (source is null || Roots.FirstOrDefault(r => r.Has<ChunkSource>()) != root)
		{
			_browser.Log($"{name} wasn't saved: its character isn't in the level that Save writes.");
			return;
		}
		if (AnimDirty(g) && !ApplyAnimToOriginal(g, source, name)) return;
		int inDefaultScripts = 0;
		foreach (var graph in _scriptEdited)
			if (!AnimEventScan.HoldsBehaviour(source.Rm2, graph) && AnimEventScan.HoldsBehaviour(source.GlobalRm2, graph))
			{
				source.GlobalRm2Dirty = true;
				inDefaultScripts++;
			}
		if (_scriptEdited.Count > 0)
			_browser.Log($"Scripts with edited events: {string.Join(", ", _scriptEdited.Select(x => x.Name ?? $"0x{x.GetID():X4}"))} — written with {(inDefaultScripts > 0 ? "Startup\\Default.rm2 and " : "")}{source.Rm2Path}.");
		_scriptEdited.Clear();
		SaveChunk();
	}

	private bool ApplyAnimToOriginal(AnimatedObject.SlotGroup g, ChunkSource source, string name)
	{
		if (!_cloneOriginal.TryGetValue(g.Anim, out var original)) return false;
		var clone = g.Anim;
		bool inLevel = HoldsAnimation(source.Rm2, original), inDefault = !inLevel && HoldsAnimation(source.GlobalRm2, original);
		if (!inLevel && !inDefault)
		{
			_browser.Log($"{name} wasn't saved: it isn't in this level's file or in Startup\\Default.rm2.");
			return false;
		}
		var packed = Compact(clone.MainAnimation);
		if (FitsGameLimits(packed) is { } why)
		{
			_browser.Log($"{name} wasn't saved: {why}.");
			return false;
		}
		var ms = new MemoryStream();
		using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) packed.Write(w);
		var back = new TwinAnimation();
		back.Read(new BinaryReader(new MemoryStream(ms.ToArray())), (int)ms.Length);
		if (!SameChannels(clone.MainAnimation, back))
		{
			_browser.Log($"{name} wasn't saved: the written data didn't read back the same (nothing was changed).");
			return false;
		}
		original.MainAnimation = packed;
		if (inDefault) source.GlobalRm2Dirty = true;
		_animEdited.Remove(clone);
		if (_animPristine.TryGetValue(original, out var pristine) && SameChannels(packed, pristine)) _animApplied.Remove(original);
		else _animApplied.Add(original);
		_browser.Log($"{name}: put in {(inDefault ? "Startup\\Default.rm2 (shared — every level that uses it)" : source.Rm2Path)}, {packed.StaticTransformations.Count} fixed + {(packed.AnimatedTransformations.Count > 0 ? packed.AnimatedTransformations[0].Count : 0)} animated values per frame, checked by reading it back.");
		return true;
	}

	private void DrawAnimRecordBar(AnimatedObject anim, AnimatedObject.SlotGroup g)
	{
		var red = new Vector4(0.85f, 0.2f, 0.2f, 1f);
		ImGui.SameLine();
		ImGui.TextDisabled("|");
		ImGui.SameLine();
		bool auto = _autoKey;
		if (auto) ImGui.PushStyleColor(ImGuiCol.Button, red);
		if (ImGui.SmallButton("Auto Key##autokey"))
		{
			_autoKey = !_autoKey;
			if (_autoKey) CommitPending();
		}
		if (auto) ImGui.PopStyleColor();
		if (ImGui.IsItemHovered()) MaybeTooltip("Auto Key (like Blender's record button).\nOff: moving a bone only poses it — press I to key the pose at this frame; changing the frame drops an unkeyed pose.\nOn: every move sets a key right away.");
		ImGui.SameLine();
		ImGui.BeginDisabled(_animJoint < 0);
		if (ImGui.SmallButton("Insert Key (I)##insertkey")) InsertKeyframe();
		ImGui.EndDisabled();
		if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) MaybeTooltip("I — key the picked bone at this frame (all its animated channels), plus any bones you posed here.");
		ImGui.SameLine();
		ImGui.BeginDisabled(!CanResetAnim(g));
		if (ImGui.SmallButton("Reset##animreset")) ResetToOriginal(g);
		ImGui.EndDisabled();
		if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) MaybeTooltip("Back to this animation as it is on the disc (Ctrl+Z brings your edits back).");
		ImGui.SameLine();
		ImGui.BeginDisabled(!CanSaveAnim(g));
		if (ImGui.SmallButton("Save##animsave")) SaveAnimToLevel(g);
		ImGui.EndDisabled();
		if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
			MaybeTooltip(_pendingPose.Count > 0 ? "Key the pose first (I), or change the frame to drop it."
				: "Puts the edited animation in the level's own animation (the character in the Scene plays it too) and saves the chunk.\nThe game's layout is rebuilt without leftovers and checked by reading it back before anything is written.\nCrash and other shared characters live in Startup\\Default.rm2 — saving changes them in every level.");
		ImGui.SameLine();
		if (_pendingPose.Count > 0 && ReferenceEquals(_pendingClone, g.Anim))
			ImGui.TextColored(new Vector4(1f, 0.6f, 0.15f, 1f), "pose not keyed — press I");
		else if (_animEdited.Contains(g.Anim) || _scriptEdited.Count > 0)
			ImGui.TextColored(new Vector4(0.95f, 0.75f, 0.25f, 1f), "edited — not saved");
		else if (_cloneOriginal.TryGetValue(g.Anim, out var original) && _animApplied.Contains(original))
			ImGui.TextColored(new Vector4(0.45f, 0.85f, 0.4f, 1f), "saved to the level");
		else ImGui.NewLine();
	}
	private Entity? _eventsTarget;
	private List<AnimEvent> _targetEvents = new();
	private readonly List<AnimEvent> _slotEvents = new();
	private float _eventPrevFrame = -1f;
	private readonly List<(Vector3 Pos, double Until, bool Step)> _eventFlashes = new();
	private (PS2BehaviourCommand Cmd, int Slot)? _selEvent;
	private readonly HashSet<PS2BehaviourGraph> _scriptEdited = new(ReferenceEqualityComparer.Instance);
	private (PS2BehaviourCommand Cmd, uint[] Before)? _eventEditUndo;
	private int _eventPreviewSurface = 10;
	private ParticleEffectsRenderer? _animFx;
	private RuntimeParticles? _animParticles;
	private IReadOnlyList<CollisionWorld.Surface>? _animSurfaces;
	private Entity? _animSurfacesTarget;

	private static readonly uint EventStepCol = 0xFF66D96Bu, EventOtherCol = 0xFFE6C24Du, EventSelCol = 0xFF2E9BFFu;
	private const ushort SandStepSystem = 0xF4;

	private sealed class ScriptArgsAction : IEditAction
	{
		public required PS2BehaviourCommand Command;
		public required uint[] Before, After;
		public required Action<PS2BehaviourCommand, uint[]> Apply;
		public void Undo() => Apply(Command, Before);
		public void Redo() => Apply(Command, After);
		public IEnumerable<Entity> AffectedEntities => Array.Empty<Entity>();
	}

	private List<AnimEvent> TargetEvents()
	{
		if (!ReferenceEquals(_eventsTarget, _animTarget))
		{
			_eventsTarget = _animTarget;
			_targetEvents = LoadTargetEvents(_animTarget);
		}
		return _targetEvents;
	}

	private ChunkSource? TargetChunkSource()
	{
		var root = _animTarget;
		while (root?.Parent is not null) root = root.Parent;
		return root?.Get<ChunkSource>();
	}

	private static List<AnimEvent> LoadTargetEvents(Entity? target)
	{
		if (target?.Get<InstanceData>() is not { } data) return new();
		var root = target;
		while (root.Parent is not null) root = root.Parent;
		if (root.Get<ChunkSource>() is not { } src) return new();
		if (AnimEventScan.FindObject(data.ObjectId, src.Rm2, src.GlobalRm2) is not { } obj) return new();
		return AnimEventScan.Scan(AnimEventScan.GraphsOf(obj, src.Rm2, src.GlobalRm2)).OrderBy(e => e.Slot).ThenByDescending(e => e.SecondsBeforeEnd).ToList();
	}

	private void RescanEvents() => _eventsTarget = null;

	private List<AnimEvent> SlotEvents(AnimatedObject.SlotGroup g)
	{
		_slotEvents.Clear();
		foreach (var e in TargetEvents())
			if (e.Slot == g.SlotIndex) _slotEvents.Add(e);
		return _slotEvents;
	}

	private AnimEvent? SelectedEvent(AnimatedObject.SlotGroup g)
	{
		if (_selEvent is not { } sel || sel.Slot != g.SlotIndex) return null;
		foreach (var e in SlotEvents(g))
			if (ReferenceEquals(e.Command, sel.Cmd)) return e;
		return null;
	}

	private bool IsSelected(AnimEvent e) => _selEvent is { } s && ReferenceEquals(s.Cmd, e.Command) && s.Slot == e.Slot;

	private static float EventFrame(AnimEvent e, AnimatedObject.SlotGroup g)
	{
		int frames = g.Anim.MainAnimation.AnimatedTransformations.Count;
		float fps = g.Anim.DefaultFPS > 0 ? g.Anim.DefaultFPS : 30f;
		return Math.Clamp(frames - e.SecondsBeforeEnd * fps, 0f, MathF.Max(0f, frames - 1));
	}

	private static string SurfaceName(int id)
	{
		string n = Enum.IsDefined(typeof(SurfaceType), (ushort)id) ? ((SurfaceType)(ushort)id).ToString() : $"surface {id}";
		if (n.StartsWith("SURF_")) n = n[5..];
		if (n.StartsWith("NORMAL_")) n = n[7..];
		n = n.Replace('_', ' ').ToLowerInvariant();
		return n.Length > 0 ? char.ToUpperInvariant(n[0]) + n[1..] : n;
	}

	private string ExitPointName(int exitPoint, PS2AnyOGI? ogi)
	{
		if (ogi is null || exitPoint >= ogi.ExitPoints.Count) return "the character's root";
		int joint = (int)ogi.ExitPoints[exitPoint].ParentJointIndex;
		return joint >= 0 && joint < ogi.Joints.Count ? $"exit point {exitPoint} on {JointLabel(ogi.Joints[joint])}" : $"exit point {exitPoint}";
	}

	private IReadOnlyList<CollisionWorld.Surface> AnimSurfaces()
	{
		if (_animSurfaces is null || !ReferenceEquals(_animSurfacesTarget, _animTarget))
		{
			_animSurfacesTarget = _animTarget;
			_animSurfaces = TargetChunkSource() is { } src ? CollisionWorld.LevelSurfaces(src.Rm2, src.GlobalRm2) : Array.Empty<CollisionWorld.Surface>();
		}
		return _animSurfaces;
	}

	private CollisionWorld.Surface? SurfaceById(int id)
	{
		foreach (var s in AnimSurfaces())
			if (s.Id == id) return s;
		return null;
	}

	private RuntimeParticles? AnimParticles()
	{
		if (_animPreview is null) return null;
		if (_animParticles is not null && _animFx?.Entity is { } holder && ReferenceEquals(holder.Parent, _animPreview)) return _animParticles;
		if (TargetChunkSource() is not { } src) return null;
		var e = new Entity("__AnimationParticles");
		_animFx = e.Add(new ParticleEffectsRenderer { DistanceGating = false });
		EnsureFxTexturePagesLoaded();
		_animFx.Pages = _fxTexturePages;
		_animFx.Decals = _fxDecals;
		_animPreview.AddChild(e);
		_animParticles = new RuntimeParticles(_animFx, src.GlobalRm2, src.Rm2);
		return _animParticles;
	}

	private string SystemName(ushort sys) =>
		sys == 0xFFFF ? "none" : AnimParticles()?.System(sys) is { } s ? $"0x{sys:X4} {ParticleNames.Clean(s.Name)}" : $"0x{sys:X4}";

	private Matrix4x4? ExitPointMatrix(int exitPoint, Dictionary<int, Matrix4x4> worlds, PS2AnyOGI ogi)
	{
		if (PreviewBody is not { } body) return null;
		if (exitPoint >= ogi.ExitPoints.Count) return body.Transform.World;
		var ep = ogi.ExitPoints[exitPoint];
		return worlds.TryGetValue((int)ep.ParentJointIndex, out var jw) ? MeshDecoder.TwinMatToSys(ep.Matrix) * jw : body.Transform.World;
	}

	private (ushort Particle, bool Footprint, bool Dust, ushort Sound, float Volume) EventEffect(AnimEvent e, int surfaceId)
	{
		if (e.Selector == AnimEventScan.NoSelector) return (e.System, false, false, 0xFFFF, -1f);
		if (SurfaceById(surfaceId) is not { } s) return (e.System, false, false, 0xFFFF, -1f);
		ushort particle = s.ParticleAt(e.Selector);
		bool print = s.Id is 10 or 15;
		float vol = -1f;
		ushort sound = 0xFFFF;
		if (e.Selector < 6) sound = s.SoundAt(e.Selector, out vol);
		return (particle, print, particle == 0xFFFF && s.Id == 10, sound, vol);
	}

	private void FireEventPreview(AnimEvent e, Dictionary<int, Matrix4x4> worlds, PS2AnyOGI ogi)
	{
		if (AnimParticles() is not { } fx || _animPreview is null || PreviewBody is not { } body) return;
		if (ExitPointMatrix(e.ExitPoint, worlds, ogi) is not { } world || !Matrix4x4.Invert(_animPreview.Transform.World, out var toGame)) return;
		var m = world * toGame;
		var (particle, print, dust, _, _) = EventEffect(e, _eventPreviewSurface);
		if (particle != 0xFFFF && fx.Spawn(particle, 1, true, m.Translation) is var h and >= 0) fx.SetEmitMatrix(h, m);
		if (dust) fx.Spawn(SandStepSystem, 1, true, m.Translation);
		if (print)
		{
			var pos = m.Translation;
			if (AnimEventScan.HasOffset(e.Command)) pos += AnimEventScan.Offset(e.Command);
			pos.Y = body.Transform.LocalMatrix is { } lm ? lm.M42 + 0.09f : body.Transform.Position.Y + 0.09f;
			var dir = new Vector3(m.M31, m.M32, m.M33);
			if (Vector3.Dot(Vector3.Normalize(dir), Vector3.UnitZ) < 0.7f) dir = Vector3.UnitZ;
			dir.Y = 0f;
			if (dir.LengthSquared() > 1e-8f) dir = Vector3.Normalize(dir);
			fx.AddDecal_1ADC18(pos, Vector3.UnitY, dir, 1, 0, 1, 1);
		}
	}

	private void DrawEventFlashes(Dictionary<int, Matrix4x4> worlds, PS2AnyOGI ogi)
	{
		double now = ImGui.GetTime();
		_animParticles?.Update();
		if (PreviewAnim is { } anim && _animGroup is { } g)
		{
			float f = anim.Frame;
			if (anim.Playing && _eventPrevFrame >= 0f && f != _eventPrevFrame)
				foreach (var e in SlotEvents(g))
				{
					float ef = EventFrame(e, g);
					bool crossed = f > _eventPrevFrame ? ef > _eventPrevFrame && ef <= f : ef > _eventPrevFrame || ef <= f;
					if (!crossed) continue;
					if (ExitPointMatrix(e.ExitPoint, worlds, ogi) is { } m) _eventFlashes.Add((m.Translation, now + 0.4, e.Selector != AnimEventScan.NoSelector));
					FireEventPreview(e, worlds, ogi);
				}
			_eventPrevFrame = f;
		}
		else _eventPrevFrame = -1f;
		if (_eventFlashes.Count == 0) return;
		var dl = ImGui.GetBackgroundDrawList();
		int sw = ViewW, sh = ViewH; // Amedo
		for (int i = _eventFlashes.Count - 1; i >= 0; i--)
		{
			var (pos, until, step) = _eventFlashes[i];
			float t = (float)(1.0 - (until - now) / 0.4);
			if (t >= 1f) { _eventFlashes.RemoveAt(i); continue; }
			if (!WorldToScreen(pos, sw, sh, out var s)) continue;
			uint col = (step ? EventStepCol : EventOtherCol) & 0x00FFFFFFu | ((uint)(255 * (1f - t)) << 24);
			dl.AddCircle(s, 5f + 18f * t, col, 24, 2.5f);
		}
	}

	private const float EventRowH = 22f;

	private bool DrawEventRow(ImDrawListPtr dl, AnimatedObject.SlotGroup g, Vector2 o, float x0, float frameW, float width, float y, Vector2 mouse, bool hovered)
	{
		var events = SlotEvents(g);
		dl.AddRectFilled(new Vector2(o.X, y), new Vector2(o.X + width, y + EventRowH), ImGui.GetColorU32(new Vector4(0.4f, 0.9f, 0.5f, 0.05f)));
		dl.AddText(new Vector2(o.X + 4f, y + 4f), ImGui.GetColorU32(ImGuiCol.Text), "Events");
		bool tip = false;
		bool inRow = hovered && mouse.Y >= y && mouse.Y < y + EventRowH;
		foreach (var e in events)
		{
			float x = x0 + EventFrame(e, g) * frameW + frameW * 0.5f;
			bool sel = IsSelected(e);
			uint col = sel ? EventSelCol : e.Selector != AnimEventScan.NoSelector ? EventStepCol : EventOtherCol;
			dl.AddLine(new Vector2(x, y + 2f), new Vector2(x, y + EventRowH - 2f), col, sel ? 2.5f : 1.5f);
			dl.AddTriangleFilled(new Vector2(x, y + 3f), new Vector2(x + 8f, y + 7f), new Vector2(x, y + 11f), col);
			if (e.ExitPoint != AnimEventScan.RootExitPoint) dl.AddText(new Vector2(x + 3f, y + 9f), col, e.ExitPoint.ToString());
			if (!inRow || tip || MathF.Abs(mouse.X - x) >= 7f) continue;
			tip = true;
			ImGui.SetTooltip(e.Selector != AnimEventScan.NoSelector ? $"Footstep · exit point {e.ExitPoint} · frame {EventFrame(e, g):0.0} — click to open it in the Inspector"
				: $"Timed effect · frame {EventFrame(e, g):0.0} — click to open it in the Inspector");
			if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) _selEvent = (e.Command, e.Slot);
		}
		if (events.Count == 0) dl.AddText(new Vector2(x0 + 4f, y + 4f), ImGui.GetColorU32(ImGuiCol.TextDisabled), "no events for this animation in the character's scripts");
		return tip;
	}

	private void BeginEventEdit(PS2BehaviourCommand cmd)
	{
		if (_eventEditUndo is null) _eventEditUndo = (cmd, cmd.Arguments.ToArray());
	}

	private void EndEventEdit(AnimEvent e)
	{
		if (_eventEditUndo is not { } u) return;
		_eventEditUndo = null;
		var after = u.Cmd.Arguments.ToArray();
		if (after.AsSpan().SequenceEqual(u.Before)) return;
		_animUndo.Push(new ScriptArgsAction { Command = u.Cmd, Before = u.Before, After = after, Apply = ApplyScriptArgs });
		_scriptEdited.Add(e.Graph);
		RescanEvents();
	}

	private void ApplyScriptArgs(PS2BehaviourCommand cmd, uint[] values)
	{
		for (int i = 0; i < values.Length && i < cmd.Arguments.Count; i++) cmd.Arguments[i] = values[i];
		foreach (var e in TargetEvents())
			if (ReferenceEquals(e.Command, cmd)) { _scriptEdited.Add(e.Graph); break; }
		RescanEvents();
	}

	private void EditEvent(AnimEvent e, Action<PS2BehaviourCommand> change)
	{
		BeginEventEdit(e.Command);
		change(e.Command);
		EndEventEdit(e);
	}

	private void DrawAnimEventsInfo(AnimatedObject.SlotGroup g)
	{
		var events = SlotEvents(g);
		if (events.Count == 0) return;
		ImGui.SeparatorText("Events (from the scripts)");
		var ogi = g.Ogi;
		for (int i = 0; i < events.Count; i++)
		{
			var e = events[i];
			var col = ImGui.ColorConvertU32ToFloat4(e.Selector != AnimEventScan.NoSelector ? EventStepCol : EventOtherCol);
			ImGui.PushStyleColor(ImGuiCol.Text, col);
			string label = e.Selector != AnimEventScan.NoSelector ? $"frame {EventFrame(e, g):0.0}   footstep · {ExitPointName(e.ExitPoint, ogi)}##ev{i}"
				: $"frame {EventFrame(e, g):0.0}   timed effect · {ExitPointName(e.ExitPoint, ogi)}##ev{i}";
			if (ImGui.Selectable(label, IsSelected(e))) _selEvent = IsSelected(e) ? null : (e.Command, e.Slot);
			ImGui.PopStyleColor();
		}
		if (SelectedEvent(g) is { } sel) DrawEventEditor(sel, g, ogi);
	}

	private void DrawEventEditor(AnimEvent e, AnimatedObject.SlotGroup g, PS2AnyOGI ogi)
	{
		bool canEdit = AnimEventScan.Editable(e.Command) && !Simulating;
		ImGui.SeparatorText(e.Selector != AnimEventScan.NoSelector ? "Event: footstep" : "Event: timed effect");
		ImGui.TextDisabled($"From the script {e.Behaviour} (state {e.State}, body {e.Body}) — it plays this animation, then adds this trail.");
		if (e.Command.Arguments.Count > 3 && e.Command.Arguments[3] != 0xFFFFFFFFu)
			ImGui.TextDisabled($"Only fires on {SurfaceName((int)e.Command.Arguments[3])}.");
		ImGui.BeginDisabled(!canEdit);

		int frames = Math.Max(1, g.Anim.MainAnimation.AnimatedTransformations.Count);
		float fps = g.Anim.DefaultFPS > 0 ? g.Anim.DefaultFPS : 30f;
		float frame = frames - e.SecondsBeforeEnd * fps;
		ImGui.TextUnformatted("Frame");
		ImGui.SameLine(110f);
		ImGui.SetNextItemWidth(-1f);
		if (ImGui.DragFloat("##evframe", ref frame, 0.05f, 0f, frames, "%.2f"))
		{
			BeginEventEdit(e.Command);
			AnimEventScan.SetSecondsBeforeEnd(e.Command, MathF.Max(0f, (frames - Math.Clamp(frame, 0f, frames)) / fps));
			RescanEvents();
		}
		if (ImGui.IsItemDeactivated()) EndEventEdit(e);
		if (ImGui.IsItemHovered()) MaybeTooltip($"The game fires it when the time left in the animation drops under {e.SecondsBeforeEnd:0.000} s (once per loop).\nStored in the script as seconds before the end.");

		ImGui.TextUnformatted("Where");
		ImGui.SameLine(110f);
		ImGui.SetNextItemWidth(-1f);
		if (ImGui.BeginCombo("##evexit", ExitPointName(e.ExitPoint, ogi)))
		{
			for (int i = 0; i <= ogi.ExitPoints.Count; i++)
			{
				int ep = i < ogi.ExitPoints.Count ? i : AnimEventScan.RootExitPoint;
				if (ImGui.Selectable($"{ExitPointName(ep, ogi)}##ep{i}", ep == e.ExitPoint) && ep != e.ExitPoint)
					EditEvent(e, c => AnimEventScan.SetExitPoint(c, ep));
			}
			ImGui.EndCombo();
		}

		ImGui.TextUnformatted("Effect");
		ImGui.SameLine(110f);
		ImGui.SetNextItemWidth(-1f);
		string effectLabel = e.Selector != AnimEventScan.NoSelector ? $"By the ground under it — effect set {e.Selector}" : "Its own particle (no ground)";
		if (ImGui.BeginCombo("##eveffect", effectLabel))
		{
			for (int set = 0; set <= 6; set++)
			{
				int value = set < 6 ? set : AnimEventScan.NoSelector;
				string name = set < 6 ? $"By the ground under it — effect set {set}" : "Its own particle (no ground)";
				if (ImGui.Selectable(name, value == e.Selector) && value != e.Selector) EditEvent(e, c => AnimEventScan.SetSelector(c, value));
			}
			ImGui.EndCombo();
		}
		if (ImGui.IsItemHovered()) MaybeTooltip("By the ground: each surface (sand, grass, water…) has its own particle and sound per effect set, in the level's surface table.\nRunning uses set 2. Its own particle: always the particle below, wherever it steps.");

		if (e.Selector == AnimEventScan.NoSelector)
		{
			ImGui.TextUnformatted("Particle");
			ImGui.SameLine(110f);
			ImGui.SetNextItemWidth(-1f);
			if (ImGui.BeginCombo("##evsystem", SystemName(e.System)))
			{
				if (ImGui.Selectable("none##sysnone", e.System == 0xFFFF) && e.System != 0xFFFF) EditEvent(e, c => AnimEventScan.SetSystem(c, 0xFFFF));
				if (AnimParticles() is { } fx)
					for (int i = 0; i < RuntimeParticles.MaxSystems; i++)
					{
						if (fx.System(i) is not { } s) continue;
						ushort id = (ushort)i;
						if (ImGui.Selectable($"0x{i:X4} {ParticleNames.Clean(s.Name)}##sys{i}", id == e.System) && id != e.System) EditEvent(e, c => AnimEventScan.SetSystem(c, id));
					}
				ImGui.EndCombo();
			}
		}
		ImGui.EndDisabled();

		ImGui.Spacing();
		ImGui.TextUnformatted("Preview on");
		ImGui.SameLine(110f);
		ImGui.SetNextItemWidth(-70f);
		if (ImGui.BeginCombo("##evsurface", SurfaceById(_eventPreviewSurface) is { } ps ? SurfaceName(ps.Id) : "none"))
		{
			foreach (var s in AnimSurfaces())
			{
				if (s.Id is 4 or 5 or 18 or 19 or 20 or 23 or 24 or 26 or 27) continue;
				if (ImGui.Selectable($"{SurfaceName(s.Id)}##surf{s.Id}", s.Id == _eventPreviewSurface)) _eventPreviewSurface = s.Id;
			}
			ImGui.EndCombo();
		}
		ImGui.SameLine();
		if (ImGui.Button("Fire##evfire", new Vector2(-1f, 0f)) && PreviewJointWorlds(out var pogi) is { } pw && pogi is not null)
		{
			FireEventPreview(e, pw, pogi);
			if (ExitPointMatrix(e.ExitPoint, pw, pogi) is { } m) _eventFlashes.Add((m.Translation, ImGui.GetTime() + 0.4, e.Selector != AnimEventScan.NoSelector));
		}
		if (ImGui.IsItemHovered()) MaybeTooltip("Fire it now at this frame, on the ground you picked (the same particle, footprint and dust the game makes).\nWhile the animation plays, every event fires like this on that ground.");

		var (particle, print, dust, sound, vol) = EventEffect(e, _eventPreviewSurface);
		ImGui.TextDisabled("What happens:");
		ImGui.BulletText($"Particle: {SystemName(particle)}{(dust ? $" — plus sand dust {SystemName(SandStepSystem)}" : "")}");
		if (print) ImGui.BulletText("Footprint on the ground");
		if (e.Selector != AnimEventScan.NoSelector)
			ImGui.BulletText(sound != 0xFFFF ? $"Sound 0x{sound:X4}, volume {vol:0.##}" : "No sound on this ground");
		else if (e.Sound != 0xFFFF) ImGui.BulletText($"Sound: the character's sound slot {e.Sound}");

		if (e.Selector != AnimEventScan.NoSelector && ImGui.TreeNode($"Every ground for effect set {e.Selector}##evtable"))
		{
			if (ImGui.BeginTable("##evsurfaces", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
			{
				ImGui.TableSetupColumn("Ground");
				ImGui.TableSetupColumn("Particle");
				ImGui.TableSetupColumn("Sound");
				ImGui.TableHeadersRow();
				foreach (var s in AnimSurfaces())
				{
					var (p, fp, du, sn, sv) = EventEffect(e, s.Id);
					if (p == 0xFFFF && !fp && sn == 0xFFFF) continue;
					ImGui.TableNextRow();
					ImGui.TableNextColumn();
					if (ImGui.Selectable($"{SurfaceName(s.Id)}##row{s.Id}", s.Id == _eventPreviewSurface, ImGuiSelectableFlags.SpanAllColumns)) _eventPreviewSurface = s.Id;
					ImGui.TableNextColumn();
					ImGui.TextUnformatted(p != 0xFFFF ? SystemName(p) + (fp ? " + footprint" : "") : du ? "sand dust + footprint" : fp ? "footprint" : "-");
					ImGui.TableNextColumn();
					ImGui.TextUnformatted(sn != 0xFFFF ? $"0x{sn:X4} @ {sv:0.##}" : "-");
				}
				ImGui.EndTable();
			}
			ImGui.TextDisabled("This table belongs to the level's surfaces (every character that steps there).");
			ImGui.TreePop();
		}
		if (_scriptEdited.Contains(e.Graph))
			ImGui.TextColored(new Vector4(0.95f, 0.75f, 0.25f, 1f), "Script edited — Save (in the Timeline) writes it with the level.");
		var others = TargetEvents().Where(x => ReferenceEquals(x.Command, e.Command) && x.Slot != e.Slot).Select(x => x.Slot).Distinct().ToList();
		if (others.Count > 0) ImGui.TextDisabled($"The same trail is also used by slot(s) {string.Join(", ", others)}.");
	}
}
