using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;

namespace CrashEngine.Importer;

public static partial class MeshDecoder
{
    private readonly record struct Rm2Sections(
        BaseTwinSection? Objects, BaseTwinSection? Ogis, BaseTwinSection? Animations, BaseTwinSection? Behaviours, BaseTwinSection? Sounds,
        PS2AnyGraphicsSection? Gfx, BaseTwinSection? RigidModels, BaseTwinSection? Meshes, BaseTwinSection? Models, BaseTwinSection? Materials,
        BaseTwinSection? Skins, BaseTwinSection? BlendSkins, PS2AnyTexturesSection? Textures)
    {
        public static Rm2Sections Of(PS2AnyTwinsanityRM2 rm2)
        {
            var code = rm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
            var gfx  = rm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
            return new Rm2Sections(
                code?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION),
                code?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION),
                code?.GetItem<BaseTwinSection>((uint)Constants.CODE_ANIMATIONS_SECTION),
                code?.GetItem<BaseTwinSection>((uint)Constants.CODE_BEHAVIOURS_SECTION),
                code?.GetItem<BaseTwinSection>((uint)Constants.CODE_SOUND_EFFECTS_SECTION),
                gfx,
                gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION),
                gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION),
                gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION),
                gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION),
                gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION),
                gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_BLEND_SKINS_SECTION),
                gfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION));
        }
    }

    private sealed class GraphicsCopy(GL gl, RmTables tables, Dictionary<uint, Texture2D> texCache, Rm2Sections src, Rm2Sections dest,
        List<uint> ogiIds, List<uint> animIds, List<uint> rigidModelIds, List<uint> modelIds,
        List<uint> skinIds, List<uint> blendSkinIds, List<uint> materialIds, List<uint> textureIds)
    {
        public static GraphicsCopy For(GL gl, RmTables tables, Dictionary<uint, Texture2D> texCache, Rm2Sections src, Rm2Sections dest, TransplantRecord rec) =>
            new(gl, tables, texCache, src, dest, rec.OgiIds, rec.AnimIds, rec.RigidModelIds, rec.ModelIds,
                rec.SkinIds, rec.BlendSkinIds, rec.MaterialIds, rec.TextureIds);

        public static GraphicsCopy For(GL gl, RmTables tables, Dictionary<uint, Texture2D> texCache, Rm2Sections src, Rm2Sections dest, FullTransplantRecord rec) =>
            new(gl, tables, texCache, src, dest, rec.OgiIds, rec.AnimIds, rec.RigidModelIds, rec.ModelIds,
                rec.SkinIds, rec.BlendSkinIds, rec.MaterialIds, rec.TextureIds);

        public void Texture(uint texId)
        {
            if (texCache.ContainsKey(texId) || src.Textures is null) return;
            var srcTex = src.Textures.GetItem<PS2AnyTexture>(texId);
            if (srcTex is null) return;
            var clone = CloneItem(srcTex);
            if (!dest.Textures!.ContainsItem(texId)) { dest.Textures.AddItem(clone); textureIds.Add(texId); }
            var t = ChunkImporter.DecodeTexture(gl, clone);
            if (t is not null) texCache[texId] = t;
        }

        public void Material(uint matId)
        {
            if (matId == 0 || src.Materials is null) return;
            var mat = dest.Materials!.GetItem<PS2AnyMaterial>(matId);
            if (mat is null)
            {
                var srcMat = src.Materials.GetItem<PS2AnyMaterial>(matId);
                if (srcMat is null) return;
                mat = CloneItem(srcMat);
                dest.Materials.AddItem(mat);
                materialIds.Add(matId);
            }
            foreach (var sh in mat.Shaders) Texture(sh.TextureId);
        }

        public void Model(uint modelId)
        {
            var model = dest.Models!.GetItem<PS2AnyModel>(modelId);
            if (model is null)
            {
                if (src.Models is null) return;
                var srcModel = src.Models.GetItem<PS2AnyModel>(modelId);
                if (srcModel is null) return;
                model = CloneItem(srcModel);
                dest.Models.AddItem(model);
                modelIds.Add(modelId);
            }
            tables.Models[modelId] = model;
        }

        public void RigidModel(uint rmId)
        {
            if (src.RigidModels is null) return;
            var rm = dest.RigidModels!.GetItem<PS2AnyRigidModel>(rmId);
            bool isNew = rm is null;
            if (isNew)
            {
                var srcRm = src.RigidModels.GetItem<PS2AnyRigidModel>(rmId);
                if (srcRm is null) return;
                rm = CloneItem(srcRm);
                dest.RigidModels.AddItem(rm);
                rigidModelIds.Add(rmId);
            }
            tables.RigidModels[rmId] = rm!;
            Model(rm!.Model);
            foreach (var m in rm.Materials) Material(m);
        }

        public void Skin(uint skinId)
        {
            if (skinId == 0 || src.Skins is null) return;
            var skin = dest.Skins!.GetItem<PS2AnySkin>(skinId);
            bool isNew = skin is null;
            if (isNew)
            {
                var srcSkin = src.Skins.GetItem<PS2AnySkin>(skinId);
                if (srcSkin is null) return;
                foreach (var ss in srcSkin.SubSkins) ss.CalculateData();
                skin = CloneItem(srcSkin);
                dest.Skins.AddItem(skin);
                skinIds.Add(skinId);
            }
            foreach (var ss in skin!.SubSkins) Material(ss.Material);
        }

        public void BlendSkin(uint blendId)
        {
            if (blendId == 0 || src.BlendSkins is null) return;
            var blend = dest.BlendSkins!.GetItem<PS2AnyBlendSkin>(blendId);
            bool isNew = blend is null;
            if (isNew)
            {
                var srcBlend = src.BlendSkins.GetItem<PS2AnyBlendSkin>(blendId);
                if (srcBlend is null) return;
                foreach (var sb in srcBlend.SubBlends)
                    foreach (var model in sb.Models)
                        model.CalculateData();
                blend = CloneItem(srcBlend);
                dest.BlendSkins.AddItem(blend);
                blendSkinIds.Add(blendId);
            }
            foreach (var sb in blend!.SubBlends) Material(sb.Material);
        }

        public bool Ogi(uint ogiId)
        {
            if (dest.Ogis!.ContainsItem(ogiId)) return true;
            if (src.Ogis is null) return false;
            var srcOgi = src.Ogis.GetItem<PS2AnyOGI>(ogiId);
            if (srcOgi is null) return false;

            var newOgi = CloneItem(srcOgi);
            dest.Ogis.AddItem(newOgi);
            ogiIds.Add(ogiId);

            foreach (var rmId in newOgi.RigidModelIds) RigidModel(rmId);
            if (newOgi.SkinID != 0)      Skin(newOgi.SkinID);
            if (newOgi.BlendSkinID != 0) BlendSkin(newOgi.BlendSkinID);

            tables.OGIs[ogiId] = newOgi;
            return true;
        }

        public bool Animation(uint animId)
        {
            if (dest.Animations!.ContainsItem(animId)) return true;
            if (src.Animations is null) return false;
            var srcAnim = src.Animations.GetItem<PS2AnyAnimation>(animId);
            if (srcAnim is null) return false;

            var newAnim = CloneItem(srcAnim);
            dest.Animations.AddItem(newAnim);
            animIds.Add(animId);

            tables.Animations[animId] = newAnim;
            return true;
        }
    }

    private sealed class SceneryGraphicsCopy
    {
        private readonly GL _gl;
        private readonly Dictionary<uint, Texture2D> _texCache;
        public readonly BaseTwinSection SrcRigidModels, SrcMeshes, SrcModels, SrcMaterials;
        public readonly PS2AnyTexturesSection SrcTextures;
        public readonly BaseTwinSection DestRigidModels, DestMeshes, DestModels, DestMaterials;
        public readonly PS2AnyTexturesSection DestTextures;
        public readonly BaseTwinSection? SrcLods, DestLods;
        public readonly Dictionary<uint, uint> TexIdMap = new(), MatIdMap = new(), ModelIdMap = new();
        public readonly List<uint> MissingTextureIds = new(), MissingMaterialIds = new(), MissingModelIds = new();

        private SceneryGraphicsCopy(GL gl, Dictionary<uint, Texture2D> texCache,
            BaseTwinSection srcRm, BaseTwinSection srcMesh, BaseTwinSection srcModel, BaseTwinSection srcMat, PS2AnyTexturesSection srcTex, BaseTwinSection? srcLod,
            BaseTwinSection destRm, BaseTwinSection destMesh, BaseTwinSection destModel, BaseTwinSection destMat, PS2AnyTexturesSection destTex, BaseTwinSection? destLod)
        {
            _gl = gl; _texCache = texCache;
            SrcRigidModels = srcRm; SrcMeshes = srcMesh; SrcModels = srcModel; SrcMaterials = srcMat; SrcTextures = srcTex; SrcLods = srcLod;
            DestRigidModels = destRm; DestMeshes = destMesh; DestModels = destModel; DestMaterials = destMat; DestTextures = destTex; DestLods = destLod;
        }

        public static SceneryGraphicsCopy? Between(GL gl, Dictionary<uint, Texture2D> texCache,
            PS2AnyGraphicsSection srcGfx, PS2AnyGraphicsSection destGfx, bool withLods)
        {
            var destRm    = EnsureGfxSubsection<PS2AnyRigidModelsSection>(destGfx, Constants.GRAPHICS_RIGID_MODELS_SECTION);
            var destMesh  = EnsureGfxSubsection<PS2AnyMeshesSection>(destGfx, Constants.GRAPHICS_MESHES_SECTION);
            var destModel = EnsureGfxSubsection<PS2AnyModelsSection>(destGfx, Constants.GRAPHICS_MODELS_SECTION);
            var destMat   = EnsureGfxSubsection<PS2AnyMaterialsSection>(destGfx, Constants.GRAPHICS_MATERIALS_SECTION);
            var destTex   = EnsureGfxSubsection<PS2AnyTexturesSection>(destGfx, Constants.GRAPHICS_TEXTURES_SECTION);
            var destLod   = withLods ? EnsureGfxSubsection<PS2AnyLODsSection>(destGfx, Constants.GRAPHICS_LODS_SECTION) : null;
            var srcRm     = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_RIGID_MODELS_SECTION);
            var srcMesh   = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MESHES_SECTION);
            var srcModel  = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MODELS_SECTION);
            var srcMat    = srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
            var srcTex    = srcGfx.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
            var srcLod    = withLods ? srcGfx.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_LODS_SECTION) : null;
            if (srcRm is null || srcMesh is null || srcModel is null || srcMat is null || srcTex is null) return null;
            return new SceneryGraphicsCopy(gl, texCache, srcRm, srcMesh, srcModel, srcMat, srcTex, srcLod,
                destRm, destMesh, destModel, destMat, destTex, destLod);
        }

        public uint Texture(uint srcTexId)
        {
            if (TexIdMap.TryGetValue(srcTexId, out var already)) return already;
            var srcTex = SrcTextures.GetItem<PS2AnyTexture>(srcTexId);
            if (srcTex is null) { MissingTextureIds.Add(srcTexId); TexIdMap[srcTexId] = srcTexId; return srcTexId; }
            var clone = CloneItem(srcTex);
            uint newId = NewGraphicsLocalId(DestTextures);
            clone.SetID(newId);
            DestTextures.AddItem(clone);
            TexIdMap[srcTexId] = newId;
            var t = ChunkImporter.DecodeTexture(_gl, clone);
            if (t is not null) _texCache[newId] = t;
            return newId;
        }

        public uint Material(uint srcMatId)
        {
            if (srcMatId == 0) return 0;
            if (MatIdMap.TryGetValue(srcMatId, out var already)) return already;
            var srcMat = SrcMaterials.GetItem<PS2AnyMaterial>(srcMatId);
            if (srcMat is null) { MissingMaterialIds.Add(srcMatId); return 0; }
            var clone = CloneItem(srcMat);
            uint newId = NewGraphicsLocalId(DestMaterials);
            clone.SetID(newId);
            foreach (var sh in clone.Shaders) sh.TextureId = Texture(sh.TextureId);
            DestMaterials.AddItem(clone);
            MatIdMap[srcMatId] = newId;
            return newId;
        }

        public uint Model(uint srcModelId)
        {
            if (ModelIdMap.TryGetValue(srcModelId, out var already)) return already;
            var srcModel = SrcModels.GetItem<PS2AnyModel>(srcModelId);
            if (srcModel is null) { MissingModelIds.Add(srcModelId); return 0; }
            var clone = CloneItem(srcModel);
            uint newId = NewGraphicsLocalId(DestModels);
            clone.SetID(newId);
            DestModels.AddItem(clone);
            ModelIdMap[srcModelId] = newId;
            return newId;
        }

        public uint? RigidModel(uint srcId)
        {
            BaseTwinSection? srcSec = null; BaseTwinSection? destSec = null;
            var rm = SrcMeshes.GetItem<PS2AnyRigidModel>(srcId);
            if (rm is not null) { srcSec = SrcMeshes; destSec = DestMeshes; }
            else
            {
                rm = SrcRigidModels.GetItem<PS2AnyRigidModel>(srcId);
                if (rm is not null) { srcSec = SrcRigidModels; destSec = DestRigidModels; }
            }
            if (rm is null || srcSec is null || destSec is null) return null;

            var clone = CloneItem(rm);
            uint newId = NewGraphicsLocalId(destSec, DestMeshes, DestRigidModels);
            clone.SetID(newId);
            clone.Model = Model(clone.Model);
            for (int i = 0; i < clone.Materials.Count; i++)
                clone.Materials[i] = Material(clone.Materials[i]);
            destSec.AddItem(clone);
            return newId;
        }
    }

    private static void RebuildMeshGpu(GL gl, PS2AnyGraphicsSection destGfx, RmTables destTables, Dictionary<uint, Texture2D> destTexCache)
    {
        var modelById2 = BuildModelLookup(destGfx);
        foreach (var kv in BuildMeshLookup(destGfx, modelById2, gl, destTexCache, destTables.GlobalSource)) destTables.ModelGpu[kv.Key] = kv.Value;
    }

    private static void RebuildTransplantGpu(GL gl, PS2AnyGraphicsSection destGfx, RmTables destTables, Dictionary<uint, Texture2D> destTexCache)
    {
        RebuildMeshGpu(gl, destGfx, destTables, destTexCache);
        foreach (var kv in BuildSkinLookup(destGfx, gl, destTexCache))             destTables.SkinGpu[kv.Key]  = kv.Value;
        foreach (var kv in BuildBlendSkinLookup(destGfx, gl, destTexCache, destTables.BlendSkinRaw))
            destTables.BlendSkinGpu[kv.Key] = kv.Value;
    }

    private static void RemoveUnusedTransplantGraphics(RmTables destTables, Dictionary<uint, Texture2D> destTexCache, Rm2Sections dest,
        List<uint> rigidModelIds, List<uint> skinIds, List<uint> blendSkinIds, List<uint> modelIds, List<uint> materialIds, List<uint> textureIds)
    {
        var remainingOgis = destTables.OGIs.Values.ToList();
        bool RmStillUsed(uint id)    => remainingOgis.Any(o => o.RigidModelIds.Contains(id));
        bool SkinStillUsed(uint id)  => remainingOgis.Any(o => o.SkinID == id);
        bool BlendStillUsed(uint id) => remainingOgis.Any(o => o.BlendSkinID == id);

        var keepMat = new HashSet<uint>();
        var keepModel = new HashSet<uint>();
        void KeepFromRigid(uint rmId)
        {
            var rm = dest.RigidModels?.GetItem<PS2AnyRigidModel>(rmId);
            if (rm is null) return;
            keepModel.Add(rm.Model);
            foreach (var m in rm.Materials) keepMat.Add(m);
        }
        foreach (var o in remainingOgis)
        {
            foreach (var rmId in o.RigidModelIds) KeepFromRigid(rmId);
            if (o.SkinID != 0 && dest.Skins?.GetItem<PS2AnySkin>(o.SkinID) is { } sk)
                foreach (var ss in sk.SubSkins) keepMat.Add(ss.Material);
            if (o.BlendSkinID != 0 && dest.BlendSkins?.GetItem<PS2AnyBlendSkin>(o.BlendSkinID) is { } bs)
                foreach (var sb in bs.SubBlends) keepMat.Add(sb.Material);
        }
        var keepTex = new HashSet<uint>();
        foreach (var matId in keepMat)
            if (dest.Materials?.GetItem<PS2AnyMaterial>(matId) is { } m)
                foreach (var sh in m.Shaders) keepTex.Add(sh.TextureId);

        foreach (var id in rigidModelIds) if (!RmStillUsed(id))    { dest.RigidModels?.RemoveItem<PS2AnyRigidModel>(id); destTables.ModelGpu.Remove(id); }
        foreach (var id in skinIds)       if (!SkinStillUsed(id))  { dest.Skins?.RemoveItem<PS2AnySkin>(id);     destTables.SkinGpu.Remove(id); }
        foreach (var id in blendSkinIds)  if (!BlendStillUsed(id)) { dest.BlendSkins?.RemoveItem<PS2AnyBlendSkin>(id); destTables.BlendSkinGpu.Remove(id); destTables.BlendSkinRaw.Remove(id); }
        foreach (var id in modelIds)      if (!keepModel.Contains(id)) dest.Models?.RemoveItem<PS2AnyModel>(id);
        foreach (var id in materialIds)   if (!keepMat.Contains(id))   dest.Materials?.RemoveItem<PS2AnyMaterial>(id);
        foreach (var id in textureIds)
            if (!keepTex.Contains(id))
            {
                dest.Textures?.RemoveItem<PS2AnyTexture>(id);
                if (destTexCache.Remove(id, out var tex)) tex.Dispose();
            }
    }
}
