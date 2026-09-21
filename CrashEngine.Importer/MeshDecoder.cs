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

public sealed class TransplantRecord
{
    public uint ObjectId;
    public bool ObjectWasNew;
    public readonly List<uint> OgiIds        = new();
    public readonly List<uint> AnimIds       = new();
    public readonly List<uint> RigidModelIds = new();
    public readonly List<uint> ModelIds      = new();
    public readonly List<uint> SkinIds       = new();
    public readonly List<uint> BlendSkinIds  = new();
    public readonly List<uint> MaterialIds   = new();
    public readonly List<uint> TextureIds    = new();
}

public static class MeshDecoder
{
    public static readonly SysVec3[] FogColors =
    {
        new(128/255f,   0/255f, 128/255f),
        new(  0f,       0f,       0f),
        new(173/255f, 216/255f, 230/255f),
        new(  0f,     255/255f,   0f),
        new(127/255f, 127/255f, 127/255f),
        new(245/255f, 245/255f, 220/255f),
    };

    public static readonly string[] FogColorNames = { "Purple", "Black", "Light blue", "Green", "Grey", "Beige" };

    public static SysVec3? LastFogColor { get; private set; }

    public static int? LastFogColorIndex { get; private set; }

    public static (SysVec3 Ambient, List<(SysVec3 Color, SysVec3 Direction)> Directional)? LastWorldLighting { get; private set; }

    public static (SysVec3 Ambient, List<(SysVec3 Color, SysVec3 Direction)> Directional)? DecodeWorldLighting(
        PS2AnyTwinsanitySM2 sm2)
    {
        var sceneryItem = sm2.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
        if (sceneryItem is null || !sceneryItem.HasLighting ||
            (sceneryItem.AmbientLights.Count == 0 && sceneryItem.DirectionalLights.Count == 0))
            return null;

        var ambient = sceneryItem.AmbientLights.Count > 0
            ? new SysVec3(sceneryItem.AmbientLights[0].Color.X, sceneryItem.AmbientLights[0].Color.Y, sceneryItem.AmbientLights[0].Color.Z)
            : SysVec3.Zero;
        var directional = sceneryItem.DirectionalLights
            .Select(l => (
                Color: new SysVec3(l.Color.X, l.Color.Y, l.Color.Z),
                Direction: SysVec3.Normalize(new SysVec3(-l.UnkVec3.X, l.UnkVec3.Y, l.UnkVec3.Z))))
            .ToList();
        return (ambient, directional);
    }


    public static object BuildInstanceMeshes(GL gl,
                                           PS2AnyTwinsanityRM2 rm2,
                                           Entity instancesRoot,
                                           Dictionary<uint, Texture2D> texCache,
                                           PS2AnyTwinsanityRM2? globalRm2 = null)
    {
        var tables = BuildLookupTables(gl, rm2, texCache, globalRm2);
        Console.WriteLine($"[MeshDecoder] Objects:{tables.Objects.Count}  OGIs:{tables.OGIs.Count}  RigidModels:{tables.RigidModels.Count}  ModelGpu:{tables.ModelGpu.Count}  Animations:{tables.Animations.Count}");

        int instWithMesh = 0, instWithAnim = 0, instWithAltStates = 0;
        foreach (var e in AllEntities(instancesRoot))
        {
            var data = e.Get<InstanceData>();
            if (data is null) continue;
            var (hadMesh, hadAnim, hadAlt) = BuildOneInstanceMesh(gl, tables, e, data);
            if (hadMesh) instWithMesh++;
            if (hadAnim) instWithAnim++;
            if (hadAlt) instWithAltStates++;
        }
        Console.WriteLine($"[MeshDecoder] Instances with mesh: {instWithMesh}  with playable animations: {instWithAnim}  with alt-OGI states: {instWithAltStates}");
        return tables;
    }

    public static void BuildMeshForInstance(GL gl, object tablesHandle, Entity e)
    {
        if (tablesHandle is not RmTables tables) return;
        var data = e.Get<InstanceData>();
        if (data is null) return;
        foreach (var child in e.Children.ToList())
            e.RemoveChild(child);
        e.RemoveComponent<AnimatedObject>();
        BuildOneInstanceMesh(gl, tables, e, data);
    }

    public static bool PreviewOgiOnInstance(GL gl, object tablesHandle, Entity e, uint ogiId)
    {
        if (tablesHandle is not RmTables tables) return false;
        if (!tables.OGIs.TryGetValue(ogiId, out var ogi)) return false;
        var parts = BuildOgiMeshes(tables, ogi);
        if (parts.Count == 0) return false;

        foreach (var child in e.Children.ToList())
            e.RemoveChild(child);
        e.RemoveComponent<AnimatedObject>();
        EmitOgiMeshEntities(gl, tables, e, parts, ogi, null);
        return true;
    }

    public static List<(uint Id, string Name)> GetObjectCatalog(object tablesHandle)
    {
        if (tablesHandle is not RmTables tables) return new();
        return tables.Objects
            .Select(kv => (kv.Key, Name: string.IsNullOrWhiteSpace(kv.Value.Name)
                ? $"Object_{kv.Key:X4}" : kv.Value.Name.Replace("|", "_")))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<(uint Id, string Name)> GetReskinnableObjectCatalog(object tablesHandle)
    {
        if (tablesHandle is not RmTables tables) return new();
        return GetObjectCatalog(tablesHandle)
            .Where(t => tables.Objects.TryGetValue(t.Id, out var obj) && ResolveDefaultOgi(tables, obj, out _) is not null)
            .ToList();
    }

    public static List<(uint Id, int Frames, int Joints)> GetAnimationCatalog(object tablesHandle)
    {
        if (tablesHandle is not RmTables tables) return new();
        return tables.Animations
            .Select(kv => (Id: kv.Key, Frames: (int)kv.Value.TotalFrames, Joints: kv.Value.MainAnimation.JointSettings.Count))
            .OrderBy(t => t.Id)
            .ToList();
    }

    public static bool SetAnimationSlot(object tablesHandle, uint objectId, int slotIndex, uint newAnimId)
    {
        if (tablesHandle is not RmTables tables) return false;
        if (!tables.Objects.TryGetValue(objectId, out var obj)) return false;
        if (slotIndex < 0 || slotIndex >= obj.AnimationSlots.Count) return false;

        obj.AnimationSlots[slotIndex] = (ushort)newAnimId;
        if (!obj.RefAnimations.Contains((ushort)newAnimId))
            obj.RefAnimations.Add((ushort)newAnimId);
        return true;
    }

    public static bool SwapCharacterSkin(PS2AnyTwinsanityRM2 rm2, object tablesHandle,
        uint targetObjectId, uint sourceOgiId, out ushort originalOgiId, out ushort clonedOgiId, out string log)
    {
        originalOgiId = 0xFFFF;
        clonedOgiId = 0xFFFF;
        log = "";
        if (tablesHandle is not RmTables tables) { log = "tables handle invalid"; return false; }
        if (!tables.Objects.TryGetValue(targetObjectId, out var targetObj))
        { log = $"ObjectId 0x{targetObjectId:X4} not found"; return false; }
        if (!tables.OGIs.TryGetValue(sourceOgiId, out var sourceOgi))
        { log = $"OGI 0x{sourceOgiId:X4} not found"; return false; }

        var targetOgi = ResolveDefaultOgi(tables, targetObj, out _);
        if (targetOgi is null) { log = $"ObjectId 0x{targetObjectId:X4} has no resolvable default OGI"; return false; }

        var code   = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var ogiSec = code?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        if (ogiSec is null) { log = "chunk has no OGIs section"; return false; }

        var newOgi = CloneItem(targetOgi);
        ushort newId = NewCodeLocalId(ogiSec);
        newOgi.SetID(newId);
        newOgi.SkinID      = sourceOgi.SkinID;
        newOgi.BlendSkinID = sourceOgi.BlendSkinID;
        ogiSec.AddItem(newOgi);
        tables.OGIs[newId] = newOgi;

        ushort oldId = (ushort)targetOgi.GetID();
        for (int i = 0; i < targetObj.OGISlots.Count; i++)
            if (targetObj.OGISlots[i] == oldId) targetObj.OGISlots[i] = newId;

        originalOgiId = oldId;
        clonedOgiId   = newId;
        log = $"Cloned OGI 0x{oldId:X4} -> new OGI 0x{newId:X4} (skin from OGI 0x{sourceOgiId:X4}) " +
              $"— original OGI 0x{oldId:X4} and source OGI 0x{sourceOgiId:X4} are untouched.";
        return true;
    }

    public static bool ResetCharacterSkin(object tablesHandle, uint targetObjectId, ushort originalOgiId, ushort cloneOgiId, out string log)
    {
        log = "";
        if (tablesHandle is not RmTables tables) { log = "tables handle invalid"; return false; }
        if (!tables.Objects.TryGetValue(targetObjectId, out var targetObj))
        { log = $"ObjectId 0x{targetObjectId:X4} not found"; return false; }

        for (int i = 0; i < targetObj.OGISlots.Count; i++)
            if (targetObj.OGISlots[i] == cloneOgiId) targetObj.OGISlots[i] = originalOgiId;

        log = $"Reset ObjectId 0x{targetObjectId:X4} back to its original OGI 0x{originalOgiId:X4} " +
              $"(reskin clone 0x{cloneOgiId:X4} left in place, unreferenced, in case you want it back).";
        return true;
    }

    public static bool RedoReskin(object tablesHandle, uint targetObjectId, ushort originalOgiId, ushort cloneOgiId, out string log)
    {
        log = "";
        if (tablesHandle is not RmTables tables) { log = "tables handle invalid"; return false; }
        if (!tables.Objects.TryGetValue(targetObjectId, out var targetObj))
        { log = $"ObjectId 0x{targetObjectId:X4} not found"; return false; }

        for (int i = 0; i < targetObj.OGISlots.Count; i++)
            if (targetObj.OGISlots[i] == originalOgiId) targetObj.OGISlots[i] = cloneOgiId;

        log = $"Re-applied existing reskin clone 0x{cloneOgiId:X4} (any Bone Mapping on it is preserved).";
        return true;
    }

    public static bool UpdateReskinClone(object tablesHandle, ushort cloneOgiId, uint sourceOgiId, out string log)
    {
        log = "";
        if (tablesHandle is not RmTables tables) { log = "tables handle invalid"; return false; }
        if (!tables.OGIs.TryGetValue(cloneOgiId, out var cloneOgi))
        { log = $"reskin clone OGI 0x{cloneOgiId:X4} not found (level reloaded since the last swap?)"; return false; }
        if (!tables.OGIs.TryGetValue(sourceOgiId, out var sourceOgi))
        { log = $"OGI 0x{sourceOgiId:X4} not found"; return false; }

        cloneOgi.SkinID      = sourceOgi.SkinID;
        cloneOgi.BlendSkinID = sourceOgi.BlendSkinID;
        log = $"Updated existing reskin (OGI 0x{cloneOgiId:X4}) with skin from OGI 0x{sourceOgiId:X4}.";
        return true;
    }

    public static List<TwinJoint>? GetJointList(object tablesHandle, uint objectId)
    {
        if (tablesHandle is not RmTables tables) return null;
        if (!tables.Objects.TryGetValue(objectId, out var obj)) return null;
        return ResolveDefaultOgi(tables, obj, out _)?.Joints;
    }

    public static List<(GpuMesh Mesh, Material Mat, Matrix4x4 JointLocal, bool IsSkinned)>? GetObjectMeshParts(
        object tablesHandle, uint objectId)
    {
        if (tablesHandle is not RmTables tables) return null;
        if (!tables.Objects.TryGetValue(objectId, out var obj)) return null;
        if (ResolveDefaultOgi(tables, obj, out var parts) is null) return null;
        return parts.Select(p => (p.Item1, p.Item2, p.Item3, p.Item4)).ToList();
    }

    public static List<TwinJoint>? GetJointListByOgi(object tablesHandle, uint ogiId)
    {
        if (tablesHandle is not RmTables tables) return null;
        return tables.OGIs.TryGetValue(ogiId, out var ogi) ? ogi.Joints : null;
    }

    public static List<(GpuMesh Mesh, Material Mat, Matrix4x4 JointLocal, bool IsSkinned)>? GetObjectMeshPartsByOgi(
        object tablesHandle, uint ogiId)
    {
        if (tablesHandle is not RmTables tables) return null;
        if (!tables.OGIs.TryGetValue(ogiId, out var ogi)) return null;
        var built = BuildOgiMeshes(tables, ogi);
        return built.Count > 0 ? built.Select(p => (p.Item1, p.Item2, p.Item3, p.Item4)).ToList() : null;
    }

    public static List<(uint RmId, Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinModel Model, List<uint> MaterialIds)>?
        GetObjectSourceModelsByOgi(object tablesHandle, uint ogiId)
    {
        if (tablesHandle is not RmTables tables) return null;
        if (!tables.OGIs.TryGetValue(ogiId, out var ogi)) return null;
        var result = new List<(uint, Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinModel, List<uint>)>();
        foreach (var rmId in ogi.RigidModelIds)
        {
            if (!tables.RigidModels.TryGetValue(rmId, out var rm)) continue;
            if (!tables.Models.TryGetValue(rm.Model, out var model)) continue;
            result.Add((rmId, model, rm.Materials));
        }
        return result.Count > 0 ? result : null;
    }

    public static List<(uint RmId, ITwinModel Model, List<uint> MaterialIds)>? GetObjectSourceModelsByOgiDirect(
        PS2AnyTwinsanityRM2 rm2, uint ogiId)
    {
        var code = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var ogiSec = code?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var ogi = ogiSec?.GetItem<PS2AnyOGI>(ogiId);
        if (ogi is null) return null;

        var gfx = rm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var rmSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var modelSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        if (rmSec is null || modelSec is null) return null;

        var result = new List<(uint, ITwinModel, List<uint>)>();
        foreach (var rmId in ogi.RigidModelIds)
        {
            var rm = rmSec.GetItem<PS2AnyRigidModel>(rmId);
            if (rm is null) continue;
            var model = modelSec.GetItem<PS2AnyModel>(rm.Model);
            if (model is null) continue;
            result.Add((rmId, model, rm.Materials));
        }
        return result.Count > 0 ? result : null;
    }

    public static List<(GpuMesh Mesh, Material Mat, Matrix4x4 JointLocal, bool IsSkinned)>? GetObjectMeshPartsByOgiDirect(
        GL gl, PS2AnyTwinsanityRM2 rm2, Dictionary<uint, Texture2D> texCache, uint ogiId)
    {
        var code = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var ogiSec = code?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var ogi = ogiSec?.GetItem<PS2AnyOGI>(ogiId);
        var srcModels = GetObjectSourceModelsByOgiDirect(rm2, ogiId);
        if (ogi is null || srcModels is null) return null;

        var jointWorld = ComputeJointWorlds(ogi);
        var result = new List<(GpuMesh, Material, Matrix4x4, bool)>();
        int rmIndex = 0;
        foreach (var (_, model, materialIds) in srcModels)
        {
            var shaders = GetMaterialsShadersDirect(rm2, materialIds);
            var subMats = new List<(Texture2D? Tex, TwinShader? Shader)>();
            foreach (var sh in shaders)
                subMats.Add((sh is not null && texCache.TryGetValue(sh.TextureId, out var t) ? t : null, sh));

            int jointIdx = rmIndex < ogi.JointIndices.Count ? ogi.JointIndices[rmIndex] : 0;
            var m = jointWorld.TryGetValue(jointIdx, out var jw) ? jw : Matrix4x4.Identity;
            foreach (var (mesh, mat) in DecodeModel(gl, model, subMats))
                result.Add((mesh, mat, m, false));
            rmIndex++;
        }
        return result.Count > 0 ? result : null;
    }

    public static List<TwinShader?> GetMaterialsShadersDirect(PS2AnyTwinsanityRM2 rm2, List<uint> materialIds)
    {
        var gfx = rm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var matSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var result = new List<TwinShader?>();
        foreach (var matId in materialIds)
        {
            var mat = matSec?.GetItem<PS2AnyMaterial>(matId);
            result.Add(mat?.Shaders is { Count: > 0 } ? mat.Shaders[0] : null);
        }
        return result;
    }

    public static PS2AnyOGI? GetRawOgi(object tablesHandle, uint ogiId)
    {
        if (tablesHandle is not RmTables tables) return null;
        return tables.OGIs.TryGetValue(ogiId, out var ogi) ? ogi : null;
    }

    public static uint? ResolveDefaultOgiId(object tablesHandle, uint objectId)
    {
        if (tablesHandle is not RmTables tables) return null;
        if (!tables.Objects.TryGetValue(objectId, out var obj)) return null;
        return ResolveDefaultOgi(tables, obj, out _)?.GetID();
    }

    public static List<ushort>? GetObjectOgiSlots(object tablesHandle, uint objectId) =>
        tablesHandle is RmTables tables && tables.Objects.TryGetValue(objectId, out var obj)
            ? obj.OGISlots : null;

    public static uint? ResolveDefaultOgiIdFromRaw(PS2AnyTwinsanityRM2 rm2, uint objectId)
    {
        var code   = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var objSec = code?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var ogiSec = code?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        if (objSec is null || ogiSec is null) return null;
        var obj = objSec.GetItem<PS2AnyObject>(objectId);
        if (obj is null) return null;
        foreach (var ogiId in obj.OGISlots)
            if (ogiSec.GetItem<PS2AnyOGI>(ogiId) is { } ogi && (ogi.SkinID != 0 || ogi.RigidModelIds.Count > 0))
                return ogiId;
        return null;
    }

    public static bool RemapReskinJoints(GL gl, PS2AnyTwinsanityRM2 rm2, object tablesHandle,
        Dictionary<uint, Texture2D> destTexCache, ushort cloneOgiId, uint sourceOgiId, Dictionary<int, int> jointMap, out string log)
    {
        log = "";
        if (tablesHandle is not RmTables tables) { log = "tables handle invalid"; return false; }
        if (!tables.OGIs.TryGetValue(cloneOgiId, out var cloneOgi))
        { log = $"reskin clone OGI 0x{cloneOgiId:X4} not found (level reloaded since the last swap?)"; return false; }
        if (!tables.OGIs.TryGetValue(sourceOgiId, out var sourceOgi))
        { log = $"source OGI 0x{sourceOgiId:X4} not found (level reloaded since the last swap?)"; return false; }
        if (sourceOgi.SkinID == 0)
        { log = "this reskin has no regular Skin (BlendSkin-only characters aren't supported by Bone Mapping yet)"; return false; }

        var code    = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var gfx     = rm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var skinSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION);
        if (skinSec is null) { log = "chunk has no Skins section"; return false; }

        var sourceSkin = skinSec.GetItem<PS2AnySkin>(sourceOgi.SkinID);
        if (sourceSkin is null) { log = $"skin 0x{sourceOgi.SkinID:X8} not found"; return false; }

        var newSkin = CloneItem(sourceSkin);
        ushort newSkinId = NewCodeLocalId(skinSec);
        newSkin.SetID(newSkinId);

        int droppedVertices = 0;
        foreach (var subSkin in newSkin.SubSkins)
        {
            subSkin.CalculateData();
            for (int i = 0; i < subSkin.SkinJoints.Count; i++)
            {
                var remapped = RemapVertexJoint(subSkin.SkinJoints[i], jointMap, out bool anyDropped);
                if (anyDropped) droppedVertices++;
                subSkin.SkinJoints[i] = remapped;
            }
            subSkin.Compile();
        }

        skinSec.AddItem(newSkin);
        cloneOgi.SkinID = newSkinId;

        var materialsById = BuildMaterialLookup(gfx!);
        var subMats = newSkin.SubSkins.Select(ss => ResolveMaterial(ss.Material, materialsById, destTexCache)).ToList();
        var gpuParts = DecodeSkin(gl, newSkin, subMats, recalculate: false);
        if (gpuParts.Count > 0) tables.SkinGpu[newSkinId] = gpuParts;

        log = $"Remapped skin (new id 0x{newSkinId:X8}) using {jointMap.Count} bone mapping(s)" +
              (droppedVertices > 0
                  ? $" — {droppedVertices} vertex weight(s) had an unmapped source bone, fell back to the root joint."
                  : ".");
        return true;
    }

    private static VertexJointInfo RemapVertexJoint(VertexJointInfo src, Dictionary<int, int> jointMap, out bool anyDropped)
    {
        anyDropped = false;
        var comps = new List<(int Index, float Weight)>();

        if (jointMap.TryGetValue(src.JointIndex1, out var m1)) comps.Add((m1, src.Weight1));
        else anyDropped = true;
        if (src.Weight2 > 0)
        {
            if (jointMap.TryGetValue(src.JointIndex2, out var m2)) comps.Add((m2, src.Weight2));
            else anyDropped = true;
        }
        if (src.Weight3 > 0)
        {
            if (jointMap.TryGetValue(src.JointIndex3, out var m3)) comps.Add((m3, src.Weight3));
            else anyDropped = true;
        }

        float total = comps.Sum(c => c.Weight);
        if (comps.Count == 0 || total <= 0f)
            return new VertexJointInfo { JointIndex1 = 0, Weight1 = 1f, Connection = src.Connection };

        var result = new VertexJointInfo { Connection = src.Connection };
        result.JointIndex1 = comps[0].Index; result.Weight1 = comps[0].Weight / total;
        if (comps.Count > 1) { result.JointIndex2 = comps[1].Index; result.Weight2 = comps[1].Weight / total; }
        if (comps.Count > 2) { result.JointIndex3 = comps[2].Index; result.Weight3 = comps[2].Weight / total; }
        return result;
    }


    public static Texture2D? DecodeUiTexture(GL gl, Twinsanity.TwinsanityInterchange.Interfaces.ITwinPTC ptc) =>
        ptc.Texture is Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture tex
            ? ChunkImporter.DecodeTexture(gl, tex)
            : null;

    public static List<Texture2D> DecodePsm(GL gl, Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PSM psm) =>
        psm.PTCs.Select(p => DecodeUiTexture(gl, p)).Where(t => t is not null).Select(t => t!).ToList();

    public static List<Texture2D> DecodeSplitFrames(GL gl, Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PSM psm)
    {
        var raw = psm.PTCs
            .Select(p => p.Texture as Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture)
            .Where(t => t is not null)
            .Select(t => DecodeRaw(t!))
            .ToList();

        if (raw.Count < 2 || raw.Count % 2 != 0 || raw.Any(r => r is null) ||
            raw.Any(r => r!.Value.w != raw[0]!.Value.w || r.Value.h != raw[0]!.Value.h))
            return DecodePsm(gl, psm);

        int tw = raw[0]!.Value.w, th = raw[0]!.Value.h, half = th / 2;
        if (half == 0) return DecodePsm(gl, psm);

        var topGroup = new List<byte[]>();
        var bottomGroup = new List<byte[]>();
        foreach (var r in raw)
        {
            var (w, h, rgba) = r!.Value;
            bool isTop = LuminanceVariance(rgba, w, 0, half) >= LuminanceVariance(rgba, w, half, half);
            var cropped = new byte[w * half * 4];
            int srcRowStart = isTop ? 0 : half;
            for (int y = 0; y < half; y++)
                Array.Copy(rgba, ((srcRowStart + y) * w) * 4, cropped, (y * w) * 4, w * 4);
            (isTop ? topGroup : bottomGroup).Add(cropped);
        }

        List<Texture2D> frames = new();
        foreach (var group in new[] { topGroup, bottomGroup })
        {
            if (group.Count == 0) continue;
            int W = tw * group.Count;
            var frame = new byte[W * half * 4];
            for (int c = 0; c < group.Count; c++)
            for (int y = 0; y < half; y++)
                Array.Copy(group[c], (y * tw) * 4, frame, (y * W + c * tw) * 4, tw * 4);
            frames.Add(new Texture2D(gl, frame, (uint)W, (uint)half));
        }
        return frames;
    }

    private static double LuminanceVariance(byte[] rgba, int width, int rowStart, int rowCount)
    {
        int n = width * rowCount;
        if (n == 0) return 0;
        var lum = new double[n];
        double sum = 0;
        for (int y = 0; y < rowCount; y++)
        for (int x = 0; x < width; x++)
        {
            int i = ((rowStart + y) * width + x) * 4;
            double l = 0.299 * rgba[i] + 0.587 * rgba[i + 1] + 0.114 * rgba[i + 2];
            lum[y * width + x] = l;
            sum += l;
        }
        double mean = sum / n, varSum = 0;
        foreach (var l in lum) varSum += (l - mean) * (l - mean);
        return varSum / n;
    }

