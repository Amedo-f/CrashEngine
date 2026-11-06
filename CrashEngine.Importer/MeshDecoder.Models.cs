using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using SysVec2 = System.Numerics.Vector2;
using SysVec3 = System.Numerics.Vector3;
using SysVec4 = System.Numerics.Vector4;
using TwinVec4 = Twinsanity.TwinsanityInterchange.Common.Vector4;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace CrashEngine.Importer;

public static partial class MeshDecoder
{
    public static List<(GpuMesh, Material)> DecodeModel(GL gl,
                                                         ITwinModel model,
                                                         IReadOnlyList<(Texture2D? Tex, TwinShader? Shader)>? subMats = null)
    {
        var result = new List<(GpuMesh, Material)>();
        var parts = new List<(GpuMesh.Vertex[] Verts, uint[] Idx, Material Mat)>(); // Amedo

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
            if (triVerts.Length < 3) continue;

            var idxArray  = Enumerable.Range(0, triVerts.Length).Select(i => (uint)i).ToArray();

            var localCenter = triVerts.Aggregate(SysVec3.Zero, (a, v) => a + v.Position) / triVerts.Length;

            var mat = BuildTwinMaterial(tex, shader, sub);
            mat.LocalCenter    = localCenter;
            mat.BoundingRadius = triVerts.Max(v => SysVec3.Distance(v.Position, localCenter));
            parts.Add((triVerts, idxArray, mat)); // Amedo
        }

