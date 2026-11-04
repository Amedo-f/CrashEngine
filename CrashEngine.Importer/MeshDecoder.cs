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

public static partial class MeshDecoder // Amedo 2026-10-05
{
    public static readonly SysVec3[] FogColors = // Amedo 2026-09-26
    {
        new( 52/255f,   0f,     247/255f),
        new(  0f,       0f,       0f),
        new(165/255f, 235/255f, 255/255f),
        new(  0f,     255/255f,  18/255f),
        new(  1f,       1f,       1f),
        new(  1f,     200/255f, 120/255f),
        new(  0f,       0f,       0f),
    };

    public static readonly string[] FogColorNames = { "Violet", "Black", "Light blue", "Green", "White", "Beige", "None" }; // Amedo 2026-09-26

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
                                           PS2AnyTwinsanityRM2? globalRm2 = null,
                                           ICollection<uint>? globalTextureIds = null, // Amedo 2026-09-29
                                           object? sharedTables = null)
    {
        var tables = BuildLookupTables(gl, rm2, texCache, globalRm2, globalTextureIds, sharedTables as RmTables);
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

    // Amedo 2026-09-28
    public sealed class ForeignGroupBuild
    {
        public required Entity Owner { get; init; }
        public required AnimatedObject.SlotGroup Group { get; init; }
        public Entity? CreatedRoot { get; init; }
        public Entity? DefaultWrapper { get; init; }
        public bool CreatedAnimator { get; init; }

        public void Undo()
        {
            if (Owner.Get<AnimatedObject>() is { } anim && ReferenceEquals(anim.CurrentGroup, Group)) anim.Stop();
            if (CreatedRoot is not null) Owner.RemoveChild(CreatedRoot);
            if (DefaultWrapper is not null)
            {
                foreach (var child in DefaultWrapper.Children.ToList())
                {
                    DefaultWrapper.RemoveChild(child);
                    child.Active = DefaultWrapper.Active; // Amedo 2026-09-29
                    Owner.AddChild(child);
                }
                DefaultWrapper.Active = true;
                Owner.RemoveChild(DefaultWrapper);
            }
            if (CreatedAnimator) Owner.RemoveComponent<AnimatedObject>();
        }
    }

    // Amedo 2026-09-28
    public static ForeignGroupBuild? BuildForeignGroup(GL gl, object tablesHandle, Entity e, ushort ogiId, ushort animId,
                                                       AnimatedObject.SlotGroup? sameModel = null)
    {
        if (tablesHandle is not RmTables tables || !tables.OGIs.TryGetValue(ogiId, out var ogi)) return null;
        PS2AnyAnimation? anim = null;
        if (animId != 0xFFFF && (!tables.Animations.TryGetValue(animId, out anim) || !anim.HasAnimationData)) return null;

        var animObj = e.Get<AnimatedObject>();
        bool createdAnimator = animObj is null;
        Entity? wrapper = null;
        if (animObj is null)
        {
            animObj = e.Add(new AnimatedObject());
            wrapper = new Entity($"{e.Name}_default");
            bool shown = true; // Amedo 2026-09-29
            foreach (var child in e.Children.ToList())
            {
                shown &= child.Active;
                child.Active = true;
                e.RemoveChild(child);
                wrapper.AddChild(child);
            }
            wrapper.Active = shown;
            e.AddChild(wrapper);
            var data = e.Get<InstanceData>();
            PS2AnyObject? obj = null; // Amedo
            var defaultOgi = data is not null && tables.Objects.TryGetValue(data.ObjectId, out obj) ? ResolveDefaultOgi(tables, obj, out _) : null;
            if (obj is not null) animObj.ReactSlots = obj.ReactJointAmount; // Amedo
            animObj.DefaultGroup = new AnimatedObject.SlotGroup { Ogi = defaultOgi!, MeshRoot = wrapper, StaticMesh = true }; // Amedo 2026-09-28
        }

        Entity? root = null;
        var group = new AnimatedObject.SlotGroup { Ogi = ogi, SlotIndex = 0xFFFF, AnimId = animId };
        var share = sameModel ?? (animObj.DefaultGroup is { StaticMesh: false } def && ReferenceEquals(def.Ogi, ogi) ? def : null); // Amedo 2026-09-28
        if (share is not null)
        {
            group.MeshRoot = share.MeshRoot;
            group.AttachedRigidParts.AddRange(share.AttachedRigidParts);
            group.MorphMeshes.AddRange(share.MorphMeshes);
        }
        else
        {
            root = NewChild(e, $"{e.Name}_foreign{ogiId:X}");
            root.Active = false;
            var (attached, morphs) = EmitOgiMeshEntities(gl, tables, root, BuildOgiMeshes(tables, ogi), ogi, animObj);
            group.MeshRoot = root;
            group.AttachedRigidParts.AddRange(attached);
            group.MorphMeshes.AddRange(morphs);
        }
        if (anim is not null) group.Anim = anim;
        return new ForeignGroupBuild
        {
            Owner = e, Group = group, CreatedRoot = root, DefaultWrapper = wrapper, CreatedAnimator = createdAnimator,
        };
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
            var shaders = GetMaterialsShadersDirect(rm2, materialIds, texCache); // Amedo 2026-09-26
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

    public static List<TwinShader?> GetMaterialsShadersDirect(PS2AnyTwinsanityRM2 rm2, List<uint> materialIds,
        Dictionary<uint, Texture2D>? texCache = null) // Amedo 2026-09-26
    {
        var gfx = rm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var matSec = gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var result = new List<TwinShader?>();
        foreach (var matId in materialIds)
        {
            var mat = matSec?.GetItem<PS2AnyMaterial>(matId);
            if (mat is not null && texCache is not null) RegisterUpperLayers(mat, texCache); // Amedo 2026-09-26
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

    // Amedo 2026-10-03
    public static PS2AnyObject? GetRawObject(object tablesHandle, uint objectId) =>
        tablesHandle is RmTables tables && tables.Objects.TryGetValue(objectId, out var obj) ? obj : null;

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
        GameMath.Length(new SysVec3(m.M11, m.M21, m.M31)),
        GameMath.Length(new SysVec3(m.M12, m.M22, m.M32)),
        GameMath.Length(new SysVec3(m.M13, m.M23, m.M33)));

    private static IEnumerable<Entity> AllEntities(Entity e)
    {
        yield return e;
        foreach (var c in e.Children)
            foreach (var sub in AllEntities(c))
                yield return sub;
    }
}
