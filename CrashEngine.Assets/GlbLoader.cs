using CrashEngine.Core;
using CrashEngine.Renderer;
using SharpGLTF.Schema2;
using Silk.NET.OpenGL;
using StbImageSharp;
using System.Numerics;
using RMat = CrashEngine.Renderer.Material;
using GMat = SharpGLTF.Schema2.Material;

namespace CrashEngine.Assets;

public static class GlbLoader
{
    public static Entity Load(GL gl, string path)
    {
        var model  = ModelRoot.Load(path);
        var root   = new Entity(Path.GetFileNameWithoutExtension(path));
        var texMap = BuildTextures(gl, model);

        _meshCount = 0; _primCount = 0; _vertCount = 0;
        foreach (var scene in model.LogicalScenes)
            foreach (var node in scene.VisualChildren)
                WalkNode(gl, node, root, texMap);

        Console.WriteLine($"[GlbLoader] {Path.GetFileName(path)}: nodes={model.LogicalNodes.Count} " +
                          $"meshNodes={_meshCount} prims={_primCount} verts={_vertCount} glErr={gl.GetError()}");
        return root;
    }

    private static int _meshCount, _primCount, _vertCount;

    private static Dictionary<int, Texture2D> BuildTextures(GL gl, ModelRoot model)
    {
        var map = new Dictionary<int, Texture2D>();
        foreach (var tex in model.LogicalTextures)
        {
            var img = tex.PrimaryImage?.Content;
            if (img is null || !img.Value.IsValid) { map[tex.LogicalIndex] = Texture2D.White(gl); continue; }

            try
            {
                using var stream = img.Value.Open();
                var result = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                map[tex.LogicalIndex] = new Texture2D(gl, result.Data, (uint)result.Width, (uint)result.Height);
            }
            catch { map[tex.LogicalIndex] = Texture2D.White(gl); }
        }
        return map;
    }

    private static void WalkNode(GL gl, Node node, Entity parent,
                                 Dictionary<int, Texture2D> texMap)
    {
        var entity = new Entity(node.Name ?? node.Mesh?.Name ?? "Node");

        var trs = node.LocalTransform;
        entity.Transform.Position = trs.Translation;
        entity.Transform.Rotation = trs.Rotation;
        entity.Transform.Scale    = trs.Scale;

        parent.AddChild(entity);

        if (node.Mesh is not null)
        {
            _meshCount++;
            foreach (var prim in node.Mesh.Primitives)
                AttachPrimitive(gl, prim, entity, texMap);
        }

        foreach (var child in node.VisualChildren)
            WalkNode(gl, child, entity, texMap);
    }

    private static void AttachPrimitive(GL gl, MeshPrimitive prim, Entity entity,
                                        Dictionary<int, Texture2D> texMap)
    {
        var positions = prim.GetVertexAccessor("POSITION")?.AsVector3Array();
        if (positions is null || positions.Count == 0) return;

        var normals = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();
        var uvs     = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
        var colors  = prim.GetVertexAccessor("COLOR_0")?.AsVector4Array();
        var rawIdx  = prim.IndexAccessor?.AsIndicesArray();

        int n = positions.Count;
        var verts = new GpuMesh.Vertex[n];
        for (int i = 0; i < n; i++)
            verts[i] = new GpuMesh.Vertex
            {
                Position = positions[i],
                Normal   = normals is not null ? normals[i] : Vector3.UnitY,
                UV       = uvs     is not null ? new Vector2(uvs[i].X, 1f - uvs[i].Y) : Vector2.Zero,
                Color    = colors  is not null ? colors[i] : Vector4.One
            };

        uint[] idx;
        if (rawIdx is not null)
            idx = rawIdx.ToArray().Select(v => (uint)v).ToArray();
        else
        {
            idx = new uint[n];
            for (uint i = 0; i < n; i++) idx[i] = i;
        }

        var mesh = new GpuMesh(gl, verts, idx);
        var mat  = BuildMaterial(prim.Material, texMap);
        _primCount++; _vertCount += n;

        var primEntity = new Entity($"{entity.Name}_prim");
        entity.AddChild(primEntity);
        var mr = primEntity.Add(new MeshRenderer());
        mr.Mesh     = mesh;
        mr.Material = mat;
    }

    private static RMat BuildMaterial(GMat? gm, Dictionary<int, Texture2D> texMap)
    {
        var mat = new RMat
        {
            Name    = gm?.Name ?? "Material",
            Culling = RMat.CullMode.Both,
            Unlit   = true,
        };
        if (gm is null) return mat;

        var baseColor = gm.FindChannel("BaseColor");
        if (baseColor.HasValue)
        {
            mat.BaseColor = Vector4.One;

            var texIdx = baseColor.Value.Texture?.LogicalIndex;
            if (texIdx.HasValue && texMap.TryGetValue(texIdx.Value, out var tex))
                mat.Albedo = tex;
        }

        return mat;
    }
}
