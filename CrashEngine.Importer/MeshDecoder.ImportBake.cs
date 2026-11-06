using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using SysVec3 = System.Numerics.Vector3;
using SysVec4 = System.Numerics.Vector4;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;

namespace CrashEngine.Importer;

public static partial class MeshDecoder
{
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

    // Amedo
    private static TwinVec4 ImportVertexColor(CrashEngine.Assets.RawSubmesh raw, uint vi, float neutral, float alpha)
    {
        if (raw.Colors is not { } c || vi >= c.Length) return new TwinVec4(neutral, neutral, neutral, alpha);
        var v = SysVec4.Clamp(c[vi], SysVec4.Zero, SysVec4.One);
        return new TwinVec4(neutral * v.X, neutral * v.Y, neutral * v.Z, alpha * v.W);
    }

    // Amedo
    private static void ApplyImportAlphaTest(TwinShader sh, float? cutoff)
    {
        if (cutoff is not { } cut) return;
        sh.ATest = TwinShader.AlphaTest.ON;
        sh.ATestMethod = TwinShader.AlphaTestMethod.GEQUAL;
        sh.AlphaValueToBeComparedTo = (byte)Math.Clamp((int)MathF.Round(cut * 128f), 1, 128);
        sh.ProcessMethodWhenAlphaTestFailed = TwinShader.ProcessAfterAlphaTestFailed.KEEP;
        sh.ABlending = TwinShader.AlphaBlending.OFF;
    }

    // Amedo 2026-10-02
    private const int MaxImportSubModelVerts = 9000;

    private static Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel NewImportSubModel() => new()
    {
        Vertexes = new List<TwinVec4>(), UVW = new List<TwinVec4>(), Colors = new List<TwinVec4>(),
        Normals = new List<TwinVec4>(), EmitColor = new List<TwinVec4>(),
        Connection = new List<bool>(), GroupSizes = new List<int>(), UnusedBlob = Array.Empty<byte>(),
    };

    private const uint NitroCrateReferenceMaterialId = 0xEE85EFBC;

    private static PS2AnyMaterial? FindSimpleMaterialTemplate(BaseTwinSection sec)
    {
        for (int i = 0; i < sec.GetItemsAmount(); i++)
            if (sec.GetItem(i) is PS2AnyMaterial cand && cand.Shaders.Count == 1 &&
                cand.Shaders[0].ShaderType is TwinShader.Type.StandardLit or TwinShader.Type.StandardUnlit)
                return cand;
        return null;
    }

    private static PS2AnyMaterial? ImportMaterialTemplate(BaseTwinSection? globalMatSec, BaseTwinSection ownMatSec) =>
        (globalMatSec?.GetItem<PS2AnyMaterial>(NitroCrateReferenceMaterialId)) ??
        (globalMatSec is not null ? FindSimpleMaterialTemplate(globalMatSec) : null) ??
        FindSimpleMaterialTemplate(ownMatSec);

    private static PS2AnyOGI? FindSimpleStaticOgiTemplate(BaseTwinSection sec)
    {
        for (int i = 0; i < sec.GetItemsAmount(); i++)
            if (sec.GetItem(i) is PS2AnyOGI cand && cand.SkinID == 0 && cand.BlendSkinID == 0)
                return cand;
        return null;
    }

    private static PS2AnyOGI? ImportOgiTemplate(BaseTwinSection? globalOgiSec, BaseTwinSection ownOgiSec) =>
        (globalOgiSec is not null ? FindSimpleStaticOgiTemplate(globalOgiSec) : null) ?? FindSimpleStaticOgiTemplate(ownOgiSec);

