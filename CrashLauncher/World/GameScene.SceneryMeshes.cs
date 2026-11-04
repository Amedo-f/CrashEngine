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
using PS2AnyPath = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout.PS2AnyPath;
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
using ITwinObject = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject;

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

    private static uint? ResolveSceneryMeshId(PS2AnyGraphicsSection? gfx, uint sourceId, bool isLod)
    {
        if (!isLod) return sourceId;
        var lodSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_LODS_SECTION);
        if (lodSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyLOD>(sourceId) is not { } lod
            || lod.Meshes.Count == 0)
            return null;
        return lod.Meshes[0];
    }

    private static List<uint> ResolveSceneryMeshIds(PS2AnyGraphicsSection? gfx, uint sourceId, bool isLod)
    {
        if (!isLod) return new List<uint> { sourceId };
        var lodSec = gfx?.GetItem<BaseTwinSection>((uint)TwinConstants.GRAPHICS_LODS_SECTION);
        if (lodSec?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyLOD>(sourceId) is not { } lod)
            return new List<uint>();
        return lod.Meshes.Distinct().ToList();
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
