using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using PS2AnyAnimation = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyAnimation;
using TwinAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation;
using TransformType = Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType;
using static CrashLauncher.AnimChannelOps;

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
			ImGui.PopID();
		}
		ImGui.Spacing();
		ImGui.TextDisabled("Mode: XYZ Euler (the game's order: X, then Y, then Z)");
		if (_animEdited.Contains(g.Anim))
			ImGui.TextColored(new Vector4(0.95f, 0.75f, 0.25f, 1f), "Edited — Save (in the Timeline) puts it in the level.");
	}
}
