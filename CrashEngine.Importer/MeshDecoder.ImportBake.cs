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
            bool ownTexture = raw.DiffusePixelsRGBA is not null && raw.TexWidth > 0 && raw.TexHeight > 0; // Amedo

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
                if (ownTexture) ChunkImporter.SaveHd(tex, raw.DiffusePixelsRGBA, raw.TexWidth, raw.TexHeight); // Amedo
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
            ApplyImportAlphaTest(mat.Shaders[0], raw.AlphaCutoff); // Amedo
            mat.Shaders[0].AlphaCorrectionValue = true; // Amedo
            mat.Shaders[0].XScrollSettings = TwinShader.XScrollFormula.Disabled;
            mat.Shaders[0].YScrollSettings = TwinShader.YScrollFormula.Disabled;
            mat.Shaders[0].UvScrollSpeed   = new TwinVec4(0f, 0f, 0f, 0f);
            mat.Shaders[0].Animation       = null;
            destMatSec.AddItem(mat);
            rec.MaterialIds.Add(newMatId); // Amedo 2026-09-20

            var sub = NewImportSubModel(); // Amedo 2026-10-02
            var subs = new List<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel> { sub };

            int triCount = raw.Indices.Length / 3;
            int inGroup = 0;
            for (int t = 0; t < triCount; t++)
            {
                if (MaxImportSubModelVerts < sub.Vertexes.Count + 3) // Amedo 2026-10-02
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
                    sub.Colors.Add(ImportVertexColor(raw, vi, 0.498f, 1f)); // Amedo
                    sub.EmitColor.Add(new TwinVec4(0.498f, 0.498f, 0.498f, 0.937f));
                    sub.Connection.Add(k == 2);
                    inGroup++;

                    minPos = SysVec3.Min(minPos, p); maxPos = SysVec3.Max(maxPos, p);
                }
            }
            if (inGroup > 0) sub.GroupSizes.Add(inGroup);
            sub.Compile();

            var model = new PS2AnyModel();
            model.SubModels.AddRange(subs); // Amedo 2026-10-02
            uint newModelId = NewGraphicsId(destModelSec);
            model.SetID(newModelId);
            destModelSec.AddItem(model);
            rec.ModelIds.Add(newModelId); // Amedo 2026-09-20

            var rm = new PS2AnyRigidModel { Model = newModelId };
            foreach (var _ in subs) rm.Materials.Add(newMatId); // Amedo 2026-10-02
            uint newRmId = NewGraphicsId(destRmSec, destMeshSec);
            rm.SetID(newRmId);
            destRmSec.AddItem(rm);
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
        foreach (var kv in BuildMeshLookup(destGfx, modelById2, gl, destTexCache, destTables.GlobalSource)) destTables.ModelGpu[kv.Key] = kv.Value; // Amedo

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
            bool ownTexture = raw.DiffusePixelsRGBA is not null && raw.TexWidth > 0 && raw.TexHeight > 0; // Amedo

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
                if (ownTexture) ChunkImporter.SaveHd(tex, raw.DiffusePixelsRGBA, raw.TexWidth, raw.TexHeight); // Amedo
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
            mat.Shaders[0].AlphaCorrectionValue = true; // Amedo
            mat.Shaders[0].XScrollSettings = TwinShader.XScrollFormula.Disabled;
            mat.Shaders[0].YScrollSettings = TwinShader.YScrollFormula.Disabled;
            mat.Shaders[0].UvScrollSpeed   = new TwinVec4(0f, 0f, 0f, 0f);
            mat.Shaders[0].Animation       = null;
            mat.Shaders[0].ShaderType = (raw.CeUnlit ?? true)
                ? TwinShader.Type.StandardUnlit : TwinShader.Type.StandardLit;
            if (raw.CeAlphaBlend.HasValue)
                mat.Shaders[0].ABlending = raw.CeAlphaBlend.Value ? TwinShader.AlphaBlending.ON : TwinShader.AlphaBlending.OFF;
            ApplyImportAlphaTest(mat.Shaders[0], raw.AlphaCutoff); // Amedo
            matSec.AddItem(mat);
            rec.MaterialIds.Add(newMatId); // Amedo 2026-09-20

            var sub = NewImportSubModel(); // Amedo 2026-10-02
            var subs = new List<Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems.PS2SubModel> { sub };

            var subMin = new SysVec3(float.MaxValue); var subMax = new SysVec3(float.MinValue);
            int triCount = raw.Indices.Length / 3;
            int inGroup = 0;
            for (int t = 0; t < triCount; t++)
            {
                if (MaxImportSubModelVerts < sub.Vertexes.Count + 3) // Amedo 2026-10-02
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
                    sub.Colors.Add(ImportVertexColor(raw, vi, 1f, 1f)); // Amedo
                    sub.EmitColor.Add(new TwinVec4(1f, 1f, 1f, 0.937f));
                    sub.Connection.Add(k == 2);
                    inGroup++;

                    subMin = SysVec3.Min(subMin, p); subMax = SysVec3.Max(subMax, p);
                }
            }
            if (inGroup > 0) sub.GroupSizes.Add(inGroup);
            sub.Compile();

            model.SubModels.AddRange(subs); // Amedo 2026-10-02
            foreach (var _ in subs) rm.Materials.Add(newMatId);
            combMin = SysVec3.Min(combMin, subMin); combMax = SysVec3.Max(combMax, subMax);
            rec.SubmeshCount++;
            rec.VertexCount += subs.Sum(x => x.Vertexes.Count); // Amedo 2026-10-02
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
                    if (GameMath.LengthSquared(sn) > 1e-12f) sn = SysVec3.Normalize(sn);
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
        foreach (var kv in BuildMeshLookup(destGfx, modelById2, gl, destTexCache, destTables.GlobalSource)) destTables.ModelGpu[kv.Key] = kv.Value; // Amedo

        log = $"reused real scenery mesh 0x{meshId:X} as new object 0x{newObjId:X4} (OGI 0x{newOgiId:X4}, " +
              $"+1 Model, +{matIdMap.Count} Material, +{rec.TextureCount} Texture) — same compiled geometry, " +
              "not re-baked.";
        return rec;
    }
}
