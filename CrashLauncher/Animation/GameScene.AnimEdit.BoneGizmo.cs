using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using PS2AnyAnimation = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyAnimation;
using PS2AnyOGI = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI;
using TwinAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation;
using static CrashLauncher.AnimChannelOps;

namespace CrashLauncher;

// Amedo 2026-10-04
public sealed partial class GameScene
{
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

	private static readonly (GizmoMode Mode, string Id, string Label)[] GizmoModeButtons =
	{
		(GizmoMode.Move, "##movegizmo", "Position"),
		(GizmoMode.Rotate, "##rotgizmo", "Rotation"),
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
			var (mode, id, label) = GizmoModeButtons[i];
			var tex = mode == GizmoMode.Move ? _moveIconTexture : _rotateIconTexture;
			bool on = _gizmoMode == mode;
			if (on) ImGui.PushStyleColor(ImGuiCol.Button, activeCol);
			bool clicked = tex != null
				? ImGui.ImageButton(id, (nint)tex.GlId, new Vector2(icon, icon), Vector2.Zero, Vector2.One, Vector4.Zero, Vector4.One)
				: ImGui.SmallButton(label + id);
			if (on) ImGui.PopStyleColor();
			if (_workspace != Workspace.Animation && ImGui.IsItemHovered()) ImGui.SetTooltip(label);
			if (clicked) _gizmoMode = mode;
		}
		DrawAiToolButton(icon); // Amedo 2026-11-15
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
		var dl = ImGui.GetBackgroundDrawList();
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
}
