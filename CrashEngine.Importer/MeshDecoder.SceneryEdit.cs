using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using SysVec3 = System.Numerics.Vector3;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using TwinSceneryBaseType = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryBaseType;
using TwinSceneryLeaf = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryLeaf;
using TwinSceneryNode = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryNode;
using TwinSceneryRoot = Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes.TwinSceneryRoot;
using ITwinScenery = Twinsanity.TwinsanityInterchange.Interfaces.Items.SM.ITwinScenery;
using System.Numerics;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;

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

        var copy = SceneryGraphicsCopy.Between(gl, destTexCache, srcGfx, destGfx, withLods: true);
        if (copy is null) { log = "source scenery is missing a required GRAPHICS subsection"; return false; }
        var destLodSec = copy.DestLods!;

        var lodIdMap = new Dictionary<uint, uint>();
        uint? CloneLod(uint srcLodId)
        {
            if (lodIdMap.TryGetValue(srcLodId, out var already)) return already;
            var srcLod = copy.SrcLods?.GetItem<PS2AnyLOD>(srcLodId);
            if (srcLod is null) return null;

            var clone = CloneItem(srcLod);
            clone.Meshes.Clear();
            foreach (var m in srcLod.Meshes)
            {
                var newMeshId = copy.RigidModel(m);
                if (newMeshId is not null) clone.Meshes.Add(newMeshId.Value);
            }
            if (clone.Meshes.Count == 0) return null;

            uint newId = NewGraphicsLocalId(destLodSec, copy.DestMeshes, copy.DestRigidModels);
            clone.SetID(newId);
            destLodSec.AddItem(clone);
            lodIdMap[srcLodId] = newId;
            return newId;
        }

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
                var newId = copy.RigidModel(srcLeaf.MeshIDs[i]);
                if (newId is null) { unresolvedMeshIds.Add((idx, srcLeaf.MeshIDs[i])); continue; }
                newLeaf.MeshIDs.Add(newId.Value);
                newLeaf.MeshModelMatrices.Add(CloneTwinMat4(srcLeaf.MeshModelMatrices[i]));
                newLeaf.BoundingBoxes.Add(CloneSceneryBox(srcLeaf.BoundingBoxes[i]));
                addedMeshes++;
            }

            for (int i = 0; i < srcLeaf.LodIDs.Count; i++)
            {
                if (allowedIds is not null && !allowedIds.Contains(srcLeaf.LodIDs[i])) continue;
                var newLodId = CloneLod(srcLeaf.LodIDs[i]);
                if (newLodId is null) { unresolvedMeshIds.Add((idx, srcLeaf.LodIDs[i])); continue; }
                newLeaf.LodIDs.Add(newLodId.Value);
                newLeaf.LodModelMatrices.Add(CloneTwinMat4(srcLeaf.LodModelMatrices[i]));
                newLeaf.BoundingBoxes.Add(CloneSceneryBox(srcLeaf.BoundingBoxes[srcLeaf.MeshIDs.Count + i]));
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
            RefitSceneryBounds(l, withLods: false);

        if (newSourceIds is not null)
            foreach (var l in newLeaves)
            {
                newSourceIds.AddRange(l.MeshIDs);
                newSourceIds.AddRange(l.LodIDs);
            }

        var mergeMode = GraftLeavesIntoScenery(destScenery, newLeaves);

        log = $"Added {addedTiles} scenery tile(s), {addedMeshes} mesh(es) total, +{copy.ModelIdMap.Count} Model, " +
              $"+{copy.MatIdMap.Count} Material, +{copy.TexIdMap.Count} Texture, +{lodIdMap.Count} LOD ({mergeMode}).";
        if (skippedTiles > 0) log += $"  {skippedTiles} tile(s) skipped (not a real leaf tile, or no meshes resolved).";
        if (unresolvedMeshIds.Count > 0)
            log += "  Unresolved mesh id(s): " + string.Join(", ",
                unresolvedMeshIds.Select(u => $"0x{u.MeshId:X8}@srcIdx{u.SourceIndex}")) +
                $" (not found in source's own Meshes[{copy.SrcMeshes.GetItemsAmount()} items]/RigidModels[{copy.SrcRigidModels.GetItemsAmount()} items] sections).";
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

    public static (SysVec3 Min, SysVec3 Max)? SceneryContentBounds(TwinSceneryBaseType node, bool withLods)
    {
        var min = new SysVec3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new SysVec3(float.MinValue, float.MinValue, float.MinValue);
        bool any = false;
        for (int i = 0; i < node.MeshIDs.Count; i++)
            GrowBounds(ref min, ref max, ref any, node.MeshModelMatrices[i], node.BoundingBoxes[i]);
        if (withLods)
            for (int i = 0; i < node.LodIDs.Count; i++)
                GrowBounds(ref min, ref max, ref any, node.LodModelMatrices[i], node.BoundingBoxes[node.MeshIDs.Count + i]);
        return any ? (min, max) : null;
    }

    public static void RefitSceneryBounds(TwinSceneryBaseType node, bool withLods)
    {
        if (SceneryContentBounds(node, withLods) is { } b) ApplySceneryBounds(node, b.Min, b.Max);
    }

    private static void GrowBounds(ref SysVec3 min, ref SysVec3 max, ref bool any, TwinMat4 m, TwinVec4[] box)
    {
        var pos = new SysVec3(m.Column4.X, m.Column4.Y, m.Column4.Z);
        var a = pos + new SysVec3(box[0].X, box[0].Y, box[0].Z);
        var b = pos + new SysVec3(box[1].X, box[1].Y, box[1].Z);
        min = SysVec3.Min(min, SysVec3.Min(a, b));
        max = SysVec3.Max(max, SysVec3.Max(a, b));
        any = true;
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
            if (SceneryContentBounds(leaf, withLods: false) is { } lb) boundsOf[leaf] = lb;

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

        AddSkydomeDraws(gfx, meshById);
    }

    private static void AddSkydomeDraws(PS2AnyGraphicsSection gfx, Dictionary<uint, List<(GpuMesh, Material)>> meshById)
    {
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
                    mat.FogEnabled = true;
                    mat.DepthWrite = false;
                    RenderPipeline.SkydomeDraws.Add((gpuMesh, mat));
                }
            }
            Console.WriteLine($"[MeshDecoder] Skydome {sky.GetID():X8}: {sky.Meshes.Count} meshes");
        }
    }
}
