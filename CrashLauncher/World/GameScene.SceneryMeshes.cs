using CrashEngine.Core;
using CrashEngine.Importer;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using PS2AnyGraphicsSection = Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.PS2AnyGraphicsSection;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private static Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyModel? ResolveSceneryModel(
        PS2AnyGraphicsSection? gfx, uint meshId)
    {
        var meshSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MESHES_SECTION);
        if (meshSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyRigidModel>(meshId) is not { } rm)
            return null;
        var modelSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_MODELS_SECTION);
        return modelSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyModel>(rm.Model);
    }

    private static List<uint> ResolveSceneryMeshIds(PS2AnyGraphicsSection? gfx, uint sourceId, bool isLod)
    {
        if (!isLod) return new List<uint> { sourceId };
        var lodSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_LODS_SECTION);
        if (lodSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyLOD>(sourceId) is not { } lod)
            return new List<uint>();
        return lod.Meshes.Distinct().ToList();
    }

    private static IEnumerable<uint> SceneryTileMeshIds(PS2AnyGraphicsSection gfx, Entity sceneryRoot)
    {
        foreach (var e in AllEntities(sceneryRoot).Where(x => x.Has<CrashEngine.Importer.MeshRenderer>()))
            if (e.Parent?.Get<SceneryTile>() is { } tile)
                foreach (var meshId in ResolveSceneryMeshIds(gfx, tile.SourceId, tile.IsLod))
                    yield return meshId;
    }

    private static IEnumerable<uint> SkydomeMeshIds(BaseTwinSection skySec)
    {
        for (int i = 0; i < skySec.GetItemsAmount(); i++)
            if (skySec.GetItem(i) is Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnySkydome sky)
                foreach (var meshId in sky.Meshes)
                    yield return meshId;
    }

    private static void RefreshSceneryModels(ChunkSource? chunkSource, PS2AnyGraphicsSection gfx, IEnumerable<uint> meshIds)
    {
        if (chunkSource?.SceneryTables is not { } tables) return;
        var refreshed = new HashSet<uint>();
        foreach (var meshId in meshIds)
            if (refreshed.Add(meshId) && ResolveSceneryModel(gfx, meshId) is { } model)
                MeshDecoder.RefreshSceneryMeshVertexData(Engine.Instance.GL, tables, meshId, model);
    }

    private void ApplyManualMeshIdChange(Entity tileEntity, SceneryTile tile, uint desiredNewMeshId)
    {
        var chunkRoot = Roots.FirstOrDefault(r => r.Has<ChunkSource>());
        var chunkSource = chunkRoot?.Get<ChunkSource>();
        var gfx = chunkSource?.Sm2?.GetItem<PS2AnyGraphicsSection>((uint)TwinConstants.SCENERY_GRAPHICS_SECTION);
        if (chunkSource?.Sm2 is null || gfx is null) { _browser.Log("Edit Mesh ID: no scenery graphics data loaded."); return; }

        uint oldId;
        if (!tile.IsLod)
        {
            oldId = tile.SourceId;
        }
        else
        {
            var lodSec = gfx.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_LODS_SECTION);
            if (lodSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyLOD>(tile.SourceId) is not { } lod
                || lod.Meshes.Count == 0)
            { _browser.Log("Edit Mesh ID: couldn't resolve this LOD tile's own Meshes[0]."); return; }
            oldId = lod.Meshes[0];
        }

        if (!MeshDecoder.SetSceneryMeshIdManually(chunkSource.Sm2, oldId, desiredNewMeshId, out var error))
        { _browser.Log($"Edit Mesh ID: {error}"); return; }

        if (chunkSource.SceneryTables is not null)
            MeshDecoder.AliasSceneryMeshTableEntry(chunkSource.SceneryTables, oldId, desiredNewMeshId);

        if (!tile.IsLod)
        {
            tile.SourceId = desiredNewMeshId;
        }
        else
        {
            var lodSec = gfx.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_LODS_SECTION);
            if (lodSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyLOD>(tile.SourceId) is { } lod)
                lod.Meshes[0] = desiredNewMeshId;
        }

        int cut = tileEntity.Name.LastIndexOf('_');
        if (cut >= 0)
            tileEntity.Name = tileEntity.Name[..(cut + 1)] + "mesh" + desiredNewMeshId.ToString("X8");

        _browser.Log($"Edit Mesh ID: 0x{oldId:X8} -> 0x{desiredNewMeshId:X8} (this tile only). " +
                      "Save Chunk to keep it, then Build ISO + test.");
    }

    private static TwinVec4[]? FindTileBoundingBox(SceneryTile tile)
    {
        var node = tile.Node;
        int idx = tile.IsLod ? node.LodModelMatrices.IndexOf(tile.Source) : node.MeshModelMatrices.IndexOf(tile.Source);
        if (idx < 0) return null;
        int bboxIdx = tile.IsLod ? node.MeshIDs.Count + idx : idx;
        return bboxIdx >= 0 && bboxIdx < node.BoundingBoxes.Count ? node.BoundingBoxes[bboxIdx] : null;
    }
}
