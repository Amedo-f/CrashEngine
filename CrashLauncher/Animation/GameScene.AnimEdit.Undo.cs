using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using PS2AnyAnimation = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyAnimation;
using TwinTransformation = Twinsanity.TwinsanityInterchange.Common.Animation.Transformation;
using TransformType = Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType;
using static CrashLauncher.AnimChannelOps;

namespace CrashLauncher;

// Amedo 2026-10-04
public sealed partial class GameScene
{
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
}
