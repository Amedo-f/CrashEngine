using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using System.Numerics;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;
using SysVec3 = System.Numerics.Vector3;
using TwinMat4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace CrashEngine.Importer;

// Amedo 2026-10-05
public static partial class MeshDecoder
{
    public const int SkinGroupVerts = 36;
    public const int SkinGroupJoints = 16;
    public const int SkinSubVerts = 3600;
    public static readonly TwinVec4 SkinVertexColor = new(0.498f, 0.498f, 0.498f, 0.467f);

    public static TransplantRecord? BakeExternalCharacterAsObject(GL gl,
        PS2AnyTwinsanityRM2 destRm2, object destTablesHandle, Dictionary<uint, Texture2D> destTexCache,
        PS2AnyTwinsanityRM2? globalRm2, IReadOnlyList<CrashEngine.Assets.RawSkinnedSubmesh> meshes,
        List<TwinJoint> joints, IReadOnlyList<PS2AnyAnimation> animations, string objectName, out string log)
    {
        if (destTablesHandle is not RmTables destTables) { log = "destination chunk has no mesh tables"; return null; }
        if (meshes.Count == 0 || joints.Count == 0) { log = "no skinned mesh or no bones"; return null; }

        var destCode     = destRm2.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var destObjSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var destOgiSec   = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var destAnimSec  = destCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_ANIMATIONS_SECTION);
        var destGfx      = destRm2.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var destSkinSec  = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION);
        var destMatSec   = destGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var destTexSec   = destGfx?.GetItem<PS2AnyTexturesSection>((uint)Constants.GRAPHICS_TEXTURES_SECTION);
        if (destObjSec is null || destOgiSec is null || destAnimSec is null || destSkinSec is null || destMatSec is null || destTexSec is null)
        { log = "destination level is missing a required CODE/GRAPHICS section"; return null; }

        var globalCode   = globalRm2?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION);
        var globalOgiSec = globalCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_OGIS_SECTION);
        var globalObjSec = globalCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_GAME_OBJECTS_SECTION);
        var globalAnimSec = globalCode?.GetItem<BaseTwinSection>((uint)Constants.CODE_ANIMATIONS_SECTION);
        var globalGfx    = globalRm2?.GetItem<PS2AnyGraphicsSection>((uint)Constants.LEVEL_GRAPHICS_SECTION);
        var globalMatSec = globalGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION);
        var globalSkinSec = globalGfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_SKINS_SECTION);

        static PS2AnyMaterial? SkinMaterialTemplate(BaseTwinSection? skins, BaseTwinSection? mats)
        {
            if (skins is null || mats is null) return null;
            for (int i = 0; i < skins.GetItemsAmount(); i++)
                if (skins.GetItem(i) is PS2AnySkin sk)
                    foreach (var ss in sk.SubSkins)
                        if (mats.GetItem<PS2AnyMaterial>(ss.Material) is { Shaders.Count: 1 } m && m.Shaders[0].ShaderType == TwinShader.Type.LitSkinnedModel)
                            return m;
            return null;
        }
        var matTemplate = SkinMaterialTemplate(globalSkinSec, globalMatSec) ?? SkinMaterialTemplate(destSkinSec, destMatSec);
        if (matTemplate is null) { log = "could not find a single-shader skinned Material to use as a template"; return null; }

        static PS2AnyOGI? SkinnedOgiTemplate(BaseTwinSection? sec)
        {
            for (int i = 0; sec is not null && i < sec.GetItemsAmount(); i++)
                if (sec.GetItem(i) is PS2AnyOGI o && o.SkinID != 0 && o.Joints.Count > 1) return o;
            return null;
        }
        var ogiTemplate = SkinnedOgiTemplate(globalOgiSec) ?? SkinnedOgiTemplate(destOgiSec);
        if (ogiTemplate is null) { log = "could not find a skinned OGI to use as a template"; return null; }

        var rec = new TransplantRecord { ObjectWasNew = true };
        var skin = new PS2AnySkin();
        var subMats = new List<(Texture2D?, TwinShader?)>();
        var minPos = new SysVec3(float.MaxValue); var maxPos = new SysVec3(float.MinValue);
        int vertexCount = 0, groupCount = 0;

        foreach (var sm in meshes)
        {
            var raw = sm.Mesh;
            if (raw.Indices.Length < 3 || raw.Positions.Length == 0) continue;

            var (texId, t2d) = BakeImportTexture(gl, raw, destTexSec, destTexCache);
            rec.TextureIds.Add(texId);

            var mat = CloneItem(matTemplate);
            uint matId = NewGraphicsLocalId(destMatSec);
            mat.SetID(matId);
            mat.Shaders[0].TextureId = texId;
            mat.Shaders[0].TxtMapping = TwinShader.TextureMapping.ON;
            if (raw.CeAlphaBlend.HasValue) // Amedo
                mat.Shaders[0].ABlending = raw.CeAlphaBlend.Value ? TwinShader.AlphaBlending.ON : TwinShader.AlphaBlending.OFF;
            ApplyImportAlphaTest(mat.Shaders[0], raw.AlphaCutoff); // Amedo
            destMatSec.AddItem(mat);
            rec.MaterialIds.Add(matId);
            RegisterUpperLayers(mat, destTexCache);

            PS2SubSkin NewSub() => new()
            {
                Material = matId, Vertexes = new List<TwinVec4>(), UVW = new List<TwinVec4>(), Colors = new List<TwinVec4>(),
                SkinJoints = new List<VertexJointInfo>(), GroupSizes = new List<int>(),
            };
            var sub = NewSub();
            var groupJoints = new HashSet<int>();
            int inGroup = 0;

            void CloseGroup()
            {
                if (inGroup > 0) { sub.GroupSizes.Add(inGroup); groupCount++; }
                inGroup = 0;
                groupJoints.Clear();
            }
            void CloseSub()
            {
                CloseGroup();
                if (sub.Vertexes.Count == 0) return;
                sub.PositionScale = BitConverter.SingleToUInt32Bits(SkinPositionScale(sub.Vertexes));
                sub.Compile();
                skin.SubSkins.Add(sub);
                subMats.Add((t2d, mat.Shaders[0]));
                sub = NewSub();
            }
            void AddVertex(uint vi, bool draws)
            {
                var p = raw.Positions[vi]; var n = raw.Normals[vi]; var uv = raw.UVs[vi];
                sub.Vertexes.Add(new TwinVec4(p.X, p.Y, p.Z, n.X));
                sub.UVW.Add(new TwinVec4(uv.X, uv.Y, n.Y, n.Z));
                sub.Colors.Add(ImportVertexColor(raw, vi, SkinVertexColor.X, SkinVertexColor.W)); // Amedo
                sub.SkinJoints.Add(JointInfo(sm, (int)vi, draws));
                minPos = SysVec3.Min(minPos, p); maxPos = SysVec3.Max(maxPos, p);
                inGroup++;
                vertexCount++;
            }
            IEnumerable<int> JointsOf(uint vi)
            {
                for (int k = 0; k < CrashEngine.Assets.CharacterImporter.WeightsPerVertex; k++)
                    if (sm.Weight[vi * CrashEngine.Assets.CharacterImporter.WeightsPerVertex + k] > 0f)
                        yield return sm.Joint[vi * CrashEngine.Assets.CharacterImporter.WeightsPerVertex + k];
            }

            int triCount = raw.Indices.Length / 3;
            for (int t = 0; t < triCount; t += 2)
            {
                int pairTris = Math.Min(2, triCount - t);
                var pairJoints = new HashSet<int>();
                for (int k = 0; k < pairTris * 3; k++) foreach (var j in JointsOf(raw.Indices[t * 3 + k])) pairJoints.Add(j);
                if (SkinSubVerts < sub.Vertexes.Count + pairTris * 3) CloseSub();
                if (SkinGroupVerts < inGroup + pairTris * 3 || SkinGroupJoints < groupJoints.Union(pairJoints).Count()) CloseGroup();
                groupJoints.UnionWith(pairJoints);
                for (int q = 0; q < pairTris; q++)
                {
                    uint a = raw.Indices[(t + q) * 3], b = raw.Indices[(t + q) * 3 + 1], c = raw.Indices[(t + q) * 3 + 2];
                    bool reversed = (sub.Vertexes.Count / 3) % 2 == 1;
                    if (reversed) (a, b) = (b, a);
                    AddVertex(a, false);
                    AddVertex(b, false);
                    AddVertex(c, true);
                }
            }
            CloseSub();
        }
        if (skin.SubSkins.Count == 0) { log = "no usable triangles found in the imported file"; return null; }

        uint skinId = NewGraphicsLocalId(destSkinSec);
        skin.SetID(skinId);
        destSkinSec.AddItem(skin);
        rec.SkinIds.Add(skinId);
        destTables.SkinGpu[skinId] = DecodeSkin(gl, skin, subMats, recalculate: false);

        var ogi = CloneItem(ogiTemplate);
        ogi.Joints = joints;
        ogi.ExitPoints = new List<TwinExitPoint>();
        ogi.RigidModelIds = new List<uint>();
        ogi.JointIndices = new List<byte>();
        ogi.SkinID = skinId;
        ogi.BlendSkinID = 0;
        ogi.Collisions.Clear();
        ogi.CollisionJointIndices.Clear();
        ogi.BoundingBox[0] = new TwinVec4(minPos.X, minPos.Y, minPos.Z, 1f);
        ogi.BoundingBox[1] = new TwinVec4(maxPos.X, maxPos.Y, maxPos.Z, 1f);
        var rest = ComputeJointWorlds(ogi);
        ogi.SkinInverseBindMatrices = new List<TwinMat4>();
        foreach (var j in joints)
            ogi.SkinInverseBindMatrices.Add(ToTwinMat4(Matrix4x4.Invert(rest[j.Index], out var inv) ? inv : Matrix4x4.Identity));
        uint ogiId = 0;
        while (destOgiSec.ContainsItem(ogiId) || (globalOgiSec?.ContainsItem(ogiId) ?? false)) ogiId++;
        ogi.SetID(ogiId);
        destOgiSec.AddItem(ogi);
        destTables.OGIs[ogiId] = ogi;
        rec.OgiIds.Add(ogiId);

        var animIds = new List<ushort>();
        uint nextAnim = 0;
        foreach (var anim in animations)
        {
            while (destAnimSec.ContainsItem(nextAnim) || (globalAnimSec?.ContainsItem(nextAnim) ?? false)) nextAnim++;
            anim.SetID(nextAnim);
            destAnimSec.AddItem(anim);
            destTables.Animations[nextAnim] = anim;
            rec.AnimIds.Add(nextAnim);
            animIds.Add((ushort)nextAnim);
            nextAnim++;
        }

        uint objId = 0;
        while (destObjSec.ContainsItem(objId) || (globalObjSec?.ContainsItem(objId) ?? false)) objId++;
        var obj = new PS2AnyObject
        {
            Type = Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.ITwinObject.ObjectType.GenericObject,
            Name = $"CE_ImportedCharacter_{objectName}_{objId:X4}",
            BehaviourPack = new Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab.PS2BehaviourCommandPack(),
        };
        if (animIds.Count == 0) { obj.OGISlots.Add((ushort)ogiId); obj.AnimationSlots.Add(0xFFFF); }
        foreach (var a in animIds) { obj.OGISlots.Add((ushort)ogiId); obj.AnimationSlots.Add(a); }
        obj.SetID(objId);
        destObjSec.AddItem(obj);
        destTables.Objects[objId] = obj;
        rec.ObjectId = objId;

        log = $"baked a skinned character as new object 0x{objId:X4}: {joints.Count} bone(s), {vertexCount} vert(s) in {skin.SubSkins.Count} sub-skin(s) / {groupCount} group(s), " +
              $"{animIds.Count} animation(s) (OGI 0x{ogiId:X4}, skin 0x{skinId:X8}, material template 0x{matTemplate.GetID():X8}, OGI template 0x{ogiTemplate.GetID():X4})";
        return rec;
    }

    public const float SkinRawLimit = 32000f;

    public static float SkinPositionScale(IEnumerable<TwinVec4> vertexes)
    {
        float max = 1f;
        foreach (var v in vertexes) max = MathF.Max(max, MathF.Max(MathF.Max(MathF.Abs(v.X), MathF.Abs(v.Y)), MathF.Max(MathF.Abs(v.Z), MathF.Abs(v.W))));
        return max / SkinRawLimit;
    }

    private static VertexJointInfo JointInfo(CrashEngine.Assets.RawSkinnedSubmesh sm, int vi, bool draws)
    {
        const int n = CrashEngine.Assets.CharacterImporter.WeightsPerVertex;
        float w1 = sm.Weight[vi * n], w2 = sm.Weight[vi * n + 1], w3 = sm.Weight[vi * n + 2];
        if (w2 <= 0f) { w1 = 1f; w2 = 0f; w3 = 0f; }
        else if (w3 <= 0f) { w2 = 1f - w1; w3 = 0f; }
        else
        {
            w3 = 1f - w1 - w2;
            while (w1 + w2 + w3 > 1f) w3 = MathF.BitDecrement(w3);
        }
        return new VertexJointInfo
        {
            JointIndex1 = sm.Joint[vi * n], JointIndex2 = w2 > 0f ? sm.Joint[vi * n + 1] : 0, JointIndex3 = w3 > 0f ? sm.Joint[vi * n + 2] : 0,
            Weight1 = w1, Weight2 = w2, Weight3 = w3, Connection = draws,
        };
    }
}
