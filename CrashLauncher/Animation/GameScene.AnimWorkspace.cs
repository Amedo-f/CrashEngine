using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using PS2AnyOGI = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI;
using TwinJoint = Twinsanity.TwinsanityInterchange.Common.TwinJoint;

namespace CrashLauncher;

// Amedo 2026-10-04
public sealed partial class GameScene
{
	private static readonly Dictionary<int, string> ReactNames = new()
	{
		[0] = "head", [1] = "spine 1", [2] = "spine 2", [3] = "spine 3", [4] = "spine 4", [5] = "pelvis",
		[6] = "leg", [7] = "leg", [8] = "leg", [9] = "leg", [10] = "leg", [11] = "leg",
		[12] = "arm", [13] = "arm", [14] = "arm", [15] = "arm", [16] = "arm", [17] = "arm", [18] = "arm", [19] = "arm",
		[20] = "hair", [21] = "hair", [22] = "finger", [23] = "finger", [24] = "foot", [25] = "foot",
		[26] = "ear", [27] = "ear", [28] = "nose",
	};

	private static string JointLabel(TwinJoint j) =>
		ReactNames.TryGetValue(j.ReactId, out var n) ? $"Bone {j.Index}  ({n} · {j.ReactId})" : $"Bone {j.Index}";

	private void DrawAnimationWorkspace()
	{
		DrawArmatureOverlay();
		var anim = PreviewAnim;
		var ogi = anim is null ? null : SkeletonOf(anim);

		if (ImGui.Begin("Characters##anim", ImGuiWindowFlags.NoCollapse))
		{
			if (ImGui.SmallButton("Refresh##animrefresh")) RefreshAnimCharacters();
			ImGui.SameLine();
			ImGui.TextDisabled($"{_animCharacters.Count} in the level");
			if (ImGui.BeginChild("##animchars", new Vector2(0f, 120f), ImGuiChildFlags.Border))
			{
				foreach (var e in _animCharacters)
				{
					if (ImGui.Selectable($"{e.Name}##animchar{e.GetHashCode()}", e == _animTarget) && e != _animTarget)
						SetAnimTarget(e);
				}
			}
			ImGui.EndChild();
			if (anim is not null && ogi is not null)
			{
				ImGui.TextUnformatted(_animTarget!.Name);
				ImGui.TextDisabled($"Skeleton: OGI 0x{ogi.GetID():X4}, {ogi.Joints.Count} joints, {ogi.ExitPoints.Count} exit points");
				ImGui.TextDisabled(ogi.SkinID != 0 ? $"Skin: 0x{ogi.SkinID:X4}" : "Skin: none");
				ImGui.TextDisabled(ogi.BlendSkinID != 0 ? $"Blend skin (face shapes): 0x{ogi.BlendSkinID:X4}" : "Blend skin: none");
				ImGui.TextDisabled($"Rigid parts: {ogi.RigidModelIds.Count}   Collision parts: {ogi.Collisions.Count}");
				if (ImGui.Button("T-pose##animtpose"))
				{
					anim.Stop();
					_animGroup = null;
				}
				if (ImGui.IsItemHovered()) MaybeTooltip("The bind pose — the skeleton with no animation on it.");
				ImGui.SameLine();
				ImGui.TextDisabled($"{anim.Groups.Count} animations");
				if (ImGui.BeginChild("##animlist", Vector2.Zero, ImGuiChildFlags.Border))
				{
					foreach (var g in anim.Groups.OrderBy(g => g.SlotIndex))
					{
						string face = g.Anim.HasFacialAnimationData ? "  face" : "";
						string label = $"Slot {g.SlotIndex,2}   0x{g.AnimId:X4}   {g.Anim.TotalFrames} f @ {g.Anim.DefaultFPS}{face}##animslot{g.SlotIndex}";
						if (ImGui.Selectable(label, g == _animGroup))
						{
							_animGroup = g;
							UseCloneIfAny(g);
							anim.Loop = true;
							anim.Play(g);
						}
					}
				}
				ImGui.EndChild();
			}
			else if (_animCharacters.Count == 0)
				ImGui.TextDisabled("This level has no animated characters.");
		}
		ImGui.End();

		var viewOgi = _animGroup?.Ogi ?? ogi;
		if (ImGui.Begin("Bones##anim", ImGuiWindowFlags.NoCollapse))
		{
			if (viewOgi is null) ImGui.TextDisabled("Pick a character.");
			else
			{
				if (_animGroup is not null && _animGroup.Ogi != ogi)
					ImGui.TextDisabled($"This animation's own skeleton: OGI 0x{viewOgi.GetID():X4}, {viewOgi.Joints.Count} joints");
				foreach (var j in viewOgi.Joints)
					if (j.ParentIndex < 0 || j.ParentIndex >= viewOgi.Joints.Count || j.ParentIndex == j.Index)
						DrawJointNode(viewOgi, j, 0);
			}
		}
		ImGui.End();

		if (ImGui.Begin("Timeline##anim", ImGuiWindowFlags.NoCollapse))
		{
			if (anim is null || _animGroup is null) ImGui.TextDisabled("Pick an animation from the Characters panel.");
			else
			{
				int frames = Math.Max(1, _animGroup.Anim.MainAnimation.AnimatedTransformations.Count);
				if (ImGui.Button(anim.Playing ? "Pause##animplay" : "Play##animplay")) anim.Playing = !anim.Playing;
				ImGui.SameLine();
				bool loop = anim.Loop;
				if (ImGui.Checkbox("Loop##animloop", ref loop)) anim.Loop = loop;
				ImGui.SameLine();
				ImGui.TextDisabled($"Slot {_animGroup.SlotIndex} · {frames} frames · {_animGroup.Anim.DefaultFPS} fps · {AnimatedObject.NaturalDuration(_animGroup):0.00} s · frame {anim.Frame:0.0}");
				ImGui.SameLine();
				ImGui.Checkbox("All frames##animallframes", ref _timelineAllFrames);
				if (ImGui.IsItemHovered()) MaybeTooltip("Off: only the real keys (frames the motion bends at — the rest are filled in between, exactly).\nOn: every frame of the game's data as a key.");
				DrawAnimRecordBar(anim, _animGroup);
				DrawChannelTimeline(anim, _animGroup, frames);
			}
		}
		ImGui.End();
	}

