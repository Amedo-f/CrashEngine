using CrashEngine.Core;
using SharpGLTF.Schema2;
using SharpGLTF.Validation;
using System.Numerics;
using GMaterial = SharpGLTF.Schema2.Material;

namespace CrashEngine.Assets.Modern;

// Amedo 2026-11-18
public static class ModernCharacterImporter
{
    public const int MaxInfluences = 8;
    private const int InfluencesPerSet = 4;

    public static ModernCharacter? Import(string path, out string error)
    {
        error = "";
        string? relaxed = null;
        ModelRoot model;
        try { model = ModelRoot.Load(path, new ReadSettings { Validation = ValidationMode.TryFix }); }
        catch (Exception strict)
        {
            try { model = ModelRoot.Load(path, new ReadSettings { Validation = ValidationMode.Skip }); }
            catch (Exception ex) { error = $"the file could not be read as glTF: {ex.Message}"; return null; }
            relaxed = strict.Message;
        }
        var scene = model.DefaultScene ?? model.LogicalScenes.FirstOrDefault();
        if (scene is null) { error = "the file has no scene"; return null; }

        var order = new List<Node>();
        void Walk(Node n) { order.Add(n); foreach (var c in n.VisualChildren) Walk(c); }
        foreach (var n in scene.VisualChildren) Walk(n);
        var meshNodes = order.Where(n => n.Mesh is not null).ToList();
        if (meshNodes.Count == 0) { error = "the file has no meshes"; return null; }

        var character = new ModernCharacter();
        if (relaxed is not null) character.Notes.Add($"the file breaks a glTF rule and was read without checks ({relaxed})");
        var joints = BuildJoints(model, order, character);
        foreach (var node in meshNodes) AddMeshes(node, joints, character);
        if (character.Meshes.Count == 0) { error = "the file has no triangle meshes"; return null; }
        foreach (var tex in model.LogicalTextures) character.Textures.Add(ReadTexture(tex));
        foreach (var gm in model.LogicalMaterials) character.Materials.Add(ReadMaterial(gm));
        foreach (var anim in model.LogicalAnimations) AddClip(anim, joints, character);
        return character;
    }

    private static Dictionary<Node, int> BuildJoints(ModelRoot model, List<Node> order, ModernCharacter character)
    {
        var inverseBind = new Dictionary<Node, Matrix4x4>();
        foreach (var skin in model.LogicalSkins)
            for (int i = 0; i < skin.JointsCount; i++)
            {
                var (joint, ibm) = skin.GetJoint(i);
                if (!inverseBind.TryAdd(joint, ibm) && !Near(inverseBind[joint], ibm))
                    character.Notes.Add($"bone '{joint.Name}' has a different bind pose in another skin; the first one is kept");
            }

        var wanted = new HashSet<Node>();
        foreach (var joint in inverseBind.Keys)
            for (var p = joint; p is not null; p = p.VisualParent) wanted.Add(p);

        var index = new Dictionary<Node, int>();
        foreach (var n in order)
        {
            if (!wanted.Contains(n)) continue;
            var t = n.LocalTransform.GetDecomposed();
            index[n] = character.Joints.Count;
            character.Joints.Add(new ModernJoint
            {
                Name = n.Name ?? $"node{n.LogicalIndex}",
                Parent = n.VisualParent is { } parent && index.TryGetValue(parent, out int pi) ? pi : -1,
                Translation = t.Translation,
                Rotation = Quaternion.Normalize(t.Rotation),
                Scale = t.Scale,
                InverseBind = inverseBind.TryGetValue(n, out var ibm) ? ibm : Inverse(n.WorldMatrix),
            });
        }
        return index;
    }

