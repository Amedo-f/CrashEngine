using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using SysVec2 = System.Numerics.Vector2;
using SysVec3 = System.Numerics.Vector3;
using SysVec4 = System.Numerics.Vector4;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinSceneryBaseType = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryBaseType;
using TwinSceneryLeaf = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryLeaf;
using TwinSceneryNode = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryNode;
using TwinSceneryRoot = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryRoot;
using ITwinScenery = Twinsanity.TwinsanityInterchange.Interfaces.Items.SM.ITwinScenery;
using TwinAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation;
using TwinMorphAnimation = Twinsanity.TwinsanityInterchange.Common.Animation.TwinMorphAnimation;
using TwinJointSettings = Twinsanity.TwinsanityInterchange.Common.Animation.JointSettings;
using TwinTransformType = Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType;
using System.Numerics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace CrashEngine.Importer;

public static partial class MeshDecoder
{
    public readonly record struct SceneryTileSummary(int Index, int MeshCount, int LodMeshCount, uint FirstTextureId);

    public static List<SceneryTileSummary> GetSceneryTileList(PS2AnyTwinsanitySM2 sm2)
    {
        var result = new List<SceneryTileSummary>();
        var scenery = sm2.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
        if (scenery is null) return result;
        var gfx = sm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        var meshSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        var rmSec   = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var matSec  = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var lodSec  = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_LODS_SECTION);