    private static (uint Id, Texture2D? Gpu) BakeImportTexture(GL gl, CrashEngine.Assets.RawSubmesh raw,
        PS2AnyTexturesSection texSec, Dictionary<uint, Texture2D> texCache)
    {
        bool ownTexture = raw.DiffusePixelsRGBA is not null && raw.TexWidth > 0 && raw.TexHeight > 0;
        if (!ownTexture)
        {
            const int whiteSize = 128;
            var white = new byte[whiteSize * whiteSize * 4];
            Array.Fill(white, (byte)255);
            raw.DiffusePixelsRGBA = white; raw.TexWidth = whiteSize; raw.TexHeight = whiteSize;
        }

        var tex = new PS2AnyTexture();
        uint texId = NewGraphicsLocalId(texSec);
        tex.SetID(texId);
        int tw = raw.TexWidth, th = raw.TexHeight;
        { int p = 1; while (p < Math.Min(tw, 256)) p <<= 1; tw = Math.Clamp(p, 8, 256); }
        { int p = 1; while (p < Math.Min(th, 256)) p <<= 1; th = Math.Clamp(p, 8, 256); }
        byte[] pixels = ResampleRGBA(raw.DiffusePixelsRGBA!, raw.TexWidth, raw.TexHeight, tw, th);
        var colors = new List<Twinsanity.TwinsanityInterchange.Common.Color>(tw * th);
        for (int p = 0; p < tw * th; p++)
            colors.Add(new Twinsanity.TwinsanityInterchange.Common.Color(
                pixels[p * 4], pixels[p * 4 + 1], pixels[p * 4 + 2], pixels[p * 4 + 3]));
        tex.FromBitmap(colors, tw,
            Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture.TextureFunction.MODULATE,
            Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinTexture.TexturePixelFormat.PSMCT32);
        texSec.AddItem(tex);
        if (ownTexture) ChunkImporter.SaveHd(tex, raw.DiffusePixelsRGBA!, raw.TexWidth, raw.TexHeight);
        var t2d = ChunkImporter.DecodeTexture(gl, tex);
        if (t2d is not null) texCache[texId] = t2d;
        return (texId, t2d);
    }

    private static uint BakeImportMaterial(PS2AnyMaterial template, BaseTwinSection matSec, CrashEngine.Assets.RawSubmesh raw,
        uint texId, TwinShader.Type? shaderType)
    {
        var mat = CloneItem(template);
        uint matId = NewGraphicsLocalId(matSec);
        mat.SetID(matId);
        var sh = mat.Shaders[0];
        sh.TextureId = texId;
        if (raw.DiffusePixelsRGBA is not null)
            sh.TxtMapping = TwinShader.TextureMapping.ON;
        if (shaderType is { } type)
            sh.ShaderType = type;
        if (raw.CeAlphaBlend.HasValue)
            sh.ABlending = raw.CeAlphaBlend.Value ? TwinShader.AlphaBlending.ON : TwinShader.AlphaBlending.OFF;
        ApplyImportAlphaTest(sh, raw.AlphaCutoff);
        sh.AlphaCorrectionValue = true;
        sh.XScrollSettings = TwinShader.XScrollFormula.Disabled;
        sh.YScrollSettings = TwinShader.YScrollFormula.Disabled;
        sh.UvScrollSpeed   = new TwinVec4(0f, 0f, 0f, 0f);
        sh.Animation       = null;
        matSec.AddItem(mat);
        return matId;
    }

    private static List<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel> BuildImportSubModels(
        CrashEngine.Assets.RawSubmesh raw, float neutral, ref SysVec3 min, ref SysVec3 max)
    {
        var sub = NewImportSubModel();
        var subs = new List<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel> { sub };

        int triCount = raw.Indices.Length / 3;
        int inGroup = 0;
        for (int t = 0; t < triCount; t++)
        {
            if (MaxImportSubModelVerts < sub.Vertexes.Count + 3)
            {
                if (inGroup > 0) sub.GroupSizes.Add(inGroup);
                sub.Compile();
                subs.Add(sub = NewImportSubModel());
                inGroup = 0;
            }
            if (inGroup + 3 > 36) { sub.GroupSizes.Add(inGroup); inGroup = 0; }
            for (int k = 0; k < 3; k++)
            {
                uint vi = raw.Indices[t * 3 + k];
                if (vi >= raw.Positions.Length) { vi = 0; }
                var p = raw.Positions[vi]; var n = raw.Normals[vi]; var uv = raw.UVs[vi];
                sub.Vertexes.Add(new TwinVec4(p.X, p.Y, p.Z, 1f));
                sub.Normals.Add(new TwinVec4(n.X, n.Y, n.Z, 1f));
                sub.UVW.Add(new TwinVec4(uv.X, uv.Y, 1f, 1f));
                sub.Colors.Add(ImportVertexColor(raw, vi, neutral, 1f));
                sub.EmitColor.Add(new TwinVec4(neutral, neutral, neutral, 0.937f));
                sub.Connection.Add(k == 2);
                inGroup++;

                min = SysVec3.Min(min, p); max = SysVec3.Max(max, p);
            }
        }
        if (inGroup > 0) sub.GroupSizes.Add(inGroup);
        sub.Compile();
        return subs;
    }