	private static readonly string[] ChannelNames =
		{ "Position X", "Position Y", "Position Z", "Rotation X", "Rotation Y", "Rotation Z", "Scale X", "Scale Y", "Scale Z" };

	private sealed class BoneChannels
	{
		public bool HasSettings;
		public readonly bool[] Animated = new bool[9];
		public readonly float[][] Values = new float[9][];
		public readonly float[] Min = new float[9], Max = new float[9];
	}

	private (AnimatedObject.SlotGroup Group, int Joint, BoneChannels Data)? _channelCache;
	private static readonly BoneChannels NoBoneChannels = new();

	private BoneChannels ChannelsOf(AnimatedObject.SlotGroup g, int joint)
	{
		if (_channelCache is { } c && c.Group == g && c.Joint == joint) return c.Data;
		var data = ReadBoneChannels(g.Anim.MainAnimation, joint);
		_channelCache = (g, joint, data);
		return data;
	}

	private static float ChannelValue(short raw, bool rotation) => rotation ? raw * (360f * 16f / 65536f) : raw / 4096f;

	private static BoneChannels ReadBoneChannels(Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation anim, int joint)
	{
		var bc = new BoneChannels();
		if (joint < 0 || joint >= anim.JointSettings.Count) return bc;
		bc.HasSettings = true;
		var js = anim.JointSettings[joint];
		var types = new[] { js.TranslateX, js.TranslateY, js.TranslateZ, js.RotateX, js.RotateY, js.RotateZ, js.ScaleX, js.ScaleY, js.ScaleZ };
		int s = js.TransformationIndex, a = js.AnimationTransformationIndex;
		int frames = anim.AnimatedTransformations.Count;
		for (int ch = 0; ch < 9; ch++)
		{
			bool rot = ch is >= 3 and <= 5;
			float[] v;
			if (types[ch] == Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType.Animated && frames > 0)
			{
				bc.Animated[ch] = true;
				v = new float[frames];
				for (int f = 0; f < frames; f++)
				{
					var tr = anim.AnimatedTransformations[f].Transforms;
					v[f] = a < tr.Count ? ChannelValue(tr[a].PureValue, rot) : 0f;
					if (rot && f > 0)
					{
						while (v[f] - v[f - 1] > 180f) v[f] -= 360f;
						while (v[f] - v[f - 1] < -180f) v[f] += 360f;
					}
				}
				a++;
			}
			else
			{
				v = new[] { s < anim.StaticTransformations.Count ? ChannelValue(anim.StaticTransformations[s].PureValue, rot) : 0f };
				s++;
			}
			bc.Values[ch] = v;
			bc.Min[ch] = v.Min();
			bc.Max[ch] = v.Max();
		}
		return bc;
	}

	private static bool BoneIsAnimated(Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation anim, int joint)
	{
		if (joint < 0 || joint >= anim.JointSettings.Count) return false;
		var js = anim.JointSettings[joint];
		var animated = Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType.Animated;
		return js.TranslateX == animated || js.TranslateY == animated || js.TranslateZ == animated
			|| js.RotateX == animated || js.RotateY == animated || js.RotateZ == animated
			|| js.ScaleX == animated || js.ScaleY == animated || js.ScaleZ == animated;
	}