    private static (int w, int h, byte[] rgba)? DecodeRaw(Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture tex)
    {
        tex.CalculateData();
        if (tex.Colors.Count == 0) return null;
        int w = tex.ImageWidthPower  > 0 ? (1 << tex.ImageWidthPower)  : 1;
        int h = tex.ImageHeightPower > 0 ? (1 << tex.ImageHeightPower) : 1;
        var rgba = new byte[Math.Max(tex.Colors.Count, w * h) * 4];
        int idx = 0;
        foreach (Twinsanity.TwinsanityInterchange.Common.Color c in tex.Colors)
        {
            if (idx >= rgba.Length) break;
            rgba[idx++] = c.R;
            rgba[idx++] = c.G;
            rgba[idx++] = c.B;
            rgba[idx++] = c.A;
        }
        return (w, h, rgba);
    }

    public static List<Texture2D> DecodePsf(GL gl, Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PSF psf) =>
        psf.FontPages.Select(p => DecodeUiTexture(gl, p)).Where(t => t is not null).Select(t => t!).ToList();

    public readonly record struct PsfGlyph(int Codepoint, int PageIndex, int X, int Y, int W, int H);

    public static (List<(int W, int H, byte[] Rgba)> Pages, List<PsfGlyph> Glyphs)? GetPsfRawGlyphs(
        Twinsanity.TwinsanityInterchange.Implementations.PS2.PS2PSF psf)
    {
        if (psf.FontPages.Count != 1) return null;

        if (psf.FontPages[0].Texture is not Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics.PS2AnyTexture tex)
            return null;
        var raw = DecodeRaw(tex);
        if (raw is null) return null;

        var glyphs = new List<PsfGlyph>();
        for (int i = 0; i < psf.UnkVecs.Count; i++)
        {
            var v = psf.UnkVecs[i];
            int x = (int)v.X, y = (int)v.Y, w = (int)v.Z, h = (int)v.W;
            if (w <= 0 || h <= 0) continue;
            glyphs.Add(new PsfGlyph(psf.UnkInt + i, 0, x, y, w, h));
        }
        return (new List<(int, int, byte[])> { raw.Value }, glyphs);
    }


    public static List<(string Name, int TexturePage)> GetParticleSystemCatalog(PS2AnyTwinsanityRM2 rm2)
    {
        var result = new List<(string, int)>();
        var data = rm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData>((uint)Constants.LEVEL_PARTICLES_ITEM);
        if (data is null) return result;
        foreach (var sys in data.ParticleSystems)
        {
            var name = new string(sys.Name).TrimEnd('\0', ' ');
            if (string.IsNullOrWhiteSpace(name)) name = "(unnamed)";
            result.Add((name, sys.UnkInt));
        }
        return result.OrderBy(t => t.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem? FindParticleSystem(PS2AnyTwinsanityRM2 rm2, string name)
    {
        var data = rm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyParticleData>((uint)Constants.LEVEL_PARTICLES_ITEM);
        if (data is null) return null;
        foreach (var sys in data.ParticleSystems)
            if (string.Equals(new string(sys.Name).TrimEnd('\0', ' '), name, StringComparison.OrdinalIgnoreCase))
                return sys;
        return null;
    }

    public readonly record struct ParticleTexturePage(Texture2D? Tex, int Width, int Height, List<(int X, int Y, int W, int H, float FillRatio)> Icons);

    public static ParticleTexturePage[] GetParticleTexturePages(GL gl, PS2AnyTwinsanityRM2 defaultRm2)
    {
        var result = new ParticleTexturePage[3];
        var data = defaultRm2.GetItem<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2DefaultParticleData>((uint)Constants.LEVEL_PARTICLES_ITEM);
        if (data is null) return result;
        var gfx = defaultRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var texSec = gfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        if (texSec is null) return result;
        for (int i = 0; i < texSec.GetItemsAmount(); i++)
        {
            if (texSec.GetItem(i) is not PS2AnyTexture tex) continue;
            for (int p = 0; p < 3; p++)
            {
                if (tex.GetID() != data.TextureIDs[p]) continue;
                var raw = DecodeRaw(tex);
                var gpuTex = ChunkImporter.DecodeTexture(gl, tex);
                var icons = raw is { } r ? SegmentSpriteIcons(r.rgba, r.w, r.h) : new List<(int, int, int, int, float)>();
                result[p] = new ParticleTexturePage(gpuTex, raw?.w ?? 0, raw?.h ?? 0, icons);
            }
        }
        return result;
    }

    private static readonly (int dx, int dy)[] NeighborOffsets = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    private static List<(int X, int Y, int W, int H, float FillRatio)> SegmentSpriteIcons(byte[] rgba, int w, int h)
    {
        var visited = new bool[w * h];
        var result = new List<(int, int, int, int, float)>();
        bool IsFg(int x, int y) => rgba[(y * w + x) * 4 + 3] > 64;
        var stack = new Stack<(int x, int y)>();
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int idx = y * w + x;
            if (visited[idx] || !IsFg(x, y)) continue;
            int minX = x, maxX = x, minY = y, maxY = y;
            int pixelCount = 1;
            stack.Push((x, y));
            visited[idx] = true;
            while (stack.Count > 0)
            {
                var (cx, cy) = stack.Pop();
                if (cx < minX) minX = cx; if (cx > maxX) maxX = cx;
                if (cy < minY) minY = cy; if (cy > maxY) maxY = cy;
                foreach (var (dx, dy) in NeighborOffsets)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    int nidx = ny * w + nx;
                    if (visited[nidx] || !IsFg(nx, ny)) continue;
                    visited[nidx] = true;
                    pixelCount++;
                    stack.Push((nx, ny));
                }
            }
            int bw = maxX - minX + 1, bh = maxY - minY + 1;
            if (bw >= 4 && bh >= 4)
                result.Add((minX, minY, bw, bh, pixelCount / (float)(bw * bh)));
        }
        return result.OrderByDescending(r => r.Item3 * r.Item4).ToList();
    }

    public static List<(float T, SysVec4 Color)> GetColorGradient(Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem sys)
    {
        var stops = new List<(float, SysVec4)>();
        foreach (var v in sys.UnkVecs)
        {
            bool allZero = v.X == 0 && v.Y == 0 && v.Z == 0 && v.W == 0;
            if (allZero && stops.Count > 0) break;
            stops.Add((v.X, new SysVec4(v.Y / 255f, v.Z / 255f, v.W / 255f, 1f)));
        }
        if (stops.Count == 0) stops.Add((0f, new SysVec4(1f, 1f, 1f, 1f)));
        return stops;
    }

    public static SysVec4 SampleColorGradient(List<(float T, SysVec4 Color)> stops, float t)
    {
        if (stops.Count == 1) return stops[0].Color;
        if (t <= stops[0].T) return stops[0].Color;
        if (t >= stops[^1].T) return stops[^1].Color;
        for (int i = 0; i < stops.Count - 1; i++)
        {
            var (t0, c0) = stops[i];
            var (t1, c1) = stops[i + 1];
            if (t >= t0 && t <= t1)
            {
                float span = t1 - t0;
                float f = span > 0.0001f ? (t - t0) / span : 0f;
                return SysVec4.Lerp(c0, c1, f);
            }
        }
        return stops[^1].Color;
    }

    public static List<(float T, float Scale)> GetSizeGradient(Twinsanity.TwinsanityInterchange.Common.Particles.TwinParticleSystem sys)
    {
        var raw = new List<float>();
        foreach (var l in sys.UnkLongs1)
        {
            bool isZero = l == 0;
            if (isZero && raw.Count > 0) break;
            int lo = (int)(l & 0xFFFFFFFFL);
            raw.Add(BitConverter.Int32BitsToSingle(lo));
        }
        if (raw.Count == 0) return new List<(float, float)> { (0f, 1f) };
        if (raw.Count == 1) return new List<(float, float)> { (0f, raw[0]) };

        var stops = new List<(float, float)>();
        for (int i = 0; i < raw.Count; i++)
            stops.Add((i / (float)(raw.Count - 1), raw[i]));
        return stops;
    }

    public static float SampleSizeGradient(List<(float T, float Scale)> stops, float t)
    {
        if (stops.Count == 1) return stops[0].Scale;
        if (t <= stops[0].T) return stops[0].Scale;
        if (t >= stops[^1].T) return stops[^1].Scale;
        for (int i = 0; i < stops.Count - 1; i++)
        {
            var (t0, s0) = stops[i];
            var (t1, s1) = stops[i + 1];
            if (t >= t0 && t <= t1)
            {
                float span = t1 - t0;
                float f = span > 0.0001f ? (t - t0) / span : 0f;
                return s0 + (s1 - s0) * f;
            }
        }
        return stops[^1].Scale;
    }

    public static SysVec4 GuessParticleTint(string name)
    {
        var n = name.ToUpperInvariant();
        if (n.Contains("NITRO") || n.Contains("SPARK"))              return new SysVec4(1.0f, 0.95f, 0.2f, 1f);
        if (n.Contains("FIRE") || n.Contains("EXPLO"))                return new SysVec4(1.0f, 0.5f, 0.1f, 1f);
        if (n.Contains("WATER"))                                     return new SysVec4(0.2f, 0.5f, 1.0f, 1f);
        if (n.Contains("SMOKE"))                                     return new SysVec4(0.6f, 0.6f, 0.6f, 1f);
        if (n.Contains("DUST"))                                      return new SysVec4(0.85f, 0.75f, 0.55f, 1f);
        if (n.Contains("SNOW") || n.Contains("ICE"))                  return new SysVec4(0.7f, 0.9f, 1.0f, 1f);
        if (n.Contains("SLIDE") || n.Contains("TRAIL"))               return new SysVec4(0.3f, 0.9f, 0.9f, 1f);
        if (n.Contains("RING") || n.Contains("WORM"))                 return new SysVec4(0.7f, 0.3f, 0.9f, 1f);
        return new SysVec4(0.85f, 0.85f, 0.85f, 1f);
    }