    private static uint AddImportedObject(RmTables tables, BaseTwinSection ogiSec, BaseTwinSection objSec,
        BaseTwinSection? globalOgiSec, BaseTwinSection? globalObjSec, PS2AnyOGI ogi, string namePrefix, out uint ogiId)
    {
        ogiId = 0;
        while (ogiSec.ContainsItem(ogiId) || (globalOgiSec?.ContainsItem(ogiId) ?? false)) ogiId++;
        ogi.SetID(ogiId);
        ogiSec.AddItem(ogi);
        tables.OGIs[ogiId] = ogi;

        uint objId = 0;
        while (objSec.ContainsItem(objId) || (globalObjSec?.ContainsItem(objId) ?? false)) objId++;
        var obj = new PS2AnyObject
        {
            Type = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject.ObjectType.GenericObject,
            Name = $"{namePrefix}_{objId:X4}",
            BehaviourPack = new Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourCommandPack(),
        };
        obj.OGISlots.Add((ushort)ogiId);
        obj.AnimationSlots.Add(0xFFFF);
        obj.SetID(objId);
        objSec.AddItem(obj);
        tables.Objects[objId] = obj;
        return objId;
    }

    public static ExternalModelBakeRecord? BakeExternalModelAsObject(GL gl,
        PS2AnyTwinsanityRM2 destRm2, object destTablesHandle, Dictionary<uint, Texture2D> destTexCache,
        PS2AnyTwinsanityRM2? globalRm2, List<CrashEngine.Assets.RawSubmesh> submeshes, string objectName, out string log)
    {
        if (destTablesHandle is not RmTables destTables) { log = "destination chunk has no mesh tables"; return null; }
        if (submeshes.Count == 0) { log = "no meshes found in the imported file"; return null; }

        var dest = Rm2Sections.Of(destRm2);
        if (dest.Objects is null || dest.Ogis is null || dest.Gfx is null || dest.RigidModels is null ||
            dest.Models is null || dest.Materials is null || dest.Textures is null)
        { log = "destination level is missing a required CODE/GRAPHICS section"; return null; }

        var global = globalRm2 is null ? default : Rm2Sections.Of(globalRm2);

        var matTemplate = ImportMaterialTemplate(global.Materials, dest.Materials);
        if (matTemplate is null)
        { log = "could not find a simple single-shader Material to use as a structural template (in the global material set or this level's own)"; return null; }

        var ogiTemplate = ImportOgiTemplate(global.Ogis, dest.Ogis);
        if (ogiTemplate is null)
        { log = "could not find a simple non-skinned OGI to use as a structural template (in the global object set or this level's own)"; return null; }

        var rec = new ExternalModelBakeRecord();
        var rigidModelIds = new List<uint>();
        var minPos = new SysVec3(float.MaxValue); var maxPos = new SysVec3(float.MinValue);

        foreach (var raw in submeshes)
        {
            if (raw.Indices.Length < 3 || raw.Positions.Length == 0) continue;

            var (texId, _) = BakeImportTexture(gl, raw, dest.Textures, destTexCache);
            rec.TextureCount++;
            rec.TextureIds.Add(texId);

            TwinShader.Type? shaderType = raw.CeUnlit.HasValue
                ? (raw.CeUnlit.Value ? TwinShader.Type.StandardUnlit : TwinShader.Type.StandardLit) : null;
            uint newMatId = BakeImportMaterial(matTemplate, dest.Materials, raw, texId, shaderType);
            rec.MaterialIds.Add(newMatId);

            var subs = BuildImportSubModels(raw, 0.498f, ref minPos, ref maxPos);

            var model = new PS2AnyModel();
            model.SubModels.AddRange(subs);
            uint newModelId = NewGraphicsLocalId(dest.Models);
            model.SetID(newModelId);
            dest.Models.AddItem(model);
            rec.ModelIds.Add(newModelId);

            var rm = new PS2AnyRigidModel { Model = newModelId };
            foreach (var _ in subs) rm.Materials.Add(newMatId);
            uint newRmId = NewGraphicsLocalId(dest.RigidModels, dest.Meshes);
            rm.SetID(newRmId);
            dest.RigidModels.AddItem(rm);
            destTables.RigidModels[newRmId] = rm;
            rigidModelIds.Add(newRmId);
            rec.RigidModelIds.Add(newRmId); // Amedo 2026-09-20

            rec.SubmeshCount++;
            rec.VertexCount += subs.Sum(x => x.Vertexes.Count); // Amedo 2026-10-02
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

        uint newObjId = AddImportedObject(destTables, dest.Ogis, dest.Objects, global.Ogis, global.Objects, ogi,
            $"CE_ImportedModel_{objectName}", out uint newOgiId);
        rec.ObjectId = newObjId;
        rec.OgiIds.Add(newOgiId);

        RebuildMeshGpu(gl, dest.Gfx, destTables, destTexCache);

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

        var globalGfx    = globalRm2?.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var globalMatSec = globalGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var matTemplate = ImportMaterialTemplate(globalMatSec, matSec);
        if (matTemplate is null)
        { log = "could not find a simple single-shader Material to use as a structural template"; return null; }

        var rec = new SceneryBakeRecord();
        var combMin = new SysVec3(float.MaxValue); var combMax = new SysVec3(float.MinValue);

        var model = new PS2AnyModel();
        var rm    = new PS2AnyRigidModel();

        foreach (var raw in submeshes)
        {
            if (raw.Indices.Length < 3 || raw.Positions.Length == 0) continue;

            var (texId, _) = BakeImportTexture(gl, raw, texSec, destSceneryTexCache);
            rec.TextureCount++;
            rec.TextureIds.Add(texId);

            uint newMatId = BakeImportMaterial(matTemplate, matSec, raw, texId,
                (raw.CeUnlit ?? true) ? TwinShader.Type.StandardUnlit : TwinShader.Type.StandardLit);
            rec.MaterialIds.Add(newMatId);

            var subMin = new SysVec3(float.MaxValue); var subMax = new SysVec3(float.MinValue);
            var subs = BuildImportSubModels(raw, 1f, ref subMin, ref subMax);

            model.SubModels.AddRange(subs);
            foreach (var _ in subs) rm.Materials.Add(newMatId);
            combMin = SysVec3.Min(combMin, subMin); combMax = SysVec3.Max(combMax, subMax);
            rec.SubmeshCount++;
            rec.VertexCount += subs.Sum(x => x.Vertexes.Count); // Amedo 2026-10-02
        }

        if (rec.SubmeshCount == 0) { log = "no usable triangles found in the imported file"; return null; }

        uint newModelId = NewGraphicsLocalId(modelSec);
        model.SetID(newModelId);
        modelSec.AddItem(model);
        rec.ModelIds.Add(newModelId); // Amedo 2026-09-20

        rm.Model = newModelId;
        uint newMeshId = NewGraphicsLocalId(meshSec);
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

        var dest = Rm2Sections.Of(destRm2);
        var destRmSec    = dest.RigidModels;
        var destModelSec = dest.Models;
        var destMatSec   = dest.Materials;
        var destTexSec   = dest.Textures;
        if (dest.Objects is null || dest.Ogis is null || dest.Gfx is null || destRmSec is null ||
            destModelSec is null || destMatSec is null || destTexSec is null)
        { log = "destination level is missing a required CODE/GRAPHICS section"; return null; }

        var global = globalRm2 is null ? default : Rm2Sections.Of(globalRm2);

        var ogiTemplate = ImportOgiTemplate(global.Ogis, dest.Ogis);
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
            uint newId = NewGraphicsLocalId(destTexSec);
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
            uint newId = NewGraphicsLocalId(destMatSec);
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
                    if (GameMath.LengthSquared(sn) > 1e-12f) sn = SysVec3.Normalize(sn);
                    sub.Normals[ni] = new TwinVec4(sn.X, sn.Y, sn.Z, n.W);
                }
                sub.Compile();
            }
        }

        uint newModelId = NewGraphicsLocalId(destModelSec);
        modelClone.SetID(newModelId);
        destModelSec.AddItem(modelClone);
        destTables.Models[newModelId] = modelClone;

        var rmClone = CloneItem(srcRm);
        rmClone.Model = newModelId;
        for (int i = 0; i < rmClone.Materials.Count; i++)
            rmClone.Materials[i] = CloneMaterial(rmClone.Materials[i]);
        uint newRmId = NewGraphicsLocalId(destRmSec, dest.Meshes);
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

        uint newObjId = AddImportedObject(destTables, dest.Ogis, dest.Objects, global.Ogis, global.Objects, ogi,
            $"CE_ConvertedScenery_{objectName}", out uint newOgiId);
        rec.ObjectId = newObjId;

        RebuildMeshGpu(gl, dest.Gfx, destTables, destTexCache);

        log = $"reused real scenery mesh 0x{meshId:X} as new object 0x{newObjId:X4} (OGI 0x{newOgiId:X4}, " +
              $"+1 Model, +{matIdMap.Count} Material, +{rec.TextureCount} Texture) — same compiled geometry, " +
              "not re-baked.";
        return rec;
    }
}
