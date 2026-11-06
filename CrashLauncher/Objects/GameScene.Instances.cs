using CrashEngine.Core;
using CrashEngine.Importer;
using CrashEngine.Stealth;
using PS2AnyInstance = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyInstance;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyTwinsanityRM2 = Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2AnyTwinsanityRM2;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using TwinIntegerRotation = Twinsanity.TwinsanityInterchange.Common.TwinIntegerRotation;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using InstanceState = Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{

	private const uint NewInstanceStateFlags = (uint)(InstanceState.Visible | InstanceState.ShadowActive);

	private static Entity InstancesRoot(Entity chunkRoot) => chunkRoot.Children.FirstOrDefault(c => c.Name == "Instances") ?? chunkRoot;

	private BaseTwinSection? FindInstanceSection(Entity chunkRoot, ChunkSource chunkSource) =>
		AllEntities(chunkRoot).Select(e => e.Get<InstanceData>()?.Section).FirstOrDefault(s => s is not null)
		?? chunkSource.Rm2.GetItem<BaseTwinSection>((uint)TwinConstants.LEVEL_LAYOUT_1_SECTION)?.GetItem<BaseTwinSection>((uint)TwinConstants.LAYOUT_INSTANCES_SECTION);

	private static PS2AnyInstance? FindLayoutInstance(PS2AnyTwinsanityRM2 rm2, Func<PS2AnyInstance, bool> match) =>
		Layouts(rm2).SelectMany(l => LayoutSectionItems<PS2AnyInstance>(l, TwinConstants.LAYOUT_INSTANCES_SECTION)).FirstOrDefault(match);

	private static PS2AnyInstance NewInstance(TwinVec4 position, uint objectId, PS2AnyInstance? template, uint stateFlags,
	                                          List<ushort>? positions = null, uint positionsRelated = 0,
	                                          List<ushort>? paths = null, uint pathsRelated = 0) => new()
	{
		Position              = position,
		RotationX             = new TwinIntegerRotation(),
		RotationY             = new TwinIntegerRotation(),
		RotationZ             = new TwinIntegerRotation(),
		ObjectId              = (ushort)objectId,
		RefListIndex          = template?.RefListIndex ?? -1,
		OnSpawnHeaderScriptID = template?.OnSpawnHeaderScriptID ?? 0xFFFF,
		StateFlags            = template?.StateFlags ?? stateFlags,
		InstancesRelated      = 0, Instances = new List<ushort>(),
		PositionsRelated      = positionsRelated, Positions = positions ?? new List<ushort>(),
		PathsRelated          = pathsRelated,     Paths     = paths ?? new List<ushort>(),
		ParamList1            = template is not null ? new List<uint>(template.ParamList1) : new List<uint>(),
		ParamList2            = template is not null ? new List<float>(template.ParamList2) : new List<float>(),
		ParamList3            = template is not null ? new List<uint>(template.ParamList3) : new List<uint>(),
	};

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
