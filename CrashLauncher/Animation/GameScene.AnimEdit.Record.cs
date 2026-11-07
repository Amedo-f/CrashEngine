using CrashEngine.Core;
using CrashEngine.Importer;
using ImGuiNET;
using System.Numerics;
using PS2AnyAnimation = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyAnimation;
using TwinAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation;
using TransformType = Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType;
using static CrashLauncher.AnimChannelOps;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

// Amedo 2026-10-04
public sealed partial class GameScene
{
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
		var sec = MeshDecoder.CodeSection(rm2, TwinConstants.CODE_ANIMATIONS_SECTION);
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
		ImGui.SameLine();
		ImGui.BeginDisabled(_animJoint < 0);
		if (ImGui.SmallButton("Insert Key (I)##insertkey")) InsertKeyframe();
		ImGui.EndDisabled();
		ImGui.SameLine();
		ImGui.BeginDisabled(!CanResetAnim(g));
		if (ImGui.SmallButton("Reset##animreset")) ResetToOriginal(g);
		ImGui.EndDisabled();
		ImGui.SameLine();
		ImGui.BeginDisabled(!CanSaveAnim(g));
		if (ImGui.SmallButton("Save##animsave")) SaveAnimToLevel(g);
		ImGui.EndDisabled();
		ImGui.SameLine();
		if (_pendingPose.Count > 0 && ReferenceEquals(_pendingClone, g.Anim))
			ImGui.TextColored(new Vector4(1f, 0.6f, 0.15f, 1f), "pose not keyed — press I");
		else if (_animEdited.Contains(g.Anim) || _scriptEdited.Count > 0)
			ImGui.TextColored(new Vector4(0.95f, 0.75f, 0.25f, 1f), "edited — not saved");
		else if (_cloneOriginal.TryGetValue(g.Anim, out var original) && _animApplied.Contains(original))
			ImGui.TextColored(new Vector4(0.45f, 0.85f, 0.4f, 1f), "saved to the level");
		else ImGui.NewLine();
	}
}
