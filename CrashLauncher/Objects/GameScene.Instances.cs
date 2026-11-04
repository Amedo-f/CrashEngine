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
	private void AddGlobalObjectInstance(uint objectId, string label)
	{
		Entity entity = base.Roots.FirstOrDefault((Entity r) => r.Has<ChunkSource>());
		ChunkSource chunkSource = entity?.Get<ChunkSource>();
		if (entity == null || chunkSource == null)
		{
			_browser.Log("Add Object: no level currently loaded.");
			return;
		}
		BaseTwinSection baseTwinSection = (from e in AllEntities(entity)
			select e.Get<InstanceData>()?.Section).FirstOrDefault((BaseTwinSection s) => s != null);
		if (baseTwinSection == null)
		{
			baseTwinSection = chunkSource.Rm2.GetItem<BaseTwinSection>(0u)?.GetItem<BaseTwinSection>(6u);
			if (baseTwinSection == null)
			{
				_browser.Log("Add Object: this level has no instances section to add to.");
				return;
			}
		}
		System.Numerics.Vector3 vector = _camera?.Transform.Position ?? System.Numerics.Vector3.Zero;

		PS2AnyInstance? srcInst = null;
		for (int lid = 0; lid <= 7 && srcInst is null; lid++)
		{
			var srcLayout = chunkSource.Rm2.GetItem<BaseTwinSection>((uint)lid);
			var srcInstSec = srcLayout?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);
			if (srcInstSec is null) continue;
			for (int i = 0; i < srcInstSec.GetItemsAmount(); i++)
			{
				if (srcInstSec.GetItem(i) is PS2AnyInstance cand && cand.ObjectId == (ushort)objectId) { srcInst = cand; break; }
			}
		}

		PS2AnyInstance pS2AnyInstance = new PS2AnyInstance
		{
			Position = new Twinsanity.TwinsanityInterchange.Common.Vector4(vector.X, vector.Y, vector.Z, 1f),
			RotationX = new TwinIntegerRotation(),
			RotationY = new TwinIntegerRotation(),
			RotationZ = new TwinIntegerRotation(),
			ObjectId = (ushort)objectId,
			RefListIndex = srcInst?.RefListIndex ?? 0,
			OnSpawnHeaderScriptID = srcInst?.OnSpawnHeaderScriptID ?? ushort.MaxValue,
			StateFlags = srcInst?.StateFlags ?? 0u,
			InstancesRelated = 0u,
			Instances = new List<ushort>(),
			PositionsRelated = 0u,
			Positions = new List<ushort>(),
			PathsRelated = 0u,
			Paths = new List<ushort>(),
			ParamList1 = srcInst is not null ? new List<uint>(srcInst.ParamList1) : new List<uint>(),
			ParamList2 = srcInst is not null ? new List<float>(srcInst.ParamList2) : new List<float>(),
			ParamList3 = srcInst is not null ? new List<uint>(srcInst.ParamList3) : new List<uint>()
		};

		Entity parent = entity.Children.FirstOrDefault((Entity c) => c.Name == "Instances") ?? entity;
		Entity entity2 = AddOrReuseInstance(baseTwinSection, pS2AnyInstance, parent, chunkSource);
		SelectClicked(entity2, additive: false);
		_browser.Log($"Add Object: placed a new '{label}' (id 0x{objectId:X4}) at the camera position. " + "Not on the Undo stack yet (Delete Selected still removes it manually). Save Chunk to keep it.");
	}

	private static uint GenerateUniqueInstanceId(BaseTwinSection section)
	{
		uint num = 0;
		while (section.ContainsItem(num)) num++;
		return num;
	}

	private Entity AddOrReuseInstance(BaseTwinSection section, PS2AnyInstance built, Entity instRoot, ChunkSource chunkSource)
	{
		PS2AnyInstance? slot = null;
		for (int i = 0; i < section.GetItemsAmount(); i++)
			if (section.GetItem(i) is PS2AnyInstance cand && CrashEngine.Importer.ChunkImporter.IsNeutered(cand)) { slot = cand; break; } // Amedo

		PS2AnyInstance target;
		bool reused = slot is not null;
		if (slot is not null)
		{
			target = slot;
			target.Position              = built.Position;
			target.RotationX             = built.RotationX;
			target.RotationY             = built.RotationY;
			target.RotationZ             = built.RotationZ;
			target.ObjectId              = built.ObjectId;
			target.RefListIndex          = built.RefListIndex;
			target.OnSpawnHeaderScriptID = built.OnSpawnHeaderScriptID;
			target.StateFlags            = built.StateFlags;
			target.InstancesRelated      = built.InstancesRelated;
			target.Instances             = built.Instances;
			target.PositionsRelated      = built.PositionsRelated;
			target.Positions             = built.Positions;
			target.PathsRelated          = built.PathsRelated;
			target.Paths                 = built.Paths;
			target.ParamList1            = built.ParamList1;
			target.ParamList2            = built.ParamList2;
			target.ParamList3            = built.ParamList3;
		}
		else
		{
			target = built;
			target.SetID(GenerateUniqueInstanceId(section));
			section.AddItem(target);
		}

		var entity = ChunkImporter.ImportInstance(target, instRoot, new Dictionary<uint, PatrolPath>(), section, isUserAdded: !reused);
		if (chunkSource.MeshTables is not null)
			MeshDecoder.BuildMeshForInstance(Engine.Instance.GL, chunkSource.MeshTables, entity);
		if (reused)
			_browser.Log($"Reused a previously-deleted native slot (id 0x{target.GetID():X4}) instead of adding a new one.");
		return entity;
	}
}