        for (int i = 0; i < scenery.Sceneries.Count; i++)
        {
            var leaf = scenery.Sceneries[i];
            if (leaf is TwinSceneryRoot || (leaf.MeshIDs.Count == 0 && leaf.LodIDs.Count == 0)) continue;
            uint firstTex = 0;
            uint firstMeshLookupId = leaf.MeshIDs.Count > 0 ? leaf.MeshIDs[0] : 0;
            if (leaf.MeshIDs.Count == 0 && leaf.LodIDs.Count > 0)
            {
                var lod = lodSec?.GetItem<PS2AnyLOD>(leaf.LodIDs[0]);
                if (lod is { Meshes.Count: > 0 }) firstMeshLookupId = lod.Meshes[0];
            }
            var rm = meshSec?.GetItem<PS2AnyRigidModel>(firstMeshLookupId) ?? rmSec?.GetItem<PS2AnyRigidModel>(firstMeshLookupId);
            if (rm is { Materials.Count: > 0 })
            {
                var mat = matSec?.GetItem<PS2AnyMaterial>(rm.Materials[0]);
                if (mat is { Shaders.Count: > 0 }) firstTex = mat.Shaders[0].TextureId;
            }
            result.Add(new SceneryTileSummary(i, leaf.MeshIDs.Count, leaf.LodIDs.Count, firstTex));
        }
        return result;
    }

    public static List<(GpuMesh Mesh, Material Mat, Matrix4x4 Model)>? DecodeSceneryTilePreview(
        GL gl, PS2AnyTwinsanitySM2 sm2, Dictionary<uint, Texture2D> texCache, int tileIndex)
    {
        var scenery = sm2.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
        if (scenery is null || tileIndex < 0 || tileIndex >= scenery.Sceneries.Count) return null;
        var leaf = scenery.Sceneries[tileIndex];
        if (leaf is TwinSceneryRoot) return null;
        if (leaf.MeshIDs.Count == 0 && leaf.LodIDs.Count == 0) return null;

        var gfx = sm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        if (gfx is null) return null;
        var meshSec = gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        var rmSec   = gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var lodSec  = gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_LODS_SECTION);
        var texSec  = gfx.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        var modelById     = BuildModelLookup(gfx);
        var materialsById = BuildMaterialLookup(gfx);

        var result = new List<(GpuMesh, Material, Matrix4x4)>();

        void DecodeIdList(List<uint> ids, List<Matrix4> matrices, bool viaLod)
        {
            for (int mi = 0; mi < ids.Count; mi++)
            {
                uint meshId = ids[mi];
                if (viaLod)
                {
                    var lod = lodSec?.GetItem<PS2AnyLOD>(meshId);
                    if (lod is null || lod.Meshes.Count == 0) continue;
                    meshId = lod.Meshes[0];
                }
                var rm = meshSec?.GetItem<PS2AnyRigidModel>(meshId) ?? rmSec?.GetItem<PS2AnyRigidModel>(meshId);
                if (rm is null || !modelById.TryGetValue(rm.Model, out var model)) continue;

                var subMats = new List<(Texture2D? Tex, TwinShader? Shader)>();
                foreach (var matId in rm.Materials)
                {
                    Texture2D?  tex    = null;
                    TwinShader? shader = null;
                    if (materialsById.TryGetValue(matId, out var mat))
                    {
                        foreach (var sh in mat.Shaders)
                        {
                            shader ??= sh;
                            if (!texCache.TryGetValue(sh.TextureId, out tex) || tex is null)
                            {
                                var srcTex = texSec?.GetItem<PS2AnyTexture>(sh.TextureId);
                                if (srcTex is not null)
                                {
                                    tex = ChunkImporter.DecodeTexture(gl, srcTex);
                                    if (tex is not null) texCache[sh.TextureId] = tex;
                                }
                            }
                            if (tex is not null) break;
                        }
                        foreach (var sh in mat.Shaders.Skip(1)) // Amedo 2026-09-26
                            if (!texCache.ContainsKey(sh.TextureId) && texSec?.GetItem<PS2AnyTexture>(sh.TextureId) is { } upperTex
                                && ChunkImporter.DecodeTexture(gl, upperTex) is { } upperGpu)
                                texCache[sh.TextureId] = upperGpu;
                        RegisterUpperLayers(mat, texCache); // Amedo 2026-09-26
                    }
                    subMats.Add((tex, shader));
                }

                var gpuList = DecodeModel(gl, model, subMats);
                Matrix4x4 modelMat = mi < matrices.Count ? TwinMatToSys(matrices[mi]) : Matrix4x4.Identity;
                foreach (var (mesh, mat) in gpuList) result.Add((mesh, mat, modelMat));
            }
        }

        DecodeIdList(leaf.MeshIDs, leaf.MeshModelMatrices, viaLod: false);
        if (result.Count == 0) DecodeIdList(leaf.LodIDs, leaf.LodModelMatrices, viaLod: true);
        return result.Count > 0 ? result : null;
    }

    public static List<(GpuMesh Mesh, Material Mat, Matrix4x4 Model)>? DecodeSceneryTilesPreview(
        GL gl, PS2AnyTwinsanitySM2 sm2, Dictionary<uint, Texture2D> texCache, IReadOnlyList<int> tileIndices)
    {
        var result = new List<(GpuMesh, Material, Matrix4x4)>();
        foreach (var idx in tileIndices)
        {
            var parts = DecodeSceneryTilePreview(gl, sm2, texCache, idx);
            if (parts is not null) result.AddRange(parts);
        }
        return result.Count > 0 ? result : null;
    }

    private static T EnsureGfxSubsection<T>(PS2AnyGraphicsSection gfx, int id) where T : BaseTwinSection, new()
    {
        var existing = gfx.GetItem<T>((uint)id);
        if (existing is not null) return existing;
        var sec = new T();
        sec.SetID((uint)id);
        gfx.AddItem(sec);
        return sec;
    }

    public static bool TransplantSceneryTiles(GL gl,
        PS2AnyTwinsanitySM2 destSm2, Dictionary<uint, Texture2D> destTexCache,
        PS2AnyTwinsanitySM2 sourceSm2, List<int> tileIndices, out string log,
        SysVec3? targetPosition = null,
        Dictionary<int, HashSet<uint>>? onlyMeshIds = null,
        List<uint>? newSourceIds = null)
    {
        newSourceIds?.Clear();
        var destGfx = destSm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        var srcGfx  = sourceSm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        if (destGfx is null || srcGfx is null) { log = "missing GRAPHICS section on one side (TransplantSceneryTiles)"; return false; }

        var srcScenery = sourceSm2.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
        var destScenery = destSm2.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
        if (srcScenery is null || destScenery is null) { log = "missing SCENERY item on one side (TransplantSceneryTiles)"; return false; }

        var destRmSec    = EnsureGfxSubsection<PS2AnyRigidModelsSection>(destGfx, Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var destMeshSec  = EnsureGfxSubsection<PS2AnyMeshesSection>(destGfx, Constants.GRAPHICS_MESHES_SECTION);
        var destModelSec = EnsureGfxSubsection<PS2AnyModelsSection>(destGfx, Constants.GRAPHICS_MODELS_SECTION);
        var destMatSec   = EnsureGfxSubsection<PS2AnyMaterialsSection>(destGfx, Constants.GRAPHICS_MATERIALS_SECTION);
        var destTexSec   = EnsureGfxSubsection<PS2AnyTexturesSection>(destGfx, Constants.GRAPHICS_TEXTURES_SECTION);
        var destLodSec   = EnsureGfxSubsection<PS2AnyLODsSection>(destGfx, Constants.GRAPHICS_LODS_SECTION);
        var srcRmSec     = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var srcMeshSec   = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        var srcModelSec  = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var srcMatSec    = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var srcTexSec    = srcGfx.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        var srcLodSec    = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_LODS_SECTION);

        if (srcRmSec is null || srcMeshSec is null || srcModelSec is null || srcMatSec is null || srcTexSec is null)
        { log = "source scenery is missing a required GRAPHICS subsection"; return false; }

        static uint NewGraphicsId(BaseTwinSection section, params BaseTwinSection?[] alsoAvoid)
        {
            var rng = new Random();
            uint id;
            do { id = (uint)rng.Next(0x10000, 0x7FFFFFF); }
            while (id == 0 || section.ContainsItem(id) || alsoAvoid.Any(s => s?.ContainsItem(id) == true));
            return id;
        }

        var texIdMap = new Dictionary<uint, uint>();
        uint CloneTexture(uint srcTexId)
        {
            if (texIdMap.TryGetValue(srcTexId, out var already)) return already;
            var srcTex = srcTexSec?.GetItem<PS2AnyTexture>(srcTexId);
            if (srcTex is null) { texIdMap[srcTexId] = srcTexId; return srcTexId; }
            var clone = CloneItem(srcTex);
            uint newId = NewGraphicsId(destTexSec);
            clone.SetID(newId);
            destTexSec.AddItem(clone);
            texIdMap[srcTexId] = newId;
            var t = ChunkImporter.DecodeTexture(gl, clone);
            if (t is not null) destTexCache[newId] = t;
            return newId;
        }

        var matIdMap = new Dictionary<uint, uint>();
        uint CloneMaterial(uint srcMatId)
        {
            if (srcMatId == 0) return 0;
            if (matIdMap.TryGetValue(srcMatId, out var already)) return already;
            var srcMat = srcMatSec?.GetItem<PS2AnyMaterial>(srcMatId);
            if (srcMat is null) return 0;
            var clone = CloneItem(srcMat);
            uint newId = NewGraphicsId(destMatSec);
            clone.SetID(newId);
            foreach (var sh in clone.Shaders) sh.TextureId = CloneTexture(sh.TextureId);
            destMatSec.AddItem(clone);
            matIdMap[srcMatId] = newId;
            return newId;
        }

        var modelIdMap = new Dictionary<uint, uint>();
        uint CloneModel(uint srcModelId)
        {
            if (modelIdMap.TryGetValue(srcModelId, out var already)) return already;
            var srcModel = srcModelSec?.GetItem<PS2AnyModel>(srcModelId);
            if (srcModel is null) return 0;
            var clone = CloneItem(srcModel);
            uint newId = NewGraphicsId(destModelSec);
            clone.SetID(newId);
            destModelSec.AddItem(clone);
            modelIdMap[srcModelId] = newId;
            return newId;
        }

        uint? CloneRigidModel(uint srcId)
        {
            BaseTwinSection? srcSec = null; BaseTwinSection? destSec = null;
            var rm = srcMeshSec?.GetItem<PS2AnyRigidModel>(srcId);
            if (rm is not null) { srcSec = srcMeshSec; destSec = destMeshSec; }
            else
            {
                rm = srcRmSec?.GetItem<PS2AnyRigidModel>(srcId);
                if (rm is not null) { srcSec = srcRmSec; destSec = destRmSec; }
            }
            if (rm is null || srcSec is null || destSec is null) return null;

            var clone = CloneItem(rm);
            uint newId = NewGraphicsId(destSec, destMeshSec, destRmSec);
            clone.SetID(newId);
            clone.Model = CloneModel(clone.Model);
            for (int i = 0; i < clone.Materials.Count; i++)
                clone.Materials[i] = CloneMaterial(clone.Materials[i]);
            destSec.AddItem(clone);
            return newId;
        }

        var lodIdMap = new Dictionary<uint, uint>();
        uint? CloneLod(uint srcLodId)
        {
            if (lodIdMap.TryGetValue(srcLodId, out var already)) return already;
            var srcLod = srcLodSec?.GetItem<PS2AnyLOD>(srcLodId);
            if (srcLod is null) return null;

            var clone = CloneItem(srcLod);
            clone.Meshes.Clear();
            foreach (var m in srcLod.Meshes)
            {
                var newMeshId = CloneRigidModel(m);
                if (newMeshId is not null) clone.Meshes.Add(newMeshId.Value);
            }
            if (clone.Meshes.Count == 0) return null;

            uint newId = NewGraphicsId(destLodSec, destMeshSec, destRmSec);
            clone.SetID(newId);
            destLodSec.AddItem(clone);
            lodIdMap[srcLodId] = newId;
            return newId;
        }

        static TwinMat4 CloneMatrix(TwinMat4 m) => new TwinMat4
        {
            Column1 = new TwinVec4(m.Column1.X, m.Column1.Y, m.Column1.Z, m.Column1.W),
            Column2 = new TwinVec4(m.Column2.X, m.Column2.Y, m.Column2.Z, m.Column2.W),
            Column3 = new TwinVec4(m.Column3.X, m.Column3.Y, m.Column3.Z, m.Column3.W),
            Column4 = new TwinVec4(m.Column4.X, m.Column4.Y, m.Column4.Z, m.Column4.W),
        };
        static TwinVec4[] CloneBB(TwinVec4[] bb) => new[]
        {
            new TwinVec4(bb[0].X, bb[0].Y, bb[0].Z, bb[0].W),
            new TwinVec4(bb[1].X, bb[1].Y, bb[1].Z, bb[1].W),
        };
        static TwinVec4 CloneVec(TwinVec4 v) => new TwinVec4(v.X, v.Y, v.Z, v.W);

        int addedTiles = 0, addedMeshes = 0, skippedTiles = 0;
        var unresolvedMeshIds = new List<(int SourceIndex, uint MeshId)>();
        var newLeaves = new List<TwinSceneryLeaf>();
        foreach (var idx in tileIndices)
        {
            if (idx < 0 || idx >= srcScenery.Sceneries.Count || srcScenery.Sceneries[idx] is TwinSceneryRoot)
            { skippedTiles++; continue; }
            var srcLeaf = srcScenery.Sceneries[idx];

            var newLeaf = new TwinSceneryLeaf
            {
                UnkVec1 = CloneVec(srcLeaf.UnkVec1), UnkVec2 = CloneVec(srcLeaf.UnkVec2),
                UnkVec3 = CloneVec(srcLeaf.UnkVec3), UnkVec4 = CloneVec(srcLeaf.UnkVec4),
            };
            Array.Copy(srcLeaf.LightsEnabler, newLeaf.LightsEnabler, srcLeaf.LightsEnabler.Length);

            var allowedIds = onlyMeshIds is not null && onlyMeshIds.TryGetValue(idx, out var allowed) ? allowed : null;
            for (int i = 0; i < srcLeaf.MeshIDs.Count; i++)
            {
                if (allowedIds is not null && !allowedIds.Contains(srcLeaf.MeshIDs[i])) continue;
                var newId = CloneRigidModel(srcLeaf.MeshIDs[i]);
                if (newId is null) { unresolvedMeshIds.Add((idx, srcLeaf.MeshIDs[i])); continue; }
                newLeaf.MeshIDs.Add(newId.Value);
                newLeaf.MeshModelMatrices.Add(CloneMatrix(srcLeaf.MeshModelMatrices[i]));
                newLeaf.BoundingBoxes.Add(CloneBB(srcLeaf.BoundingBoxes[i]));
                addedMeshes++;
            }

            for (int i = 0; i < srcLeaf.LodIDs.Count; i++)
            {
                if (allowedIds is not null && !allowedIds.Contains(srcLeaf.LodIDs[i])) continue;
                var newLodId = CloneLod(srcLeaf.LodIDs[i]);
                if (newLodId is null) { unresolvedMeshIds.Add((idx, srcLeaf.LodIDs[i])); continue; }
                newLeaf.LodIDs.Add(newLodId.Value);
                newLeaf.LodModelMatrices.Add(CloneMatrix(srcLeaf.LodModelMatrices[i]));
                newLeaf.BoundingBoxes.Add(CloneBB(srcLeaf.BoundingBoxes[srcLeaf.MeshIDs.Count + i]));
                addedMeshes++;
            }

            if (newLeaf.MeshIDs.Count == 0 && newLeaf.LodIDs.Count == 0) { skippedTiles++; continue; }

            newLeaves.Add(newLeaf);
            addedTiles++;
        }

        if (targetPosition is { } target && newLeaves.Count > 0)
        {
            SysVec3 centroid = SysVec3.Zero;
            int counted = 0;
            foreach (var l in newLeaves)
            {
                var mats = l.MeshModelMatrices.Count > 0 ? l.MeshModelMatrices : l.LodModelMatrices;
                if (mats.Count > 0)
                { var c4 = mats[0].Column4; centroid += new SysVec3(c4.X, c4.Y, c4.Z); counted++; }
            }
            if (counted > 0)
            {
                centroid /= counted;
                var delta = target - centroid;
                void Shift(TwinMat4 m) =>
                    m.Column4 = new TwinVec4(m.Column4.X + delta.X, m.Column4.Y + delta.Y, m.Column4.Z + delta.Z, m.Column4.W);
                foreach (var l in newLeaves)
                {
                    foreach (var m in l.MeshModelMatrices) Shift(m);
                    foreach (var m in l.LodModelMatrices) Shift(m);
                }
            }
        }

        foreach (var l in newLeaves)
        {
            var lMin = new SysVec3(float.MaxValue, float.MaxValue, float.MaxValue);
            var lMax = new SysVec3(float.MinValue, float.MinValue, float.MinValue);
            bool lAny = false;
            for (int i = 0; i < l.MeshIDs.Count; i++)
            {
                var c4 = l.MeshModelMatrices[i].Column4;
                var pos = new SysVec3(c4.X, c4.Y, c4.Z);
                var box = l.BoundingBoxes[i];
                var a = pos + new SysVec3(box[0].X, box[0].Y, box[0].Z);
                var b = pos + new SysVec3(box[1].X, box[1].Y, box[1].Z);
                lMin = SysVec3.Min(lMin, SysVec3.Min(a, b));
                lMax = SysVec3.Max(lMax, SysVec3.Max(a, b));
                lAny = true;
            }
            if (lAny) ApplySceneryBounds(l, lMin, lMax);
        }

        if (newSourceIds is not null)
            foreach (var l in newLeaves)
            {
                newSourceIds.AddRange(l.MeshIDs);
                newSourceIds.AddRange(l.LodIDs);
            }

        var mergeMode = GraftLeavesIntoScenery(destScenery, newLeaves);

        log = $"Added {addedTiles} scenery tile(s), {addedMeshes} mesh(es) total, +{modelIdMap.Count} Model, " +
              $"+{matIdMap.Count} Material, +{texIdMap.Count} Texture, +{lodIdMap.Count} LOD ({mergeMode}).";
        if (skippedTiles > 0) log += $"  {skippedTiles} tile(s) skipped (not a real leaf tile, or no meshes resolved).";
        if (unresolvedMeshIds.Count > 0)
            log += "  Unresolved mesh id(s): " + string.Join(", ",
                unresolvedMeshIds.Select(u => $"0x{u.MeshId:X8}@srcIdx{u.SourceIndex}")) +
                $" (not found in source's own Meshes[{srcMeshSec.GetItemsAmount()} items]/RigidModels[{srcRmSec.GetItemsAmount()} items] sections).";
        return addedTiles > 0;
    }

    public static string GraftLeavesIntoScenery(PS2AnyScenery destScenery, List<TwinSceneryLeaf> newLeaves)
    {
        var oldRoot = destScenery.Sceneries.Count > 0 ? destScenery.Sceneries[0] as TwinSceneryRoot : null;

        static TwinSceneryNode DemoteRootToNode(TwinSceneryRoot r) => new TwinSceneryNode
        {
            SceneryTypes = r.SceneryTypes, MeshIDs = r.MeshIDs, LodIDs = r.LodIDs,
            BoundingBoxes = r.BoundingBoxes, MeshModelMatrices = r.MeshModelMatrices, LodModelMatrices = r.LodModelMatrices,
            UnkVec1 = r.UnkVec1, UnkVec2 = r.UnkVec2, UnkVec3 = r.UnkVec3, UnkVec4 = r.UnkVec4,
            LightsEnabler = r.LightsEnabler,
        };

        string mergeMode;
        if (newLeaves.Count == 0)
        {
            mergeMode = "no-op (nothing new)";
        }
        else if (oldRoot is null)
        {
            destScenery.Sceneries.Clear();
            destScenery.Sceneries.AddRange(RebuildSceneryTree(newLeaves));
            mergeMode = "full rebuild (destination was empty)";
        }
        else
        {
            TwinSceneryBaseType newContentTop;
            List<TwinSceneryBaseType> newContentFlattened;
            if (newLeaves.Count == 1)
            {
                newContentTop = newLeaves[0];
                newContentFlattened = new List<TwinSceneryBaseType> { newLeaves[0] };
            }
            else
            {
                var built = RebuildSceneryTree(newLeaves);
                var demotedNewTop = DemoteRootToNode((TwinSceneryRoot)built[0]);
                newContentFlattened = new List<TwinSceneryBaseType> { demotedNewTop };
                newContentFlattened.AddRange(built.Skip(1));
                newContentTop = demotedNewTop;
            }
            var newMin = new SysVec3(newContentTop.UnkVec2.X, newContentTop.UnkVec2.Y, newContentTop.UnkVec2.Z);
            var newMax = new SysVec3(newContentTop.UnkVec3.X, newContentTop.UnkVec3.Y, newContentTop.UnkVec3.Z);
            var oldMin = new SysVec3(oldRoot.UnkVec2.X, oldRoot.UnkVec2.Y, oldRoot.UnkVec2.Z);
            var oldMax = new SysVec3(oldRoot.UnkVec3.X, oldRoot.UnkVec3.Y, oldRoot.UnkVec3.Z);

            // Amedo 2026-09-21
            int lastUsed = -1;
            for (int i = 0; i < oldRoot.SceneryTypes.Length; i++)
                if (oldRoot.SceneryTypes[i] != ITwinScenery.SceneryType.None) lastUsed = i;
            int freeSlot = (lastUsed < oldRoot.SceneryTypes.Length - 1) ? lastUsed + 1 : -1;
            if (freeSlot >= 0)
            {
                oldRoot.SceneryTypes[freeSlot] = newContentTop.GetObjectIndex();
                ApplySceneryBounds(oldRoot, SysVec3.Min(oldMin, newMin), SysVec3.Max(oldMax, newMax));
                destScenery.Sceneries.AddRange(newContentFlattened);
                mergeMode = $"grafted into root's own free slot {freeSlot}";
            }
            else
            {
                var demotedOldRoot = DemoteRootToNode(oldRoot);
                var newTopRoot = new TwinSceneryRoot();
                newTopRoot.SceneryTypes[0] = ITwinScenery.SceneryType.Node;
                newTopRoot.SceneryTypes[1] = newContentTop.GetObjectIndex();
                for (int i = 2; i < 8; i++) newTopRoot.SceneryTypes[i] = ITwinScenery.SceneryType.None;
                ApplySceneryBounds(newTopRoot, SysVec3.Min(oldMin, newMin), SysVec3.Max(oldMax, newMax));
                Array.Copy(demotedOldRoot.LightsEnabler, newTopRoot.LightsEnabler, demotedOldRoot.LightsEnabler.Length);

                var oldDescendants = destScenery.Sceneries.Skip(1).ToList();
                destScenery.Sceneries.Clear();
                destScenery.Sceneries.Add(newTopRoot);
                destScenery.Sceneries.Add(demotedOldRoot);
                destScenery.Sceneries.AddRange(oldDescendants);
                destScenery.Sceneries.AddRange(newContentFlattened);
                mergeMode = "root was full — wrapped under a new top Root";
            }
        }

        return mergeMode;
    }

    public static (SysVec3 Min, SysVec3 Max) PadZoneToWholeMap(PS2AnyScenery scenery, SysVec3 min, SysVec3 max)
    {
        var center = (min + max) * 0.5f;
        var half = SysVec3.Max((max - min), new SysVec3(20f));
        var padMin = center - half;
        var padMax = center + half;

        if (scenery.Sceneries.Count > 0 && scenery.Sceneries[0] is { } root)
        {
            var rootMin = new SysVec3(root.UnkVec2.X, root.UnkVec2.Y, root.UnkVec2.Z);
            var rootMax = new SysVec3(root.UnkVec3.X, root.UnkVec3.Y, root.UnkVec3.Z);
            var slack = SysVec3.Max((rootMax - rootMin) * 0.1f, new SysVec3(20f));
            padMin = SysVec3.Min(padMin, rootMin - slack);
            padMax = SysVec3.Max(padMax, rootMax + slack);
        }

        return (padMin, padMax);
    }

    public static void GraftIndependentSceneryLeaf(PS2AnyScenery destScenery, TwinSceneryLeaf leaf, SysVec3 tightMin, SysVec3 tightMax)
    {
        var (pMin, pMax) = PadZoneToWholeMap(destScenery, tightMin, tightMax);
        ApplySceneryBounds(leaf, pMin, pMax);
        GraftLeavesIntoScenery(destScenery, new List<TwinSceneryLeaf> { leaf });
    }

    private static (SysVec3 Min, SysVec3 Max, bool Any) CollectSceneryContent(
        List<TwinSceneryBaseType> flat, ref int cursor,
        List<uint> meshIds, List<TwinMat4> meshMats, List<TwinVec4[]> meshBoxes,
        List<uint> lodIds, List<TwinMat4> lodMats, List<TwinVec4[]> lodBoxes,
        bool[] lights)
    {
        var self = flat[cursor];
        cursor++;

        var min = new SysVec3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new SysVec3(float.MinValue, float.MinValue, float.MinValue);
        bool any = false;

        int meshCount = self.MeshIDs.Count;
        for (int i = 0; i < meshCount; i++)
        {
            meshIds.Add(self.MeshIDs[i]);
            meshMats.Add(self.MeshModelMatrices[i]);
            var bb = self.BoundingBoxes[i];
            meshBoxes.Add(bb);
            var c4 = self.MeshModelMatrices[i].Column4;
            var pos = new SysVec3(c4.X, c4.Y, c4.Z);
            min = SysVec3.Min(min, pos + SysVec3.Min(new SysVec3(bb[0].X, bb[0].Y, bb[0].Z), new SysVec3(bb[1].X, bb[1].Y, bb[1].Z)));
            max = SysVec3.Max(max, pos + SysVec3.Max(new SysVec3(bb[0].X, bb[0].Y, bb[0].Z), new SysVec3(bb[1].X, bb[1].Y, bb[1].Z)));
            any = true;
        }
        for (int i = 0; i < self.LodIDs.Count; i++)
        {
            lodIds.Add(self.LodIDs[i]);
            lodMats.Add(self.LodModelMatrices[i]);
            var bb = self.BoundingBoxes[meshCount + i];
            lodBoxes.Add(bb);
            var c4 = self.LodModelMatrices[i].Column4;
            var pos = new SysVec3(c4.X, c4.Y, c4.Z);
            min = SysVec3.Min(min, pos + SysVec3.Min(new SysVec3(bb[0].X, bb[0].Y, bb[0].Z), new SysVec3(bb[1].X, bb[1].Y, bb[1].Z)));
            max = SysVec3.Max(max, pos + SysVec3.Max(new SysVec3(bb[0].X, bb[0].Y, bb[0].Z), new SysVec3(bb[1].X, bb[1].Y, bb[1].Z)));
            any = true;
        }
        for (int i = 0; i < 128; i++) lights[i] |= self.LightsEnabler[i];

        if (self is TwinSceneryNode node)
        {
            foreach (var t in node.SceneryTypes)
            {
                if (t == ITwinScenery.SceneryType.None) continue;
                var (cMin, cMax, cAny) = CollectSceneryContent(flat, ref cursor, meshIds, meshMats, meshBoxes, lodIds, lodMats, lodBoxes, lights);
                if (!cAny) continue;
                min = SysVec3.Min(min, cMin);
                max = SysVec3.Max(max, cMax);
                any = true;
            }
        }

        return (min, max, any);
    }

    public static void FlattenSceneryToSingleNode(PS2AnyScenery scenery)
    {
        if (scenery.Sceneries.Count == 0) return;

        var meshIds = new List<uint>();
        var meshMats = new List<TwinMat4>();
        var meshBoxes = new List<TwinVec4[]>();
        var lodIds = new List<uint>();
        var lodMats = new List<TwinMat4>();
        var lodBoxes = new List<TwinVec4[]>();
        var lights = new bool[128];

        int cursor = 0;
        var (min, max, any) = CollectSceneryContent(scenery.Sceneries, ref cursor, meshIds, meshMats, meshBoxes, lodIds, lodMats, lodBoxes, lights);

        var root = new TwinSceneryRoot
        {
            MeshIDs = meshIds,
            MeshModelMatrices = meshMats,
            LodIDs = lodIds,
            LodModelMatrices = lodMats,
        };
        root.BoundingBoxes = new List<TwinVec4[]>(meshBoxes);
        root.BoundingBoxes.AddRange(lodBoxes);
        Array.Copy(lights, root.LightsEnabler, 128);
        for (int i = 0; i < 8; i++) root.SceneryTypes[i] = ITwinScenery.SceneryType.None;
        if (any) ApplySceneryBounds(root, min, max);

        scenery.Sceneries.Clear();
        scenery.Sceneries.Add(root);
    }

    public static void ApplySceneryBounds(TwinSceneryBaseType item, SysVec3 min, SysVec3 max)
    {
        var center = (min + max) * 0.5f;
        var half   = max - center;
        float radius = GameMath.Length(half);
        item.UnkVec1 = new TwinVec4(center.X, center.Y, center.Z, radius);
        item.UnkVec2 = new TwinVec4(min.X, min.Y, min.Z, 1f);
        item.UnkVec3 = new TwinVec4(max.X, max.Y, max.Z, 1f);
        item.UnkVec4 = new TwinVec4(half.X, half.Y, half.Z, radius);
    }

    private static (SysVec3 Min, SysVec3 Max, bool Any) RecomputeSubtreeBoundsRec(List<TwinSceneryBaseType> flat, ref int cursor)
    {
        var self = flat[cursor];
        cursor++;

        var min = new SysVec3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new SysVec3(float.MinValue, float.MinValue, float.MinValue);
        bool any = false;
        for (int i = 0; i < self.MeshIDs.Count; i++)
        {
            var c4 = self.MeshModelMatrices[i].Column4;
            var pos = new SysVec3(c4.X, c4.Y, c4.Z);
            var bb = self.BoundingBoxes[i];
            var a = pos + new SysVec3(bb[0].X, bb[0].Y, bb[0].Z);
            var b = pos + new SysVec3(bb[1].X, bb[1].Y, bb[1].Z);
            min = SysVec3.Min(min, SysVec3.Min(a, b));
            max = SysVec3.Max(max, SysVec3.Max(a, b));
            any = true;
        }
        for (int i = 0; i < self.LodIDs.Count; i++)
        {
            var c4 = self.LodModelMatrices[i].Column4;
            var pos = new SysVec3(c4.X, c4.Y, c4.Z);
            var bb = self.BoundingBoxes[self.MeshIDs.Count + i];
            var a = pos + new SysVec3(bb[0].X, bb[0].Y, bb[0].Z);
            var b = pos + new SysVec3(bb[1].X, bb[1].Y, bb[1].Z);
            min = SysVec3.Min(min, SysVec3.Min(a, b));
            max = SysVec3.Max(max, SysVec3.Max(a, b));
            any = true;
        }

        if (self is TwinSceneryNode node)
        {
            foreach (var t in node.SceneryTypes)
            {
                if (t == ITwinScenery.SceneryType.None) continue;
                var (cMin, cMax, cAny) = RecomputeSubtreeBoundsRec(flat, ref cursor);
                if (!cAny) continue;
                min = SysVec3.Min(min, cMin);
                max = SysVec3.Max(max, cMax);
                any = true;
            }
        }

        if (any) ApplySceneryBounds(self, min, max);
        return (min, max, any);
    }

    public static void RecomputeAllSceneryBounds(PS2AnyScenery scenery)
    {
        if (scenery.Sceneries.Count == 0) return;
        int cursor = 0;
        RecomputeSubtreeBoundsRec(scenery.Sceneries, ref cursor);
    }

    private static List<TwinSceneryBaseType> RebuildSceneryTree(List<TwinSceneryLeaf> leaves)
    {
        const int W = 8;
        var flatOut = new List<TwinSceneryBaseType>();

        if (leaves.Count == 0)
        {
            var emptyRoot = new TwinSceneryRoot();
            for (int i = 0; i < W; i++) emptyRoot.SceneryTypes[i] = ITwinScenery.SceneryType.None;
            flatOut.Add(emptyRoot);
            return flatOut;
        }

        var boundsOf = new Dictionary<TwinSceneryBaseType, (SysVec3 Min, SysVec3 Max)>();
        foreach (var leaf in leaves)
        {
            if (leaf.MeshIDs.Count == 0) continue;
            var min = new SysVec3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new SysVec3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < leaf.MeshIDs.Count; i++)
            {
                var c4 = leaf.MeshModelMatrices[i].Column4;
                var pos = new SysVec3(c4.X, c4.Y, c4.Z);
                var bb = leaf.BoundingBoxes[i];
                var a = pos + new SysVec3(bb[0].X, bb[0].Y, bb[0].Z);
                var b = pos + new SysVec3(bb[1].X, bb[1].Y, bb[1].Z);
                min = SysVec3.Min(min, SysVec3.Min(a, b));
                max = SysVec3.Max(max, SysVec3.Max(a, b));
            }
            boundsOf[leaf] = (min, max);
        }

        var childrenOf = new Dictionary<TwinSceneryBaseType, List<TwinSceneryBaseType>>();
        List<TwinSceneryBaseType> level = leaves.Cast<TwinSceneryBaseType>().ToList();
        while (level.Count > 1)
        {
            var nextLevel = new List<TwinSceneryBaseType>();
            for (int i = 0; i < level.Count; i += W)
            {
                var chunk = level.Skip(i).Take(W).ToList();
                var node = new TwinSceneryNode();
                for (int s = 0; s < W; s++)
                    node.SceneryTypes[s] = s < chunk.Count ? chunk[s].GetObjectIndex() : ITwinScenery.SceneryType.None;
                childrenOf[node] = chunk;

                var min = new SysVec3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new SysVec3(float.MinValue, float.MinValue, float.MinValue);
                bool any = false;
                foreach (var c in chunk)
                    if (boundsOf.TryGetValue(c, out var cb))
                    { min = SysVec3.Min(min, cb.Min); max = SysVec3.Max(max, cb.Max); any = true; }
                if (any) { boundsOf[node] = (min, max); ApplySceneryBounds(node, min, max); }

                foreach (var c in chunk)
                    for (int b = 0; b < node.LightsEnabler.Length; b++)
                        node.LightsEnabler[b] |= c.LightsEnabler[b];

                nextLevel.Add(node);
            }
            level = nextLevel;
        }

        var top = level[0];
        TwinSceneryRoot root;
        if (top is TwinSceneryNode topNode)
        {
            root = new TwinSceneryRoot();
            Array.Copy(topNode.SceneryTypes, root.SceneryTypes, W);
            Array.Copy(topNode.LightsEnabler, root.LightsEnabler, topNode.LightsEnabler.Length);
            var topChildren = childrenOf[topNode];
            childrenOf.Remove(topNode);
            childrenOf[root] = topChildren;
            if (boundsOf.TryGetValue(topNode, out var tb)) ApplySceneryBounds(root, tb.Min, tb.Max);
        }
        else
        {
            root = new TwinSceneryRoot();
            root.SceneryTypes[0] = top.GetObjectIndex();
            for (int i = 1; i < W; i++) root.SceneryTypes[i] = ITwinScenery.SceneryType.None;
            Array.Copy(top.LightsEnabler, root.LightsEnabler, top.LightsEnabler.Length);
            childrenOf[root] = new List<TwinSceneryBaseType> { top };
            if (boundsOf.TryGetValue(top, out var tb)) ApplySceneryBounds(root, tb.Min, tb.Max);
        }

        void Flatten(TwinSceneryBaseType item)
        {
            flatOut.Add(item);
            if (childrenOf.TryGetValue(item, out var kids))
                foreach (var k in kids) Flatten(k);
        }
        Flatten(root);
        return flatOut;
    }

    public static void RebuildSkydomeDraws(GL gl, PS2AnyTwinsanitySM2 sm2, Dictionary<uint, Texture2D> texCache)
    {
        RenderPipeline.SkydomeDraws.Clear();
        var gfx = sm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        if (gfx is null) return;

        var modelById = BuildModelLookup(gfx);
        var meshById  = BuildMeshLookup(gfx, modelById, gl, texCache);

        var skySec = gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKYDOMES_SECTION);
        if (skySec is null) return;
        for (int i = 0; i < skySec.GetItemsAmount(); i++)
        {
            if (skySec.GetItem(i) is not PS2AnySkydome sky) continue;
            foreach (var meshId in sky.Meshes)
            {
                if (!meshById.TryGetValue(meshId, out var entry)) continue;
                foreach (var (gpuMesh, mat) in entry)
                {
                    mat.FogEnabled = true; // Amedo 2026-09-26
                    mat.DepthWrite = false;
                    RenderPipeline.SkydomeDraws.Add((gpuMesh, mat));
                }
            }
            Console.WriteLine($"[MeshDecoder] Skydome {sky.GetID():X8}: {sky.Meshes.Count} meshes");
        }
    }
}
