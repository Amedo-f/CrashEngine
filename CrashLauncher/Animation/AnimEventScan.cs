using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using PS2BehaviourState = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourState;
using PS2BehaviourStateBody = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourStateBody;
using PS2BehaviourCommand = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourCommand;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

// Amedo 2026-10-04
internal readonly record struct AnimEvent(int Slot, int Type, int ExitPoint, float SecondsBeforeEnd, int Selector, ushort System, ushort Sound,
	string Behaviour, uint BehaviourId, int State, int Body, PS2BehaviourCommand Command, PS2BehaviourGraph Graph);

internal static class AnimEventScan
{
	internal const int AnimTimedTrail = 7;

	internal static bool IsDoAnimation(ushort op) => op is 9 or 543;
	internal static bool IsAddTrail(ushort op) => op is 1 or 541;

	private static BaseTwinSection? CodeSection(PS2AnyTwinsanityRM2? rm2, int id) =>
		rm2?.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION)?.GetItem<BaseTwinSection>((uint)id);

	internal static PS2AnyObject? FindObject(uint id, params PS2AnyTwinsanityRM2?[] files)
	{
		foreach (var f in files)
			if (CodeSection(f, TwinConstants.CODE_GAME_OBJECTS_SECTION) is { } sec && sec.ContainsItem(id) && sec.GetItem<PS2AnyObject>(id) is { } o) return o;
		return null;
	}

	internal static List<PS2BehaviourGraph> GraphsOf(PS2AnyObject obj, params PS2AnyTwinsanityRM2?[] files)
	{
		var result = new List<PS2BehaviourGraph>();
		var seen = new HashSet<uint>();
		foreach (uint id in obj.RefBehaviours.Concat(obj.BehaviourSlots).Select(x => (uint)x))
		{
			if (id == 0xFFFF || !seen.Add(id)) continue;
			foreach (var f in files)
				if (CodeSection(f, TwinConstants.CODE_BEHAVIOURS_SECTION) is { } sec && sec.ContainsItem(id))
				{
					if (sec.GetItem<Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem>(id) is PS2BehaviourGraph g) result.Add(g);
					break;
				}
		}
		return result;
	}

	internal const int NoSelector = 0xFF, RootExitPoint = 63;

	internal static bool Editable(PS2BehaviourCommand c) => c.Arguments.Count >= 23;

	internal static void SetSecondsBeforeEnd(PS2BehaviourCommand c, float seconds) => c.Arguments[22] = BitConverter.SingleToUInt32Bits(seconds);

	internal static void SetExitPoint(PS2BehaviourCommand c, int exitPoint) =>
		c.Arguments[9] = (c.Arguments[9] & ~0x00FF0000u) | ((uint)(exitPoint & 0xFF) << 16);

	internal static void SetSelector(PS2BehaviourCommand c, int selector) =>
		c.Arguments[2] = (c.Arguments[2] & ~(0xFFu << 6)) | ((uint)(selector & 0xFF) << 6);

	internal static void SetSystem(PS2BehaviourCommand c, ushort system) =>
		c.Arguments[4] = (c.Arguments[4] & 0xFFFF0000u) | system;

	internal static bool HasOffset(PS2BehaviourCommand c) => c.Arguments.Count > 2 && (c.Arguments[2] & (1u << 5)) != 0;

	internal static System.Numerics.Vector3 Offset(PS2BehaviourCommand c) => c.Arguments.Count > 27
		? new System.Numerics.Vector3(BitConverter.UInt32BitsToSingle(c.Arguments[25]), BitConverter.UInt32BitsToSingle(c.Arguments[26]), BitConverter.UInt32BitsToSingle(c.Arguments[27]))
		: default;

	internal static bool HoldsBehaviour(PS2AnyTwinsanityRM2? rm2, PS2BehaviourGraph g)
	{
		if (CodeSection(rm2, TwinConstants.CODE_BEHAVIOURS_SECTION) is not { } sec) return false;
		for (int i = 0; i < sec.GetItemsAmount(); i++)
			if (ReferenceEquals(sec.GetItem(i), g)) return true;
		return false;
	}

	internal static IEnumerable<int> Slots(PS2BehaviourCommand c)
	{
		var a = c.Arguments;
		if (a.Count < 6) yield break;
		int count = (int)(a[0] & 0xF);
		for (int i = 0; i < count && 5 + i / 4 < a.Count; i++) yield return (int)((a[5 + i / 4] >> (8 * (i % 4))) & 0xFF);
	}

	internal static List<AnimEvent> Scan(IEnumerable<PS2BehaviourGraph> graphs)
	{
		var events = new List<AnimEvent>();
		foreach (var g in graphs)
			for (int s = 0; s < g.ScriptStates.Count; s++)
			{
				if (g.ScriptStates[s] is not PS2BehaviourState state) continue;
				for (int b = 0; b < state.Bodies.Count; b++)
				{
					if (state.Bodies[b] is not PS2BehaviourStateBody body) continue;
					var slots = new List<int>();
					foreach (var c in body.Commands)
					{
						if (c is not PS2BehaviourCommand pc) continue;
						if (IsDoAnimation(pc.CommandIndex)) { slots = Slots(pc).Distinct().ToList(); continue; }
						if (!IsAddTrail(pc.CommandIndex)) continue;
						var a = pc.Arguments;
						uint W(int i) => 1 + i < a.Count ? a[1 + i] : 0u;
						int type = (int)(W(0) & 0xF);
						if (type != AnimTimedTrail) continue;
						foreach (int slot in slots)
							events.Add(new AnimEvent(slot, type, (int)((W(8) >> 16) & 0xFF), BitConverter.UInt32BitsToSingle(W(21)),
								(int)((W(1) >> 6) & 0xFF), (ushort)(W(3) & 0xFFFF), (ushort)(W(3) >> 16), g.Name ?? $"0x{g.GetID():X4}", g.GetID(), s, b, pc, g));
					}
				}
			}
		return events;
	}
}
