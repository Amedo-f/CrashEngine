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
        foreach (var kv in BuildMeshLookup(destGfx!, modelById2, gl, destTexCache, destTables.GlobalSource)) destTables.ModelGpu[kv.Key] = kv.Value; // Amedo
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
        foreach (var kv in BuildMeshLookup(destGfx!, modelById2, gl, destTexCache, destTables.GlobalSource)) destTables.ModelGpu[kv.Key] = kv.Value; // Amedo
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
        foreach (var kv in BuildMeshLookup(destGfx!, modelById2, gl, destTexCache, destTables.GlobalSource)) destTables.ModelGpu[kv.Key] = kv.Value; // Amedo
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
}