    private static void AddMeshes(Node node, Dictionary<Node, int> joints, ModernCharacter character)
    {
        var skin = node.Skin;
        int rigid = -1;
        for (var p = node; p is not null && rigid < 0; p = p.VisualParent)
            if (joints.TryGetValue(p, out int j)) rigid = j;
        var rigidBake = Matrix4x4.Identity;
        if (skin is null)
        {
            rigidBake = node.WorldMatrix;
            if (rigid >= 0) rigidBake *= Inverse(WorldOf(character, rigid)) * Inverse(character.Joints[rigid].InverseBind);
            character.Notes.Add(rigid >= 0 ? $"mesh '{node.Mesh!.Name ?? node.Name}' has no skin and moves with bone '{character.Joints[rigid].Name}'"
                                           : $"mesh '{node.Mesh!.Name ?? node.Name}' has no skin and stays still");
        }

        int primitive = 0;
        foreach (var prim in node.Mesh!.Primitives)
        {
            string name = $"{node.Mesh.Name ?? node.Name ?? "mesh"}#{primitive++}";
            if (prim.DrawPrimitiveType != PrimitiveType.TRIANGLES) { character.Notes.Add($"mesh '{name}' is not triangles and was skipped"); continue; }
            var positions = prim.GetVertexAccessor("POSITION")?.AsVector3Array().ToArray();
            if (positions is null || positions.Length == 0) continue;
            int count = positions.Length;
            var indices = prim.GetTriangleIndices().SelectMany(t => new[] { (uint)t.A, (uint)t.B, (uint)t.C }).ToArray();
            var normals = prim.GetVertexAccessor("NORMAL")?.AsVector3Array().ToArray() ?? SmoothNormals(positions, indices);
            var uv = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array().ToArray() ?? new Vector2[count];
            var tangents = prim.GetVertexAccessor("TANGENT")?.AsVector4Array().ToArray() ?? Tangents(positions, normals, uv, indices);
            var colors = prim.GetVertexAccessor("COLOR_0") is { } colorAccessor
                ? (colorAccessor.Dimensions == DimensionType.VEC3 ? colorAccessor.AsVector3Array().Select(c => new Vector4(c, 1f)).ToArray() : colorAccessor.AsVector4Array().ToArray())
                : null;
            if (prim.MorphTargetsCount > 0) character.Notes.Add($"mesh '{name}' has {prim.MorphTargetsCount} shape key(s); they are not read yet");

            var jointIds = new ushort[count * MaxInfluences];
            var weights = new float[count * MaxInfluences];
            int influences = 0;
            if (skin is not null)
            {
                var map = new int[skin.JointsCount];
                for (int i = 0; i < map.Length; i++) map[i] = joints[skin.GetJoint(i).Joint];
                int sets = 0;
                while (prim.GetVertexAccessor($"JOINTS_{sets}") is not null && prim.GetVertexAccessor($"WEIGHTS_{sets}") is not null) sets++;
                if (sets * InfluencesPerSet > MaxInfluences) character.Notes.Add($"mesh '{name}' has {sets * InfluencesPerSet} weights per vertex; the strongest {MaxInfluences} are kept");
                var all = new List<(int Joint, float Weight)>[count];
                for (int s = 0; s < sets; s++)
                {
                    var js = prim.GetVertexAccessor($"JOINTS_{s}").AsVector4Array();
                    var ws = prim.GetVertexAccessor($"WEIGHTS_{s}").AsVector4Array();
                    for (int v = 0; v < count; v++)
                        for (int k = 0; k < InfluencesPerSet; k++)
                        {
                            float w = Component(ws[v], k);
                            if (w > 0f) (all[v] ??= new()).Add((map[(int)Component(js[v], k)], w));
                        }
                }
                int unweighted = 0;
                for (int v = 0; v < count; v++)
                {
                    var list = all[v];
                    if (list is null) { unweighted++; jointIds[v * MaxInfluences] = (ushort)Math.Max(rigid, 0); weights[v * MaxInfluences] = rigid >= 0 ? 1f : 0f; continue; }
                    var top = list.OrderByDescending(x => x.Weight).Take(MaxInfluences).ToList();
                    float sum = top.Sum(x => x.Weight);
                    influences = Math.Max(influences, top.Count);
                    for (int k = 0; k < top.Count; k++)
                    {
                        jointIds[v * MaxInfluences + k] = (ushort)top[k].Joint;
                        weights[v * MaxInfluences + k] = top[k].Weight / sum;
                    }
                }
                if (unweighted > 0) character.Notes.Add($"mesh '{name}' has {unweighted} vertex(es) with no weights");
            }
            else
            {
                for (int v = 0; v < count; v++)
                {
                    positions[v] = Vector3.Transform(positions[v], rigidBake);
                    normals[v] = GameMath.Normalize(Vector3.TransformNormal(normals[v], rigidBake));
                    var t = GameMath.Normalize(Vector3.TransformNormal(new Vector3(tangents[v].X, tangents[v].Y, tangents[v].Z), rigidBake));
                    tangents[v] = new Vector4(t, tangents[v].W);
                    jointIds[v * MaxInfluences] = (ushort)Math.Max(rigid, 0);
                    weights[v * MaxInfluences] = rigid >= 0 ? 1f : 0f;
                }
                influences = rigid >= 0 ? 1 : 0;
            }

            character.Meshes.Add(new ModernMesh
            {
                Name = name, Material = prim.Material?.LogicalIndex ?? -1,
                Positions = positions, Normals = normals, Tangents = tangents, Uv = uv, Colors = colors,
                Influences = influences, Joints = jointIds, Weights = weights, Indices = indices,
            });
        }
    }

