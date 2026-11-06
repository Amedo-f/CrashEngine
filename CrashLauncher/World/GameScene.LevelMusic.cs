using CrashEngine.Core;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
	private static readonly Dictionary<uint, string> MusicNames = new()
	{
		{ 7, "BP" }, { 8, "ClassroomCortex" }, { 9, "Henchmania" }, { 10, "WormChase" },
		{ 27, "TitleTheme" }, { 28, "Cavern" }, { 29, "BeeChase" }, { 30, "MechaBandicoot" },
		{ 31, "TotemRiver" }, { 32, "BossTikimon" }, { 33, "IcebergLab" }, { 34, "IceClimb" },
		{ 35, "BossUka" }, { 36, "WalrusChase" }, { 37, "Academy" }, { 38, "AcademyNoLaugh" },
		{ 39, "Undefined" }, { 40, "BossDingodile" }, { 41, "Rooftop" }, { 53, "IcebergLabFast" },
		{ 54, "SlipSlide" }, { 55, "BossNGin" }, { 56, "Hijinks" }, { 57, "Boiler" },
		{ 58, "ClassroomCrash" }, { 59, "BossAmberly" }, { 60, "AltLab" }, { 61, "Rockslide" },
		{ 62, "TwinsanityIsland" }, { 63, "AntAgony" }, { 64, "BossTwins" }, { 136, "BoilerUnused" },
	};

	private static (uint Id, int Count, bool FromDjInstance)? FindLevelMusic(PS2AnyTwinsanityRM2? rm2)
	{
		const int beginMusicCommandId = 88;
		if (rm2 is null) return null;

		var code = rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_CODE_SECTION);

		var objSec = code?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_GAME_OBJECTS_SECTION);
		if (objSec is not null)
		{
			uint? djObjectId = null;
			for (int i = 0; i < objSec.GetItemsAmount(); i++)
				if (objSec.GetItem(i) is PS2AnyObject obj && obj.RefBehaviours.Contains((ushort)DjDefaultBehaviourId))
				{ djObjectId = obj.GetID(); break; }

			if (djObjectId is { } oid)
			{
				if (FindLayoutInstance(rm2, inst => inst.ObjectId == oid && inst.ParamList3.Count > 0) is { } dj)
					return (dj.ParamList3[0], 1, true);
			}
		}

		var behSec = code?.GetItem<BaseTwinSection>((uint)TwinConstants.CODE_BEHAVIOURS_SECTION);
		if (behSec is null) return null;

		var tally = new Dictionary<uint, int>();
		for (int i = 0; i < behSec.GetItemsAmount(); i++)
		{
			if (behSec.GetItem(i) is not PS2BehaviourGraph g) continue;
			foreach (var state in g.ScriptStates)
				foreach (var body in state.Bodies)
					foreach (var cmd in body.Commands)
					{
						if (cmd.CommandIndex != beginMusicCommandId || cmd.Arguments.Count == 0) continue;
						var id = cmd.Arguments[0];
						tally[id] = tally.GetValueOrDefault(id) + 1;
					}
		}
		if (tally.Count == 0) return null;
		var best = tally.OrderByDescending(kv => kv.Value).First();
		return (best.Key, best.Value, false);
	}
}
