using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using SysVec3 = System.Numerics.Vector3;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;

namespace CrashEngine.Importer;

public static partial class MeshDecoder
{
    private static uint CloneMeshChainCached(BaseTwinSection meshSec, BaseTwinSection modelSec, uint oldId, Dictionary<uint, uint> remap)
    {
        if (remap.TryGetValue(oldId, out var already)) return already;
        var rm = meshSec.GetItem<PS2AnyRigidModel>(oldId);
        var model = rm is not null ? modelSec.GetItem<PS2AnyModel>(rm.Model) : null;
        if (rm is null || model is null) { remap[oldId] = oldId; return oldId; }

        var newModel = CloneItem(model);
        uint newModelId = NewGraphicsLocalId(modelSec, meshSec);
        newModel.SetID(newModelId);
        foreach (var subItem in newModel.SubModels)
            if (subItem is Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel newSub)
                newSub.CalculateData();
        modelSec.AddItem(newModel);

        var newRm = CloneItem(rm);
        uint newMeshId = NewGraphicsLocalId(meshSec, modelSec);
        newRm.SetID(newMeshId);
        newRm.Model = newModelId;
        meshSec.AddItem(newRm);

        remap[oldId] = newMeshId;
        return newMeshId;
    }

    public static bool SetSceneryMeshIdManually(PS2AnyTwinsanitySM2 sm2, uint oldId, uint desiredNewMeshId, out string error)
    {
        error = "";
        var gfx = sm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        var meshSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        var modelSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        if (meshSec is null || modelSec is null) { error = "this level has no scenery graphics data"; return false; }
        if (meshSec.ContainsItem(desiredNewMeshId) || modelSec.ContainsItem(desiredNewMeshId))
        { error = $"id 0x{desiredNewMeshId:X8} is already used by something else in this level's own graphics data"; return false; }

        var rm = meshSec.GetItem<PS2AnyRigidModel>(oldId);
        var model = rm is not null ? modelSec.GetItem<PS2AnyModel>(rm.Model) : null;
        if (rm is null || model is null) { error = $"source mesh 0x{oldId:X8} not found"; return false; }

        var newModel = CloneItem(model);
        uint newModelId = NewGraphicsLocalId(modelSec, meshSec);
        newModel.SetID(newModelId);
        foreach (var subItem in newModel.SubModels)
            if (subItem is Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel newSub)
                newSub.CalculateData();
        modelSec.AddItem(newModel);

        var newRm = CloneItem(rm);
        newRm.SetID(desiredNewMeshId);
        newRm.Model = newModelId;
        meshSec.AddItem(newRm);
        return true;
    }

    public static int PrivatizeSharedSceneryMeshIds(PS2AnyTwinsanitySM2 sm2, string extractedRoot,
        out Dictionary<uint, uint> remapOut, out string log)
    {
        var logSb = new System.Text.StringBuilder();
        log = "";
        remapOut = new Dictionary<uint, uint>();

        var gfx = sm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        var meshSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        var modelSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var lodSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_LODS_SECTION);
        var scenery = sm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
        var links = sm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2.PS2AnyLink>((uint)Constants.SCENERY_LINK_ITEM);
        if (gfx is null || meshSec is null || modelSec is null || scenery is null || links is null || links.LinksList.Count == 0)
        {
            log = "no scenery/graphics/link data -- nothing to privatize.";
            return 0;
        }

        var foreignIds = new HashSet<uint>();
        foreach (var link in links.LinksList)
        {
            try
            {
                using var pkg = PackageReader.Open(extractedRoot);
                using var stream = pkg.OpenByPath($"{link.Path}.sm2");
                if (stream is null) continue;
                var foreignSm2 = new PS2AnyTwinsanitySM2();
                using var reader = new BinaryReader(stream);
                foreignSm2.Read(reader, (int)stream.Length);
                var foreignGfx = foreignSm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
                var foreignMeshSec = foreignGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
                if (foreignMeshSec is null) continue;
                for (int i = 0; i < foreignMeshSec.GetItemsAmount(); i++)
                    if (foreignMeshSec.GetItem(i) is BaseTwinItem bi) foreignIds.Add(bi.GetID());
            }
            catch (Exception ex) { logSb.AppendLine($"  (couldn't check link '{link.Path}': {ex.Message})"); }
        }
        if (foreignIds.Count == 0)
        {
            log = "no linked level's mesh ids could be read -- nothing to privatize." + logSb;
            return 0;
        }

