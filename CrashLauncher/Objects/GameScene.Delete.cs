using CrashEngine.Core;
using CrashEngine.Importer;
using TwinChunkLink = Twinsanity.TwinsanityInterchange.Common.TwinChunkLink;
using PS2AnyLink = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink;
using BaseTwinSection = Twinsanity.TwinsanityInterchange.Implementations.Base.BaseTwinSection;
using ITwinItem = Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem;
using TwinConstants = Twinsanity.TwinsanityInterchange.Enumerations.Constants;
using PS2AnyParticleData = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData;
using PS2AnyObject = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyObject;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{

    private const string EmptyPlaceholderObjectName = "CE_EmptyPlaceholder";

    private static ushort GetOrCreateEmptyPlaceholderObjectId(BaseTwinSection objSec, BaseTwinSection? globalObjSec)
    {
        for (int i = 0; i < objSec.GetItemsAmount(); i++)
            if (objSec.GetItem(i) is PS2AnyObject existing && existing.Name == EmptyPlaceholderObjectName)
                return (ushort)existing.GetID();

        uint newId = 0;
        while (objSec.ContainsItem(newId) || (globalObjSec?.ContainsItem(newId) ?? false)) newId++;

        var empty = new PS2AnyObject
        {
            Type = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject.ObjectType.GenericObject,
            Name = EmptyPlaceholderObjectName,
            BehaviourPack = new Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourCommandPack(),
        };
        empty.SetID(newId);
        objSec.AddItem(empty);
        return (ushort)newId;
    }

    private void DeleteSelected()
    {
        if (_selectedSet.Count == 0) return;

        var actions   = new List<IEditAction>();
        int cubeCount = 0, importCount = 0, aiPosDelCount = 0;
        var deletedTransplantObjIds = new HashSet<uint>();

        var chunkSourceForPlaceholder = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        var objSecForPlaceholder = MeshDecoder.CodeSection(chunkSourceForPlaceholder?.Rm2, TwinConstants.CODE_GAME_OBJECTS_SECTION);
        var globalObjSecForPlaceholder = MeshDecoder.CodeSection(chunkSourceForPlaceholder?.GlobalRm2, TwinConstants.CODE_GAME_OBJECTS_SECTION);
        ushort? placeholderObjectId = objSecForPlaceholder is not null
            ? GetOrCreateEmptyPlaceholderObjectId(objSecForPlaceholder, globalObjSecForPlaceholder)
            : null;

        foreach (var e in _selectedSet.ToList())
        {
            if (e.Has<PlayerCameraMarker>()) continue; // Amedo 2026-11-15
            var parent = e.Parent;

            foreach (var desc in AllEntities(e))
            {
                if (desc == e) continue;
                if (desc.Has<CollisionShapeMarker>() && _cubes.FirstOrDefault(c => c.Ent == desc) is { } childCube
                    && !_pendingDel.Contains(childCube))
                { _pendingDel.Add(childCube); cubeCount++; }
            }

            if (parent is not null && e.Get<InstanceData>() is { } data)
            {
                DeleteInstance(e, parent, data, placeholderObjectId, actions);
                deletedTransplantObjIds.Add(data.ObjectId);
            }
            else if (parent is not null && e.Get<SceneryTile>() is { } tile)
            {
                if (DeleteSceneryTile(e, parent, tile, chunkSourceForPlaceholder, actions)) importCount++;
            }
            else if (parent is not null && e.Get<TriggerMarker>() is { } trigMarker)
            {
                var neuter = new TriggerVolumeNeuterAction { Entity = e, Parent = parent, Trigger = trigMarker.Source.Trigger };
                neuter.Redo();
                actions.Add(neuter);
            }
            else if (parent is not null && e.Get<CameraMarker>() is { } camMarker)
            {
                var neuter = new TriggerVolumeNeuterAction { Entity = e, Parent = parent, Trigger = camMarker.Source.CamTrigger };
                neuter.Redo();
                actions.Add(neuter);
            }
            else if (parent is not null && e.Get<ParticleEmitterMarker>() is { } emitterMarker)
            {
                var particleData = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Rm2
                    .GetItem<PS2AnyParticleData>((uint)TwinConstants.LEVEL_PARTICLES_ITEM);
                if (particleData is not null)
                {
                    var add = new ParticleEmitterAddAction { Parent = parent, Entity = e, List = particleData.ParticleEmitters, Item = emitterMarker.Source };
                    add.Undo();
                    actions.Add(new InverseEditAction { Inner = add });
                }
            }
            else if (parent is not null && e.Get<CrashEngine.Importer.LoadWallMarker>() is { } wallMarker)
            {
                DeleteLoadLinkPair(wallMarker.Source, actions);
            }
            else if (parent is not null && e.Get<CrashEngine.Importer.LinkedSceneryLink>() is { } sceneryLink)
            {
                DeleteLoadLinkPair(sceneryLink.Source, actions);
            }
            else if (parent is not null && e.Get<AiPositionMarker>() is { } aiMarker)
            {
                DeleteAiPosition(e, parent, aiMarker, chunkSourceForPlaceholder, actions);
                aiPosDelCount++;
            }
            // Amedo 2026-09-19
            else if (parent is not null && e.Get<CrashEngine.Importer.SceneryLightMarker>() is { } lightMarker && lightMarker.Source is not null)
            {
                DeleteSceneryLight(e, parent, lightMarker, chunkSourceForPlaceholder);
                aiPosDelCount++;
            }
            else if (e.Has<PositionMarker>())
            {
                _browser.Log("Delete: (non-AI) Position markers aren't deletable yet -- they may be " +
                              "referenced by an instance's Positions list by numeric id.");
            }
            else if (_cubes.FirstOrDefault(c => c.Ent == e) is { } cube)
            {
                if (!_pendingDel.Contains(cube)) _pendingDel.Add(cube);
                cubeCount++;
            }
            else if (_importedRoots.Contains(e))
            {
                if (!_pendingDelImports.Contains(e)) _pendingDelImports.Add(e);
                importCount++;
            }
        }

        if (actions.Count == 0 && cubeCount == 0 && importCount == 0 && aiPosDelCount == 0)
        {
            _browser.Log("Delete: nothing deletable in the selection.");
            return;
        }
        if (aiPosDelCount > 0)
            _browser.Log($"Delete: removed {aiPosDelCount} AI position(s) + any paths that referenced them. Save Chunk to persist.");

        var chunkSourceForCleanup = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>();
        if (chunkSourceForCleanup?.MeshTables is not null)
            RemoveUnusedTransplants(chunkSourceForCleanup, deletedTransplantObjIds);

        if (actions.Count > 0)
            _undoStack.Push(actions.Count == 1 ? actions[0] : new CompositeEditAction { Actions = actions });

        _selectedSet.Clear();
        _selected = null;
        SyncSelSnapshot(); // Amedo 2026-09-20
        _browser.Log($"Deleted {actions.Count + cubeCount + importCount} object(s)" +
                     (actions.Count > 0 && (cubeCount + importCount) > 0 ? " (mixed — only PS2 objects are undo-able)." : "."));
    }

    private static void DeleteInstance(Entity e, Entity parent, InstanceData data, ushort? placeholderObjectId, List<IEditAction> actions)
    {
        if (data.IsUserAdded)
        {
            var add = new InstanceAddAction { Parent = parent, Entity = e, Section = data.Section, Item = (ITwinItem)data.Source };
            add.Undo();
            actions.Add(new InverseEditAction { Inner = add });
        }
        else
        {
            var neuter = new InstanceNeuterAction { Entity = e, Parent = parent, Item = data.Source, PlaceholderObjectId = placeholderObjectId };
            neuter.Redo();
            actions.Add(neuter);
        }
    }

    private bool DeleteSceneryTile(Entity e, Entity parent, SceneryTile tile, ChunkSource? chunkSourceForPlaceholder, List<IEditAction> actions)
    {
        var bbox = FindTileBoundingBox(tile);
        if (bbox is null) return false;
        var add = new SceneryAddAction
        {
            Parent = parent, Entity = e, Node = tile.Node,
            IsLod = tile.IsLod, SourceId = tile.SourceId, Matrix = tile.Source, BoundingBox = bbox,
        };
        add.Undo();

        // Amedo 2026-09-20
        var csBake = chunkSourceForPlaceholder;
        if (csBake?.Sm2 is not null && csBake.SceneryBakes.TryGetValue(tile.SourceId, out var bakeRec))
        {
            MeshDecoder.RemoveSceneryBake(csBake.Sm2, bakeRec, csBake.SceneryTexCache);
            csBake.SceneryBakes.Remove(tile.SourceId);
            _browser.Log($"Deleted imported scenery (mesh 0x{tile.SourceId:X}) and freed its baked graphics " +
                          $"({bakeRec.ModelIds.Count} model, {bakeRec.MaterialIds.Count} material, {bakeRec.TextureIds.Count} texture) — chunk stays clean.");
            return true;
        }
        actions.Add(new InverseEditAction { Inner = add });
        return false;
    }

    private static void DeleteAiPosition(Entity e, Entity parent, AiPositionMarker aiMarker, ChunkSource? chunkSourceForPlaceholder, List<IEditAction> actions)
    {
        var layouts = new List<BaseTwinSection>();
        if (chunkSourceForPlaceholder?.Rm2 is { } rm2)
            for (int lid = TwinConstants.LEVEL_LAYOUT_1_SECTION; lid <= TwinConstants.LEVEL_LAYOUT_8_SECTION; lid++)
                if (rm2.GetItem<BaseTwinSection>((uint)lid) is { } layout) layouts.Add(layout);
        var delete = new AiPositionDeleteAction { Parent = parent, Entity = e, Position = aiMarker.Source, Layouts = layouts };
        delete.Redo();
        actions.Add(delete);
    }

    private static void DeleteSceneryLight(Entity e, Entity parent, CrashEngine.Importer.SceneryLightMarker lightMarker, ChunkSource? chunkSourceForPlaceholder)
    {
        var scenery = chunkSourceForPlaceholder?.Sm2
            ?.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery>((uint)TwinConstants.SCENERY_SECENERY_ITEM);
        if (scenery is not null)
        {
            if (lightMarker.IsNegative && lightMarker.Source is Twinsanity.TwinsanityInterchange.Common.Lights.NegativeLight nl)
                scenery.NegativeLights.Remove(nl);
            else if (!lightMarker.IsNegative && lightMarker.Source is Twinsanity.TwinsanityInterchange.Common.Lights.PointLight pl)
                scenery.PointLights.Remove(pl);
        }
        parent.RemoveChild(e);
    }

    private void RemoveUnusedTransplants(ChunkSource chunkSourceForCleanup, HashSet<uint> deletedTransplantObjIds)
    {
        foreach (var objId in deletedTransplantObjIds)
        {
            if (!chunkSourceForCleanup.Transplants.TryGetValue(objId, out var rec)) continue;
            bool stillUsed = FlatEntities().Any(fe => fe.Get<InstanceData>()?.ObjectId == objId);
            if (stillUsed) continue;
            MeshDecoder.RemoveTransplant(chunkSourceForCleanup.Rm2,
                chunkSourceForCleanup.MeshTables, chunkSourceForCleanup.TexCache, rec);
            chunkSourceForCleanup.Transplants.Remove(objId);
            _browser.Log($"Removed transplant data for object 0x{objId:X4} (last instance deleted) — chunk is clean.");
        }

        foreach (var objId in deletedTransplantObjIds)
        {
            if (!chunkSourceForCleanup.FullTransplants.TryGetValue(objId, out var fullRec)) continue;
            bool stillUsed = FlatEntities().Any(fe => fe.Get<InstanceData>()?.ObjectId == objId);
            if (stillUsed) continue;
            MeshDecoder.RemoveFullTransplant(chunkSourceForCleanup.Rm2,
                chunkSourceForCleanup.MeshTables, chunkSourceForCleanup.TexCache, fullRec);
            chunkSourceForCleanup.FullTransplants.Remove(objId);
            _browser.Log($"Removed full transplant data for object 0x{objId:X4} (last instance deleted) — " +
                          "object graph + scripts + graphics all cleaned up.");
        }
    }

    private void DeleteLoadLinkPair(TwinChunkLink link, List<IEditAction> actions)
    {
        var linkItem = Roots.FirstOrDefault(r => r.Has<ChunkSource>())?.Get<ChunkSource>()?.Sm2
            ?.GetItem<PS2AnyLink>((uint)TwinConstants.SCENERY_LINK_ITEM);
        if (linkItem is null) return;

        var pairs = new List<(Entity Parent, Entity Entity)>();
        foreach (var ent in FlatEntities())
        {
            if (ent.Parent is null) continue;
            if (ReferenceEquals(ent.Get<CrashEngine.Importer.LoadWallMarker>()?.Source, link) ||
                ReferenceEquals(ent.Get<CrashEngine.Importer.LinkedSceneryLink>()?.Source, link))
                pairs.Add((ent.Parent, ent));
        }
        if (pairs.Count == 0) return;

        var action = new LoadLinkPairDeleteAction { Entities = pairs, LinkItem = linkItem, Link = link };
        action.Redo();
        actions.Add(action);
    }
}
