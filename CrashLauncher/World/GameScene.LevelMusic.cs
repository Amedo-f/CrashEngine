using CrashEngine.Assets;
using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Renderer;
using CrashEngine.Stealth;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using System.Numerics;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTexture = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using PS2AnyTexturesSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics.PS2AnyTexturesSection;
using ITwinTexture = Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinColor = Twinsanity.TwinsanityInterchange.Common.Color;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyTrigger = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyTrigger;
using PS2AnyCamera = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyCamera;
using PS2AnyPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPosition;
using PS2AnyAIPosition = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyAIPosition;
using PS2AnyCollisionData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using PS2AnyTwinsanitySM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanitySM2;
using TwinCollisionTriangle = Twinsanity.TwinsanityInterchange.Common.Collision.TwinCollisionTriangle;
using TwinGroupInformation = Twinsanity.TwinsanityInterchange.Common.Collision.TwinGroupInformation;
using SurfaceType = Twinsanity.TwinsanityInterchange.Enumerations.Enums.SurfaceType;
using PS2AnyScenery = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using TwinParticleSystem = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem;
using TwinParticleEmitter = Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleEmitter;
using PS2BehaviourGraph = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourGraph;
using TwinBehaviourStarter = Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter;
using BaseTwinItem = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinItem;
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
				for (int lid = 0; lid <= 7; lid++)
				{
					var layout = rm2.GetItem<BaseTwinSection>((uint)lid);
					var instSec = layout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
					if (instSec is null) continue;
					for (int i = 0; i < instSec.GetItemsAmount(); i++)
						if (instSec.GetItem(i) is PS2AnyInstance inst && inst.ObjectId == oid && inst.ParamList3.Count > 0)
							return (inst.ParamList3[0], 1, true);
				}
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