	private void DrawChannelTimeline(AnimatedObject anim, AnimatedObject.SlotGroup g, int frames)
	{
		bool hasBone = _animJoint >= 0;
		var bc = hasBone ? ChannelsOf(g, _animJoint) : NoBoneChannels;
		const float labelW = 110f, rowH = 22f, rulerH = 20f;
		float eventsH = EventRowH;
		float avail = ImGui.GetContentRegionAvail().X;
		float frameW = MathF.Max(8f, (avail - labelW - 16f) / frames);
		var size = new Vector2(labelW + frameW * frames + 8f, rulerH + eventsH + rowH * (hasBone ? 9f : 1f));
		if (!ImGui.BeginChild("##animtrack", Vector2.Zero, ImGuiChildFlags.Border, ImGuiWindowFlags.HorizontalScrollbar)) { ImGui.EndChild(); return; }
		var dl = ImGui.GetWindowDrawList();
		var o = ImGui.GetCursorScreenPos();
		ImGui.InvisibleButton("##animtrackarea", size);
		bool hovered = ImGui.IsItemHovered(), held = ImGui.IsItemActive();
		var mouse = ImGui.GetMousePos();
		float x0 = o.X + labelW;
		float FrameX(float f) => x0 + f * frameW + frameW * 0.5f;

		uint textCol = ImGui.GetColorU32(ImGuiCol.Text), dimCol = ImGui.GetColorU32(ImGuiCol.TextDisabled);
		uint rowA = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.03f)), rowB = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.06f));
		uint keyCol = ImGui.GetColorU32(new Vector4(0.95f, 0.75f, 0.25f, 1f)), curveCol = ImGui.GetColorU32(new Vector4(0.35f, 0.75f, 1f, 0.9f));
		uint staticCol = ImGui.GetColorU32(new Vector4(0.6f, 0.6f, 0.6f, 0.5f)), headCol = ImGui.GetColorU32(new Vector4(1f, 0.3f, 0.3f, 1f));

		int labelStep = frameW >= 28f ? 1 : frameW >= 14f ? 2 : frameW >= 9f ? 5 : 10;
		for (int f = 0; f < frames; f++)
		{
			float x = FrameX(f);
			bool major = f % labelStep == 0;
			dl.AddLine(new Vector2(x, o.Y + rulerH - (major ? 7f : 4f)), new Vector2(x, o.Y + rulerH), dimCol);
			if (major) dl.AddText(new Vector2(x - 3f, o.Y + 1f), dimCol, f.ToString());
		}

		bool eventTip = DrawEventRow(dl, g, o, x0, frameW, size.X, o.Y + rulerH, mouse, hovered);
		if (!hasBone) dl.AddText(new Vector2(o.X + 4f, o.Y + rulerH + eventsH + 4f), dimCol, "Pick a bone in the Bones panel to see its channels. Bones marked • move in this animation.");
		for (int ch = 0; hasBone && ch < 9; ch++)
		{
			float y0 = o.Y + rulerH + eventsH + ch * rowH, yc = y0 + rowH * 0.5f;
			dl.AddRectFilled(new Vector2(o.X, y0), new Vector2(o.X + size.X, y0 + rowH), ch % 2 == 0 ? rowA : rowB);
			dl.AddText(new Vector2(o.X + 4f, y0 + 4f), bc.HasSettings ? textCol : dimCol, ChannelNames[ch]);
			if (!bc.HasSettings)
			{
				if (ch == 0) dl.AddText(new Vector2(x0 + 4f, y0 + 4f), dimCol, "not in this animation — the bone stays in its bind pose");
				continue;
			}
			var v = bc.Values[ch];
			if (!bc.Animated[ch])
			{
				dl.AddLine(new Vector2(x0, yc), new Vector2(x0 + frameW * frames, yc), staticCol, 2f);
				dl.AddText(new Vector2(x0 + 4f, y0 + 4f), dimCol, ch is >= 3 and <= 5 ? $"{v[0]:0.##}° (fixed)" : $"{v[0]:0.###} (fixed)");
				continue;
			}
			float lo = bc.Min[ch], hi = bc.Max[ch], span = hi - lo;
			float Y(float val) => span < 1e-6f ? yc : y0 + rowH - 3f - (val - lo) / span * (rowH - 6f);
			for (int f = 1; f < v.Length; f++)
				dl.AddLine(new Vector2(FrameX(f - 1), Y(v[f - 1])), new Vector2(FrameX(f), Y(v[f])), curveCol, 1.5f);
			float k = MathF.Min(4f, frameW * 0.3f);
			var keyMask = _timelineAllFrames ? null : Channel(g.Anim.MainAnimation, _animJoint, ch).IsKey;
			for (int f = 0; f < v.Length; f++)
			{
				if (keyMask is not null && (f >= keyMask.Length || !keyMask[f])) continue;
				var c = new Vector2(FrameX(f), yc);
				dl.AddQuadFilled(c + new Vector2(0f, -k), c + new Vector2(k, 0f), c + new Vector2(0f, k), c + new Vector2(-k, 0f), keyCol);
			}
		}

		float headX = FrameX(anim.Frame);
		dl.AddLine(new Vector2(headX, o.Y), new Vector2(headX, o.Y + size.Y), headCol, 2f);

		if ((held || (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))) && mouse.X >= x0)
		{
			float f = Math.Clamp((mouse.X - x0) / frameW - 0.5f, 0f, frames - 1);
			anim.Playing = false;
			anim.Frame = f;
		}
		if (hovered && !eventTip && mouse.X >= x0 && bc.HasSettings)
		{
			int f = Math.Clamp((int)((mouse.X - x0) / frameW), 0, frames - 1);
			int ch = mouse.Y < o.Y + rulerH + eventsH ? -1 : (int)((mouse.Y - o.Y - rulerH - eventsH) / rowH);
			if (ch is >= 0 and < 9)
			{
				var v = bc.Values[ch];
				float val = bc.Animated[ch] ? v[Math.Min(f, v.Length - 1)] : v[0];
				ImGui.SetTooltip($"{ChannelNames[ch]} at frame {f}: {(ch is >= 3 and <= 5 ? $"{val:0.###}°" : $"{val:0.####}")}{(bc.Animated[ch] ? "" : " (fixed for the whole animation)")}");
			}
		}
		ImGui.EndChild();
	}

	private void DrawJointNode(PS2AnyOGI ogi, TwinJoint j, int depth)
	{
		if (depth > 64) return;
		var children = ogi.Joints.Where(c => c.ParentIndex == j.Index && c.Index != j.Index).ToList();
		var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth;
		if (children.Count == 0) flags |= ImGuiTreeNodeFlags.Leaf;
		if (_animJoint == j.Index) flags |= ImGuiTreeNodeFlags.Selected;
		string moving = _animGroup is not null && BoneIsAnimated(_animGroup.Anim.MainAnimation, j.Index) ? " •" : "";
		bool open = ImGui.TreeNodeEx($"{JointLabel(j)}{moving}##joint{j.Index}", flags);
		if (ImGui.IsItemClicked()) _animJoint = j.Index;
		if (!open) return;
		foreach (var c in children) DrawJointNode(ogi, c, depth + 1);
		ImGui.TreePop();
	}

	private void DrawAnimationInspector()
	{
		var anim = PreviewAnim;
		var ogi = anim is null ? null : _animGroup?.Ogi ?? SkeletonOf(anim);
		if (anim is null || ogi is null) { ImGui.TextDisabled("Pick a character in the Characters panel."); return; }
		ImGui.TextUnformatted(_animTarget!.Name);
		if (_animGroup is { } g)
		{
			ImGui.SeparatorText("Animation");
			ImGui.TextDisabled($"Slot {g.SlotIndex}, animation 0x{g.AnimId:X4}, OGI 0x{g.Ogi.GetID():X4}");
			ImGui.TextDisabled($"{g.Anim.TotalFrames} frames at {g.Anim.DefaultFPS} fps, {AnimatedObject.NaturalDuration(g):0.00} s");
			ImGui.TextDisabled(g.Anim.HasFacialAnimationData ? "Has face shape keys" : "No face shape keys");
			DrawAnimEventsInfo(g);
		}
		if (_animJoint >= 0 && _animJoint < ogi.Joints.Count)
		{
			var j = ogi.Joints[_animJoint];
			ImGui.SeparatorText("Bone");
			ImGui.TextUnformatted(JointLabel(j));
			ImGui.TextDisabled($"Parent: {(j.ParentIndex >= 0 && j.ParentIndex != j.Index ? $"Bone {j.ParentIndex}" : "none")}");
			ImGui.TextDisabled($"Bind position: {j.LocalTranslation.X:0.###}, {j.LocalTranslation.Y:0.###}, {j.LocalTranslation.Z:0.###}");
			ImGui.TextDisabled($"Bind rotation: {j.LocalRotation.X:0.###}, {j.LocalRotation.Y:0.###}, {j.LocalRotation.Z:0.###}, {j.LocalRotation.W:0.###}");
			if (anim.JointWorlds is { } worlds && worlds.TryGetValue(j.Index, out var m))
				ImGui.TextDisabled($"Now (world): {m.Translation.X:0.###}, {m.Translation.Y:0.###}, {m.Translation.Z:0.###}");
			if (_animGroup is { } ag) DrawBoneTransformPanel(anim, ag, j.Index);
		}
	}
}