    public static List<(uint Id, string Name)> GetObjectCatalogFromRaw(PS2AnyTwinsanityRM2 rm2)
    {
        var result = new List<(uint, string)>();
        var code   = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var objSec = code?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        if (objSec is null) return result;
        for (int i = 0; i < objSec.GetItemsAmount(); i++)
            if (objSec.GetItem(i) is PS2AnyObject o)
                result.Add((o.GetID(), string.IsNullOrWhiteSpace(o.Name) ? $"Object_{o.GetID():X4}" : o.Name.Replace("|", "_")));
        return result.OrderBy(t => t.Item2, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<(uint Id, string Name)> GetReskinnableObjectCatalogFromRaw(PS2AnyTwinsanityRM2 rm2)
    {
        var result = new List<(uint, string)>();
        var code   = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var objSec = code?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var ogiSec = code?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        if (objSec is null || ogiSec is null) return result;

        bool HasRenderableOgi(PS2AnyObject obj)
        {
            foreach (var ogiId in obj.OGISlots)
                if (ogiSec.GetItem<PS2AnyOGI>(ogiId) is { } ogi && (ogi.SkinID != 0 || ogi.RigidModelIds.Count > 0))
                    return true;
            return false;
        }

        for (int i = 0; i < objSec.GetItemsAmount(); i++)
            if (objSec.GetItem(i) is PS2AnyObject o && HasRenderableOgi(o))
                result.Add((o.GetID(), string.IsNullOrWhiteSpace(o.Name) ? $"Object_{o.GetID():X4}" : o.Name.Replace("|", "_")));
        return result.OrderBy(t => t.Item2, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static T CloneItem<T>(T source) where T : BaseTwinItem, new()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms))
            source.Write(w);
        var bytes = ms.ToArray();
        var clone = new T();
        using var rs = new MemoryStream(bytes);
        using var r  = new BinaryReader(rs);
        clone.Read(r, bytes.Length);
        clone.SetID(source.GetID());
        return clone;
    }

    private static ushort NewCodeLocalId(BaseTwinSection section)
    {
        var rng = new Random();
        ushort id;
        do { id = (ushort)rng.Next(0x8000, 0xFFFF); }
        while (section.ContainsItem(id));
        return id;
    }

    private static uint NewGraphicsLocalId(BaseTwinSection section, params BaseTwinSection?[] alsoAvoid)
    {
        var rng = new Random();
        uint id;
        do { id = (uint)rng.Next(0x10000, 0x7FFFFFF); }
        while (id == 0 || section.ContainsItem(id) || alsoAvoid.Any(s => s?.ContainsItem(id) == true));
        return id;
    }

    private static ushort NewCodeLocalIdWithParity(BaseTwinSection section, uint requiredParity)
    {
        var rng = new Random();
        ushort id;
        do
        {
            id = (ushort)(rng.Next(0x4000, 0x7FFF) * 2 + (int)requiredParity);
        }
        while (section.ContainsItem(id));
        return id;
    }

    public static TransplantRecord? TransplantObject(GL gl,
        PS2AnyTwinsanityRM2 destRm2, object destTablesHandle, Dictionary<uint, Texture2D> destTexCache,
        PS2AnyTwinsanityRM2 sourceRm2, uint sourceObjectId, out string log)
    {
        if (destTablesHandle is not RmTables destTables) { log = "destination chunk has no mesh tables"; return null; }

        var srcCode     = sourceRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var srcObjSec   = srcCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var srcOgiSec   = srcCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var srcAnimSec  = srcCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_ANIMATIONS_SECTION);
        var srcGfx      = sourceRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var srcRmSec    = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var srcModelSec = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var srcMatSec   = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var srcSkinSec  = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION);
        var srcBlendSec = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_BLEND_SKINS_SECTION);
        var srcTexSec   = srcGfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);

        if (srcObjSec is null) { log = "source level has no GameObjects section"; return null; }
        var srcObj = srcObjSec.GetItem<PS2AnyObject>(sourceObjectId);
        if (srcObj is null) { log = "object not found in source level"; return null; }

        var destCode     = destRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var destObjSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var destOgiSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var destAnimSec  = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_ANIMATIONS_SECTION);
        var destSndSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_SOUND_EFFECTS_SECTION);
        var destGfx      = destRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var destRmSec    = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var destModelSec = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var destMatSec   = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var destSkinSec  = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION);
        var destBlendSec = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_BLEND_SKINS_SECTION);
        var destTexSec   = destGfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);

        if (destObjSec is null || destOgiSec is null || destAnimSec is null || destRmSec is null ||
            destModelSec is null || destMatSec is null || destSkinSec is null || destBlendSec is null || destTexSec is null)
        {
            log = "destination chunk is missing a required CODE/GRAPHICS section";
            return null;
        }

        var rec = new TransplantRecord();

        void EnsureTexture(uint texId)
        {
            if (destTexCache.ContainsKey(texId) || srcTexSec is null) return;
            var srcTex = srcTexSec.GetItem<PS2AnyTexture>(texId);
            if (srcTex is null) return;
            var clone = CloneItem(srcTex);
            if (!destTexSec.ContainsItem(texId)) { destTexSec.AddItem(clone); rec.TextureIds.Add(texId); }
            var t = ChunkImporter.DecodeTexture(gl, clone);
            if (t is not null) destTexCache[texId] = t;
        }

        void EnsureMaterial(uint matId)
        {
            if (matId == 0 || srcMatSec is null) return;
            var mat = destMatSec.GetItem<PS2AnyMaterial>(matId);
            if (mat is null)
            {
                var srcMat = srcMatSec.GetItem<PS2AnyMaterial>(matId);
                if (srcMat is null) return;
                mat = CloneItem(srcMat);
                destMatSec.AddItem(mat);
                rec.MaterialIds.Add(matId);
            }
            foreach (var sh in mat.Shaders) EnsureTexture(sh.TextureId);
        }

        void EnsureModel(uint modelId)
        {
            var model = destModelSec.GetItem<PS2AnyModel>(modelId);
            if (model is null)
            {
                if (srcModelSec is null) return;
                var srcModel = srcModelSec.GetItem<PS2AnyModel>(modelId);
                if (srcModel is null) return;
                model = CloneItem(srcModel);
                destModelSec.AddItem(model);
                rec.ModelIds.Add(modelId);
            }
            destTables.Models[modelId] = model;
        }

        void EnsureRigidModel(uint rmId)
        {
            if (srcRmSec is null) return;
            var rm = destRmSec.GetItem<PS2AnyRigidModel>(rmId);
            bool isNew = rm is null;
            if (isNew)
            {
                var srcRm = srcRmSec.GetItem<PS2AnyRigidModel>(rmId);
                if (srcRm is null) return;
                rm = CloneItem(srcRm);
                destRmSec.AddItem(rm);
                rec.RigidModelIds.Add(rmId);
            }
            destTables.RigidModels[rmId] = rm!;
            EnsureModel(rm!.Model);
            foreach (var m in rm.Materials) EnsureMaterial(m);
        }

        void EnsureSkin(uint skinId)
        {
            if (skinId == 0 || srcSkinSec is null) return;
            var skin = destSkinSec.GetItem<PS2AnySkin>(skinId);
            bool isNew = skin is null;
            if (isNew)
            {
                var srcSkin = srcSkinSec.GetItem<PS2AnySkin>(skinId);
                if (srcSkin is null) return;
                foreach (var ss in srcSkin.SubSkins) ss.CalculateData();
                skin = CloneItem(srcSkin);
                destSkinSec.AddItem(skin);
                rec.SkinIds.Add(skinId);
            }
            foreach (var ss in skin!.SubSkins) EnsureMaterial(ss.Material);
        }

        void EnsureBlendSkin(uint blendId)
        {
            if (blendId == 0 || srcBlendSec is null) return;
            var blend = destBlendSec.GetItem<PS2AnyBlendSkin>(blendId);
            bool isNew = blend is null;
            if (isNew)
            {
                var srcBlend = srcBlendSec.GetItem<PS2AnyBlendSkin>(blendId);
                if (srcBlend is null) return;
                foreach (var sb in srcBlend.SubBlends)
                    foreach (var model in sb.Models)
                        model.CalculateData();
                blend = CloneItem(srcBlend);
                destBlendSec.AddItem(blend);
                rec.BlendSkinIds.Add(blendId);
            }
            foreach (var sb in blend!.SubBlends) EnsureMaterial(sb.Material);
        }

        ushort? CloneOgi(uint srcOgiId)
        {
            if (destOgiSec.ContainsItem(srcOgiId)) return (ushort)srcOgiId;
            if (srcOgiSec is null) return null;
            var srcOgi = srcOgiSec.GetItem<PS2AnyOGI>(srcOgiId);
            if (srcOgi is null) return null;

            var newOgi = CloneItem(srcOgi);
            destOgiSec.AddItem(newOgi);
            rec.OgiIds.Add(srcOgiId);

            foreach (var rmId in newOgi.RigidModelIds) EnsureRigidModel(rmId);
            if (newOgi.SkinID != 0)      EnsureSkin(newOgi.SkinID);
            if (newOgi.BlendSkinID != 0) EnsureBlendSkin(newOgi.BlendSkinID);

            destTables.OGIs[(ushort)srcOgiId] = newOgi;
            return (ushort)srcOgiId;
        }

        ushort? CloneAnimation(uint srcAnimId)
        {
            if (destAnimSec.ContainsItem(srcAnimId)) return (ushort)srcAnimId;
            if (srcAnimSec is null) return null;
            var srcAnim = srcAnimSec.GetItem<PS2AnyAnimation>(srcAnimId);
            if (srcAnim is null) return null;

            var newAnim = CloneItem(srcAnim);
            destAnimSec.AddItem(newAnim);
            rec.AnimIds.Add(srcAnimId);

            destTables.Animations[(ushort)srcAnimId] = newAnim;
            return (ushort)srcAnimId;
        }

        bool objectAlreadyPresent = destObjSec.ContainsItem(sourceObjectId);
        var newObj = objectAlreadyPresent ? destObjSec.GetItem<PS2AnyObject>(sourceObjectId)! : CloneItem(srcObj);

        int nulledObjSlots = 0, nulledSndSlots = 0, prunedRefObj = 0, prunedRefSnd = 0;
        if (!objectAlreadyPresent)
        {
            for (int i = 0; i < newObj.OGISlots.Count; i++)
            {
                if (newObj.OGISlots[i] == 0xFFFF) continue;
                var mapped = CloneOgi(newObj.OGISlots[i]);
                newObj.OGISlots[i] = mapped ?? 0xFFFF;
            }
            for (int i = 0; i < newObj.AnimationSlots.Count; i++)
            {
                if (newObj.AnimationSlots[i] == 0xFFFF) continue;
                var mapped = CloneAnimation(newObj.AnimationSlots[i]);
                newObj.AnimationSlots[i] = mapped ?? 0xFFFF;
            }

            bool ObjPresent(uint id) => id == 0xFFFF || (destObjSec?.ContainsItem(id) ?? false);
            bool SndPresent(uint id) => id == 0xFFFF || (destSndSec?.ContainsItem(id) ?? false);
            for (int i = 0; i < newObj.ObjectSlots.Count; i++)
                if (!ObjPresent(newObj.ObjectSlots[i])) { newObj.ObjectSlots[i] = 0xFFFF; nulledObjSlots++; }
            for (int i = 0; i < newObj.SoundSlots.Count; i++)
                if (!SndPresent(newObj.SoundSlots[i]))  { newObj.SoundSlots[i]  = 0xFFFF; nulledSndSlots++; }
            prunedRefObj = newObj.RefObjects.RemoveAll(id => !ObjPresent(id));
            prunedRefSnd = newObj.RefSounds.RemoveAll(id => !SndPresent(id));

            destObjSec.AddItem(newObj);
        }

        ushort newObjId = (ushort)sourceObjectId;
        destTables.Objects[newObjId] = newObj;
        rec.ObjectId = newObjId;
        rec.ObjectWasNew = !objectAlreadyPresent;

        var modelById2 = BuildModelLookup(destGfx!);
        foreach (var kv in BuildMeshLookup(destGfx!, modelById2, gl, destTexCache)) destTables.ModelGpu[kv.Key] = kv.Value;
        foreach (var kv in BuildSkinLookup(destGfx!, gl, destTexCache))             destTables.SkinGpu[kv.Key]  = kv.Value;
        foreach (var kv in BuildBlendSkinLookup(destGfx!, gl, destTexCache, destTables.BlendSkinRaw))
            destTables.BlendSkinGpu[kv.Key] = kv.Value;

        log = objectAlreadyPresent
            ? $"'{newObj.Name}' (id 0x{newObjId:X4}) already exists in this chunk under the same shared id — reused as-is, nothing duplicated."
            : $"transplanted '{newObj.Name}' (id 0x{newObjId:X4}, preserved from source) — " +
              $"+{rec.OgiIds.Count} OGI, +{rec.AnimIds.Count} Animation, +{rec.RigidModelIds.Count} RigidModel, +{rec.ModelIds.Count} Model, " +
              $"+{rec.SkinIds.Count} Skin, +{rec.BlendSkinIds.Count} BlendSkin, +{rec.MaterialIds.Count} Material, +{rec.TextureIds.Count} Texture. " +
              $"Neutralized dangling refs (load-safety): {nulledObjSlots} ObjectSlot + {nulledSndSlots} SoundSlot nulled, {prunedRefObj} RefObject + {prunedRefSnd} RefSound dropped. Behaviours kept as-is.";
        return rec;
    }

    public static TransplantRecord? TransplantOgiOnly(GL gl,
        PS2AnyTwinsanityRM2 destRm2, object destTablesHandle, Dictionary<uint, Texture2D> destTexCache,
        PS2AnyTwinsanityRM2 sourceRm2, uint sourceOgiId, out string log)
    {
        if (destTablesHandle is not RmTables destTables) { log = "destination chunk has no mesh tables"; return null; }

        var srcCode     = sourceRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var srcOgiSec   = srcCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var srcGfx      = sourceRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var srcRmSec    = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var srcModelSec = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var srcMatSec   = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var srcSkinSec  = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION);
        var srcBlendSec = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_BLEND_SKINS_SECTION);
        var srcTexSec   = srcGfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);

        if (srcOgiSec is null) { log = "source level has no OGIs section"; return null; }
        var srcOgi = srcOgiSec.GetItem<PS2AnyOGI>(sourceOgiId);
        if (srcOgi is null) { log = "OGI not found in source level"; return null; }

        var destCode     = destRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var destOgiSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var destGfx      = destRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var destRmSec    = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var destModelSec = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var destMatSec   = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var destSkinSec  = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION);
        var destBlendSec = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_BLEND_SKINS_SECTION);
        var destTexSec   = destGfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);

        if (destOgiSec is null || destRmSec is null || destModelSec is null || destMatSec is null ||
            destSkinSec is null || destBlendSec is null || destTexSec is null)
        { log = "destination chunk is missing a required GRAPHICS/OGI section"; return null; }

        var rec = new TransplantRecord();

        void EnsureTexture(uint texId)
        {
            if (destTexCache.ContainsKey(texId) || srcTexSec is null) return;
            var srcTex = srcTexSec.GetItem<PS2AnyTexture>(texId);
            if (srcTex is null) return;
            var clone = CloneItem(srcTex);
            if (!destTexSec.ContainsItem(texId)) { destTexSec.AddItem(clone); rec.TextureIds.Add(texId); }
            var t = ChunkImporter.DecodeTexture(gl, clone);
            if (t is not null) destTexCache[texId] = t;
        }

        void EnsureMaterial(uint matId)
        {
            if (matId == 0 || srcMatSec is null) return;
            var mat = destMatSec.GetItem<PS2AnyMaterial>(matId);
            if (mat is null)
            {
                var srcMat = srcMatSec.GetItem<PS2AnyMaterial>(matId);
                if (srcMat is null) return;
                mat = CloneItem(srcMat);
                destMatSec.AddItem(mat);
                rec.MaterialIds.Add(matId);
            }
            foreach (var sh in mat.Shaders) EnsureTexture(sh.TextureId);
        }

        void EnsureModel(uint modelId)
        {
            var model = destModelSec.GetItem<PS2AnyModel>(modelId);
            if (model is null)
            {
                if (srcModelSec is null) return;
                var srcModel = srcModelSec.GetItem<PS2AnyModel>(modelId);
                if (srcModel is null) return;
                model = CloneItem(srcModel);
                destModelSec.AddItem(model);
                rec.ModelIds.Add(modelId);
            }
            destTables.Models[modelId] = model;
        }

        void EnsureRigidModel(uint rmId)
        {
            if (srcRmSec is null) return;
            var rm = destRmSec.GetItem<PS2AnyRigidModel>(rmId);
            bool isNew = rm is null;
            if (isNew)
            {
                var srcRm = srcRmSec.GetItem<PS2AnyRigidModel>(rmId);
                if (srcRm is null) return;
                rm = CloneItem(srcRm);
                destRmSec.AddItem(rm);
                rec.RigidModelIds.Add(rmId);
            }
            destTables.RigidModels[rmId] = rm!;
            EnsureModel(rm!.Model);
            foreach (var m in rm.Materials) EnsureMaterial(m);
        }

        void EnsureSkin(uint skinId)
        {
            if (skinId == 0 || srcSkinSec is null) return;
            var skin = destSkinSec.GetItem<PS2AnySkin>(skinId);
            bool isNew = skin is null;
            if (isNew)
            {
                var srcSkin = srcSkinSec.GetItem<PS2AnySkin>(skinId);
                if (srcSkin is null) return;
                foreach (var ss in srcSkin.SubSkins) ss.CalculateData();
                skin = CloneItem(srcSkin);
                destSkinSec.AddItem(skin);
                rec.SkinIds.Add(skinId);
            }
            foreach (var ss in skin!.SubSkins) EnsureMaterial(ss.Material);
        }

        void EnsureBlendSkin(uint blendId)
        {
            if (blendId == 0 || srcBlendSec is null) return;
            var blend = destBlendSec.GetItem<PS2AnyBlendSkin>(blendId);
            bool isNew = blend is null;
            if (isNew)
            {
                var srcBlend = srcBlendSec.GetItem<PS2AnyBlendSkin>(blendId);
                if (srcBlend is null) return;
                foreach (var sb in srcBlend.SubBlends)
                    foreach (var model in sb.Models)
                        model.CalculateData();
                blend = CloneItem(srcBlend);
                destBlendSec.AddItem(blend);
                rec.BlendSkinIds.Add(blendId);
            }
            foreach (var sb in blend!.SubBlends) EnsureMaterial(sb.Material);
        }

        if (!destOgiSec.ContainsItem(sourceOgiId))
        {
            var newOgi = CloneItem(srcOgi);
            destOgiSec.AddItem(newOgi);
            rec.OgiIds.Add(sourceOgiId);

            foreach (var rmId in newOgi.RigidModelIds) EnsureRigidModel(rmId);
            if (newOgi.SkinID != 0)      EnsureSkin(newOgi.SkinID);
            if (newOgi.BlendSkinID != 0) EnsureBlendSkin(newOgi.BlendSkinID);

            destTables.OGIs[(ushort)sourceOgiId] = newOgi;
        }

        var modelById2 = BuildModelLookup(destGfx!);
        foreach (var kv in BuildMeshLookup(destGfx!, modelById2, gl, destTexCache)) destTables.ModelGpu[kv.Key] = kv.Value;
        foreach (var kv in BuildSkinLookup(destGfx!, gl, destTexCache))             destTables.SkinGpu[kv.Key]  = kv.Value;
        foreach (var kv in BuildBlendSkinLookup(destGfx!, gl, destTexCache, destTables.BlendSkinRaw))
            destTables.BlendSkinGpu[kv.Key] = kv.Value;

        log = rec.OgiIds.Count > 0
            ? $"transplanted OGI 0x{sourceOgiId:X4} (visual only — no object/script data brought in) — " +
              $"+{rec.RigidModelIds.Count} RigidModel, +{rec.ModelIds.Count} Model, +{rec.SkinIds.Count} Skin, " +
              $"+{rec.BlendSkinIds.Count} BlendSkin, +{rec.MaterialIds.Count} Material, +{rec.TextureIds.Count} Texture."
            : $"OGI 0x{sourceOgiId:X4} already exists in this chunk under the same shared id — reused as-is, nothing duplicated.";
        return rec;
    }

    public static void RemoveTransplant(PS2AnyTwinsanityRM2 destRm2, object destTablesHandle,
        Dictionary<uint, Texture2D> destTexCache, TransplantRecord rec)
    {
        if (destTablesHandle is not RmTables destTables) return;

        var destCode     = destRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var destObjSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var destOgiSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var destAnimSec  = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_ANIMATIONS_SECTION);
        var destGfx      = destRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var destRmSec    = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var destModelSec = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var destMatSec   = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var destSkinSec  = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION);
        var destBlendSec = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_BLEND_SKINS_SECTION);
        var destTexSec   = destGfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);

        if (rec.ObjectWasNew)
        {
            destObjSec?.RemoveItem<PS2AnyObject>(rec.ObjectId);
            destTables.Objects.Remove(rec.ObjectId);
        }

        foreach (var ogiId in rec.OgiIds)
        {
            destOgiSec?.RemoveItem<PS2AnyOGI>(ogiId);
            destTables.OGIs.Remove(ogiId);
        }
        foreach (var animId in rec.AnimIds)
        {
            destAnimSec?.RemoveItem<PS2AnyAnimation>(animId);
            destTables.Animations.Remove(animId);
        }

        var remainingOgis = destTables.OGIs.Values.ToList();
        bool RmStillUsed(uint id)   => remainingOgis.Any(o => o.RigidModelIds.Contains(id));
        bool SkinStillUsed(uint id) => remainingOgis.Any(o => o.SkinID == id);
        bool BlendStillUsed(uint id)=> remainingOgis.Any(o => o.BlendSkinID == id);

        var keepMat = new HashSet<uint>();
        var keepModel = new HashSet<uint>();
        void KeepFromRigid(uint rmId)
        {
            var rm = destRmSec?.GetItem<PS2AnyRigidModel>(rmId);
            if (rm is null) return;
            keepModel.Add(rm.Model);
            foreach (var m in rm.Materials) keepMat.Add(m);
        }
        foreach (var o in remainingOgis)
        {
            foreach (var rmId in o.RigidModelIds) KeepFromRigid(rmId);
            if (o.SkinID != 0 && destSkinSec?.GetItem<PS2AnySkin>(o.SkinID) is { } sk)
                foreach (var ss in sk.SubSkins) keepMat.Add(ss.Material);
            if (o.BlendSkinID != 0 && destBlendSec?.GetItem<PS2AnyBlendSkin>(o.BlendSkinID) is { } bs)
                foreach (var sb in bs.SubBlends) keepMat.Add(sb.Material);
        }
        var keepTex = new HashSet<uint>();
        foreach (var matId in keepMat)
            if (destMatSec?.GetItem<PS2AnyMaterial>(matId) is { } m)
                foreach (var sh in m.Shaders) keepTex.Add(sh.TextureId);

        foreach (var id in rec.RigidModelIds) if (!RmStillUsed(id))    { destRmSec?.RemoveItem<PS2AnyRigidModel>(id); destTables.ModelGpu.Remove(id); }
        foreach (var id in rec.SkinIds)       if (!SkinStillUsed(id))  { destSkinSec?.RemoveItem<PS2AnySkin>(id);     destTables.SkinGpu.Remove(id); }
        foreach (var id in rec.BlendSkinIds)  if (!BlendStillUsed(id)) { destBlendSec?.RemoveItem<PS2AnyBlendSkin>(id); destTables.BlendSkinGpu.Remove(id); destTables.BlendSkinRaw.Remove(id); }
        foreach (var id in rec.ModelIds)      if (!keepModel.Contains(id)) destModelSec?.RemoveItem<PS2AnyModel>(id);
        foreach (var id in rec.MaterialIds)   if (!keepMat.Contains(id))   destMatSec?.RemoveItem<PS2AnyMaterial>(id);
        foreach (var id in rec.TextureIds)
            if (!keepTex.Contains(id))
            {
                destTexSec?.RemoveItem<PS2AnyTexture>(id);
                if (destTexCache.Remove(id, out var tex)) tex.Dispose();
            }
    }

    public sealed class FullTransplantRecord
    {
        public uint RootObjectId;
        public readonly Dictionary<uint, uint> ObjectIdMap = new();
        public readonly List<uint> OgiIds = new();
        public readonly List<uint> AnimIds = new();
        public readonly List<uint> BehaviourIds = new();
        public readonly List<uint> SoundIds = new();
        public readonly List<uint> RigidModelIds = new();
        public readonly List<uint> ModelIds = new();
        public readonly List<uint> SkinIds = new();
        public readonly List<uint> BlendSkinIds = new();
        public readonly List<uint> MaterialIds = new();
        public readonly List<uint> TextureIds = new();
    }

    public static void RemoveFullTransplant(PS2AnyTwinsanityRM2 destRm2, object destTablesHandle,
        Dictionary<uint, Texture2D> destTexCache, FullTransplantRecord rec)
    {
        if (destTablesHandle is not RmTables destTables) return;

        var destCode     = destRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var destObjSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var destOgiSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var destAnimSec  = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_ANIMATIONS_SECTION);
        var destBehSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_BEHAVIOURS_SECTION);
        var destSndSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_SOUND_EFFECTS_SECTION);
        var destGfx      = destRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var destRmSec    = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var destModelSec = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var destMatSec   = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var destSkinSec  = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION);
        var destBlendSec = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_BLEND_SKINS_SECTION);
        var destTexSec   = destGfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);

        var removedObjIds = new HashSet<uint>(rec.ObjectIdMap.Values);
        bool ObjStillNeeded(uint id) => destTables.Objects.Values.Any(o =>
            !removedObjIds.Contains(o.GetID()) &&
            (o.RefObjects.Contains((ushort)id) || o.ObjectSlots.Contains((ushort)id)));

        foreach (var newObjId in rec.ObjectIdMap.Values)
        {
            if (newObjId != rec.RootObjectId && ObjStillNeeded(newObjId)) continue;
            destObjSec?.RemoveItem<PS2AnyObject>(newObjId);
            destTables.Objects.Remove(newObjId);
        }

        var remainingObjects = destTables.Objects.Values.ToList();
        bool OgiStillNeeded(uint id)  => remainingObjects.Any(o => o.OGISlots.Contains((ushort)id) || o.RefOGIs.Contains((ushort)id));
        bool AnimStillNeeded(uint id) => remainingObjects.Any(o => o.AnimationSlots.Contains((ushort)id) || o.RefAnimations.Contains((ushort)id));
        bool SoundStillNeeded(uint id) => remainingObjects.Any(o => o.SoundSlots.Contains((ushort)id) || o.RefSounds.Contains((ushort)id));
        bool BehStillNeeded(uint id)
        {
            if (remainingObjects.Any(o => o.BehaviourSlots.Contains((ushort)id) || o.RefBehaviours.Contains((ushort)id)))
                return true;
            for (int i = 0; i < (destBehSec?.GetItemsAmount() ?? 0); i++)
                if (destBehSec!.GetItem(i) is TwinBehaviourStarter starter &&
                    !rec.BehaviourIds.Contains(starter.GetID()) &&
                    starter.Assigners.Any(a => a.Behaviour == (int)id))
                    return true;
            return false;
        }

        foreach (var id in rec.BehaviourIds) if (!BehStillNeeded(id))   destBehSec?.RemoveItem<BaseTwinItem>(id);
        foreach (var id in rec.SoundIds)     if (!SoundStillNeeded(id)) destSndSec?.RemoveItem<PS2AnySound>(id);

        foreach (var ogiId in rec.OgiIds)
        {
            if (OgiStillNeeded(ogiId)) continue;
            destOgiSec?.RemoveItem<PS2AnyOGI>(ogiId);
            destTables.OGIs.Remove(ogiId);
        }
        foreach (var animId in rec.AnimIds)
        {
            if (AnimStillNeeded(animId)) continue;
            destAnimSec?.RemoveItem<PS2AnyAnimation>(animId);
            destTables.Animations.Remove(animId);
        }

        var remainingOgis = destTables.OGIs.Values.ToList();
        bool RmStillUsed(uint id)    => remainingOgis.Any(o => o.RigidModelIds.Contains(id));
        bool SkinStillUsed(uint id)  => remainingOgis.Any(o => o.SkinID == id);
        bool BlendStillUsed(uint id) => remainingOgis.Any(o => o.BlendSkinID == id);

        var keepMat = new HashSet<uint>();
        var keepModel = new HashSet<uint>();
        void KeepFromRigid(uint rmId)
        {
            var rm = destRmSec?.GetItem<PS2AnyRigidModel>(rmId);
            if (rm is null) return;
            keepModel.Add(rm.Model);
            foreach (var m in rm.Materials) keepMat.Add(m);
        }
        foreach (var o in remainingOgis)
        {
            foreach (var rmId in o.RigidModelIds) KeepFromRigid(rmId);
            if (o.SkinID != 0 && destSkinSec?.GetItem<PS2AnySkin>(o.SkinID) is { } sk)
                foreach (var ss in sk.SubSkins) keepMat.Add(ss.Material);
            if (o.BlendSkinID != 0 && destBlendSec?.GetItem<PS2AnyBlendSkin>(o.BlendSkinID) is { } bs)
                foreach (var sb in bs.SubBlends) keepMat.Add(sb.Material);
        }
        var keepTex = new HashSet<uint>();
        foreach (var matId in keepMat)
            if (destMatSec?.GetItem<PS2AnyMaterial>(matId) is { } m)
                foreach (var sh in m.Shaders) keepTex.Add(sh.TextureId);

        foreach (var id in rec.RigidModelIds) if (!RmStillUsed(id))    { destRmSec?.RemoveItem<PS2AnyRigidModel>(id); destTables.ModelGpu.Remove(id); }
        foreach (var id in rec.SkinIds)       if (!SkinStillUsed(id))  { destSkinSec?.RemoveItem<PS2AnySkin>(id);     destTables.SkinGpu.Remove(id); }
        foreach (var id in rec.BlendSkinIds)  if (!BlendStillUsed(id)) { destBlendSec?.RemoveItem<PS2AnyBlendSkin>(id); destTables.BlendSkinGpu.Remove(id); destTables.BlendSkinRaw.Remove(id); }
        foreach (var id in rec.ModelIds)      if (!keepModel.Contains(id)) destModelSec?.RemoveItem<PS2AnyModel>(id);
        foreach (var id in rec.MaterialIds)   if (!keepMat.Contains(id))   destMatSec?.RemoveItem<PS2AnyMaterial>(id);
        foreach (var id in rec.TextureIds)
            if (!keepTex.Contains(id))
            {
                destTexSec?.RemoveItem<PS2AnyTexture>(id);
                if (destTexCache.Remove(id, out var tex)) tex.Dispose();
            }
    }

    private static BaseTwinItem CloneItemDynamic(BaseTwinItem source)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms)) source.Write(w);
        var bytes = ms.ToArray();
        var clone = (BaseTwinItem)Activator.CreateInstance(source.GetType())!;
        using var rs = new MemoryStream(bytes);
        using var r = new BinaryReader(rs);
        clone.Read(r, bytes.Length);
        clone.SetID(source.GetID());
        return clone;
    }

    public static List<uint> GetObjectBehaviourIds(PS2AnyTwinsanityRM2 rm2, uint objId)
    {
        var res = new List<uint>();
        var obj = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION)
            ?.GetItem<PS2AnyObject>(objId);
        if (obj is null) return res;
        foreach (var b in obj.BehaviourSlots) if (b != 0xFFFF && !res.Contains(b)) res.Add(b);
        foreach (var b in obj.RefBehaviours)  if (b != 0xFFFF && !res.Contains(b)) res.Add(b);
        return res;
    }

    public static List<uint> TransplantBehaviours(PS2AnyTwinsanityRM2 srcRm2, PS2AnyTwinsanityRM2 destRm2,
        uint srcObjId, uint targetObjId, IReadOnlyCollection<uint> selectedIds)
    {
        var srcBehSec  = srcRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)Constants.CODE_BEHAVIOURS_SECTION);
        var srcObj     = srcRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
            ?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION)?.GetItem<PS2AnyObject>(srcObjId);
        var destCode   = destRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var destBehSec = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_BEHAVIOURS_SECTION);
        var targetObj  = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION)
            ?.GetItem<PS2AnyObject>(targetObjId);
        var attached = new List<uint>();
        if (srcBehSec is null || destBehSec is null || srcObj is null || targetObj is null) return attached;

        var sel = new HashSet<uint>(selectedIds);

        void Clone(uint behId)
        {
            if (destBehSec.ContainsItem(behId)) return;
            var srcBeh = srcBehSec.GetItem<BaseTwinItem>(behId);
            if (srcBeh is null) return;
            destBehSec.AddItem(CloneItemDynamic(srcBeh));
            if (srcBeh is Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourStarter starter)
                foreach (var a in starter.Assigners) if (a.Behaviour != 0) Clone((uint)a.Behaviour);
        }
        foreach (var id in selectedIds) Clone(id);

        targetObj.BehaviourSlots.Clear();
        foreach (var b in srcObj.BehaviourSlots)
            if (b == 0xFFFF || sel.Contains(b))
            {
                targetObj.BehaviourSlots.Add(b);
                if (b != 0xFFFF && destBehSec.ContainsItem(b) && !attached.Contains(b)) attached.Add(b);
            }
        targetObj.RefBehaviours.Clear();
        foreach (var b in srcObj.RefBehaviours)
            if (b == 0xFFFF || sel.Contains(b))
            {
                targetObj.RefBehaviours.Add(b);
                if (b != 0xFFFF && destBehSec.ContainsItem(b) && !attached.Contains(b)) attached.Add(b);
            }
        return attached;
    }

    public static FullTransplantRecord? TransplantObjectFull(GL gl,
        PS2AnyTwinsanityRM2 destRm2, object destTablesHandle, Dictionary<uint, Texture2D> destTexCache,
        PS2AnyTwinsanityRM2 sourceRm2, uint rootSourceObjectId, out string log)
    {
        if (destTablesHandle is not RmTables destTables) { log = "destination chunk has no mesh tables"; return null; }

        var srcCode     = sourceRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var srcObjSec   = srcCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var srcOgiSec   = srcCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var srcAnimSec  = srcCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_ANIMATIONS_SECTION);
        var srcBehSec   = srcCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_BEHAVIOURS_SECTION);
        var srcSndSec   = srcCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_SOUND_EFFECTS_SECTION);
        var srcGfx      = sourceRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var srcRmSec    = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var srcModelSec = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var srcMatSec   = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var srcSkinSec  = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION);
        var srcBlendSec = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_BLEND_SKINS_SECTION);
        var srcTexSec   = srcGfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);

        if (srcObjSec is null) { log = "source level has no GameObjects section"; return null; }
        var rootSrcObj = srcObjSec.GetItem<PS2AnyObject>(rootSourceObjectId);
        if (rootSrcObj is null) { log = "root object not found in source level"; return null; }

        var destCode     = destRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var destObjSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var destOgiSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var destAnimSec  = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_ANIMATIONS_SECTION);
        var destBehSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_BEHAVIOURS_SECTION);
        var destSndSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_SOUND_EFFECTS_SECTION);
        var destGfx      = destRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var destRmSec    = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var destModelSec = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var destMatSec   = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var destSkinSec  = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION);
        var destBlendSec = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_BLEND_SKINS_SECTION);
        var destTexSec   = destGfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);

        if (destObjSec is null || destOgiSec is null || destAnimSec is null || destBehSec is null || destSndSec is null ||
            destRmSec is null || destModelSec is null || destMatSec is null || destSkinSec is null || destBlendSec is null || destTexSec is null)
        { log = "destination chunk is missing a required CODE/GRAPHICS section"; return null; }

        var rec = new FullTransplantRecord();

        void EnsureTexture(uint texId)
        {
            if (destTexCache.ContainsKey(texId) || srcTexSec is null) return;
            var srcTex = srcTexSec.GetItem<PS2AnyTexture>(texId);
            if (srcTex is null) return;
            var clone = CloneItem(srcTex);
            if (!destTexSec.ContainsItem(texId)) { destTexSec.AddItem(clone); rec.TextureIds.Add(texId); }
            var t = ChunkImporter.DecodeTexture(gl, clone);
            if (t is not null) destTexCache[texId] = t;
        }
        void EnsureMaterial(uint matId)
        {
            if (matId == 0 || srcMatSec is null) return;
            var mat = destMatSec.GetItem<PS2AnyMaterial>(matId);
            if (mat is null)
            {
                var srcMat = srcMatSec.GetItem<PS2AnyMaterial>(matId);
                if (srcMat is null) return;
                mat = CloneItem(srcMat);
                destMatSec.AddItem(mat);
                rec.MaterialIds.Add(matId);
            }
            foreach (var sh in mat.Shaders) EnsureTexture(sh.TextureId);
        }
        void EnsureModel(uint modelId)
        {
            var model = destModelSec.GetItem<PS2AnyModel>(modelId);
            if (model is null)
            {
                if (srcModelSec is null) return;
                var srcModel = srcModelSec.GetItem<PS2AnyModel>(modelId);
                if (srcModel is null) return;
                model = CloneItem(srcModel);
                destModelSec.AddItem(model);
                rec.ModelIds.Add(modelId);
            }
            destTables.Models[modelId] = model;
        }
        void EnsureRigidModel(uint rmId)
        {
            if (srcRmSec is null) return;
            var rm = destRmSec.GetItem<PS2AnyRigidModel>(rmId);
            bool isNew = rm is null;
            if (isNew)
            {
                var srcRm = srcRmSec.GetItem<PS2AnyRigidModel>(rmId);
                if (srcRm is null) return;
                rm = CloneItem(srcRm);
                destRmSec.AddItem(rm);
                rec.RigidModelIds.Add(rmId);
            }
            destTables.RigidModels[rmId] = rm!;
            EnsureModel(rm!.Model);
            foreach (var m in rm.Materials) EnsureMaterial(m);
        }
        void EnsureSkin(uint skinId)
        {
            if (skinId == 0 || srcSkinSec is null) return;
            var skin = destSkinSec.GetItem<PS2AnySkin>(skinId);
            bool isNew = skin is null;
            if (isNew)
            {
                var srcSkin = srcSkinSec.GetItem<PS2AnySkin>(skinId);
                if (srcSkin is null) return;
                foreach (var ss in srcSkin.SubSkins) ss.CalculateData();
                skin = CloneItem(srcSkin);
                destSkinSec.AddItem(skin);
                rec.SkinIds.Add(skinId);
            }
            foreach (var ss in skin!.SubSkins) EnsureMaterial(ss.Material);
        }
        void EnsureBlendSkin(uint blendId)
        {
            if (blendId == 0 || srcBlendSec is null) return;
            var blend = destBlendSec.GetItem<PS2AnyBlendSkin>(blendId);
            bool isNew = blend is null;
            if (isNew)
            {
                var srcBlend = srcBlendSec.GetItem<PS2AnyBlendSkin>(blendId);
                if (srcBlend is null) return;
                foreach (var sb in srcBlend.SubBlends)
                    foreach (var model in sb.Models)
                        model.CalculateData();
                blend = CloneItem(srcBlend);
                destBlendSec.AddItem(blend);
                rec.BlendSkinIds.Add(blendId);
            }
            foreach (var sb in blend!.SubBlends) EnsureMaterial(sb.Material);
        }

        void CloneOgi(uint ogiId)
        {
            if (destOgiSec.ContainsItem(ogiId) || srcOgiSec is null) return;
            var srcOgi = srcOgiSec.GetItem<PS2AnyOGI>(ogiId);
            if (srcOgi is null) return;
            var newOgi = CloneItem(srcOgi);
            destOgiSec.AddItem(newOgi);
            rec.OgiIds.Add(ogiId);
            foreach (var rmId in newOgi.RigidModelIds) EnsureRigidModel(rmId);
            if (newOgi.SkinID != 0)      EnsureSkin(newOgi.SkinID);
            if (newOgi.BlendSkinID != 0) EnsureBlendSkin(newOgi.BlendSkinID);
            destTables.OGIs[ogiId] = newOgi;
        }
        void CloneAnimation(uint animId)
        {
            if (destAnimSec.ContainsItem(animId) || srcAnimSec is null) return;
            var srcAnim = srcAnimSec.GetItem<PS2AnyAnimation>(animId);
            if (srcAnim is null) return;
            var newAnim = CloneItem(srcAnim);
            destAnimSec.AddItem(newAnim);
            rec.AnimIds.Add(animId);
            destTables.Animations[animId] = newAnim;
        }
        void CloneBehaviour(uint behId)
        {
            if (destBehSec.ContainsItem(behId) || srcBehSec is null) return;
            var srcBeh = srcBehSec.GetItem<BaseTwinItem>(behId);
            if (srcBeh is null) return;
            var newBeh = CloneItemDynamic(srcBeh);
            destBehSec.AddItem(newBeh);
            rec.BehaviourIds.Add(behId);
            if (newBeh is TwinBehaviourStarter starter)
                foreach (var assigner in starter.Assigners)
                    if (assigner.Behaviour != 0)
                        CloneBehaviour((uint)assigner.Behaviour);
        }
        void CloneSound(uint sndId)
        {
            if (destSndSec.ContainsItem(sndId) || srcSndSec is null) return;
            var srcSnd = srcSndSec.GetItem<PS2AnySound>(sndId);
            if (srcSnd is null) return;
            var newSnd = new PS2AnySound
            {
                Header = srcSnd.Header, UnkFlag = srcSnd.UnkFlag, FreqFac = srcSnd.FreqFac,
                Param1 = srcSnd.Param1, Param2 = srcSnd.Param2, Param3 = srcSnd.Param3, Param4 = srcSnd.Param4,
                Sound = (byte[])srcSnd.Sound.Clone(),
            };
            newSnd.SetID(sndId);
            destSndSec.AddItem(newSnd);
            rec.SoundIds.Add(sndId);
        }

        var objQueue = new Queue<uint>();
        objQueue.Enqueue(rootSourceObjectId);
        var visited = new HashSet<uint>();
        int unhandledCodeModels = 0, unhandledUnknowns = 0, newObjectCount = 0;

        while (objQueue.Count > 0)
        {
            var srcId = objQueue.Dequeue();
            if (visited.Contains(srcId)) continue;
            visited.Add(srcId);
            rec.ObjectIdMap[srcId] = srcId;

            var srcObj = srcObjSec.GetItem<PS2AnyObject>(srcId);
            if (srcObj is null) continue;

            if (srcObj.RefCodeModels.Count > 0) unhandledCodeModels += srcObj.RefCodeModels.Count;
            if (srcObj.RefUnknowns.Count > 0)   unhandledUnknowns   += srcObj.RefUnknowns.Count;

            bool isNewObj = !destObjSec.ContainsItem(srcId);
            if (isNewObj)
            {
                var newObj = CloneItem(srcObj);
                destObjSec.AddItem(newObj);
                destTables.Objects[srcId] = newObj;
                newObjectCount++;
            }

            foreach (var s in srcObj.OGISlots)        if (s != 0xFFFF) CloneOgi(s);
            foreach (var s in srcObj.AnimationSlots)  if (s != 0xFFFF) CloneAnimation(s);
            foreach (var s in srcObj.SoundSlots)      if (s != 0xFFFF) CloneSound(s);
            foreach (var s in srcObj.BehaviourSlots)  if (s != 0xFFFF) CloneBehaviour(s);
            foreach (var refOgi in srcObj.RefOGIs)        CloneOgi(refOgi);
            foreach (var refAnim in srcObj.RefAnimations) CloneAnimation(refAnim);
            foreach (var refBeh in srcObj.RefBehaviours)  CloneBehaviour(refBeh);
            foreach (var refSnd in srcObj.RefSounds)      CloneSound(refSnd);

            foreach (var slotObjId in srcObj.ObjectSlots)
                if (slotObjId != 0xFFFF && !visited.Contains(slotObjId))
                    objQueue.Enqueue(slotObjId);
            foreach (var refObjId in srcObj.RefObjects)
                if (!visited.Contains(refObjId))
                    objQueue.Enqueue(refObjId);
        }

        var modelById2 = BuildModelLookup(destGfx!);
        foreach (var kv in BuildMeshLookup(destGfx!, modelById2, gl, destTexCache)) destTables.ModelGpu[kv.Key] = kv.Value;
        foreach (var kv in BuildSkinLookup(destGfx!, gl, destTexCache))             destTables.SkinGpu[kv.Key]  = kv.Value;
        foreach (var kv in BuildBlendSkinLookup(destGfx!, gl, destTexCache, destTables.BlendSkinRaw))
            destTables.BlendSkinGpu[kv.Key] = kv.Value;

        rec.RootObjectId = rootSourceObjectId;
        log = $"Full transplant of '{rootSrcObj.Name}' (id 0x{rec.RootObjectId:X4}, unchanged — " +
              $"CODE ids are shared/global, never reassigned) — {newObjectCount} new GameObject(s) " +
              $"added (root + referenced; others already present were reused as-is), " +
              $"{rec.OgiIds.Count} OGI, {rec.AnimIds.Count} Animation, {rec.BehaviourIds.Count} Behaviour, " +
              $"{rec.SoundIds.Count} Sound, {rec.RigidModelIds.Count} RigidModel, {rec.ModelIds.Count} Model, " +
              $"{rec.SkinIds.Count} Skin, {rec.BlendSkinIds.Count} BlendSkin, {rec.MaterialIds.Count} Material, " +
              $"{rec.TextureIds.Count} Texture." +
              (unhandledCodeModels + unhandledUnknowns > 0
                  ? $" WARNING: {unhandledCodeModels} RefCodeModel(s) + {unhandledUnknowns} RefUnknown(s) " +
                    "were NOT copied (unhandled) — this object references data this transplant doesn't understand yet."
                  : "") +
              " Behaviour graphs copied as opaque data — any internal id references THEY make on " +
              "their own (not exposed via RefObjects/RefOGIs/RefAnimations/RefSounds) are NOT specially " +
              "handled, but since ids are never reassigned, any such reference that was valid in the " +
              "source is still valid here as long as its target got cloned too.";
        return rec;
    }

    public static List<ushort> CopyInstanceLayoutRefs<TItem>(
        PS2AnyTwinsanityRM2 sourceRm2, PS2AnyTwinsanityRM2 destRm2,
        BaseTwinSection destInstancesSection, List<ushort> srcIds, int sectionConstant,
        out int notFoundCount)
        where TItem : BaseTwinItem, new()
    {
        notFoundCount = 0;
        var result = new List<ushort>();
        if (srcIds.Count == 0) return result;

        BaseTwinSection? destSec = null;
        for (int lid = 0; lid <= 7 && destSec is null; lid++)
        {
            var layout = destRm2.GetItem<BaseTwinSection>((uint)lid);
            var instSec = layout?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_INSTANCES_SECTION);
            if (!ReferenceEquals(instSec, destInstancesSection)) continue;
            destSec = layout?.GetItem<BaseTwinSection>((uint)sectionConstant);
        }
        if (destSec is null) { notFoundCount = srcIds.Count; return result; }

        foreach (var srcId in srcIds)
        {
            TItem? srcItem = null;
            for (int lid = 0; lid <= 7 && srcItem is null; lid++)
            {
                var srcLayout = sourceRm2.GetItem<BaseTwinSection>((uint)lid);
                var srcSec = srcLayout?.GetItem<BaseTwinSection>((uint)sectionConstant);
                srcItem = srcSec?.GetItem<TItem>(srcId);
            }
            if (srcItem is null) { notFoundCount++; continue; }

            var clone = CloneItem(srcItem);
            uint newId = 0;
            while (destSec.ContainsItem(newId)) newId++;
            clone.SetID(newId);
            destSec.AddItem(clone);
            result.Add((ushort)newId);
        }
        return result;
    }

    public static bool TransplantSkydome(GL gl,
        PS2AnyTwinsanitySM2 destSm2, Dictionary<uint, Texture2D> destTexCache,
        PS2AnyTwinsanitySM2 sourceSm2, out string log)
    {
        var destGfx = destSm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        var srcGfx  = sourceSm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        if (destGfx is null || srcGfx is null) { log = "missing GRAPHICS section on one side"; return false; }

        var srcSkySec = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKYDOMES_SECTION);
        var srcSky = srcSkySec is not null && srcSkySec.GetItemsAmount() > 0
            ? srcSkySec.GetItem(0) as PS2AnySkydome : null;
        if (srcSky is null) { log = "source level has no skydome data"; return false; }

        var destRmSec    = EnsureGfxSubsection<PS2AnyRigidModelsSection>(destGfx, Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var destMeshSec  = EnsureGfxSubsection<PS2AnyMeshesSection>(destGfx, Constants.GRAPHICS_MESHES_SECTION);
        var destModelSec = EnsureGfxSubsection<PS2AnyModelsSection>(destGfx, Constants.GRAPHICS_MODELS_SECTION);
        var destMatSec   = EnsureGfxSubsection<PS2AnyMaterialsSection>(destGfx, Constants.GRAPHICS_MATERIALS_SECTION);
        var destTexSec   = EnsureGfxSubsection<PS2AnyTexturesSection>(destGfx, Constants.GRAPHICS_TEXTURES_SECTION);
        var srcRmSec     = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var srcMeshSec   = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        var srcModelSec  = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var srcMatSec    = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var srcTexSec    = srcGfx.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);

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

        var missingMeshIds = new List<uint>();
        var missingMaterialIds = new List<uint>();
        var missingModelIds = new List<uint>();
        var missingTextureIds = new List<uint>();

        var texIdMap = new Dictionary<uint, uint>();
        uint CloneTexture(uint srcTexId)
        {
            if (texIdMap.TryGetValue(srcTexId, out var already)) return already;
            var srcTex = srcTexSec?.GetItem<PS2AnyTexture>(srcTexId);
            if (srcTex is null) { missingTextureIds.Add(srcTexId); texIdMap[srcTexId] = srcTexId; return srcTexId; }
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
            if (srcMat is null) { missingMaterialIds.Add(srcMatId); return 0; }
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
            if (srcModel is null) { missingModelIds.Add(srcModelId); return 0; }
            var clone = CloneItem(srcModel);
            uint newId = NewGraphicsId(destModelSec);
            clone.SetID(newId);
            destModelSec.AddItem(clone);
            modelIdMap[srcModelId] = newId;
            return newId;
        }

        var newMeshIds = new List<uint>();
        foreach (var meshId in srcSky.Meshes)
        {
            BaseTwinSection? srcSec = null; BaseTwinSection? destSec = null;
            var rm = srcMeshSec?.GetItem<PS2AnyRigidModel>(meshId);
            if (rm is not null) { srcSec = srcMeshSec; destSec = destMeshSec; }
            else
            {
                rm = srcRmSec?.GetItem<PS2AnyRigidModel>(meshId);
                if (rm is not null) { srcSec = srcRmSec; destSec = destRmSec; }
            }
            if (rm is null || srcSec is null || destSec is null) { missingMeshIds.Add(meshId); continue; }

            var clone = CloneItem(rm);
            uint newRmId = NewGraphicsId(destSec, destMeshSec, destRmSec);
            clone.SetID(newRmId);
            clone.Model = CloneModel(clone.Model);
            for (int i = 0; i < clone.Materials.Count; i++)
                clone.Materials[i] = CloneMaterial(clone.Materials[i]);
            destSec.AddItem(clone);
            newMeshIds.Add(newRmId);
        }

        if (newMeshIds.Count == 0)
        {
            log = "source skydome had no resolvable meshes" +
                  (missingMeshIds.Count > 0 ? $" (all {missingMeshIds.Count} mesh id(s) unresolved: {string.Join(", ", missingMeshIds.Select(m => $"0x{m:X}"))})" : "");
            return false;
        }

        var destSkySec = destGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKYDOMES_SECTION);
        if (destSkySec is null)
        {
            destSkySec = new PS2AnySkydomesSection();
            destSkySec.SetID((uint)Constants.GRAPHICS_SKYDOMES_SECTION);
            destGfx.AddItem(destSkySec);
        }
        var destSky = destSkySec.GetItemsAmount() > 0 ? destSkySec.GetItem(0) as PS2AnySkydome : null;
        if (destSky is null)
        {
            destSky = new PS2AnySkydome();
            destSky.SetID(NewGraphicsId(destSkySec));
            destSkySec.AddItem(destSky);
        }
        destSky.Meshes.Clear();
        destSky.Meshes.AddRange(newMeshIds);

        var destScenery = destSm2.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
        if (destScenery is not null) destScenery.SkydomeID = destSky.GetID();

        var totalSource = srcSky.Meshes.Count;
        log = $"Skydome swapped — {newMeshIds.Count}/{totalSource} mesh(es), +{modelIdMap.Count} Model, " +
              $"+{matIdMap.Count} Material, +{texIdMap.Count} Texture. Old skydome data left in " +
              "place (unused, harmless).";
        var gaps = new List<string>();
        if (missingMeshIds.Count > 0) gaps.Add($"{missingMeshIds.Count} mesh id(s) not found: {string.Join(", ", missingMeshIds.Select(m => $"0x{m:X}"))}");
        if (missingModelIds.Count > 0) gaps.Add($"{missingModelIds.Count} Model id(s) not found: {string.Join(", ", missingModelIds.Select(m => $"0x{m:X}"))}");
        if (missingMaterialIds.Count > 0) gaps.Add($"{missingMaterialIds.Count} Material id(s) not found: {string.Join(", ", missingMaterialIds.Select(m => $"0x{m:X}"))}");
        if (missingTextureIds.Count > 0) gaps.Add($"{missingTextureIds.Count} Texture id(s) not found (left pointing at the OLD id, likely wrong/missing in-game): {string.Join(", ", missingTextureIds.Select(m => $"0x{m:X}"))}");
        if (gaps.Count > 0)
            log += "  INCOMPLETE — " + string.Join("; ", gaps);
        return true;
    }

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

            int freeSlot = Array.IndexOf(oldRoot.SceneryTypes, ITwinScenery.SceneryType.None);
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

    public sealed class ExternalModelBakeRecord
    {
        public uint ObjectId;
        public int  SubmeshCount;
        public int  VertexCount;
        public int  TextureCount;
        public readonly List<uint> OgiIds        = new();
        public readonly List<uint> RigidModelIds = new();
        public readonly List<uint> ModelIds      = new();
        public readonly List<uint> MaterialIds   = new();
        public readonly List<uint> TextureIds    = new();
    }

    public static TransplantRecord ToTransplantRecord(ExternalModelBakeRecord bake)
    {
        var r = new TransplantRecord { ObjectId = bake.ObjectId, ObjectWasNew = true };
        r.OgiIds.AddRange(bake.OgiIds);
        r.RigidModelIds.AddRange(bake.RigidModelIds);
        r.ModelIds.AddRange(bake.ModelIds);
        r.MaterialIds.AddRange(bake.MaterialIds);
        r.TextureIds.AddRange(bake.TextureIds);
        return r;
    }

    internal static byte[] ResampleRGBA(byte[] src, int sw, int sh, int dw, int dh)
    {
        if (sw <= 0 || sh <= 0 || dw <= 0 || dh <= 0) return src;
        if (sw == dw && sh == dh) return src;
        var dst = new byte[dw * dh * 4];
        for (int y = 0; y < dh; y++)
        {
            int sy0 = (int)((long)y * sh / dh);
            int sy1 = (int)(((long)(y + 1) * sh + dh - 1) / dh);
            if (sy1 <= sy0) sy1 = sy0 + 1;
            if (sy1 > sh) sy1 = sh;
            for (int x = 0; x < dw; x++)
            {
                int sx0 = (int)((long)x * sw / dw);
                int sx1 = (int)(((long)(x + 1) * sw + dw - 1) / dw);
                if (sx1 <= sx0) sx1 = sx0 + 1;
                if (sx1 > sw) sx1 = sw;
                long r = 0, g = 0, b = 0, a = 0, cnt = 0;
                for (int yy = sy0; yy < sy1; yy++)
                    for (int xx = sx0; xx < sx1; xx++)
                    {
                        int si = (yy * sw + xx) * 4;
                        r += src[si]; g += src[si + 1]; b += src[si + 2]; a += src[si + 3]; cnt++;
                    }
                if (cnt == 0) cnt = 1;
                int di = (y * dw + x) * 4;
                dst[di]     = (byte)(r / cnt);
                dst[di + 1] = (byte)(g / cnt);
                dst[di + 2] = (byte)(b / cnt);
                dst[di + 3] = (byte)(a / cnt);
            }
        }
        return dst;
    }

    public static ExternalModelBakeRecord? BakeExternalModelAsObject(GL gl,
        PS2AnyTwinsanityRM2 destRm2, object destTablesHandle, Dictionary<uint, Texture2D> destTexCache,
        PS2AnyTwinsanityRM2? globalRm2, List<CrashEngine.Assets.RawSubmesh> submeshes, string objectName, out string log)
    {
        if (destTablesHandle is not RmTables destTables) { log = "destination chunk has no mesh tables"; return null; }
        if (submeshes.Count == 0) { log = "no meshes found in the imported file"; return null; }

        var destCode     = destRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var destObjSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var destOgiSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var destGfx      = destRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var destRmSec    = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var destMeshSec  = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        var destModelSec = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var destMatSec   = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var destTexSec   = destGfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        if (destObjSec is null || destOgiSec is null || destGfx is null || destRmSec is null ||
            destModelSec is null || destMatSec is null || destTexSec is null)
        { log = "destination level is missing a required CODE/GRAPHICS section"; return null; }

        static uint NewGraphicsId(BaseTwinSection section, params BaseTwinSection?[] alsoAvoid)
        {
            var rng = new Random();
            uint id;
            do { id = (uint)rng.Next(0x10000, 0x7FFFFFF); }
            while (id == 0 || section.ContainsItem(id) || alsoAvoid.Any(s => s?.ContainsItem(id) == true));
            return id;
        }

        var globalCode    = globalRm2?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var globalOgiSec  = globalCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var globalObjSec  = globalCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var globalGfx     = globalRm2?.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var globalMatSec  = globalGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);

        static PS2AnyMaterial? FindSimpleMaterialTemplate(BaseTwinSection sec)
        {
            for (int i = 0; i < sec.GetItemsAmount(); i++)
                if (sec.GetItem(i) is PS2AnyMaterial cand && cand.Shaders.Count == 1 &&
                    cand.Shaders[0].ShaderType is TwinShader.Type.StandardLit or TwinShader.Type.StandardUnlit)
                    return cand;
            return null;
        }
        const uint NitroCrateReferenceMaterialId = 0xEE85EFBC;
        var matTemplate = (globalMatSec?.GetItem<PS2AnyMaterial>(NitroCrateReferenceMaterialId)) ??
                          (globalMatSec is not null ? FindSimpleMaterialTemplate(globalMatSec) : null) ??
                          FindSimpleMaterialTemplate(destMatSec);
        if (matTemplate is null)
        { log = "could not find a simple single-shader Material to use as a structural template (in the global material set or this level's own)"; return null; }

        static PS2AnyOGI? FindSimpleStaticOgiTemplate(BaseTwinSection sec)
        {
            for (int i = 0; i < sec.GetItemsAmount(); i++)
                if (sec.GetItem(i) is PS2AnyOGI cand && cand.SkinID == 0 && cand.BlendSkinID == 0)
                    return cand;
            return null;
        }
        var ogiTemplate = (globalOgiSec is not null ? FindSimpleStaticOgiTemplate(globalOgiSec) : null) ?? FindSimpleStaticOgiTemplate(destOgiSec);
        if (ogiTemplate is null)
        { log = "could not find a simple non-skinned OGI to use as a structural template (in the global object set or this level's own)"; return null; }

        var rec = new ExternalModelBakeRecord();
        var rigidModelIds = new List<uint>();
        var minPos = new SysVec3(float.MaxValue); var maxPos = new SysVec3(float.MinValue);

        foreach (var raw in submeshes)
        {
            if (raw.Indices.Length < 3 || raw.Positions.Length == 0) continue;

            if (raw.DiffusePixelsRGBA is null || raw.TexWidth <= 0 || raw.TexHeight <= 0)
            {
                const int whiteSize = 128;
                var white = new byte[whiteSize * whiteSize * 4];
                Array.Fill(white, (byte)255);
                raw.DiffusePixelsRGBA = white; raw.TexWidth = whiteSize; raw.TexHeight = whiteSize;
            }

            uint texId = matTemplate.Shaders[0].TextureId;
            if (raw.DiffusePixelsRGBA is not null && raw.TexWidth > 0 && raw.TexHeight > 0)
            {
                var tex = new PS2AnyTexture();
                uint newTexId = NewGraphicsId(destTexSec);
                tex.SetID(newTexId);
                int tw = raw.TexWidth, th = raw.TexHeight;
                { int p = 1; while (p < Math.Min(tw, 256)) p <<= 1; tw = Math.Clamp(p, 8, 256); }
                { int p = 1; while (p < Math.Min(th, 256)) p <<= 1; th = Math.Clamp(p, 8, 256); }
                byte[] pixels = ResampleRGBA(raw.DiffusePixelsRGBA, raw.TexWidth, raw.TexHeight, tw, th);
                var colors = new List<Twinsanity.TwinsanityInterchange.Common.Color>(tw * th);
                for (int p = 0; p < tw * th; p++)
                    colors.Add(new Twinsanity.TwinsanityInterchange.Common.Color(
                        pixels[p * 4], pixels[p * 4 + 1], pixels[p * 4 + 2], pixels[p * 4 + 3]));
                tex.FromBitmap(colors, tw,
                    Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture.TextureFunction.MODULATE,
                    Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture.TexturePixelFormat.PSMCT32);
                destTexSec.AddItem(tex);
                var t2d = ChunkImporter.DecodeTexture(gl, tex);
                if (t2d is not null) destTexCache[newTexId] = t2d;
                texId = newTexId;
                rec.TextureCount++;
                rec.TextureIds.Add(newTexId); // Amedo 2026-09-20
            }

            var mat = CloneItem(matTemplate);
            uint newMatId = NewGraphicsId(destMatSec);
            mat.SetID(newMatId);
            mat.Shaders[0].TextureId = texId;
            if (raw.DiffusePixelsRGBA is not null)
                mat.Shaders[0].TxtMapping = TwinShader.TextureMapping.ON;
            if (raw.CeUnlit.HasValue)
                mat.Shaders[0].ShaderType = raw.CeUnlit.Value ? TwinShader.Type.StandardUnlit : TwinShader.Type.StandardLit;
            if (raw.CeAlphaBlend.HasValue)
                mat.Shaders[0].ABlending = raw.CeAlphaBlend.Value ? TwinShader.AlphaBlending.ON : TwinShader.AlphaBlending.OFF;
            mat.Shaders[0].XScrollSettings = TwinShader.XScrollFormula.Disabled;
            mat.Shaders[0].YScrollSettings = TwinShader.YScrollFormula.Disabled;
            mat.Shaders[0].UvScrollSpeed   = new TwinVec4(0f, 0f, 0f, 0f);
            mat.Shaders[0].Animation       = null;
            destMatSec.AddItem(mat);
            rec.MaterialIds.Add(newMatId); // Amedo 2026-09-20

            var sub = new Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel
            {
                Vertexes = new List<TwinVec4>(), UVW = new List<TwinVec4>(), Colors = new List<TwinVec4>(),
                Normals = new List<TwinVec4>(), EmitColor = new List<TwinVec4>(),
                Connection = new List<bool>(), GroupSizes = new List<int>(), UnusedBlob = Array.Empty<byte>(),
            };

            int triCount = raw.Indices.Length / 3;
            int inGroup = 0;
            for (int t = 0; t < triCount; t++)
            {
                if (inGroup + 3 > 36) { sub.GroupSizes.Add(inGroup); inGroup = 0; }
                for (int k = 0; k < 3; k++)
                {
                    uint vi = raw.Indices[t * 3 + k];
                    if (vi >= raw.Positions.Length) { vi = 0; }
                    var p = raw.Positions[vi]; var n = raw.Normals[vi]; var uv = raw.UVs[vi];
                    sub.Vertexes.Add(new TwinVec4(p.X, p.Y, p.Z, 1f));
                    sub.Normals.Add(new TwinVec4(n.X, n.Y, n.Z, 1f));
                    sub.UVW.Add(new TwinVec4(uv.X, uv.Y, 1f, 1f));
                    sub.Colors.Add(new TwinVec4(0.498f, 0.498f, 0.498f, 1f));
                    sub.EmitColor.Add(new TwinVec4(0.498f, 0.498f, 0.498f, 0.937f));
                    sub.Connection.Add(k == 2);
                    inGroup++;

                    minPos = SysVec3.Min(minPos, p); maxPos = SysVec3.Max(maxPos, p);
                }
            }
            if (inGroup > 0) sub.GroupSizes.Add(inGroup);
            sub.Compile();

            var model = new PS2AnyModel();
            model.SubModels.Add(sub);
            uint newModelId = NewGraphicsId(destModelSec);
            model.SetID(newModelId);
            destModelSec.AddItem(model);
            rec.ModelIds.Add(newModelId); // Amedo 2026-09-20

            var rm = new PS2AnyRigidModel { Model = newModelId };
            rm.Materials.Add(newMatId);
            uint newRmId = NewGraphicsId(destRmSec, destMeshSec);
            rm.SetID(newRmId);
            destRmSec.AddItem(rm);
            destTables.RigidModels[newRmId] = rm;
            rigidModelIds.Add(newRmId);
            rec.RigidModelIds.Add(newRmId); // Amedo 2026-09-20

            rec.SubmeshCount++;
            rec.VertexCount += sub.Vertexes.Count;
        }

        if (rigidModelIds.Count == 0) { log = "no usable triangles found in the imported file"; return null; }

        bool rootJointSafe = ogiTemplate.Joints.Count > 0 && ogiTemplate.Joints[0].Index is >= 0 and <= 255;
        byte rootJointIdx = rootJointSafe ? (byte)ogiTemplate.Joints[0].Index : (byte)0xFF;
        var ogi = CloneItem(ogiTemplate);
        ogi.RigidModelIds = new List<uint>(rigidModelIds);
        ogi.JointIndices  = new List<byte>();
        for (int i = 0; i < rigidModelIds.Count; i++)
            ogi.JointIndices.Add(rootJointSafe ? rootJointIdx : (i < ogiTemplate.JointIndices.Count ? ogiTemplate.JointIndices[i] : (byte)0xFF));
        ogi.Collisions.Clear();
        ogi.CollisionJointIndices.Clear();
        ogi.BoundingBox[0] = new TwinVec4(minPos.X, minPos.Y, minPos.Z, 1f);
        ogi.BoundingBox[1] = new TwinVec4(maxPos.X, maxPos.Y, maxPos.Z, 1f);

        uint newOgiId = 0;
        while (destOgiSec.ContainsItem(newOgiId) || (globalOgiSec?.ContainsItem(newOgiId) ?? false)) newOgiId++;
        ogi.SetID(newOgiId);
        destOgiSec.AddItem(ogi);
        destTables.OGIs[newOgiId] = ogi;

        uint newObjId = 0;
        while (destObjSec.ContainsItem(newObjId) || (globalObjSec?.ContainsItem(newObjId) ?? false)) newObjId++;
        var obj = new PS2AnyObject
        {
            Type = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject.ObjectType.GenericObject,
            Name = $"CE_ImportedModel_{objectName}_{newObjId:X4}",
            BehaviourPack = new Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourCommandPack(),
        };
        obj.OGISlots.Add((ushort)newOgiId);
        obj.AnimationSlots.Add(0xFFFF);
        obj.SetID(newObjId);
        destObjSec.AddItem(obj);
        destTables.Objects[newObjId] = obj;
        rec.ObjectId = newObjId;
        rec.OgiIds.Add(newOgiId); // Amedo 2026-09-20

        var modelById2 = BuildModelLookup(destGfx);
        foreach (var kv in BuildMeshLookup(destGfx, modelById2, gl, destTexCache)) destTables.ModelGpu[kv.Key] = kv.Value;

        log = $"baked {rec.SubmeshCount} submesh(es), {rec.VertexCount} vert(s) as new object 0x{newObjId:X4} " +
              $"(OGI 0x{newOgiId:X4}, +{rec.TextureCount} Texture) — real PS2 RigidModel/OGI/Object, not a preview. " +
              $"[diag: rootJointSafe={rootJointSafe}, Joints[0].Index={(ogiTemplate.Joints.Count > 0 ? ogiTemplate.Joints[0].Index : -1)}, " +
              $"ogiTemplate=0x{ogiTemplate.GetID():X}, matTemplate=0x{matTemplate.GetID():X}]";
        return rec;
    }

    public sealed class SceneryBakeRecord
    {
        public List<uint> MeshIds = new();
        public List<TwinVec4[]> LocalBoxes = new();
        public SysVec3 CombinedMin;
        public SysVec3 CombinedMax;
        public int SubmeshCount;
        public int VertexCount;
        public int TextureCount;
        public List<uint> ModelIds = new();
        public List<uint> MaterialIds = new();
        public List<uint> TextureIds = new();
    }

    public static void RemoveSceneryBake(PS2AnyTwinsanitySM2 sm2, SceneryBakeRecord rec,
        Dictionary<uint, Texture2D>? sceneryTexCache = null)
    {
        var gfx     = sm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        var meshSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        var modelSec= gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var matSec  = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var texSec  = gfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        var lodSec  = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_LODS_SECTION);
        var sc      = sm2.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
        if (gfx is null || meshSec is null || modelSec is null || matSec is null || texSec is null || sc is null) return;

        var usedMesh = new HashSet<uint>();
        foreach (var nd in sc.Sceneries)
        {
            foreach (var m in nd.MeshIDs) usedMesh.Add(m);
            foreach (var lid in nd.LodIDs)
                if (lodSec?.GetItem<PS2AnyLOD>(lid) is PS2AnyLOD lod)
                    foreach (var m in lod.Meshes) usedMesh.Add(m);
        }
        foreach (var mid in rec.MeshIds)
            if (!usedMesh.Contains(mid)) meshSec.RemoveItem<PS2AnyRigidModel>(mid);

        var usedModel = new HashSet<uint>(); var usedMat = new HashSet<uint>();
        for (int i = 0; i < meshSec.GetItemsAmount(); i++)
            if (meshSec.GetItem(i) is PS2AnyRigidModel rm) { usedModel.Add(rm.Model); foreach (var mt in rm.Materials) usedMat.Add(mt); }
        foreach (var mid in rec.ModelIds)    if (!usedModel.Contains(mid)) modelSec.RemoveItem<PS2AnyModel>(mid);
        foreach (var mid in rec.MaterialIds) if (!usedMat.Contains(mid))   matSec.RemoveItem<PS2AnyMaterial>(mid);

        var usedTex = new HashSet<uint>();
        for (int i = 0; i < matSec.GetItemsAmount(); i++)
            if (matSec.GetItem(i) is PS2AnyMaterial mat) foreach (var sh in mat.Shaders) usedTex.Add(sh.TextureId);
        foreach (var tid in rec.TextureIds)
            if (!usedTex.Contains(tid)) { texSec.RemoveItem<PS2AnyTexture>(tid); sceneryTexCache?.Remove(tid); }
    }

    public static SceneryBakeRecord? BakeExternalModelAsScenery(GL gl,
        PS2AnyTwinsanitySM2 destSm2, Dictionary<uint, Texture2D> destSceneryTexCache,
        PS2AnyTwinsanityRM2? globalRm2, List<CrashEngine.Assets.RawSubmesh> submeshes,
        string objectName, out string log)
    {
        if (submeshes.Count == 0) { log = "no meshes found in the imported file"; return null; }

        var gfx      = destSm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        var meshSec  = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        var modelSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var matSec   = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var texSec   = gfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        if (gfx is null || meshSec is null || modelSec is null || matSec is null || texSec is null)
        { log = "this chunk's scenery (SM2) is missing a required GRAPHICS sub-section"; return null; }

        static uint NewGraphicsId(BaseTwinSection section, params BaseTwinSection?[] alsoAvoid)
        {
            var rng = new Random();
            uint id;
            do { id = (uint)rng.Next(0x10000, 0x7FFFFFF); }
            while (id == 0 || section.ContainsItem(id) || alsoAvoid.Any(s => s?.ContainsItem(id) == true));
            return id;
        }

        static PS2AnyMaterial? FindSimpleMaterialTemplate(BaseTwinSection sec)
        {
            for (int i = 0; i < sec.GetItemsAmount(); i++)
                if (sec.GetItem(i) is PS2AnyMaterial cand && cand.Shaders.Count == 1 &&
                    cand.Shaders[0].ShaderType is TwinShader.Type.StandardLit or TwinShader.Type.StandardUnlit)
                    return cand;
            return null;
        }
        var globalGfx    = globalRm2?.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var globalMatSec = globalGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        const uint NitroCrateReferenceMaterialId = 0xEE85EFBC;
        var matTemplate = (globalMatSec?.GetItem<PS2AnyMaterial>(NitroCrateReferenceMaterialId)) ??
                          (globalMatSec is not null ? FindSimpleMaterialTemplate(globalMatSec) : null) ??
                          FindSimpleMaterialTemplate(matSec);
        if (matTemplate is null)
        { log = "could not find a simple single-shader Material to use as a structural template"; return null; }

        var rec = new SceneryBakeRecord();
        var combMin = new SysVec3(float.MaxValue); var combMax = new SysVec3(float.MinValue);

        var model = new PS2AnyModel();
        var rm    = new PS2AnyRigidModel();

        foreach (var raw in submeshes)
        {
            if (raw.Indices.Length < 3 || raw.Positions.Length == 0) continue;

            if (raw.DiffusePixelsRGBA is null || raw.TexWidth <= 0 || raw.TexHeight <= 0)
            {
                const int whiteSize = 128;
                var white = new byte[whiteSize * whiteSize * 4];
                Array.Fill(white, (byte)255);
                raw.DiffusePixelsRGBA = white; raw.TexWidth = whiteSize; raw.TexHeight = whiteSize;
            }

            uint texId = matTemplate.Shaders[0].TextureId;
            if (raw.DiffusePixelsRGBA is not null && raw.TexWidth > 0 && raw.TexHeight > 0)
            {
                var tex = new PS2AnyTexture();
                uint newTexId = NewGraphicsId(texSec);
                tex.SetID(newTexId);
                int tw = raw.TexWidth, th = raw.TexHeight;
                { int p = 1; while (p < Math.Min(tw, 256)) p <<= 1; tw = Math.Clamp(p, 8, 256); }
                { int p = 1; while (p < Math.Min(th, 256)) p <<= 1; th = Math.Clamp(p, 8, 256); }
                byte[] pixels = ResampleRGBA(raw.DiffusePixelsRGBA, raw.TexWidth, raw.TexHeight, tw, th);
                var colors = new List<Twinsanity.TwinsanityInterchange.Common.Color>(tw * th);
                for (int p = 0; p < tw * th; p++)
                    colors.Add(new Twinsanity.TwinsanityInterchange.Common.Color(
                        pixels[p * 4], pixels[p * 4 + 1], pixels[p * 4 + 2], pixels[p * 4 + 3]));
                tex.FromBitmap(colors, tw,
                    Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture.TextureFunction.MODULATE,
                    Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture.TexturePixelFormat.PSMCT32);
                texSec.AddItem(tex);
                var t2d = ChunkImporter.DecodeTexture(gl, tex);
                if (t2d is not null) destSceneryTexCache[newTexId] = t2d;
                texId = newTexId;
                rec.TextureCount++;
                rec.TextureIds.Add(newTexId); // Amedo 2026-09-20
            }

            var mat = CloneItem(matTemplate);
            uint newMatId = NewGraphicsId(matSec);
            mat.SetID(newMatId);
            mat.Shaders[0].TextureId = texId;
            if (raw.DiffusePixelsRGBA is not null)
                mat.Shaders[0].TxtMapping = TwinShader.TextureMapping.ON;
            mat.Shaders[0].XScrollSettings = TwinShader.XScrollFormula.Disabled;
            mat.Shaders[0].YScrollSettings = TwinShader.YScrollFormula.Disabled;
            mat.Shaders[0].UvScrollSpeed   = new TwinVec4(0f, 0f, 0f, 0f);
            mat.Shaders[0].Animation       = null;
            mat.Shaders[0].ShaderType = (raw.CeUnlit ?? true)
                ? TwinShader.Type.StandardUnlit : TwinShader.Type.StandardLit;
            if (raw.CeAlphaBlend.HasValue)
                mat.Shaders[0].ABlending = raw.CeAlphaBlend.Value ? TwinShader.AlphaBlending.ON : TwinShader.AlphaBlending.OFF;
            matSec.AddItem(mat);
            rec.MaterialIds.Add(newMatId); // Amedo 2026-09-20

            var sub = new Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel
            {
                Vertexes = new List<TwinVec4>(), UVW = new List<TwinVec4>(), Colors = new List<TwinVec4>(),
                Normals = new List<TwinVec4>(), EmitColor = new List<TwinVec4>(),
                Connection = new List<bool>(), GroupSizes = new List<int>(), UnusedBlob = Array.Empty<byte>(),
            };

            var subMin = new SysVec3(float.MaxValue); var subMax = new SysVec3(float.MinValue);
            int triCount = raw.Indices.Length / 3;
            int inGroup = 0;
            for (int t = 0; t < triCount; t++)
            {
                if (inGroup + 3 > 36) { sub.GroupSizes.Add(inGroup); inGroup = 0; }
                for (int k = 0; k < 3; k++)
                {
                    uint vi = raw.Indices[t * 3 + k];
                    if (vi >= raw.Positions.Length) { vi = 0; }
                    var p = raw.Positions[vi]; var n = raw.Normals[vi]; var uv = raw.UVs[vi];
                    sub.Vertexes.Add(new TwinVec4(p.X, p.Y, p.Z, 1f));
                    sub.Normals.Add(new TwinVec4(n.X, n.Y, n.Z, 1f));
                    sub.UVW.Add(new TwinVec4(uv.X, uv.Y, 1f, 1f));
                    sub.Colors.Add(new TwinVec4(1f, 1f, 1f, 1f));
                    sub.EmitColor.Add(new TwinVec4(1f, 1f, 1f, 0.937f));
                    sub.Connection.Add(k == 2);
                    inGroup++;

                    subMin = SysVec3.Min(subMin, p); subMax = SysVec3.Max(subMax, p);
                }
            }
            if (inGroup > 0) sub.GroupSizes.Add(inGroup);
            sub.Compile();

            model.SubModels.Add(sub);
            rm.Materials.Add(newMatId);
            combMin = SysVec3.Min(combMin, subMin); combMax = SysVec3.Max(combMax, subMax);
            rec.SubmeshCount++;
            rec.VertexCount += sub.Vertexes.Count;
        }

        if (rec.SubmeshCount == 0) { log = "no usable triangles found in the imported file"; return null; }

        uint newModelId = NewGraphicsId(modelSec);
        model.SetID(newModelId);
        modelSec.AddItem(model);
        rec.ModelIds.Add(newModelId); // Amedo 2026-09-20

        rm.Model = newModelId;
        uint newMeshId = NewGraphicsId(meshSec);
        rm.SetID(newMeshId);
        meshSec.AddItem(rm);

        rec.MeshIds.Add(newMeshId);
        rec.LocalBoxes.Add(new[]
        {
            new TwinVec4(combMin.X, combMin.Y, combMin.Z, 1f),
            new TwinVec4(combMax.X, combMax.Y, combMax.Z, 1f),
        });
        rec.CombinedMin = combMin; rec.CombinedMax = combMax;
        log = $"baked {rec.SubmeshCount} submesh(es), {rec.VertexCount} vert(s) as ONE scenery mesh (0x{newMeshId:X}) " +
              $"(+{rec.TextureCount} Texture) into SM2 scenery graphics — real static scenery, not a preview. " +
              $"[matTemplate=0x{matTemplate.GetID():X}]";
        return rec;
    }

    public static ExternalModelBakeRecord? ConvertSceneryMeshToObject(GL gl,
        PS2AnyTwinsanityRM2 destRm2, object destTablesHandle, Dictionary<uint, Texture2D> destTexCache,
        PS2AnyTwinsanitySM2 sm2, PS2AnyTwinsanityRM2? globalRm2, uint meshId,
        TwinVec4 bboxMin, TwinVec4 bboxMax, SysVec3 bakeScale, string objectName, out string log)
    {
        if (destTablesHandle is not RmTables destTables) { log = "destination chunk has no mesh tables"; return null; }

        var srcGfx      = sm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        var srcMeshSec  = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        var srcModelSec = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var srcMatSec   = srcGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var srcTexSec   = srcGfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        if (srcMeshSec is null || srcModelSec is null || srcMatSec is null || srcTexSec is null)
        { log = "source scenery has no usable GRAPHICS sections"; return null; }

        var srcRm = srcMeshSec.GetItem<PS2AnyRigidModel>(meshId);
        if (srcRm is null) { log = $"scenery mesh id 0x{meshId:X} not found"; return null; }
        var srcModel = srcModelSec.GetItem<PS2AnyModel>(srcRm.Model);
        if (srcModel is null) { log = $"scenery mesh's own Model (0x{srcRm.Model:X}) not found"; return null; }

        var destCode     = destRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var destObjSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var destOgiSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var destGfx      = destRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var destRmSec    = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
        var destMeshSec  = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        var destModelSec = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        var destMatSec   = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var destTexSec   = destGfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        if (destObjSec is null || destOgiSec is null || destGfx is null || destRmSec is null ||
            destModelSec is null || destMatSec is null || destTexSec is null)
        { log = "destination level is missing a required CODE/GRAPHICS section"; return null; }

        static uint NewGraphicsId(BaseTwinSection section, params BaseTwinSection?[] alsoAvoid)
        {
            var rng = new Random();
            uint id;
            do { id = (uint)rng.Next(0x10000, 0x7FFFFFF); }
            while (id == 0 || section.ContainsItem(id) || alsoAvoid.Any(s => s?.ContainsItem(id) == true));
            return id;
        }

        var globalCode   = globalRm2?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var globalOgiSec = globalCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var globalObjSec = globalCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var globalGfx    = globalRm2?.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var globalMatSec = globalGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);

        static PS2AnyOGI? FindSimpleStaticOgiTemplate(BaseTwinSection sec)
        {
            for (int i = 0; i < sec.GetItemsAmount(); i++)
                if (sec.GetItem(i) is PS2AnyOGI cand && cand.SkinID == 0 && cand.BlendSkinID == 0)
                    return cand;
            return null;
        }
        var ogiTemplate = (globalOgiSec is not null ? FindSimpleStaticOgiTemplate(globalOgiSec) : null) ?? FindSimpleStaticOgiTemplate(destOgiSec);
        if (ogiTemplate is null)
        { log = "could not find a simple non-skinned OGI to use as a structural template (in the global object set or this level's own)"; return null; }

        var rec = new ExternalModelBakeRecord();

        var texIdMap = new Dictionary<uint, uint>();
        uint CloneTexture(uint srcTexId)
        {
            if (texIdMap.TryGetValue(srcTexId, out var already)) return already;
            var srcTex = srcTexSec!.GetItem<PS2AnyTexture>(srcTexId);
            if (srcTex is null) return 0;
            var clone = CloneItem(srcTex);
            uint newId = NewGraphicsId(destTexSec);
            clone.SetID(newId);
            destTexSec.AddItem(clone);
            var t2d = ChunkImporter.DecodeTexture(gl, clone);
            if (t2d is not null) destTexCache[newId] = t2d;
            texIdMap[srcTexId] = newId;
            rec.TextureCount++;
            return newId;
        }
        var matIdMap = new Dictionary<uint, uint>();
        uint CloneMaterial(uint srcMatId)
        {
            if (matIdMap.TryGetValue(srcMatId, out var already)) return already;
            var srcMat = srcMatSec!.GetItem<PS2AnyMaterial>(srcMatId);
            if (srcMat is null) return 0;
            var clone = CloneItem(srcMat);
            foreach (var sh in clone.Shaders)
                if (sh.TextureId != 0) sh.TextureId = CloneTexture(sh.TextureId);
            uint newId = NewGraphicsId(destMatSec);
            clone.SetID(newId);
            destMatSec.AddItem(clone);
            matIdMap[srcMatId] = newId;
            return newId;
        }

        var modelClone = CloneItem(srcModel);

        if (MathF.Abs(bakeScale.X - 1f) > 0.001f || MathF.Abs(bakeScale.Y - 1f) > 0.001f || MathF.Abs(bakeScale.Z - 1f) > 0.001f)
        {
            foreach (var sub in modelClone.SubModels)
            {
                sub.CalculateData();
                for (int vi = 0; vi < sub.Vertexes.Count; vi++)
                {
                    var p = sub.Vertexes[vi];
                    sub.Vertexes[vi] = new TwinVec4(p.X * bakeScale.X, p.Y * bakeScale.Y, p.Z * bakeScale.Z, p.W);
                }
                for (int ni = 0; ni < sub.Normals.Count; ni++)
                {
                    var n = sub.Normals[ni];
                    var sn = new SysVec3(n.X * bakeScale.X, n.Y * bakeScale.Y, n.Z * bakeScale.Z);
                    if (sn.LengthSquared() > 1e-12f) sn = SysVec3.Normalize(sn);
                    sub.Normals[ni] = new TwinVec4(sn.X, sn.Y, sn.Z, n.W);
                }
                sub.Compile();
            }
        }

        uint newModelId = NewGraphicsId(destModelSec);
        modelClone.SetID(newModelId);
        destModelSec.AddItem(modelClone);
        destTables.Models[newModelId] = modelClone;

        var rmClone = CloneItem(srcRm);
        rmClone.Model = newModelId;
        for (int i = 0; i < rmClone.Materials.Count; i++)
            rmClone.Materials[i] = CloneMaterial(rmClone.Materials[i]);
        uint newRmId = NewGraphicsId(destRmSec, destMeshSec);
        rmClone.SetID(newRmId);
        destRmSec.AddItem(rmClone);
        destTables.RigidModels[newRmId] = rmClone;
        rec.SubmeshCount = 1;
        rec.VertexCount  = modelClone.SubModels.Sum(sm => sm.Vertexes?.Count ?? 0);

        var ogi = CloneItem(ogiTemplate);
        ogi.RigidModelIds = new List<uint> { newRmId };
        ogi.JointIndices  = new List<byte> { ogiTemplate.Joints.Count > 0 ? (byte)ogiTemplate.Joints[0].Index : (byte)0xFF };
        ogi.Collisions.Clear();
        ogi.CollisionJointIndices.Clear();
        ogi.BoundingBox[0] = bboxMin;
        ogi.BoundingBox[1] = bboxMax;

        uint newOgiId = 0;
        while (destOgiSec.ContainsItem(newOgiId) || (globalOgiSec?.ContainsItem(newOgiId) ?? false)) newOgiId++;
        ogi.SetID(newOgiId);
        destOgiSec.AddItem(ogi);
        destTables.OGIs[newOgiId] = ogi;

        uint newObjId = 0;
        while (destObjSec.ContainsItem(newObjId) || (globalObjSec?.ContainsItem(newObjId) ?? false)) newObjId++;
        var obj = new PS2AnyObject
        {
            Type = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject.ObjectType.GenericObject,
            Name = $"CE_ConvertedScenery_{objectName}_{newObjId:X4}",
            BehaviourPack = new Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourCommandPack(),
        };
        obj.OGISlots.Add((ushort)newOgiId);
        obj.AnimationSlots.Add(0xFFFF);
        obj.SetID(newObjId);
        destObjSec.AddItem(obj);
        destTables.Objects[newObjId] = obj;
        rec.ObjectId = newObjId;

        var modelById2 = BuildModelLookup(destGfx);
        foreach (var kv in BuildMeshLookup(destGfx, modelById2, gl, destTexCache)) destTables.ModelGpu[kv.Key] = kv.Value;

        log = $"reused real scenery mesh 0x{meshId:X} as new object 0x{newObjId:X4} (OGI 0x{newOgiId:X4}, " +
              $"+1 Model, +{matIdMap.Count} Material, +{rec.TextureCount} Texture) — same compiled geometry, " +
              "not re-baked.";
        return rec;
    }

    public static void ApplySceneryBounds(TwinSceneryBaseType item, SysVec3 min, SysVec3 max)
    {
        var center = (min + max) * 0.5f;
        var half   = max - center;
        float radius = half.Length();
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
                    mat.FogEnabled = false;
                    mat.DepthWrite = false;
                    RenderPipeline.SkydomeDraws.Add((gpuMesh, mat));
                }
            }
            Console.WriteLine($"[MeshDecoder] Skydome {sky.GetID():X8}: {sky.Meshes.Count} meshes");
        }
    }

    private static (bool HadMesh, bool HadAnim, bool HadAlt) BuildOneInstanceMesh(
        GL gl, RmTables tables, Entity e, InstanceData data)
    {
        if (!tables.Objects.TryGetValue(data.ObjectId, out var obj)) return (false, false, false);

        string instBaseName = $"Inst_{data.Source.GetID():X4}";
        e.Name = string.IsNullOrWhiteSpace(obj.Name)
            ? instBaseName
            : $"{obj.Name.Replace("|", "_")}_{instBaseName}";

        var defaultOgi = ResolveDefaultOgi(tables, obj, out var defaultParts);
        bool hadMesh = defaultParts.Count > 0;

        var slotSpecs = new List<(int Index, ushort AnimId, PS2AnyAnimation Anim, PS2AnyOGI Ogi)>();
        for (int i = 0; i < obj.AnimationSlots.Count; i++)
        {
            var animId = obj.AnimationSlots[i];
            if (animId == 0xFFFF) continue;
            if (!tables.Animations.TryGetValue(animId, out var animItem) || !animItem.HasAnimationData) continue;
            ushort ogiSlotId = i < obj.OGISlots.Count ? obj.OGISlots[i] : (ushort)0xFFFF;
            if (ogiSlotId == 0xFFFF || !tables.OGIs.TryGetValue(ogiSlotId, out var slotOgi)) continue;
            slotSpecs.Add((i, animId, animItem, slotOgi));
        }

        AnimatedObject? animObj = slotSpecs.Count > 0 ? e.Add(new AnimatedObject()) : null;
        bool hadAnim = animObj is not null;

        AnimatedObject.SlotGroup? defaultGroup = null;
        if (defaultOgi is not null)
        {
            var parent = animObj is null ? e : NewChild(e, $"{e.Name}_default");
            var (attached, morphs) = EmitOgiMeshEntities(gl, tables, parent, defaultParts, defaultOgi, animObj);
            if (animObj is not null)
            {
                defaultGroup = new AnimatedObject.SlotGroup { Ogi = defaultOgi, MeshRoot = parent };
                defaultGroup.AttachedRigidParts.AddRange(attached);
                defaultGroup.MorphMeshes.AddRange(morphs);
            }
        }
        if (animObj is null) return (hadMesh, hadAnim, false);
        animObj.DefaultGroup = defaultGroup;

        bool anyAlt = false;
        foreach (var (index, animId, animItem, slotOgi) in slotSpecs)
        {
            AnimatedObject.SlotGroup group;
            if (defaultGroup is not null && ReferenceEquals(slotOgi, defaultOgi))
            {
                group = new AnimatedObject.SlotGroup { Ogi = slotOgi, MeshRoot = defaultGroup.MeshRoot };
                group.AttachedRigidParts.AddRange(defaultGroup.AttachedRigidParts);
                group.MorphMeshes.AddRange(defaultGroup.MorphMeshes);
            }
            else
            {
                anyAlt = true;
                var altRoot = NewChild(e, $"{e.Name}_slot{index}");
                altRoot.Active = false;
                var altParts = BuildOgiMeshes(tables, slotOgi);
                var (attached, morphs) = EmitOgiMeshEntities(gl, tables, altRoot, altParts, slotOgi, animObj);
                group = new AnimatedObject.SlotGroup { Ogi = slotOgi, MeshRoot = altRoot };
                group.AttachedRigidParts.AddRange(attached);
                group.MorphMeshes.AddRange(morphs);
            }
            group.SlotIndex = (ushort)index;
            group.AnimId    = animId;
            group.Anim      = animItem;
            animObj.Groups.Add(group);
        }
        return (hadMesh, hadAnim, anyAlt);
    }

    private static Entity NewChild(Entity parent, string name)
    {
        var child = new Entity(name);
        parent.AddChild(child);
        return child;
    }

    private static readonly HashSet<SurfaceType> DeadlySurfaceTypes = new()
    {
        SurfaceType.SURF_LAVA, SurfaceType.SURF_GENERIC_INSTANT_DEATH, SurfaceType.SURF_FALL_THRU_DEATH,
        SurfaceType.SURF_DROWNING_PLANE, SurfaceType.SURF_NONSOLID_ELECTRIC_DEATH,
    };
    private static readonly HashSet<SurfaceType> BlockingSurfaceTypes = new()
    {
        SurfaceType.SURF_GLASS_WALL, SurfaceType.SURF_CAMERA_BLOCKING,
        SurfaceType.SURF_BLOCK_PLAYER, SurfaceType.SURF_BLOCK_AI_ONLY,
    };

    public static List<SurfaceType>? BuildSurfaceTypeLookup(PS2AnyTwinsanityRM2 rm2, PS2AnyTwinsanityRM2? globalRm2 = null)
    {
        List<SurfaceType>? TryFind(PS2AnyTwinsanityRM2 source)
        {
            for (int lid = Constants.LEVEL_LAYOUT_1_SECTION; lid <= Constants.LEVEL_LAYOUT_8_SECTION; lid++)
            {
                var layout = source.GetItem<BaseTwinSection>((uint)lid);
                var surfSec = layout?.GetItem<BaseTwinSection>((uint)Constants.LAYOUT_SURFACES_SECTION);
                if (surfSec is null || surfSec.GetItemsAmount() == 0) continue;

                var list = new List<SurfaceType>(surfSec.GetItemsAmount());
                for (int i = 0; i < surfSec.GetItemsAmount(); i++)
                    list.Add(surfSec.GetItem(i) is PS2AnyCollisionSurface s ? s.SurfaceId : SurfaceType.SURF_DEFAULT);
                return list;
            }
            return null;
        }

        return TryFind(rm2) ?? (globalRm2 is not null ? TryFind(globalRm2) : null);
    }

    private static readonly SysVec4 CollisionFallbackColor = new(1f, 0.15f, 0.85f, 0.92f);

    public static SysVec4 SurfaceDebugColor(SurfaceType st) => st switch
    {
        SurfaceType.SURF_NORMAL_SAND  => new SysVec4(1f, 0.85f, 0.1f, 0.92f),
        SurfaceType.SURF_NORMAL_GRASS => new SysVec4(0.15f, 0.8f, 0.2f, 0.92f),
        SurfaceType.SURF_NORMAL_WATER => new SysVec4(0.15f, 0.4f, 0.95f, 0.92f),
        SurfaceType.SURF_NORMAL_WOOD  => new SysVec4(0.72f, 0.52f, 0.3f, 0.92f),
        SurfaceType.SURF_NORMAL_METAL or SurfaceType.SURF_SLIPPY_METAL
            => new SysVec4(0.22f, 0.22f, 0.26f, 0.92f),
        SurfaceType.SURF_NORMAL_ROCK or SurfaceType.SURF_SLIPPY_ROCK
            or SurfaceType.SURF_NORMAL_STONE_TILES
            => new SysVec4(0.32f, 0.20f, 0.10f, 0.92f),
        SurfaceType.SURF_NORMAL_MUD   => new SysVec4(0.35f, 0.25f, 0.12f, 0.92f),
        SurfaceType.SURF_NORMAL_SNOW or SurfaceType.SURF_STICKY_SNOW
            => new SysVec4(0.9f, 0.9f, 0.95f, 0.92f),
        SurfaceType.SURF_ICE or SurfaceType.SURF_ICE_LOW_SLIPPY
            => new SysVec4(0.6f, 0.85f, 0.95f, 0.92f),
        SurfaceType.SURF_BLOCK_PLAYER or SurfaceType.SURF_BLOCK_AI_ONLY
            or SurfaceType.SURF_CAMERA_BLOCKING
            or SurfaceType.SURF_GENERIC_MEDIUM_SLIPPY_RIGID_ONLY
            => new SysVec4(0.55f, 0.55f, 0.55f, 0.92f),
        SurfaceType.SURF_GLASS_WALL
            => new SysVec4(0.5f, 0.3f, 0.12f, 0.92f),
        SurfaceType.SURF_LAVA or SurfaceType.SURF_GENERIC_INSTANT_DEATH
            or SurfaceType.SURF_FALL_THRU_DEATH or SurfaceType.SURF_DROWNING_PLANE
            or SurfaceType.SURF_NONSOLID_ELECTRIC_DEATH
            => new SysVec4(1f, 0.1f, 0.1f, 0.92f),
        _ => CollisionFallbackColor,
    };

    public static (GpuMesh Mesh, Material Mat)? DecodeCollision(GL gl,
        Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.PS2AnyCollisionData coll,
        IReadOnlyList<SurfaceType>? surfaceTypes = null)
    {
        if (coll.Triangles.Count == 0 || coll.Vectors.Count == 0) return null;

        var verts = new List<GpuMesh.Vertex>(coll.Triangles.Count * 3);

        foreach (var tri in coll.Triangles)
        {
            if (tri.Vector1Index < 0 || tri.Vector2Index < 0 || tri.Vector3Index < 0 ||
                tri.Vector1Index >= coll.Vectors.Count ||
                tri.Vector2Index >= coll.Vectors.Count ||
                tri.Vector3Index >= coll.Vectors.Count)
                continue;

            var debugColor = CollisionFallbackColor;
            if (surfaceTypes is not null && tri.SurfaceIndex >= 0 && tri.SurfaceIndex < surfaceTypes.Count)
                debugColor = SurfaceDebugColor(surfaceTypes[tri.SurfaceIndex]);

            var a = coll.Vectors[tri.Vector1Index];
            var b = coll.Vectors[tri.Vector2Index];
            var c = coll.Vectors[tri.Vector3Index];
            var pa = new SysVec3(a.X, a.Y, a.Z);
            var pb = new SysVec3(b.X, b.Y, b.Z);
            var pc = new SysVec3(c.X, c.Y, c.Z);

            var normal = SysVec3.Cross(pb - pa, pc - pa);
            normal = normal.LengthSquared() > 1e-12f ? SysVec3.Normalize(normal) : SysVec3.UnitY;

            verts.Add(new GpuMesh.Vertex { Position = pa, Normal = normal, Color = debugColor });
            verts.Add(new GpuMesh.Vertex { Position = pb, Normal = normal, Color = debugColor });
            verts.Add(new GpuMesh.Vertex { Position = pc, Normal = normal, Color = debugColor });
        }
        if (verts.Count < 3) return null;

        var idx  = Enumerable.Range(0, verts.Count).Select(i => (uint)i).ToArray();
        var mesh = new GpuMesh(gl, verts.ToArray(), idx);

        var localCenter = verts.Aggregate(SysVec3.Zero, (acc, v) => acc + v.Position) / verts.Count;
        var mat = new Material
        {
            Culling         = Material.CullMode.Both,
            DoubleColor     = 1f,
            AlphaBlend      = true,
            DepthWrite      = false,
            FogEnabled      = false,
            ShaderType      = "StandardUnlit",
            LocalCenter     = localCenter,
            BoundingRadius  = verts.Max(v => SysVec3.Distance(v.Position, localCenter)),
        };
        return (mesh, mat);
    }

    public static object BuildSceneryMeshes(GL gl,
                                          PS2AnyTwinsanitySM2 sm2,
                                          Entity sceneryEnt,
                                          Dictionary<uint, Texture2D> texCache,
                                          bool applyGlobalEffects = true)
    {
        var gfx = sm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        Console.WriteLine($"[MeshDecoder] SM2 gfx section: {(gfx is null ? "NULL" : "OK")}");
        if (gfx is null) return new Dictionary<uint, List<(GpuMesh, Material)>>();

        var modelById = BuildModelLookup(gfx);
        Console.WriteLine($"[MeshDecoder] SM2 models: {modelById.Count}");
        var meshById  = BuildMeshLookup(gfx, modelById, gl, texCache);
        Console.WriteLine($"[MeshDecoder] SM2 decoded gpu meshes: {meshById.Count}");

        if (applyGlobalEffects)
        {
            var sceneryItem = sm2.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
            if (sceneryItem is not null)
            {
                uint fogIdx = sceneryItem.FogColor;
                LastFogColor = fogIdx < FogColors.Length ? FogColors[fogIdx] : new SysVec3(0.58f, 0.72f, 0.88f);
                LastFogColorIndex = fogIdx < FogColors.Length ? (int)fogIdx : null;
                Console.WriteLine($"[MeshDecoder] Fog colour index {fogIdx} → {LastFogColor}");

                LastWorldLighting = DecodeWorldLighting(sm2);
                if (LastWorldLighting is { } wl)
                    Console.WriteLine($"[MeshDecoder] World lighting: ambient={wl.Ambient}, {wl.Directional.Count} directional light(s)");
            }

            RenderPipeline.SkydomeDraws.Clear();
            var skySec = gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKYDOMES_SECTION);
            if (skySec is not null)
            {
                for (int i = 0; i < skySec.GetItemsAmount(); i++)
                {
                    if (skySec.GetItem(i) is not PS2AnySkydome sky) continue;
                    foreach (var meshId in sky.Meshes)
                    {
                        if (!meshById.TryGetValue(meshId, out var entry)) continue;
                        foreach (var (gpuMesh, mat) in entry)
                        {
                            mat.FogEnabled = false;
                            mat.DepthWrite = false;
                            RenderPipeline.SkydomeDraws.Add((gpuMesh, mat));
                        }
                    }
                    Console.WriteLine($"[MeshDecoder] Skydome {sky.GetID():X8}: {sky.Meshes.Count} meshes");
                }
            }
        }

        var dyn = sm2.GetItem<PS2AnyDynamicScenery>((uint)Constants.SCENERY_DYNAMIC_SECENERY_ITEM);
        if (dyn is not null)
        {
            var dynEnt = new Entity("DynamicScenery");
            int placed = 0;
            foreach (var model in dyn.DynamicModels)
            {
                if (!meshById.TryGetValue(model.MeshID, out var entry)) continue;
                var (pos, rot) = Frame0Transform(model.Animation);
                var ent = new Entity($"dyn_{model.MeshID:X8}");
                ent.Transform.Position = pos;
                ent.Transform.Rotation = rot;
                foreach (var (gpuMesh, mat) in entry)
                {
                    var child = new Entity($"dynm_{model.MeshID:X8}");
                    child.Add(new MeshRenderer { Mesh = gpuMesh, Material = mat });
                    ent.AddChild(child);
                }
                dynEnt.AddChild(ent);
                placed++;
            }
            sceneryEnt.AddChild(dynEnt);
            Console.WriteLine($"[MeshDecoder] Dynamic scenery: {placed}/{dyn.DynamicModels.Count} models placed");
        }

        var lodById = new Dictionary<uint, PS2AnyLOD>();
        var lodSec  = gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_LODS_SECTION);
        if (lodSec is not null)
            for (int i = 0; i < lodSec.GetItemsAmount(); i++)
                if (lodSec.GetItem(i) is PS2AnyLOD lod && lodSec.GetItem(i) is BaseTwinItem bi)
                    lodById[bi.GetID()] = lod;

        var scenery = sm2.GetItem<PS2AnyScenery>((uint)Constants.SCENERY_SECENERY_ITEM);
        Console.WriteLine($"[MeshDecoder] SM2 scenery: {(scenery is null ? "NULL" : $"{scenery.Sceneries.Count} nodes")}  LODs: {lodById.Count}");
        if (scenery is null) return meshById;

        int idx = 0, lodPlaced = 0;

        void Place(List<(GpuMesh, Material)> entry, TwinMat4 mat, string src, uint srcId,
                   TwinSceneryBaseType node, bool isLod)
        {
            var meshEnt = new Entity($"scenery_{idx++}_{src}{srcId:X8}");
            meshEnt.Add(new SceneryTile { Source = mat, Node = node, SourceId = srcId, IsLod = isLod });
            meshEnt.Transform.LocalMatrix = TwinMatToSys(mat);
            foreach (var (gpuMesh, m) in entry)
            {
                var child = new Entity($"sm_{idx}");
                child.Add(new MeshRenderer { Mesh = gpuMesh, Material = m });
                meshEnt.AddChild(child);
            }
            sceneryEnt.AddChild(meshEnt);
        }

        foreach (var node in scenery.Sceneries)
        {
            for (int i = 0; i < node.MeshIDs.Count; i++)
                if (meshById.TryGetValue(node.MeshIDs[i], out var entry))
                    Place(entry, node.MeshModelMatrices[i], "mesh", node.MeshIDs[i], node, isLod: false);

            for (int i = 0; i < node.LodIDs.Count; i++)
            {
                if (!lodById.TryGetValue(node.LodIDs[i], out var lod) || lod.Meshes.Count == 0) continue;
                if (!meshById.TryGetValue(lod.Meshes[0], out var entry)) continue;
                Place(entry, node.LodModelMatrices[i], "lod", node.LodIDs[i], node, isLod: true);
                lodPlaced++;
            }
        }
        Console.WriteLine($"[MeshDecoder] Scenery placed: {idx} meshes ({lodPlaced} via LODs)");
        return new SceneryTables { MeshById = meshById, LodById = lodById };
    }

    private sealed class SceneryTables
    {
        public required Dictionary<uint, List<(GpuMesh, Material)>> MeshById;
        public required Dictionary<uint, PS2AnyLOD> LodById;
    }

    public static uint? ResolveSceneryMeshId(object tablesHandle, SceneryTile tile)
    {
        if (tablesHandle is not SceneryTables tables) return null;
        if (!tile.IsLod) return tile.SourceId;
        return tables.LodById.TryGetValue(tile.SourceId, out var lod) && lod.Meshes.Count > 0 ? lod.Meshes[0] : null;
    }

    public static void BuildMeshForSceneryTile(GL gl, object tablesHandle, Entity e)
    {
        if (tablesHandle is not SceneryTables tables) return;
        var tile = e.Get<SceneryTile>();
        if (tile is null) return;

        uint meshId = tile.SourceId;
        if (tile.IsLod)
        {
            if (!tables.LodById.TryGetValue(tile.SourceId, out var lod) || lod.Meshes.Count == 0) return;
            meshId = lod.Meshes[0];
        }
        if (!tables.MeshById.TryGetValue(meshId, out var entry)) return;

        int i = 0;
        foreach (var (gpuMesh, m) in entry)
        {
            var child = new Entity($"{e.Name}_sm_{i++}");
            child.Add(new MeshRenderer { Mesh = gpuMesh, Material = m });
            e.AddChild(child);
        }
    }


    private sealed class RmTables
    {
        public Dictionary<uint, PS2AnyObject>      Objects      = new();
        public Dictionary<uint, PS2AnyOGI>         OGIs         = new();
        public Dictionary<uint, PS2AnyRigidModel>  RigidModels  = new();
        public Dictionary<uint, PS2AnyAnimation>   Animations   = new();
        public Dictionary<uint, ITwinModel>        Models       = new();
        public Dictionary<uint, List<(GpuMesh, Material)>> ModelGpu     = new();
        public Dictionary<uint, List<(GpuMesh, Material)>> SkinGpu      = new();
        public Dictionary<uint, List<(GpuMesh, Material)>> BlendSkinGpu = new();

        public Dictionary<uint, (Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinBlendSkin Blend,
            List<(Texture2D? Tex, TwinShader? Shader)> SubMats)> BlendSkinRaw = new();
    }

    private static RmTables BuildLookupTables(GL gl,
                                              PS2AnyTwinsanityRM2 rm2,
                                              Dictionary<uint, Texture2D> texCache,
                                              PS2AnyTwinsanityRM2? globalRm2 = null)
    {
        var tables = new RmTables();
        if (globalRm2 is not null) AddRm2ToTables(gl, globalRm2, texCache, tables);
        AddRm2ToTables(gl, rm2, texCache, tables);
        Console.WriteLine($"[MeshDecoder] Skins decoded: {tables.SkinGpu.Count}");
        return tables;
    }

    private static void AddRm2ToTables(GL gl, PS2AnyTwinsanityRM2 rm2,
                                       Dictionary<uint, Texture2D> texCache, RmTables tables)
    {
        var code = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        if (code is not null)
        {
            FillSection(code, (uint)Constants.CODE_GAME_OBJECTS_SECTION,
                        tables.Objects,  i => i is PS2AnyObject o ? o : null);
            FillSection(code, (uint)Constants.CODE_OGIS_SECTION,
                        tables.OGIs, i => i is PS2AnyOGI o ? o : null);
            FillSection(code, (uint)Constants.CODE_ANIMATIONS_SECTION,
                        tables.Animations, i => i is PS2AnyAnimation a ? a : null);
        }

        var gfx = rm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        if (gfx is not null)
        {
            FillSection(gfx, (uint)Constants.GRAPHICS_RIGID_MODELS_SECTION,
                        tables.RigidModels, i => i is PS2AnyRigidModel r ? r : null);

            var modelById = BuildModelLookup(gfx);
            foreach (var kv in modelById) tables.Models[kv.Key] = kv.Value;
            foreach (var kv in BuildMeshLookup(gfx, modelById, gl, texCache)) tables.ModelGpu[kv.Key]     = kv.Value;
            foreach (var kv in BuildSkinLookup(gfx, gl, texCache))            tables.SkinGpu[kv.Key]      = kv.Value;
            foreach (var kv in BuildBlendSkinLookup(gfx, gl, texCache, tables.BlendSkinRaw)) tables.BlendSkinGpu[kv.Key] = kv.Value;
        }
    }

    private static Dictionary<uint, List<(GpuMesh, Material)>> BuildBlendSkinLookup(
        PS2AnyGraphicsSection gfx, GL gl, Dictionary<uint, Texture2D> texCache,
        Dictionary<uint, (Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinBlendSkin Blend,
            List<(Texture2D? Tex, TwinShader? Shader)> SubMats)> rawOut)
    {
        var result        = new Dictionary<uint, List<(GpuMesh, Material)>>();
        var materialsById = BuildMaterialLookup(gfx);

        var sec = gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_BLEND_SKINS_SECTION);
        if (sec is null) return result;

        for (int i = 0; i < sec.GetItemsAmount(); i++)
        {
            if (sec.GetItem(i) is not Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinBlendSkin blend) continue;
            if (sec.GetItem(i) is not BaseTwinItem bi) continue;

            var subMats = new List<(Texture2D?, TwinShader?)>();
            foreach (var sb in blend.SubBlends)
                subMats.Add(ResolveMaterial(sb.Material, materialsById, texCache));

            var gpu = DecodeBlendSkin(gl, blend, subMats);
            if (gpu.Count > 0) result[bi.GetID()] = gpu;
            rawOut[bi.GetID()] = (blend, subMats);
        }
        return result;
    }

    private static Dictionary<uint, List<(GpuMesh, Material)>> BuildSkinLookup(
        PS2AnyGraphicsSection gfx, GL gl, Dictionary<uint, Texture2D> texCache)
    {
        var result        = new Dictionary<uint, List<(GpuMesh, Material)>>();
        var materialsById = BuildMaterialLookup(gfx);

        var skinSec = gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION);
        if (skinSec is null) return result;

        for (int i = 0; i < skinSec.GetItemsAmount(); i++)
        {
            if (skinSec.GetItem(i) is not Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinSkin skin) continue;
            if (skinSec.GetItem(i) is not BaseTwinItem bi) continue;

            var subMats = new List<(Texture2D?, TwinShader?)>();
            foreach (var ss in skin.SubSkins)
                subMats.Add(ResolveMaterial(ss.Material, materialsById, texCache));

            var gpu = DecodeSkin(gl, skin, subMats);
            if (gpu.Count > 0) result[bi.GetID()] = gpu;
        }
        return result;
    }

    private static (Texture2D?, TwinShader?) ResolveMaterial(
        uint matId, Dictionary<uint, PS2AnyMaterial> materialsById, Dictionary<uint, Texture2D> texCache)
    {
        Texture2D?  tex    = null;
        TwinShader? shader = null;
        if (materialsById.TryGetValue(matId, out var mat))
            foreach (var sh in mat.Shaders)
            {
                shader ??= sh;
                if (texCache.TryGetValue(sh.TextureId, out tex) && tex is not null) break;
            }
        return (tex, shader);
    }

    private static void FillSection<T>(BaseTwinSection parent, uint subId,
                                        Dictionary<uint, T> dict,
                                        Func<object, T?> cast) where T : class
    {
        var sec = parent.GetItem<BaseTwinSection>(subId);
        if (sec is null) return;
        for (int i = 0; i < sec.GetItemsAmount(); i++)
        {
            var item = sec.GetItem(i);
            var t    = cast(item);
            if (t is not null && item is BaseTwinItem bi)
                dict[bi.GetID()] = t;
        }
    }


    private static Dictionary<uint, ITwinModel> BuildModelLookup(PS2AnyGraphicsSection gfx)
    {
        var dict = new Dictionary<uint, ITwinModel>();
        var sec  = gfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        if (sec is null) return dict;
        for (int i = 0; i < sec.GetItemsAmount(); i++)
        {
            if (sec.GetItem(i) is ITwinModel m && sec.GetItem(i) is BaseTwinItem bi)
                dict[bi.GetID()] = m;
        }
        return dict;
    }

    private static Dictionary<uint, PS2AnyMaterial> BuildMaterialLookup(PS2AnyGraphicsSection gfx)
    {
        var dict = new Dictionary<uint, PS2AnyMaterial>();
        FillSection(gfx, (uint)Constants.GRAPHICS_MATERIALS_SECTION,
                    dict, i => i is PS2AnyMaterial m ? m : null);
        Console.WriteLine($"[MeshDecoder] Materials: {dict.Count}");
        return dict;
    }

    private static Dictionary<uint, List<(GpuMesh, Material)>> BuildMeshLookup(
        PS2AnyGraphicsSection gfx,
        Dictionary<uint, ITwinModel> modelById,
        GL gl,
        Dictionary<uint, Texture2D> texCache)
    {
        var result = new Dictionary<uint, List<(GpuMesh, Material)>>();
        var materialsById = BuildMaterialLookup(gfx);

        DecodeMeshSection(gfx, (uint)Constants.GRAPHICS_MESHES_SECTION,
                          modelById, gl, texCache, materialsById, result);

        DecodeMeshSection(gfx, (uint)Constants.GRAPHICS_RIGID_MODELS_SECTION,
                          modelById, gl, texCache, materialsById, result);

        return result;
    }

    private static void DecodeMeshSection(PS2AnyGraphicsSection gfx,
                                           uint sectionId,
                                           Dictionary<uint, ITwinModel> modelById,
                                           GL gl,
                                           Dictionary<uint, Texture2D> texCache,
                                           Dictionary<uint, PS2AnyMaterial> materialsById,
                                           Dictionary<uint, List<(GpuMesh, Material)>> result)
    {
        var sec = gfx.GetItem<BaseTwinSection>(sectionId);
        if (sec is null) return;

        for (int i = 0; i < sec.GetItemsAmount(); i++)
        {
            if (sec.GetItem(i) is not PS2AnyRigidModel rm) continue;
            if (sec.GetItem(i) is not BaseTwinItem bi) continue;

            if (!modelById.TryGetValue(rm.Model, out var model)) continue;

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
                        if (texCache.TryGetValue(sh.TextureId, out tex) && tex is not null)
                            break;
                    }
                }
                subMats.Add((tex, shader));
            }

            var gpuList = DecodeModel(gl, model, subMats);
            if (gpuList.Count > 0)
                result[bi.GetID()] = gpuList;
        }
    }


    private static List<(GpuMesh Mesh, Material Mat, Matrix4x4 JointLocal, bool IsSkinned, bool IsBlendSkin, int JointIndex)> BuildOgiMeshes(
        RmTables tables, PS2AnyOGI ogi)
    {
        var result = new List<(GpuMesh, Material, Matrix4x4, bool, bool, int)>();

        var jointWorld = ComputeJointWorlds(ogi);

        for (int i = 0; i < ogi.RigidModelIds.Count; i++)
        {
            var rmId = ogi.RigidModelIds[i];
            if (!tables.ModelGpu.TryGetValue(rmId, out var meshes)) continue;

            int jointIdx = i < ogi.JointIndices.Count ? ogi.JointIndices[i] : 0;
            var m = jointWorld.TryGetValue(jointIdx, out var jw) ? jw : Matrix4x4.Identity;

            foreach (var (mesh, mat) in meshes)
                result.Add((mesh, mat, m, false, false, jointIdx));
        }

        if (ogi.SkinID != 0 && tables.SkinGpu.TryGetValue(ogi.SkinID, out var skinMeshes))
            foreach (var (mesh, mat) in skinMeshes)
                result.Add((mesh, mat, Matrix4x4.Identity, true, false, -1));

        if (ogi.BlendSkinID != 0 && tables.BlendSkinGpu.TryGetValue(ogi.BlendSkinID, out var blendMeshes))
            foreach (var (mesh, mat) in blendMeshes)
            {
                mat.AlwaysOnTop = true;
                result.Add((mesh, mat, Matrix4x4.Identity, true, true, -1));
            }

        return result;
    }

    private static PS2AnyOGI? ResolveDefaultOgi(RmTables tables, PS2AnyObject obj,
        out List<(GpuMesh, Material, Matrix4x4, bool, bool, int)> parts)
    {
        foreach (var ogiSlot in obj.OGISlots)
        {
            if (!tables.OGIs.TryGetValue(ogiSlot, out var ogi)) continue;
            var built = BuildOgiMeshes(tables, ogi);
            if (built.Count > 0) { parts = built; return ogi; }
        }
        parts = new();
        return null;
    }

    private static (List<(Entity Child, int JointIndex, Matrix4x4 BindLocal)> AttachedRigidParts,
                    List<MorphMesh> MorphMeshes) EmitOgiMeshEntities(
        GL gl, RmTables tables, Entity parent,
        List<(GpuMesh Mesh, Material Mat, Matrix4x4 JointLocal, bool IsSkinned, bool IsBlendSkin, int JointIndex)> parts,
        PS2AnyOGI ogi, AnimatedObject? animObj)
    {
        var attached = new List<(Entity, int, Matrix4x4)>();
        var morphs   = new List<MorphMesh>();

        foreach (var (gpuMesh, mat, jointLocal, isSkinned, isBlendSkin, jointIndex) in parts)
        {
            if (isBlendSkin && animObj is not null) continue;

            var child = new Entity($"{parent.Name}_mesh");
            child.Transform.LocalMatrix = jointLocal;
            var mr = child.Add(new MeshRenderer { Mesh = gpuMesh, Material = mat, IsSkinned = isSkinned });
            if (isSkinned) mr.Skeleton = animObj;
            else if (animObj is not null && jointIndex >= 0)
                attached.Add((child, jointIndex, jointLocal));
            parent.AddChild(child);
        }

        if (animObj is not null && ogi.BlendSkinID != 0 &&
            tables.BlendSkinRaw.TryGetValue(ogi.BlendSkinID, out var raw))
        {
            foreach (var mm in DecodeBlendSkinMorph(gl, raw.Blend, raw.SubMats))
            {
                mm.Mat.AlwaysOnTop = true;
                morphs.Add(mm);

                var child = new Entity($"{parent.Name}_face");
                var mr = child.Add(new MeshRenderer { Mesh = mm.Mesh, Material = mm.Mat, IsSkinned = true });
                mr.Skeleton = animObj;
                parent.AddChild(child);
            }
        }

        return (attached, morphs);
    }

    public static Dictionary<int, Matrix4x4> ComputeJointWorlds(PS2AnyOGI ogi)
    {
        var world = new Dictionary<int, Matrix4x4>();
        if (ogi.Joints.Count == 0) return world;

        world[ogi.Joints[0].Index] = Matrix4x4.Identity;
        for (int i = 1; i < ogi.Joints.Count; i++)
        {
            var j = ogi.Joints[i];
            var q = new Quaternion(j.LocalRotation.X, j.LocalRotation.Y, j.LocalRotation.Z, j.LocalRotation.W);
            var local = Matrix4x4.CreateFromQuaternion(q) *
                        Matrix4x4.CreateTranslation(j.LocalTranslation.X, j.LocalTranslation.Y, j.LocalTranslation.Z);
            var parent = world.TryGetValue(j.ParentIndex, out var pw) ? pw : Matrix4x4.Identity;
            world[j.Index] = local * parent;
        }
        return world;
    }


    public static Matrix4x4[] SampleAnimationPose(PS2AnyOGI ogi, TwinAnimation anim, float frameF)
        => SampleAnimationPose(ogi, anim, frameF, out _);

    public static Matrix4x4[] SampleAnimationPose(PS2AnyOGI ogi, TwinAnimation anim, float frameF,
        out Dictionary<int, Matrix4x4> jointWorlds)
    {
        var bones = new Matrix4x4[RenderPipeline.MaxBones];
        for (int i = 0; i < bones.Length; i++) bones[i] = Matrix4x4.Identity;
        jointWorlds = new Dictionary<int, Matrix4x4>();
        if (ogi.Joints.Count == 0 || anim.JointSettings.Count == 0 || anim.AnimatedTransformations.Count == 0)
            return bones;

        var bindWorld = ComputeJointWorlds(ogi);

        int lastFrame = anim.AnimatedTransformations.Count - 1;
        frameF = Math.Clamp(frameF, 0, lastFrame);
        int f0 = (int)MathF.Floor(frameF);
        int f1 = Math.Min(f0 + 1, lastFrame);
        float t = frameF - f0;

        int jn = Math.Min(anim.JointSettings.Count, ogi.Joints.Count);

        var rawScale  = new Dictionary<int, SysVec3> { [ogi.Joints[0].Index] = SysVec3.One };
        var animWorld = new Dictionary<int, Matrix4x4> { [ogi.Joints[0].Index] = Matrix4x4.Identity };

        for (int i = 1; i < ogi.Joints.Count; i++)
        {
            var j = ogi.Joints[i];
            Matrix4x4 local;
            SysVec3   jointRawScale;

            if (i < jn)
            {
                var (translate, rot, scale) = SampleJointTRS(anim, anim.JointSettings[i], f0, f1, t, j.AdditionalAnimationRotation);
                jointRawScale = scale;

                var effScale = scale;
                if (anim.JointSettings[i].UseParentJointScale && rawScale.TryGetValue(j.ParentIndex, out var pScale))
                    effScale = new SysVec3(
                        pScale.X != 0f ? scale.X / pScale.X : scale.X,
                        pScale.Y != 0f ? scale.Y / pScale.Y : scale.Y,
                        pScale.Z != 0f ? scale.Z / pScale.Z : scale.Z);

                local = Matrix4x4.CreateScale(effScale) * rot * Matrix4x4.CreateTranslation(translate);
            }
            else
            {
                local = Matrix4x4.Identity;
                jointRawScale = SysVec3.One;
            }

            rawScale[j.Index] = jointRawScale;
            var parent = animWorld.TryGetValue(j.ParentIndex, out var pw) ? pw : Matrix4x4.Identity;
            animWorld[j.Index] = local * parent;
        }

        foreach (var j in ogi.Joints)
        {
            if (j.Index < 0 || j.Index >= RenderPipeline.MaxBones) continue;
            if (!bindWorld.TryGetValue(j.Index, out var bw)) continue;
            if (!Matrix4x4.Invert(bw, out var invBind)) continue;
            var aw = animWorld.TryGetValue(j.Index, out var awv) ? awv : bw;
            bones[j.Index] = invBind * aw;
        }
        jointWorlds = animWorld;
        return bones;
    }

    public static float[] SampleFacialWeights(TwinMorphAnimation facialAnim, float frameF)
    {
        var weights = new float[MaxMorphShapes];
        if (facialAnim.JointSettings.Count == 0 || facialAnim.AnimatedTransformations.Count == 0)
            return weights;

        int lastFrame = facialAnim.AnimatedTransformations.Count - 1;
        frameF = Math.Clamp(frameF, 0, lastFrame);
        int f0 = (int)MathF.Floor(frameF);
        int f1 = Math.Min(f0 + 1, lastFrame);
        float t = frameF - f0;

        var js = facialAnim.JointSettings[0];
        int staticIdx = js.TransformationIndex;
        int curIdx    = js.AnimationTransformationIndex;
        int nextIdx   = js.AnimationTransformationIndex;

        int shapes = Math.Min((int)js.FacialShapesAmount, MaxMorphShapes);
        for (int i = 0; i < shapes; i++)
        {
            if (js.AnimationMorph[i] == TwinTransformType.Animated)
            {
                float w0 = facialAnim.AnimatedTransformations[f0].Transforms[curIdx++].Value;
                float w1 = facialAnim.AnimatedTransformations[f1].Transforms[nextIdx++].Value;
                weights[i] = w0 + (w1 - w0) * t;
            }
            else
            {
                weights[i] = facialAnim.StaticTransformations[staticIdx++].Value;
            }
        }
        return weights;
    }

    private static (SysVec3 Translate, Matrix4x4 Rot, SysVec3 Scale) SampleJointTRS(
        TwinAnimation anim, TwinJointSettings js, int f0, int f1, float t,
        Twinsanity.TwinsanityInterchange.Common.Vector4 additionalRotation)
    {
        int staticIdx = js.TransformationIndex;
        int curIdx    = js.AnimationTransformationIndex;
        int nextIdx   = js.AnimationTransformationIndex;

        var (cx, nx) = SampleLinear(anim, js.TranslateX, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (cy, ny) = SampleLinear(anim, js.TranslateY, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (cz, nz) = SampleLinear(anim, js.TranslateZ, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var translate = SysVec3.Lerp(new SysVec3(cx, cy, cz), new SysVec3(nx, ny, nz), t);

        var (crx, nrx) = SampleRotation(anim, js.RotateX, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (cry, nry) = SampleRotation(anim, js.RotateY, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (crz, nrz) = SampleRotation(anim, js.RotateZ, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var q1 = Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateRotationX(crx) * Matrix4x4.CreateRotationY(cry) * Matrix4x4.CreateRotationZ(crz));
        var q2 = Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateRotationX(nrx) * Matrix4x4.CreateRotationY(nry) * Matrix4x4.CreateRotationZ(nrz));
        var q  = Quaternion.Slerp(q1, q2, t);

        var (csx, nsx) = SampleLinear(anim, js.ScaleX, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (csy, nsy) = SampleLinear(anim, js.ScaleY, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var (csz, nsz) = SampleLinear(anim, js.ScaleZ, f0, f1, ref staticIdx, ref curIdx, ref nextIdx);
        var scale = SysVec3.Lerp(new SysVec3(csx, csy, csz), new SysVec3(nsx, nsy, nsz), t);
        if (scale == SysVec3.Zero) scale = SysVec3.One;

        var rot = Matrix4x4.CreateFromQuaternion(q);
        if (js.UseAdditionalRotation)
        {
            var addQ = new Quaternion(additionalRotation.X, additionalRotation.Y, additionalRotation.Z, additionalRotation.W);
            rot *= Matrix4x4.CreateFromQuaternion(addQ);
        }

        return (translate, rot, scale);
    }

    private static (float cur, float next) SampleLinear(TwinAnimation anim, TwinTransformType type,
        int f0, int f1, ref int staticIdx, ref int curIdx, ref int nextIdx)
    {
        if (type == TwinTransformType.Animated)
        {
            float c = anim.AnimatedTransformations[f0].Transforms[curIdx++].Value;
            float n = anim.AnimatedTransformations[f1].Transforms[nextIdx++].Value;
            return (c, n);
        }
        float v = anim.StaticTransformations[staticIdx++].Value;
        return (v, v);
    }

    private static (float cur, float next) SampleRotation(TwinAnimation anim, TwinTransformType type,
        int f0, int f1, ref int staticIdx, ref int curIdx, ref int nextIdx)
    {
        if (type == TwinTransformType.Animated)
        {
            int rot1 = anim.AnimatedTransformations[f0].Transforms[curIdx++].PureValue * 16;
            int rot2 = anim.AnimatedTransformations[f1].Transforms[nextIdx++].PureValue * 16;
            return GetRotationChanges(rot1, rot2);
        }
        float rot = anim.StaticTransformations[staticIdx++].RotationValue;
        return (rot, rot);
    }

    private static (float, float) GetRotationChanges(int rot1, int rot2)
    {
        var diff = rot1 - rot2;
        if (diff < -0x8000) rot1 += 0x10000;
        if (diff > 0x8000)  rot1 -= 0x10000;

        var rot1Rad = rot1 / (float)(ushort.MaxValue + 1) * (float)(Math.PI * 2);
        var rot2Rad = rot2 / (float)(ushort.MaxValue + 1) * (float)(Math.PI * 2);
        return (rot1Rad, rot2Rad);
    }


    public static List<(GpuMesh, Material)> DecodeModel(GL gl,
                                                         ITwinModel model,
                                                         IReadOnlyList<(Texture2D? Tex, TwinShader? Shader)>? subMats = null)
    {
        var result = new List<(GpuMesh, Material)>();

        for (int si = 0; si < model.SubModels.Count; si++)
        {
            var sub = model.SubModels[si];
            var (tex, shader) = subMats is { Count: > 0 }
                ? subMats[Math.Min(si, subMats.Count - 1)]
                : (null, null);
            try
            {
                sub.CalculateData();
            }
            catch (Exception ex) { Console.WriteLine($"[MeshDecoder][DecodeModel] submodel {si} decode FAILED: {ex.Message}"); continue; }

            if (sub.Vertexes.Count < 3) continue;

            var triVerts = BuildTriangleListVerts(sub);
            if (triVerts.Count < 3) continue;

            if (result.Count == 0)
            {
                Console.WriteLine($"[MeshDecoder] First vert pos: {triVerts[0].Position}  tris:{triVerts.Count/3}  alpha:{triVerts[0].Color.W:F3}  color:({triVerts[0].Color.X:F2},{triVerts[0].Color.Y:F2},{triVerts[0].Color.Z:F2})");
            }

            var vertArray = triVerts.ToArray();
            var idxArray  = Enumerable.Range(0, triVerts.Count).Select(i => (uint)i).ToArray();
            var gpuMesh   = new GpuMesh(gl, vertArray, idxArray);

            var localCenter = triVerts.Aggregate(SysVec3.Zero, (a, v) => a + v.Position) / triVerts.Count;

            var mat = BuildTwinMaterial(tex, shader, sub);
            mat.LocalCenter    = localCenter;
            mat.BoundingRadius = triVerts.Max(v => SysVec3.Distance(v.Position, localCenter));
            result.Add((gpuMesh, mat));
        }

        return result;
    }

    private static List<GpuMesh.Vertex> BuildTriangleListVerts(
        Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems.ITwinSubModel sub)
    {
        var verts = new List<GpuMesh.Vertex>(sub.Vertexes.Count);
        for (int j = 0; j < sub.Vertexes.Count; j++)
        {
            var p = sub.Vertexes[j];
            var n = j < sub.Normals.Count ? sub.Normals[j]
                                           : new Twinsanity.TwinsanityInterchange.Common.Vector4(0, 1, 0, 0);
            var uv = j < sub.UVW.Count ? sub.UVW[j]
                                        : new Twinsanity.TwinsanityInterchange.Common.Vector4(0, 0, 0, 0);
            var c = j < sub.Colors.Count ? sub.Colors[j]
                                         : new Twinsanity.TwinsanityInterchange.Common.Vector4(1, 1, 1, 1);

            verts.Add(new GpuMesh.Vertex
            {
                Position = new SysVec3(p.X, p.Y, p.Z),
                Normal   = new SysVec3(n.X, n.Y, n.Z),
                UV       = new SysVec2(uv.X, uv.Y),
                Color    = new SysVec4(
                    Math.Clamp(c.X, 0f, 1f),
                    Math.Clamp(c.Y, 0f, 1f),
                    Math.Clamp(c.Z, 0f, 1f),
                    Math.Clamp(c.W, 0f, 1f))
            });
        }

        var triVerts = new List<GpuMesh.Vertex>();
        bool winding = false;
        int connLen  = sub.Connection.Count;
        for (int j = 2; j < sub.Vertexes.Count; j++)
        {
            if (j >= connLen || !sub.Connection[j])
            {
                winding = !winding;
                continue;
            }
            if (!winding)
            {
                triVerts.Add(verts[j - 2]);
                triVerts.Add(verts[j - 1]);
                triVerts.Add(verts[j]);
            }
            else
            {
                triVerts.Add(verts[j - 1]);
                triVerts.Add(verts[j - 2]);
                triVerts.Add(verts[j]);
            }
            winding = !winding;
        }
        return triVerts;
    }

    public static List<(int A, int B, int C)> GetSubModelTriangleIndices(
        Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems.ITwinSubModel sub)
    {
        var tris = new List<(int, int, int)>();
        bool winding = false;
        int connLen = sub.Connection.Count;
        for (int j = 2; j < sub.Vertexes.Count; j++)
        {
            if (j >= connLen || !sub.Connection[j]) { winding = !winding; continue; }
            tris.Add(winding ? (j - 1, j - 2, j) : (j - 2, j - 1, j));
            winding = !winding;
        }
        return tris;
    }

    public static void RefreshSceneryMeshVertexData(GL gl, object tablesHandle, uint meshId, ITwinModel model)
    {
        if (tablesHandle is not SceneryTables tables) return;
        if (!tables.MeshById.TryGetValue(meshId, out var entry)) return;

        for (int si = 0; si < entry.Count && si < model.SubModels.Count; si++)
        {
            var sub = model.SubModels[si];
            if (sub.Vertexes is null || sub.Colors is null) continue;
            var triVerts = BuildTriangleListVerts(sub);
            var gpuMesh = entry[si].Item1;
            if (triVerts.Count != gpuMesh.VertexCount) continue;
            gpuMesh.UpdateVertices(gl, triVerts.ToArray());
        }
    }

    public static void RefreshObjectMeshVertexData(GL gl, object tablesHandle, uint rmId, ITwinModel model)
    {
        if (tablesHandle is not RmTables tables) return;
        if (!tables.ModelGpu.TryGetValue(rmId, out var entry)) return;

        for (int si = 0; si < entry.Count && si < model.SubModels.Count; si++)
        {
            var sub = model.SubModels[si];
            if (sub.Vertexes is null) continue;
            var triVerts = BuildTriangleListVerts(sub);
            var gpuMesh = entry[si].Item1;
            if (triVerts.Count != gpuMesh.VertexCount) continue;
            gpuMesh.UpdateVertices(gl, triVerts.ToArray());
        }
    }

    public static void AliasSceneryMeshTableEntry(object tablesHandle, uint oldId, uint newId)
    {
        if (tablesHandle is not SceneryTables tables) return;
        if (tables.MeshById.TryGetValue(oldId, out var entry))
            tables.MeshById[newId] = entry;
    }

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

    public static uint PrivatizeOneSceneryMeshId(PS2AnyTwinsanitySM2 sm2, uint oldId)
    {
        var gfx = sm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        var meshSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
        var modelSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
        if (meshSec is null || modelSec is null) return oldId;
        var remap = new Dictionary<uint, uint>();
        return CloneMeshChainCached(meshSec, modelSec, oldId, remap);
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

        var newObj  = CloneItem(srcObj);
        uint newObjId = NewCodeLocalId(objSec);
        newObj.SetID(newObjId);

        var rmIdMap = new Dictionary<uint, uint>();
        bool anyMesh = false;

        for (int i = 0; i < newObj.OGISlots.Count; i++)
        {
            var oldOgiId = newObj.OGISlots[i];
            if (oldOgiId == 0xFFFF) continue;
            if (!tables.OGIs.TryGetValue(oldOgiId, out var srcOgi)) continue;

            var newOgi = CloneItem(srcOgi);
            ushort newOgiId = NewCodeLocalId(ogiSec);
            newOgi.SetID(newOgiId);

            for (int r = 0; r < newOgi.RigidModelIds.Count; r++)
            {
                var oldRmId = newOgi.RigidModelIds[r];
                if (rmIdMap.TryGetValue(oldRmId, out var already)) { newOgi.RigidModelIds[r] = already; continue; }
                if (!tables.RigidModels.TryGetValue(oldRmId, out var srcRm)) continue;

                var newRm = CloneItem(srcRm);
                uint newRmId = NewGraphicsLocalId(rmSec, meshSec);
                newRm.SetID(newRmId);

                PS2AnyModel? newModel = null;
                if (tables.Models.TryGetValue(srcRm.Model, out var srcModelItem) && srcModelItem is PS2AnyModel srcModel)
                {
                    newModel = CloneItem(srcModel);
                    uint newModelId = NewGraphicsLocalId(modelSec);
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
                    var subMats = new List<(Texture2D? Tex, TwinShader? Shader)>();
                    foreach (var matId in newRm.Materials)
                    {
                        Texture2D? tex = null; TwinShader? shader = null;
                        if (materialsById.TryGetValue(matId, out var mat))
                            foreach (var sh in mat.Shaders)
                            { shader ??= sh; if (texCache.TryGetValue(sh.TextureId, out tex) && tex is not null) break; }
                        subMats.Add((tex, shader));
                    }
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
                        if (triVerts.Count != gpuMesh.VertexCount) continue;
                        gpuMesh.UpdateVertices(gl, triVerts.ToArray());
                    }
                }
            }
        }
        return touched;
    }


    public static List<(GpuMesh, Material)> DecodeBlendSkin(GL gl,
        Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinBlendSkin blend,
        IReadOnlyList<(Texture2D? Tex, TwinShader? Shader)>? subMats = null)
    {
        var result = new List<(GpuMesh, Material)>();

        for (int si = 0; si < blend.SubBlends.Count; si++)
        {
            var sb = blend.SubBlends[si];
            if (sb.Models.Count == 0) continue;

            var (tex, shader) = subMats is { Count: > 0 }
                ? subMats[Math.Min(si, subMats.Count - 1)]
                : (null, null);

            var tri = new List<GpuMesh.Vertex>();
            foreach (var model in sb.Models)
            {
                try { model.CalculateData(); } catch { continue; }
                if (model.Vertexes.Count < 3) continue;

                GpuMesh.Vertex V(int j)
                {
                    var ji = model.SkinJoints[j];
                    return new()
                    {
                        Position = new SysVec3(model.Vertexes[j].X, model.Vertexes[j].Y, model.Vertexes[j].Z),
                        Normal   = SysVec3.UnitY,
                        UV       = new System.Numerics.Vector2(model.UVW[j].X, model.UVW[j].Y),
                        Color    = new SysVec4(
                            Math.Clamp(model.Colors[j].X, 0f, 1f), Math.Clamp(model.Colors[j].Y, 0f, 1f),
                            Math.Clamp(model.Colors[j].Z, 0f, 1f), Math.Clamp(model.Colors[j].W, 0f, 1f)),
                        BoneIndices = new SysVec3(ji.JointIndex1, ji.JointIndex2, ji.JointIndex3),
                        BoneWeights = new SysVec3(ji.Weight1,     ji.Weight2,     ji.Weight3),
                    };
                }

                for (int i = 0; i < model.Vertexes.Count - 2; i++)
                {
                    if (i + 2 >= model.SkinJoints.Count || !model.SkinJoints[i + 2].Connection) continue;
                    if (i % 2 == 0) { tri.Add(V(i));     tri.Add(V(i + 1)); tri.Add(V(i + 2)); }
                    else            { tri.Add(V(i + 1)); tri.Add(V(i));     tri.Add(V(i + 2)); }
                }
            }
            if (tri.Count < 3) continue;

            var idx = Enumerable.Range(0, tri.Count).Select(k => (uint)k).ToArray();
            var gpu = new GpuMesh(gl, tri.ToArray(), idx);
            var mat = BuildTwinMaterial(tex, shader);
            var localCenter = tri.Aggregate(SysVec3.Zero, (a, v) => a + v.Position) / tri.Count;
            mat.LocalCenter    = localCenter;
            mat.BoundingRadius = tri.Max(v => SysVec3.Distance(v.Position, localCenter));
            result.Add((gpu, mat));
        }
        return result;
    }

    public const int MaxMorphShapes = 15;

    public sealed class MorphMesh
    {
        public GpuMesh Mesh { get; }
        public Material Mat { get; }
        private readonly GpuMesh.Vertex[] _base;
        private readonly SysVec3[][]      _shapeOffsets;
        private readonly GpuMesh.Vertex[] _scratch;

        public MorphMesh(GpuMesh mesh, Material mat, GpuMesh.Vertex[] baseVerts, SysVec3[][] shapeOffsets)
        {
            Mesh = mesh; Mat = mat; _base = baseVerts; _shapeOffsets = shapeOffsets;
            _scratch = new GpuMesh.Vertex[baseVerts.Length];
        }

        public void ApplyWeights(GL gl, ReadOnlySpan<float> weights)
        {
            Array.Copy(_base, _scratch, _base.Length);
            for (int s = 0; s < _shapeOffsets.Length && s < weights.Length; s++)
            {
                if (weights[s] == 0f) continue;
                var off = _shapeOffsets[s];
                for (int v = 0; v < off.Length && v < _scratch.Length; v++)
                    _scratch[v].Position += off[v] * weights[s];
            }
            Mesh.UpdateVertices(gl, _scratch);
        }
    }

    public static List<MorphMesh> DecodeBlendSkinMorph(GL gl,
        Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinBlendSkin blend,
        IReadOnlyList<(Texture2D? Tex, TwinShader? Shader)>? subMats = null)
    {
        var result = new List<MorphMesh>();

        for (int si = 0; si < blend.SubBlends.Count; si++)
        {
            var sb = blend.SubBlends[si];
            if (sb.Models.Count == 0) continue;

            var (tex, shader) = subMats is { Count: > 0 }
                ? subMats[Math.Min(si, subMats.Count - 1)]
                : (null, null);

            var tri = new List<GpuMesh.Vertex>();
            var shapeLists = new List<SysVec3>[MaxMorphShapes];
            for (int s = 0; s < MaxMorphShapes; s++) shapeLists[s] = new List<SysVec3>();

            foreach (var model in sb.Models)
            {
                try { model.CalculateData(); } catch { continue; }
                if (model.Vertexes.Count < 3) continue;
                foreach (var face in model.Faces) { try { face.CalculateData(); } catch { } }

                GpuMesh.Vertex V(int j)
                {
                    var ji = model.SkinJoints[j];
                    return new()
                    {
                        Position = new SysVec3(model.Vertexes[j].X, model.Vertexes[j].Y, model.Vertexes[j].Z),
                        Normal   = SysVec3.UnitY,
                        UV       = new System.Numerics.Vector2(model.UVW[j].X, model.UVW[j].Y),
                        Color    = new SysVec4(
                            Math.Clamp(model.Colors[j].X, 0f, 1f), Math.Clamp(model.Colors[j].Y, 0f, 1f),
                            Math.Clamp(model.Colors[j].Z, 0f, 1f), Math.Clamp(model.Colors[j].W, 0f, 1f)),
                        BoneIndices = new SysVec3(ji.JointIndex1, ji.JointIndex2, ji.JointIndex3),
                        BoneWeights = new SysVec3(ji.Weight1,     ji.Weight2,     ji.Weight3),
                    };
                }

                SysVec3 Off(int shapeIdx, int j)
                {
                    if (shapeIdx >= model.Faces.Count) return SysVec3.Zero;
                    var verts = model.Faces[shapeIdx].Vertices;
                    if (verts is null || j >= verts.Count) return SysVec3.Zero;
                    var o = verts[j].Offset;
                    return new SysVec3(o.X, o.Y, o.Z);
                }

                void Emit(int j)
                {
                    tri.Add(V(j));
                    for (int s = 0; s < MaxMorphShapes; s++) shapeLists[s].Add(Off(s, j));
                }

                for (int i = 0; i < model.Vertexes.Count - 2; i++)
                {
                    if (i + 2 >= model.SkinJoints.Count || !model.SkinJoints[i + 2].Connection) continue;
                    if (i % 2 == 0) { Emit(i);     Emit(i + 1); Emit(i + 2); }
                    else            { Emit(i + 1); Emit(i);     Emit(i + 2); }
                }
            }
            if (tri.Count < 3) continue;

            var idx = Enumerable.Range(0, tri.Count).Select(k => (uint)k).ToArray();
            var baseVerts = tri.ToArray();
            var gpu = new GpuMesh(gl, baseVerts, idx);
            var mat = BuildTwinMaterial(tex, shader);
            var localCenter = tri.Aggregate(SysVec3.Zero, (a, v) => a + v.Position) / tri.Count;
            mat.LocalCenter    = localCenter;
            mat.BoundingRadius = tri.Max(v => SysVec3.Distance(v.Position, localCenter));

            var shapeArrays = new SysVec3[MaxMorphShapes][];
            for (int s = 0; s < MaxMorphShapes; s++)
                shapeArrays[s] = shapeLists[s].Any(v => v != SysVec3.Zero) ? shapeLists[s].ToArray() : Array.Empty<SysVec3>();

            result.Add(new MorphMesh(gpu, mat, baseVerts, shapeArrays));
        }
        return result;
    }

    public static List<(GpuMesh, Material)> DecodeSkin(GL gl,
        Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinSkin skin,
        IReadOnlyList<(Texture2D? Tex, TwinShader? Shader)>? subMats = null, bool recalculate = true)
    {
        var result = new List<(GpuMesh, Material)>();

        for (int si = 0; si < skin.SubSkins.Count; si++)
        {
            var sub = skin.SubSkins[si];
            if (recalculate) { try { sub.CalculateData(); } catch { continue; } }
            if (sub.Vertexes.Count < 3) continue;

            var (tex, shader) = subMats is { Count: > 0 }
                ? subMats[Math.Min(si, subMats.Count - 1)]
                : (null, null);

            var tri = new List<GpuMesh.Vertex>();
            bool winding = false;
            GpuMesh.Vertex V(int j)
            {
                var ji = sub.SkinJoints[j];
                return new()
                {
                    Position = new SysVec3(sub.Vertexes[j].X, sub.Vertexes[j].Y, sub.Vertexes[j].Z),
                    Normal   = SysVec3.UnitY,
                    UV       = new System.Numerics.Vector2(sub.UVW[j].X, sub.UVW[j].Y),
                    Color    = new SysVec4(
                        Math.Clamp(sub.Colors[j].X, 0f, 1f),
                        Math.Clamp(sub.Colors[j].Y, 0f, 1f),
                        Math.Clamp(sub.Colors[j].Z, 0f, 1f),
                        Math.Clamp(sub.Colors[j].W, 0f, 1f)),
                    BoneIndices = new SysVec3(ji.JointIndex1, ji.JointIndex2, ji.JointIndex3),
                    BoneWeights = new SysVec3(ji.Weight1,     ji.Weight2,     ji.Weight3),
                };
            }
            for (int j = 2; j < sub.Vertexes.Count; j++)
            {
                if (j >= sub.SkinJoints.Count || !sub.SkinJoints[j].Connection)
                {
                    winding = !winding;
                    continue;
                }
                if (!winding) { tri.Add(V(j - 2)); tri.Add(V(j - 1)); tri.Add(V(j)); }
                else          { tri.Add(V(j - 1)); tri.Add(V(j - 2)); tri.Add(V(j)); }
                winding = !winding;
            }

            if (tri.Count < 3) continue;

            var idx = Enumerable.Range(0, tri.Count).Select(k => (uint)k).ToArray();
            var gpu = new GpuMesh(gl, tri.ToArray(), idx);
            var localCenter = tri.Aggregate(SysVec3.Zero, (a, v) => a + v.Position) / tri.Count;

            var mat = BuildTwinMaterial(tex, shader);
            mat.LocalCenter    = localCenter;
            mat.BoundingRadius = tri.Max(v => SysVec3.Distance(v.Position, localCenter));
            result.Add((gpu, mat));
        }
        return result;
    }

    public static Material BuildTwinMaterial(Texture2D? tex, TwinShader? shader,
        Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems.ITwinSubModel? sourceSubModel = null)
    {
        if (shader is null)
            return new Material { Culling = Material.CullMode.Both, DoubleColor = 1f };

        bool unlit = shader.ShaderType switch
        {
            TwinShader.Type.StandardLit
            or TwinShader.Type.LitSkinnedModel
            or TwinShader.Type.LitEnvironmentMap
            or TwinShader.Type.LitMetallic
            or TwinShader.Type.LitReflectionSurface => false,
            _ => true,
        };
        bool envMap = shader.ShaderType is TwinShader.Type.LitEnvironmentMap
                                        or TwinShader.Type.UnlitEnvironmentMap;

        var uvScroll = SysVec2.Zero;
        if (shader.XScrollSettings != TwinShader.XScrollFormula.Disabled)
            uvScroll.X = shader.UvScrollSpeed.Z;
        if (shader.YScrollSettings != TwinShader.YScrollFormula.Disabled)
            uvScroll.Y = shader.UvScrollSpeed.W;

        var deform = SysVec2.Zero;
        if (shader.ShaderType is TwinShader.Type.UnlitClothDeformation
                              or TwinShader.Type.UnlitClothDeformation2)
        {
            deform.X = shader.FloatParam[0];
            deform.Y = shader.FloatParam[1];
        }

        // Amedo 2026-09-21
        ResolveGsBlend(shader, out var blendSrc, out var blendDst, out var blendEq, out var blendConstA);

        return new Material
        {
            Albedo          = shader.TxtMapping == TwinShader.TextureMapping.ON ? tex : null,
            Culling         = Material.CullMode.Both,
            DoubleColor     = unlit ? 1f : 2f,
            AlphaBlend      = shader.ABlending == TwinShader.AlphaBlending.ON,
            AlphaTest       = shader.ATest == TwinShader.AlphaTest.ON
                                  ? shader.AlphaValueToBeComparedTo / 255f : 0f,
            AlphaTestFunc   = shader.ATest == TwinShader.AlphaTest.ON ? (int)shader.ATestMethod : 1,
            TextureNearest  = shader.TextureFilterWhenTextureIsExpanded == TwinShader.TextureFilter.NEAREST,
            BlendSrcFactor  = blendSrc,
            BlendDstFactor  = blendDst,
            BlendEquation   = blendEq,
            BlendConstantAlpha = blendConstA,
            BillboardRender = shader.ShaderType == TwinShader.Type.UnlitBillboard,
            ReflectDist     = shader.ShaderType == TwinShader.Type.LitReflectionSurface
                                  ? new SysVec2(1f, shader.FloatParam[0]) : SysVec2.Zero,
            MetalicSpecular = shader.ShaderType is TwinShader.Type.LitMetallic
                                                or TwinShader.Type.UnlitGlossy ? 1f : 0f,
            DeformSpeed     = deform,
            EnvMap          = envMap ? 1f : 0f,
            UvScrollSpeed   = uvScroll,
            DepthWrite      = shader.ZValueDrawingMask == TwinShader.ZValueDrawMask.UPDATE,
            FogEnabled      = shader.Fog == TwinShader.Fogging.ON,
            ShaderType      = shader.ShaderType.ToString(),
            SourceShader    = shader,
            SourceSubModel  = sourceSubModel,
        };
    }

    // Amedo 2026-09-21
    private static void ResolveGsBlend(TwinShader shader,
        out Silk.NET.OpenGL.BlendingFactor src, out Silk.NET.OpenGL.BlendingFactor dst,
        out Silk.NET.OpenGL.BlendEquationModeEXT eq, out float constAlpha)
    {
        var CS = TwinShader.ColorSpecMethod.SOURCE;
        var FB = TwinShader.ColorSpecMethod.FB;
        var Z  = TwinShader.ColorSpecMethod.ZERO;

        TwinShader.ColorSpecMethod a, b, d;
        TwinShader.AlphaSpecMethod c;
        if (shader.UseCustomAlphaRegSettings)
        {
            a = shader.SpecOfColA; b = shader.SpecOfColB; c = shader.SpecOfAlphaC; d = shader.SpecOfColD;
        }
        else
        {
            switch (shader.AlphaRegSettingsIndex)
            {
                case TwinShader.AlphaBlendPresets.Add: a = CS; b = Z;  c = TwinShader.AlphaSpecMethod.SOURCE; d = FB; break;
                case TwinShader.AlphaBlendPresets.Sub: a = Z;  b = CS; c = TwinShader.AlphaSpecMethod.SOURCE; d = FB; break;
                case TwinShader.AlphaBlendPresets.Source:      a = CS; b = Z; c = TwinShader.AlphaSpecMethod.FIX; d = Z; break;
                case TwinShader.AlphaBlendPresets.Zero:
                case TwinShader.AlphaBlendPresets.Destination: a = Z; b = Z; c = TwinShader.AlphaSpecMethod.SOURCE; d = FB; break;
                case TwinShader.AlphaBlendPresets.Alpha:       a = CS; b = FB; c = TwinShader.AlphaSpecMethod.FB; d = FB; break;
                default:                                       a = CS; b = FB; c = TwinShader.AlphaSpecMethod.SOURCE; d = FB; break; // Mix
            }
        }

        constAlpha = Math.Clamp(shader.FixedAlphaValue / 128f, 0f, 1f);

        Silk.NET.OpenGL.BlendingFactor cf, cInv;
        switch (c)
        {
            case TwinShader.AlphaSpecMethod.FB:  cf = Silk.NET.OpenGL.BlendingFactor.DstAlpha;      cInv = Silk.NET.OpenGL.BlendingFactor.OneMinusDstAlpha; break;
            case TwinShader.AlphaSpecMethod.FIX: cf = Silk.NET.OpenGL.BlendingFactor.ConstantAlpha; cInv = Silk.NET.OpenGL.BlendingFactor.OneMinusConstantAlpha; break;
            default:                             cf = Silk.NET.OpenGL.BlendingFactor.SrcAlpha;      cInv = Silk.NET.OpenGL.BlendingFactor.OneMinusSrcAlpha; break;
        }

        eq = Silk.NET.OpenGL.BlendEquationModeEXT.FuncAdd;
        if      (a == CS && b == FB && d == FB) { src = cf; dst = cInv; }
        else if (a == CS && b == Z  && d == FB) { src = cf; dst = Silk.NET.OpenGL.BlendingFactor.One; }
        else if (a == Z  && b == CS && d == FB) { src = cf; dst = Silk.NET.OpenGL.BlendingFactor.One; eq = Silk.NET.OpenGL.BlendEquationModeEXT.FuncReverseSubtract; }
        else if (a == Z  && b == Z  && d == FB) { src = Silk.NET.OpenGL.BlendingFactor.Zero; dst = Silk.NET.OpenGL.BlendingFactor.One; }
        else if (a == CS && d == Z)             { src = Silk.NET.OpenGL.BlendingFactor.One;  dst = Silk.NET.OpenGL.BlendingFactor.Zero; }
        else                                    { src = Silk.NET.OpenGL.BlendingFactor.SrcAlpha; dst = Silk.NET.OpenGL.BlendingFactor.OneMinusSrcAlpha; }
    }

    private static (SysVec3, Quaternion) Frame0Transform(
        Twinsanity.TwinsanityInterchange.Common.DynamicScenery.TwinDynamicSceneryAnimation? anim)
    {
        if (anim is null || anim.ModelSettings.Count == 0)
            return (SysVec3.Zero, Quaternion.Identity);

        var m  = anim.ModelSettings[0];
        int si = m.StaticTransformationIndex;
        int ai = m.AnimationTransformationIndex;

        float Get(Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType t) =>
            t == Twinsanity.TwinsanityInterchange.Common.Animation.Enums.TransformType.Animated
                ? (anim.AnimatedTransformations.Count > 0
                    ? anim.AnimatedTransformations[0].TransformationValues[ai++] : 0f)
                : anim.StaticTransformations[si++].Value;

        float x  = Get(m.TranslateX), y  = Get(m.TranslateY), z  = Get(m.TranslateZ);
        float rx = Get(m.RotateX),    ry = Get(m.RotateY),    rz = Get(m.RotateZ);
        Get(m.RotateW);

        var rot = Quaternion.CreateFromRotationMatrix(
            Matrix4x4.CreateRotationX(rx) * Matrix4x4.CreateRotationY(ry) * Matrix4x4.CreateRotationZ(rz));
        return (new SysVec3(x, y, z), rot);
    }


    public static Matrix4x4 TwinMatToSys(TwinMat4 m)
    {
        return new Matrix4x4(
            m.Column1.X, m.Column1.Y, m.Column1.Z, m.Column1.W,
            m.Column2.X, m.Column2.Y, m.Column2.Z, m.Column2.W,
            m.Column3.X, m.Column3.Y, m.Column3.Z, m.Column3.W,
            m.Column4.X, m.Column4.Y, m.Column4.Z, m.Column4.W);
    }

    private static SysVec3 ExtractTranslation(Matrix4x4 m) =>
        new(m.M14, m.M24, m.M34);

    private static SysVec3 ExtractScale(Matrix4x4 m) => new(
        new SysVec3(m.M11, m.M21, m.M31).Length(),
        new SysVec3(m.M12, m.M22, m.M32).Length(),
        new SysVec3(m.M13, m.M23, m.M33).Length());

    private static IEnumerable<Entity> AllEntities(Entity e)
    {
        yield return e;
        foreach (var c in e.Children)
            foreach (var sub in AllEntities(c))
                yield return sub;
    }
}

public sealed class AnimatedObject : CrashEngine.Core.Component
{
    public sealed class SlotGroup
    {
        public ushort SlotIndex;
        public ushort AnimId;
        public Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyAnimation Anim { get; set; } = null!;
        public Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI Ogi { get; set; } = null!;
        public Entity MeshRoot { get; set; } = null!;
        public List<(Entity Child, int JointIndex, System.Numerics.Matrix4x4 BindLocal)> AttachedRigidParts { get; } = new();
        public List<MeshDecoder.MorphMesh> MorphMeshes { get; } = new();
    }

    public List<SlotGroup> Groups { get; } = new();
    public SlotGroup? DefaultGroup { get; set; }
    public SlotGroup? CurrentGroup { get; private set; }

    public Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyAnimation? Current => CurrentGroup?.Anim;
    public float Frame   { get; set; }
    public bool  Playing { get; set; }
    public bool  Loop    { get; set; } = true;
    public System.Numerics.Matrix4x4[] BoneMatrices { get; private set; } = RenderPipeline.IdentityBoneMatrices;

    public void Play(SlotGroup group)
    {
        var previouslyVisible = CurrentGroup ?? DefaultGroup;
        if (previouslyVisible is not null && previouslyVisible.MeshRoot != group.MeshRoot)
            previouslyVisible.MeshRoot.Active = false;
        group.MeshRoot.Active = true;

        CurrentGroup = group;
        Frame        = 0f;
        Playing      = true;
    }

    public void Stop()
    {
        Playing      = false;
        BoneMatrices = RenderPipeline.IdentityBoneMatrices;
        if (CurrentGroup is not null)
        {
            foreach (var (child, _, bindLocal) in CurrentGroup.AttachedRigidParts)
                child.Transform.LocalMatrix = bindLocal;
            ResetFace(CurrentGroup);

            if (DefaultGroup is not null && CurrentGroup.MeshRoot != DefaultGroup.MeshRoot)
            {
                CurrentGroup.MeshRoot.Active = false;
                DefaultGroup.MeshRoot.Active = true;
            }
        }
        CurrentGroup = null;
    }

    private static void ResetFace(SlotGroup g)
    {
        if (g.MorphMeshes.Count == 0) return;
        var gl   = Engine.Instance.GL;
        var zero = new float[MeshDecoder.MaxMorphShapes];
        foreach (var mm in g.MorphMeshes) mm.ApplyWeights(gl, zero);
    }

    public override void OnUpdate()
    {
        var g = CurrentGroup;
        if (g is null || !g.Anim.HasAnimationData) return;

        int lastFrame = g.Anim.MainAnimation.AnimatedTransformations.Count - 1;
        if (lastFrame < 0) return;

        if (Playing)
        {
            float fps = g.Anim.DefaultFPS > 0 ? g.Anim.DefaultFPS : 30f;
            Frame += EngineTime.Delta * fps;
            if (Frame > lastFrame)
            {
                if (Loop) Frame %= MathF.Max(1f, lastFrame);
                else { Frame = lastFrame; Playing = false; }
            }
        }

        BoneMatrices = MeshDecoder.SampleAnimationPose(g.Ogi, g.Anim.MainAnimation, Frame, out var jointWorlds);

        foreach (var (child, jointIndex, _) in g.AttachedRigidParts)
            if (jointWorlds.TryGetValue(jointIndex, out var jw))
                child.Transform.LocalMatrix = jw;

        if (g.Anim.HasFacialAnimationData && g.MorphMeshes.Count > 0)
        {
            var weights = MeshDecoder.SampleFacialWeights(g.Anim.FacialAnimation, Frame);
            var gl = Engine.Instance.GL;
            foreach (var mm in g.MorphMeshes) mm.ApplyWeights(gl, weights);
        }
    }
}