        SmoothTogether(gl, parts, result); // Amedo
        return result;
    }

    // Amedo
    private static void SmoothTogether(GL gl, List<(GpuMesh.Vertex[] Verts, uint[] Idx, Material Mat)> parts, List<(GpuMesh, Material)> result)
    {
        GpuMesh.SmoothNormals(parts.ConvertAll(p => p.Verts), parts.ConvertAll(p => p.Idx));
        foreach (var (verts, idx, mat) in parts) result.Add((GpuMesh.FromOwned(gl, verts, idx), mat));
    }

    // Amedo
    private static GpuMesh.Vertex[] BuildTriangleListVerts(
        Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems.ITwinSubModel sub)
    {
        int count = sub.Vertexes.Count;
        int connLen  = sub.Connection.Count;
        int tris = 0;
        for (int j = 2; j < count; j++)
            if (j < connLen && sub.Connection[j]) tris++;
        var triVerts = new GpuMesh.Vertex[tris * 3];
        if (tris == 0) return triVerts;
        var verts = System.Buffers.ArrayPool<GpuMesh.Vertex>.Shared.Rent(count);
        for (int j = 0; j < count; j++)
        {
            var p = sub.Vertexes[j];
            bool hasN = j < sub.Normals.Count, hasUv = j < sub.UVW.Count, hasC = j < sub.Colors.Count; // Amedo
            var n = hasN ? sub.Normals[j] : default;
            var uv = hasUv ? sub.UVW[j] : default;
            var c = hasC ? sub.Colors[j] : default;

            verts[j] = new GpuMesh.Vertex
            {
                Position = new SysVec3(p.X, p.Y, p.Z),
                Normal   = !hasN ? new SysVec3(0f, 1f, 0f) : new SysVec3(n.X, n.Y, n.Z), // Amedo
                UV       = !hasUv ? SysVec2.Zero : new SysVec2(uv.X, uv.Y),
                Color    = !hasC ? SysVec4.One : new SysVec4(
                    Math.Clamp(c.X, 0f, 1f),
                    Math.Clamp(c.Y, 0f, 1f),
                    Math.Clamp(c.Z, 0f, 1f),
                    Math.Clamp(c.W, 0f, 1f))
            };
        }

        bool winding = false;
        int k = 0;
        for (int j = 2; j < count; j++)
        {
            if (j >= connLen || !sub.Connection[j])
            {
                winding = !winding;
                continue;
            }
            if (!winding)
            {
                triVerts[k++] = verts[j - 2];
                triVerts[k++] = verts[j - 1];
                triVerts[k++] = verts[j];
            }
            else
            {
                triVerts[k++] = verts[j - 1];
                triVerts[k++] = verts[j - 2];
                triVerts[k++] = verts[j];
            }
            winding = !winding;
        }
        System.Buffers.ArrayPool<GpuMesh.Vertex>.Shared.Return(verts);
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
            if (triVerts.Length != gpuMesh.VertexCount) continue; // Amedo
            gpuMesh.UpdateVertices(gl, triVerts);
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
            if (triVerts.Length != gpuMesh.VertexCount) continue; // Amedo
            gpuMesh.UpdateVertices(gl, triVerts);
        }
    }

    public static void AliasSceneryMeshTableEntry(object tablesHandle, uint oldId, uint newId)
    {
        if (tablesHandle is not SceneryTables tables) return;
        if (tables.MeshById.TryGetValue(oldId, out var entry))
            tables.MeshById[newId] = entry;
    }

    private static GpuMesh.Vertex SkinnedVertex(List<TwinVec4> vertexes, List<TwinVec4> uvw, List<TwinVec4> colors,
        List<VertexJointInfo> joints, int j)
    {
        var ji = joints[j];
        return new()
        {
            Position = new SysVec3(vertexes[j].X, vertexes[j].Y, vertexes[j].Z),
            Normal   = new SysVec3(vertexes[j].W, uvw[j].Z, uvw[j].W),
            UV       = new System.Numerics.Vector2(uvw[j].X, uvw[j].Y),
            Color    = new SysVec4(
                Math.Clamp(colors[j].X, 0f, 1f), Math.Clamp(colors[j].Y, 0f, 1f),
                Math.Clamp(colors[j].Z, 0f, 1f), GsAlpha(colors[j].W)),
            BoneIndices = new SysVec3(ji.JointIndex1, ji.JointIndex2, ji.JointIndex3),
            BoneWeights = new SysVec3(ji.Weight1,     ji.Weight2,     ji.Weight3),
        };
    }

    public static List<(GpuMesh, Material)> DecodeBlendSkin(GL gl,
        Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinBlendSkin blend,
        IReadOnlyList<(Texture2D? Tex, TwinShader? Shader)>? subMats = null)
    {
        var result = new List<(GpuMesh, Material)>();
        var parts = new List<(GpuMesh.Vertex[] Verts, uint[] Idx, Material Mat)>(); // Amedo

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

                GpuMesh.Vertex V(int j) => SkinnedVertex(model.Vertexes, model.UVW, model.Colors, model.SkinJoints, j);

                for (int i = 0; i < model.Vertexes.Count - 2; i++)
                {
                    if (i + 2 >= model.SkinJoints.Count || !model.SkinJoints[i + 2].Connection) continue;
                    if (i % 2 == 0) { tri.Add(V(i));     tri.Add(V(i + 1)); tri.Add(V(i + 2)); }
                    else            { tri.Add(V(i + 1)); tri.Add(V(i));     tri.Add(V(i + 2)); }
                }
            }
            if (tri.Count < 3) continue;

            var idx = Enumerable.Range(0, tri.Count).Select(k => (uint)k).ToArray();
            var triArray = tri.ToArray(); // Amedo
            var mat = BuildTwinMaterial(tex, shader);
            var localCenter = tri.Aggregate(SysVec3.Zero, (a, v) => a + v.Position) / tri.Count;
            mat.LocalCenter    = localCenter;
            mat.BoundingRadius = tri.Max(v => SysVec3.Distance(v.Position, localCenter));
            parts.Add((triArray, idx, mat)); // Amedo
        }
        SmoothTogether(gl, parts, result); // Amedo
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
        private bool _atBase = true; // Amedo

        public MorphMesh(GpuMesh mesh, Material mat, GpuMesh.Vertex[] baseVerts, SysVec3[][] shapeOffsets)
        {
            Mesh = mesh; Mat = mat; _base = baseVerts; _shapeOffsets = shapeOffsets;
            _scratch = new GpuMesh.Vertex[baseVerts.Length];
        }

        public void ApplyWeights(GL gl, ReadOnlySpan<float> weights)
        {
            bool zero = true; // Amedo
            foreach (float w in weights) zero &= w == 0f;
            if (zero && _atBase) return;
            _atBase = zero;
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

    // Amedo 2026-10-02
    private sealed record MorphData(int SubIndex, GpuMesh.Vertex[] Base, uint[] Index, SysVec3[][] Shapes, SysVec3 Center, float Radius);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinBlendSkin, List<MorphData>> MorphCache = new();

    public static List<MorphMesh> DecodeBlendSkinMorph(GL gl,
        Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinBlendSkin blend,
        IReadOnlyList<(Texture2D? Tex, TwinShader? Shader)>? subMats = null)
    {
        var result = new List<MorphMesh>();
        foreach (var d in MorphCache.GetValue(blend, DecodeMorphData)) // Amedo 2026-10-02
        {
            var (tex, shader) = subMats is { Count: > 0 }
                ? subMats[Math.Min(d.SubIndex, subMats.Count - 1)]
                : (null, null);
            var gpu = new GpuMesh(gl, d.Base, d.Index);
            var mat = BuildTwinMaterial(tex, shader);
            mat.LocalCenter    = d.Center;
            mat.BoundingRadius = d.Radius;
            result.Add(new MorphMesh(gpu, mat, d.Base, d.Shapes));
        }
        return result;
    }

    // Amedo 2026-10-02
    private static List<MorphData> DecodeMorphData(Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinBlendSkin blend)
    {
        var result = new List<MorphData>();

        for (int si = 0; si < blend.SubBlends.Count; si++)
        {
            var sb = blend.SubBlends[si];
            if (sb.Models.Count == 0) continue;

            var tri = new List<GpuMesh.Vertex>();
            var shapeLists = new List<SysVec3>[MaxMorphShapes];
            for (int s = 0; s < MaxMorphShapes; s++) shapeLists[s] = new List<SysVec3>();

            foreach (var model in sb.Models)
            {
                try { model.CalculateData(); } catch { continue; }
                if (model.Vertexes.Count < 3) continue;
                foreach (var face in model.Faces) { try { face.CalculateData(); } catch { } }

                GpuMesh.Vertex V(int j) => SkinnedVertex(model.Vertexes, model.UVW, model.Colors, model.SkinJoints, j);

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
            var localCenter = tri.Aggregate(SysVec3.Zero, (a, v) => a + v.Position) / tri.Count;
            float radius = tri.Max(v => SysVec3.Distance(v.Position, localCenter)); // Amedo 2026-10-02

            var shapeArrays = new SysVec3[MaxMorphShapes][];
            for (int s = 0; s < MaxMorphShapes; s++)
                shapeArrays[s] = shapeLists[s].Any(v => v != SysVec3.Zero) ? shapeLists[s].ToArray() : Array.Empty<SysVec3>();

            result.Add(new MorphData(si, baseVerts, idx, shapeArrays, localCenter, radius)); // Amedo 2026-10-02
        }
        GpuMesh.SmoothNormals(result.ConvertAll(d => d.Base), result.ConvertAll(d => d.Index)); // Amedo
        return result;
    }

    public static List<(GpuMesh, Material)> DecodeSkin(GL gl,
        Twinsanity.TwinsanityInterchange.Interfaces.Items.ITwinSkin skin,
        IReadOnlyList<(Texture2D? Tex, TwinShader? Shader)>? subMats = null, bool recalculate = true)
    {
        var result = new List<(GpuMesh, Material)>();
        var parts = new List<(GpuMesh.Vertex[] Verts, uint[] Idx, Material Mat)>(); // Amedo

        for (int si = 0; si < skin.SubSkins.Count; si++)
        {
            var sub = skin.SubSkins[si];
            if (recalculate) { try { sub.CalculateData(); } catch { continue; } }
            if (sub.Vertexes.Count < 3) continue;

            var (tex, shader) = subMats is { Count: > 0 }
                ? subMats[Math.Min(si, subMats.Count - 1)]
                : (null, null);

            var tri = new List<GpuMesh.Vertex>(Math.Max(0, (sub.Vertexes.Count - 2) * 3)); // Amedo 2026-09-29
            bool winding = false;
            GpuMesh.Vertex V(int j) => SkinnedVertex(sub.Vertexes, sub.UVW, sub.Colors, sub.SkinJoints, j);
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
            var triArray = tri.ToArray(); // Amedo
            var localCenter = tri.Aggregate(SysVec3.Zero, (a, v) => a + v.Position) / tri.Count;

            var mat = BuildTwinMaterial(tex, shader);
            mat.LocalCenter    = localCenter;
            mat.BoundingRadius = tri.Max(v => SysVec3.Distance(v.Position, localCenter));
            parts.Add((triArray, idx, mat)); // Amedo
        }
        SmoothTogether(gl, parts, result); // Amedo
        return result;
    }

    // Amedo 2026-09-26
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TwinShader, List<(Texture2D? Tex, TwinShader Shader)>> _upperLayers = new();

    // Amedo 2026-09-26
    private static readonly Random _clothRandom = new(1);

    // Amedo 2026-09-26
    private static float ClothRand() => (float)((_clothRandom.NextDouble() * 2.0 - 1.0) * Math.PI);

    // Amedo
    public sealed class ClothState
    {
        public readonly SysVec3[] Phases = new SysVec3[16];
        public readonly SysVec3[] Phases100 = new SysVec3[16];
        public readonly System.Runtime.CompilerServices.StrongBox<double> Clock = new();
        public required TwinShader Shader { get; init; }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TwinShader, ClothState> _clothStates = new(); // Amedo

    // Amedo
    public static ClothState ClothStateOf(TwinShader shader) => _clothStates.GetValue(shader, s =>
    {
        var c = new ClothState { Shader = s };
        lock (_clothRandom)
            for (int i = 0; i < 16; i++)
                c.Phases[i] = new SysVec3(ClothRand(), ClothRand(), ClothRand());
        return c;
    });

    // Amedo
    public static List<ClothState> ClothShadersOf(PS2AnyTwinsanitySM2? sm2)
    {
        var list = new List<ClothState>();
        var gfx = sm2?.GetItem<BaseTwinSection>((uint)Constants.SCENERY_GRAPHICS_SECTION);
        if (gfx?.GetItem<BaseTwinSection>((uint)Constants.GRAPHICS_MATERIALS_SECTION) is not { } mats) return list;
        for (int i = 0; i < mats.GetItemsAmount(); i++)
        {
            if (mats.GetItem(i) is not PS2AnyMaterial mat) continue;
            foreach (var sh in mat.Shaders)
                if (sh.ShaderType is TwinShader.Type.UnlitClothDeformation or TwinShader.Type.UnlitClothDeformation2) list.Add(ClothStateOf(sh));
        }
        return list;
    }

    // Amedo 2026-09-26
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TwinShader, System.Runtime.CompilerServices.StrongBox<uint>> _dmaChainOf = new();

    // Amedo 2026-09-26
    private static void RegisterUpperLayers(PS2AnyMaterial mat, Dictionary<uint, Texture2D> texCache)
    {
        if (mat.Shaders.Count == 0) return;
        _dmaChainOf.AddOrUpdate(mat.Shaders[0], new System.Runtime.CompilerServices.StrongBox<uint>(mat.DmaChainIndex));
        if (mat.Shaders.Count < 2) return;
        var layers = new List<(Texture2D? Tex, TwinShader Shader)>();
        for (int i = 1; i < mat.Shaders.Count; i++)
            layers.Add((texCache.TryGetValue(mat.Shaders[i].TextureId, out var t) ? t : null, mat.Shaders[i]));
        _upperLayers.AddOrUpdate(mat.Shaders[0], layers);
    }

    public static Material BuildTwinMaterial(Texture2D? tex, TwinShader? shader,
        Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems.ITwinSubModel? sourceSubModel = null)
    {
        var baseMat = BuildTwinLayer(tex, shader, sourceSubModel); // Amedo 2026-09-26
        if (shader is not null && _upperLayers.TryGetValue(shader, out var upper)) // Amedo 2026-09-26
            baseMat.Layers = upper.Select(l => BuildTwinLayer(l.Tex, l.Shader, sourceSubModel)).ToList();
        if (shader is not null && _dmaChainOf.TryGetValue(shader, out var chain)) // Amedo 2026-09-26
        {
            baseMat.DmaChain = chain.Value;
            baseMat.FogEnabled = chain.Value <= GameFog.Chain_1AE698;
            if (baseMat.Layers is { } ls) foreach (var l in ls) { l.DmaChain = chain.Value; l.FogEnabled = baseMat.FogEnabled; }
        }
        return baseMat;
    }

    // Amedo
    private static bool GsMipClass(TwinShader.Type type) => type is TwinShader.Type.StandardUnlit or TwinShader.Type.StandardLit
        or TwinShader.Type.LitSkinnedModel or TwinShader.Type.SHADER_20 or TwinShader.Type.UnlitGlossy
        or TwinShader.Type.UnlitEnvironmentMap or TwinShader.Type.UnlitClothDeformation or TwinShader.Type.SHADER_25
        or TwinShader.Type.UnlitClothDeformation2 or TwinShader.Type.UnlitBillboard or TwinShader.Type.SHADER_30
        or TwinShader.Type.SHADER_31 or TwinShader.Type.SHADER_32;

    // Amedo 2026-09-26
    private static Material BuildTwinLayer(Texture2D? tex, TwinShader? shader,
        Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems.ITwinSubModel? sourceSubModel)
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
        var uvPhase = SysVec2.Zero; // Amedo 2026-09-26
        if ((int)shader.XScrollSettings >= (int)TwinShader.XScrollFormula.Linear) // Amedo 2026-09-26
            (uvScroll.X, uvPhase.X) = (shader.UvScrollSpeed.Z, shader.UvScrollSpeed.X);
        if ((int)shader.YScrollSettings >= (int)TwinShader.YScrollFormula.Linear) // Amedo 2026-09-26
            (uvScroll.Y, uvPhase.Y) = (shader.UvScrollSpeed.W, shader.UvScrollSpeed.Y);
        var uvScrollMode = new SysVec2( // Amedo 2026-09-26
            shader.XScrollSettings == TwinShader.XScrollFormula.Disabled ? 1 : (int)shader.XScrollSettings,
            shader.YScrollSettings == TwinShader.YScrollFormula.Disabled ? 1 : (int)shader.YScrollSettings);

        var deform = SysVec2.Zero;
        int clothKind = shader.ShaderType switch // Amedo 2026-09-26
        {
            TwinShader.Type.UnlitClothDeformation  => 1,
            TwinShader.Type.UnlitClothDeformation2 => 2,
            _ => 0,
        };
        SysVec3[]? clothPhases = null; // Amedo 2026-09-26
        ClothState? cloth = clothKind > 0 ? ClothStateOf(shader) : null; // Amedo
        if (cloth is not null) clothPhases = cloth.Phases; // Amedo

        var blend = shader.AlphaRegSettingsIndex switch
        {
            TwinShader.AlphaBlendPresets.Add => Material.BlendMode.Additive,
            TwinShader.AlphaBlendPresets.Sub => Material.BlendMode.Subtractive,
            _                                => Material.BlendMode.Standard,
        };

        return new Material
        {
            Albedo          = shader.TxtMapping == TwinShader.TextureMapping.ON ? tex : null,
            Culling         = Material.CullMode.Both,
            DoubleColor     = unlit ? 1f : 2f,
            AlphaBlend      = shader.ABlending == TwinShader.AlphaBlending.ON,
            AlphaTest       = shader.ATest == TwinShader.AlphaTest.ON // Amedo 2026-09-26
                                  ? shader.AlphaValueToBeComparedTo / 128f : 0f,
            BillboardRender = shader.ShaderType == TwinShader.Type.UnlitBillboard,
            ReflectDist     = shader.ShaderType == TwinShader.Type.LitReflectionSurface // Amedo 2026-09-26
                                  ? new SysVec2(0f, shader.FloatParam[0]) : SysVec2.Zero,
            MetalicSpecular = 0f, // Amedo 2026-09-26
            DeformSpeed     = deform,
            EnvMap          = envMap ? 1f : shader.ShaderType switch // Amedo 2026-09-26
            {
                TwinShader.Type.LitMetallic          => 2f,
                TwinShader.Type.LitReflectionSurface => 3f,
                _                                     => 0f,
            },
            UvScrollSpeed   = uvScroll,
            UvScrollMode    = uvScrollMode, // Amedo 2026-09-26
            UvScrollPhase   = uvPhase, // Amedo 2026-09-26
            ClothKind       = clothKind, // Amedo 2026-09-26
            ClothMode       = (int)shader.IntParam, // Amedo 2026-09-26
            ClothSpeed      = shader.FloatParam[0], // Amedo 2026-09-26
            ClothAmplitude  = clothKind == 2 // Amedo 2026-09-26
                                  ? new SysVec3(shader.FloatParam[1], shader.FloatParam[2], shader.FloatParam[3])
                                  : new SysVec3(shader.FloatParam[1]),
            ClothPhases     = clothPhases, // Amedo 2026-09-26
            ClothClock      = cloth?.Clock, // Amedo
            Blend           = blend,
            DepthWrite      = shader.ZValueDrawingMask == TwinShader.ZValueDrawMask.UPDATE,
            FogEnabled      = true, // Amedo 2026-09-26
            ShaderType      = shader.ShaderType.ToString(),
            GameLit         = shader.ShaderType is TwinShader.Type.StandardLit or TwinShader.Type.LitSkinnedModel // Amedo 2026-09-26
                                                 or TwinShader.Type.LitEnvironmentMap or TwinShader.Type.LitMetallic
                                                 or TwinShader.Type.LitReflectionSurface or TwinShader.Type.SHADER_17,
            ShadowReceiver  = shader.AlphaCorrectionValue, // Amedo 2026-09-26
            GsMipmaps       = GsMipClass(shader.ShaderType), // Amedo
            GsLodK          = ((shader.LodParamK & 0xFFF) ^ 0x800) - 0x800 is var k ? k / 16f : 0f, // Amedo
            GsLodL          = 1 << (shader.LodParamL & 3), // Amedo
            SourceShader    = shader,
            SourceSubModel  = sourceSubModel,
        };
    }
}