    private static void AddClip(Animation anim, Dictionary<Node, int> joints, ModernCharacter character)
    {
        var clip = new ModernClip { Name = anim.Name ?? $"clip{anim.LogicalIndex}" };
        int skipped = 0, morph = 0;
        foreach (var channel in anim.Channels)
        {
            if (channel.TargetNodePath == PropertyPath.weights) { morph++; continue; }
            if (channel.TargetNode is not { } node || !joints.TryGetValue(node, out int joint)) { skipped++; continue; }
            var c = channel.TargetNodePath switch
            {
                PropertyPath.translation => Channel(channel.GetTranslationSampler(), joint, ModernChannelPath.Translation, v => new Vector4(v, 0f)),
                PropertyPath.rotation => Channel(channel.GetRotationSampler(), joint, ModernChannelPath.Rotation, q => new Vector4(q.X, q.Y, q.Z, q.W)),
                PropertyPath.scale => Channel(channel.GetScaleSampler(), joint, ModernChannelPath.Scale, v => new Vector4(v, 0f)),
                _ => null,
            };
            if (c is null) { skipped++; continue; }
            clip.Channels.Add(c);
            clip.Duration = MathF.Max(clip.Duration, c.Times[^1]);
        }
        if (skipped > 0) character.Notes.Add($"clip '{clip.Name}': {skipped} channel(s) move nodes outside the skeleton and were skipped");
        if (morph > 0) character.Notes.Add($"clip '{clip.Name}': {morph} shape key channel(s) are not read yet");
        character.Clips.Add(clip);
    }

    private static ModernChannel Channel<T>(IAnimationSampler<T> sampler, int joint, ModernChannelPath path, Func<T, Vector4> pack)
    {
        var times = new List<float>();
        var values = new List<Vector4>();
        var mode = sampler.InterpolationMode switch
        {
            AnimationInterpolationMode.STEP => ModernInterpolation.Step,
            AnimationInterpolationMode.CUBICSPLINE => ModernInterpolation.CubicSpline,
            _ => ModernInterpolation.Linear,
        };
        if (mode == ModernInterpolation.CubicSpline)
            foreach (var (time, (tangentIn, value, tangentOut)) in sampler.GetCubicKeys())
            {
                times.Add(time);
                values.Add(pack(tangentIn)); values.Add(pack(value)); values.Add(pack(tangentOut));
            }
        else
            foreach (var (time, value) in sampler.GetLinearKeys())
            {
                times.Add(time);
                values.Add(pack(value));
            }
        return new ModernChannel { Joint = joint, Path = path, Interpolation = mode, Times = times.ToArray(), Values = values.ToArray() };
    }

    private static ModernTexture ReadTexture(Texture tex)
    {
        var image = tex.PrimaryImage?.Content;
        return new ModernTexture
        {
            Name = tex.Name ?? tex.PrimaryImage?.Name ?? $"texture{tex.LogicalIndex}",
            Encoded = image is { IsValid: true } img ? img.Content.ToArray() : Array.Empty<byte>(),
            MimeType = image is { IsValid: true } m ? m.MimeType : "",
        };
    }