        var remap = new Dictionary<uint, uint>();
        uint GetOrClonePrivate(uint oldId) => CloneMeshChainCached(meshSec, modelSec, oldId, remap);

        int placementsRepointed = 0;
        foreach (var entry in scenery.Sceneries)
        {
            for (int i = 0; i < entry.MeshIDs.Count; i++)
            {
                if (!foreignIds.Contains(entry.MeshIDs[i])) continue;
                entry.MeshIDs[i] = GetOrClonePrivate(entry.MeshIDs[i]);
                placementsRepointed++;
            }
            if (lodSec is null) continue;
            foreach (var lodId in entry.LodIDs)
            {
                if (lodSec.GetItem<PS2AnyLOD>(lodId) is not { } lod) continue;
                for (int k = 0; k < lod.Meshes.Count; k++)
                {
                    if (!foreignIds.Contains(lod.Meshes[k])) continue;
                    lod.Meshes[k] = GetOrClonePrivate(lod.Meshes[k]);
                    placementsRepointed++;
                }
            }
        }

        remapOut = remap;
        log = remap.Count == 0
            ? "no shared mesh ids found in this level's own scenery placements -- nothing to privatize." + logSb
            : $"privatized {remap.Count} shared mesh id(s) ({placementsRepointed} placement reference(s) repointed) -- " +
              "this level's scenery colors can no longer visibly leak into (or be affected by) a directly linked level." + logSb;
        return remap.Count;
    }

    public static uint? PrivatizeInstanceGeometryForScale(GL gl, PS2AnyTwinsanityRM2 rm2,
        object tablesHandle, Dictionary<uint, Texture2D> texCache, uint objectId, out string log)
    {
        log = "";
        if (tablesHandle is not RmTables tables) { log = "tables handle invalid"; return null; }
        if (!tables.Objects.TryGetValue(objectId, out var srcObj))
        { log = $"ObjectId 0x{objectId:X4} not found"; return null; }

        var code     = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var objSec   = code?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var ogiSec   = code?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var gfx      = rm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var rmSec    = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var modelSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var meshSec  = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        if (objSec is null || ogiSec is null || rmSec is null || modelSec is null || gfx is null)
        { log = "chunk is missing a required CODE/GRAPHICS section"; return null; }

        var materialsById = BuildMaterialLookup(gfx);
        if (tables.GlobalSource?.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION) is { } globalGfx) // Amedo
            foreach (var kv in BuildMaterialLookup(globalGfx)) materialsById.TryAdd(kv.Key, kv.Value);

        var newObj  = CloneItem(srcObj);
        uint newObjId;
        do newObjId = NewCodeLocalId(objSec); while (tables.Objects.ContainsKey(newObjId)); // Amedo
        newObj.SetID(newObjId);

        var rmIdMap = new Dictionary<uint, uint>();
        bool anyMesh = false;

        for (int i = 0; i < newObj.OGISlots.Count; i++)
        {
            var oldOgiId = newObj.OGISlots[i];
            if (oldOgiId == 0xFFFF) continue;
            if (!tables.OGIs.TryGetValue(oldOgiId, out var srcOgi)) continue;

            var newOgi = CloneItem(srcOgi);
            ushort newOgiId;
            do newOgiId = NewCodeLocalId(ogiSec); while (tables.OGIs.ContainsKey(newOgiId)); // Amedo
            newOgi.SetID(newOgiId);

            for (int r = 0; r < newOgi.RigidModelIds.Count; r++)
            {
                var oldRmId = newOgi.RigidModelIds[r];
                if (rmIdMap.TryGetValue(oldRmId, out var already)) { newOgi.RigidModelIds[r] = already; continue; }
                if (!tables.RigidModels.TryGetValue(oldRmId, out var srcRm)) continue;

                var newRm = CloneItem(srcRm);
                uint newRmId;
                do newRmId = NewGraphicsLocalId(rmSec, meshSec); while (tables.RigidModels.ContainsKey(newRmId) || tables.ModelGpu.ContainsKey(newRmId)); // Amedo
                newRm.SetID(newRmId);

                PS2AnyModel? newModel = null;
                if (tables.Models.TryGetValue(srcRm.Model, out var srcModelItem) && srcModelItem is PS2AnyModel srcModel)
                {
                    newModel = CloneItem(srcModel);
                    uint newModelId;
                    do newModelId = NewGraphicsLocalId(modelSec); while (tables.Models.ContainsKey(newModelId)); // Amedo
                    newModel.SetID(newModelId);
                    modelSec.AddItem(newModel);
                    tables.Models[newModelId] = newModel;
                    newRm.Model = newModelId;
                }

                rmSec.AddItem(newRm);
                tables.RigidModels[newRmId] = newRm;
                rmIdMap[oldRmId] = newRmId;
                newOgi.RigidModelIds[r] = newRmId;

                if (newModel is not null)
                {
                    var subMats = SubMaterials(newRm.Materials, materialsById, texCache);
                    var gpuList = DecodeModel(gl, newModel, subMats);
                    if (gpuList.Count > 0) { tables.ModelGpu[newRmId] = gpuList; anyMesh = true; }
                }
            }

            ogiSec.AddItem(newOgi);
            tables.OGIs[newOgiId] = newOgi;
            newObj.OGISlots[i] = newOgiId;
        }

        if (!anyMesh)
        { log = $"ObjectId 0x{objectId:X4} has no rigid-model mesh to privatize (nothing to protect)"; return null; }

        objSec.AddItem(newObj);
        tables.Objects[newObjId] = newObj;

        log = $"Privatized ObjectId 0x{objectId:X4} -> own private ObjectId 0x{newObjId:X4} " +
              "(fresh OGI/RigidModel/Model copy) — this placement's scale can no longer affect " +
              "the original object or any other instance sharing it.";
        return newObjId;
    }

    public static HashSet<uint> BakeInstanceScale(GL gl, object? tablesHandle, uint objectId, SysVec3 scale)
    {
        var touched = new HashSet<uint>();
        if (tablesHandle is not RmTables tables) return touched;
        if (SysVec3.Distance(scale, SysVec3.One) < 0.0001f) return touched;
        if (!tables.Objects.TryGetValue(objectId, out var obj)) return touched;

        foreach (var ogiId in obj.OGISlots)
        {
            if (ogiId == 0xFFFF) continue;
            if (!tables.OGIs.TryGetValue(ogiId, out var ogi)) continue;

            ogi.BoundingBox[0] = new TwinVec4(ogi.BoundingBox[0].X * scale.X, ogi.BoundingBox[0].Y * scale.Y, ogi.BoundingBox[0].Z * scale.Z, ogi.BoundingBox[0].W);
            ogi.BoundingBox[1] = new TwinVec4(ogi.BoundingBox[1].X * scale.X, ogi.BoundingBox[1].Y * scale.Y, ogi.BoundingBox[1].Z * scale.Z, ogi.BoundingBox[1].W);

            foreach (var coll in ogi.Collisions)
                for (int pi = 0; pi < coll.BoundingBoxPoints.Count; pi++)
                {
                    var p = coll.BoundingBoxPoints[pi];
                    coll.BoundingBoxPoints[pi] = new TwinVec4(p.X * scale.X, p.Y * scale.Y, p.Z * scale.Z, p.W);
                }

            foreach (var rmId in ogi.RigidModelIds)
            {
                if (!touched.Add(rmId)) continue;
                if (!tables.RigidModels.TryGetValue(rmId, out var rm)) continue;
                if (!tables.Models.TryGetValue(rm.Model, out var model)) continue;

                foreach (var sub in model.SubModels)
                {
                    if (sub.Vertexes is null)
                    {
                        try { sub.CalculateData(); } catch { continue; }
                    }
                    if (sub.Vertexes is null) continue;

                    for (int vi = 0; vi < sub.Vertexes.Count; vi++)
                    {
                        var v = sub.Vertexes[vi];
                        sub.Vertexes[vi] = new TwinVec4(v.X * scale.X, v.Y * scale.Y, v.Z * scale.Z, v.W);
                    }

                    sub.Compile();
                }

                if (tables.ModelGpu.TryGetValue(rmId, out var gpuList))
                {
                    for (int si = 0; si < gpuList.Count && si < model.SubModels.Count; si++)
                    {
                        var sub = model.SubModels[si];
                        if (sub.Vertexes is null) continue;
                        var triVerts = BuildTriangleListVerts(sub);
                        var gpuMesh  = gpuList[si].Item1;
                        if (triVerts.Length != gpuMesh.VertexCount) continue; // Amedo
                        gpuMesh.UpdateVertices(gl, triVerts);
                    }
                }
            }
        }
        return touched;
    }
}