    private static ModernMaterial ReadMaterial(GMaterial gm)
    {
        var mat = new ModernMaterial
        {
            Name = gm.Name ?? $"material{gm.LogicalIndex}",
            Alpha = gm.Alpha switch { AlphaMode.MASK => ModernAlpha.Mask, AlphaMode.BLEND => ModernAlpha.Blend, _ => ModernAlpha.Opaque },
            AlphaCutoff = gm.AlphaCutoff,
            DoubleSided = gm.DoubleSided,
            Unlit = gm.Unlit,
        };
        if (gm.FindChannel("BaseColor") is { } baseColor)
        {
            mat.BaseColor = baseColor.Parameter;
            mat.BaseColorTexture = baseColor.Texture?.LogicalIndex ?? -1;
        }
        if (gm.FindChannel("MetallicRoughness") is { } mr)
        {
            mat.Metallic = mr.Parameter.X;
            mat.Roughness = mr.Parameter.Y;
            mat.MetallicRoughnessTexture = mr.Texture?.LogicalIndex ?? -1;
        }
        if (gm.FindChannel("Normal") is { } normal)
        {
            mat.NormalScale = normal.Parameter.X;
            mat.NormalTexture = normal.Texture?.LogicalIndex ?? -1;
        }
        if (gm.FindChannel("Occlusion") is { } occlusion)
        {
            mat.OcclusionStrength = occlusion.Parameter.X;
            mat.OcclusionTexture = occlusion.Texture?.LogicalIndex ?? -1;
        }
        if (gm.FindChannel("Emissive") is { } emissive)
        {
            mat.Emissive = new Vector3(emissive.Parameter.X, emissive.Parameter.Y, emissive.Parameter.Z);
            mat.EmissiveTexture = emissive.Texture?.LogicalIndex ?? -1;
        }
        return mat;
    }

    private static Matrix4x4 WorldOf(ModernCharacter character, int joint)
    {
        var world = Matrix4x4.Identity;
        for (int j = joint; j >= 0; j = character.Joints[j].Parent) world *= character.Joints[j].RestLocal;
        return world;
    }

    private static Vector3[] SmoothNormals(Vector3[] positions, uint[] indices)
    {
        var normals = new Vector3[positions.Length];
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            var (a, b, c) = (indices[i], indices[i + 1], indices[i + 2]);
            var n = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
            normals[a] += n; normals[b] += n; normals[c] += n;
        }
        for (int v = 0; v < normals.Length; v++) normals[v] = GameMath.LengthSquared(normals[v]) > 0f ? GameMath.Normalize(normals[v]) : Vector3.UnitY;
        return normals;
    }

    private static Vector4[] Tangents(Vector3[] positions, Vector3[] normals, Vector2[] uv, uint[] indices)
    {
        var tan = new Vector3[positions.Length];
        var bit = new Vector3[positions.Length];
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            var (a, b, c) = (indices[i], indices[i + 1], indices[i + 2]);
            var e1 = positions[b] - positions[a];
            var e2 = positions[c] - positions[a];
            var d1 = uv[b] - uv[a];
            var d2 = uv[c] - uv[a];
            float det = d1.X * d2.Y - d2.X * d1.Y;
            if (MathF.Abs(det) < 1e-12f) continue;
            float r = 1f / det;
            var t = (e1 * d2.Y - e2 * d1.Y) * r;
            var s = (e2 * d1.X - e1 * d2.X) * r;
            tan[a] += t; tan[b] += t; tan[c] += t;
            bit[a] += s; bit[b] += s; bit[c] += s;
        }
        var result = new Vector4[positions.Length];
        for (int v = 0; v < result.Length; v++)
        {
            var n = normals[v];
            var t = tan[v] - n * GameMath.Dot(n, tan[v]);
            if (GameMath.LengthSquared(t) < 1e-12f) t = Vector3.Cross(n, MathF.Abs(n.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY);
            t = GameMath.Normalize(t);
            float w = GameMath.Dot(Vector3.Cross(n, t), bit[v]) < 0f ? -1f : 1f;
            result[v] = new Vector4(t, w);
        }
        return result;
    }

    private static float Component(Vector4 v, int k) => k switch { 0 => v.X, 1 => v.Y, 2 => v.Z, _ => v.W };

    private static Matrix4x4 Inverse(in Matrix4x4 m) => Matrix4x4.Invert(m, out var inv) ? inv : Matrix4x4.Identity;

    private static bool Near(in Matrix4x4 a, in Matrix4x4 b)
    {
        var d = a - b;
        return MathF.Abs(d.M11) + MathF.Abs(d.M12) + MathF.Abs(d.M13) + MathF.Abs(d.M21) + MathF.Abs(d.M22) + MathF.Abs(d.M23)
             + MathF.Abs(d.M31) + MathF.Abs(d.M32) + MathF.Abs(d.M33) + MathF.Abs(d.M41) + MathF.Abs(d.M42) + MathF.Abs(d.M43) < 1e-3f;
    }
}
