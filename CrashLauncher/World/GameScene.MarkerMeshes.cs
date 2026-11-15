using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashLauncher;

public sealed partial class GameScene : Scene
{
    private void EnsureCubeMesh(GL gl)
    {
        if (_cubeMesh is not null) return;
        var w = Vector4.One;
        GpuMesh.Vertex V(float x, float y, float z, Vector3 n, float u, float v) =>
            new() { Position = new(x, y, z), Normal = n, UV = new(u, v), Color = w };
        var U = Vector3.UnitY;  var D = -Vector3.UnitY;
        var F = Vector3.UnitZ;  var B = -Vector3.UnitZ;
        var R = Vector3.UnitX;  var L = -Vector3.UnitX;
        _cubeMesh = new GpuMesh(gl, new GpuMesh.Vertex[]
        {
            V(-1,2,-1,U,0,0), V( 1,2,-1,U,1,0), V( 1,2, 1,U,1,1),
            V( 1,2, 1,U,1,1), V(-1,2, 1,U,0,1), V(-1,2,-1,U,0,0),
            V(-1,0,-1,D,0,0), V( 1,0, 1,D,1,1), V( 1,0,-1,D,1,0),
            V( 1,0, 1,D,1,1), V(-1,0,-1,D,0,0), V(-1,0, 1,D,0,1),
            V(-1,0,1,F,0,1), V( 1,2,1,F,1,0), V(-1,2,1,F,0,0),
            V(-1,0,1,F,0,1), V( 1,0,1,F,1,1), V( 1,2,1,F,1,0),
            V(-1,0,-1,B,1,1), V(-1,2,-1,B,1,0), V( 1,2,-1,B,0,0),
            V(-1,0,-1,B,1,1), V( 1,2,-1,B,0,0), V( 1,0,-1,B,0,1),
            V(1,0,-1,R,0,1), V(1,2, 1,R,1,0), V(1,0, 1,R,1,1),
            V(1,0,-1,R,0,1), V(1,2,-1,R,0,0), V(1,2, 1,R,1,0),
            V(-1,0,-1,L,1,1), V(-1,0, 1,L,0,1), V(-1,2, 1,L,0,0),
            V(-1,0,-1,L,1,1), V(-1,2, 1,L,0,0), V(-1,2,-1,L,1,0),
        }, PrimitiveType.Triangles);
    }

    private void EnsureCubeWireMesh(GL gl)
    {
        if (_cubeWireMesh is not null) return;
        var w = Vector4.One;
        GpuMesh.Vertex V(float x, float y, float z) =>
            new() { Position = new(x, y, z), Normal = Vector3.UnitY, UV = Vector2.Zero, Color = w };
        void Edge(List<GpuMesh.Vertex> v, (float x, float y, float z) a, (float x, float y, float z) b)
        {
            v.Add(V(a.x, a.y, a.z));
            v.Add(V(b.x, b.y, b.z));
        }
        var bl = new List<GpuMesh.Vertex>();
        Edge(bl, (-1,0,-1), ( 1,0,-1)); Edge(bl, ( 1,0,-1), ( 1,0, 1));
        Edge(bl, ( 1,0, 1), (-1,0, 1)); Edge(bl, (-1,0, 1), (-1,0,-1));
        Edge(bl, (-1,2,-1), ( 1,2,-1)); Edge(bl, ( 1,2,-1), ( 1,2, 1));
        Edge(bl, ( 1,2, 1), (-1,2, 1)); Edge(bl, (-1,2, 1), (-1,2,-1));
        Edge(bl, (-1,0,-1), (-1,2,-1)); Edge(bl, ( 1,0,-1), ( 1,2,-1));
        Edge(bl, ( 1,0, 1), ( 1,2, 1)); Edge(bl, (-1,0, 1), (-1,2, 1));
        _cubeWireMesh = new GpuMesh(gl, bl.ToArray(), PrimitiveType.Lines);
    }

    // Amedo
    private sealed record MarkerModel(GpuMesh Mesh, byte[] Pixels, int TexWidth, int TexHeight, float Height,
                                      Vector3[] Positions, uint[] Indices, Vector3 Min, Vector3 Max);
    private static readonly Vector3 MarkerModelLight = Vector3.Normalize(new Vector3(-0.35f, 0.85f, 0.6f));

    private static MarkerModel? LoadMarkerModel(GL gl, string fileName) =>
        ReadMarkerModel(fileName) is { } m ? new MarkerModel(new GpuMesh(gl, m.Vertices, m.Indices), m.Pixels, m.TexWidth, m.TexHeight, m.Height, m.Positions, m.Indices, m.Min, m.Max) : null;

    // Amedo 2026-11-15
    private static bool IsIconMarker(Entity e) => e.Children.Count > 0 && e.Children[0].Name.EndsWith("_icon");

    // Amedo 2026-11-15
    private bool TryCameraEye(out Vector3 eye)
    {
        eye = default;
        if (_camera is null || !Matrix4x4.Invert(_camera.View, out var camWorld)) return false;
        eye = camWorld.Translation;
        return true;
    }

    // Amedo 2026-11-15
    private static void TurnIconToCamera(Entity marker, Vector3 eye, bool pitch)
    {
        var icon = marker.Children[0];
        if (!Matrix4x4.Invert(marker.Transform.World, out var toLocal)) return;
        var to = Vector3.TransformNormal(eye - icon.Transform.World.Translation, toLocal);
        float flat = MathF.Sqrt(to.X * to.X + to.Z * to.Z);
        icon.Transform.Rotation = Quaternion.CreateFromYawPitchRoll(MathF.Atan2(to.X, to.Z), pitch ? -MathF.Atan2(to.Y, flat) : 0f, 0f);
    }

    // Amedo 2026-11-15
    private static bool RayHitsMarkerModel(Entity icon, MarkerModel model, Vector3 orig, Vector3 dir, out float t)
    {
        t = float.MaxValue;
        if (!Matrix4x4.Invert(icon.Transform.World, out var inv)) return false;
        var o = Vector3.Transform(orig, inv);
        var d = Vector3.TransformNormal(dir, inv);
        if (!RayAABB(o, d, model.Min, model.Max, out _)) return false;
        var pos = model.Positions; var ind = model.Indices;
        for (int i = 0; i + 2 < ind.Length; i += 3)
        {
            var a = pos[ind[i]];
            var e1 = pos[ind[i + 1]] - a;
            var e2 = pos[ind[i + 2]] - a;
            var pv = Vector3.Cross(d, e2);
            float det = Vector3.Dot(e1, pv);
            if (MathF.Abs(det) < 1e-12f) continue;
            float inv_det = 1f / det;
            var tv = o - a;
            float u = Vector3.Dot(tv, pv) * inv_det;
            if (u < 0f || u > 1f) continue;
            var qv = Vector3.Cross(tv, e1);
            float v = Vector3.Dot(d, qv) * inv_det;
            if (v < 0f || u + v > 1f) continue;
            float tt = Vector3.Dot(e2, qv) * inv_det;
            if (tt > 0f && tt < t) t = tt;
        }
        return t < float.MaxValue;
    }

    // Amedo 2026-11-12: the .glb is read once for every scene (it was read again each time a scene opened); only the GPU mesh is per scene
    private sealed record MarkerModelData(GpuMesh.Vertex[] Vertices, byte[] Pixels, int TexWidth, int TexHeight, float Height,
                                          Vector3[] Positions, uint[] Indices, Vector3 Min, Vector3 Max);
    private static readonly Dictionary<string, MarkerModelData?> MarkerModelFiles = new();

    private static MarkerModelData? ReadMarkerModel(string fileName)
    {
        lock (MarkerModelFiles)
        {
            if (!MarkerModelFiles.TryGetValue(fileName, out var data))
                MarkerModelFiles[fileName] = data = ReadMarkerModelFile(fileName);
            return data;
        }
    }

    private static MarkerModelData? ReadMarkerModelFile(string fileName)
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Models", fileName);
        if (!System.IO.File.Exists(path)) return null;
        var parts = CrashEngine.Assets.ModelImporter.ExtractRawSubmeshes(path);
        if (parts is not { Count: > 0 }) return null;
        var verts = new List<GpuMesh.Vertex>();
        var idx = new List<uint>();
        byte[]? px = null; int tw = 0, th = 0;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in parts)
        {
            uint baseIndex = (uint)verts.Count;
            for (int i = 0; i < p.Positions.Length; i++)
            {
                float shade = 0.55f + 0.45f * MathF.Max(0f, Vector3.Dot(p.Normals[i], MarkerModelLight));
                verts.Add(new GpuMesh.Vertex { Position = p.Positions[i], Normal = p.Normals[i], UV = p.UVs[i], Color = new Vector4(shade, shade, shade, 1f) });
                minY = MathF.Min(minY, p.Positions[i].Y); maxY = MathF.Max(maxY, p.Positions[i].Y);
            }
            foreach (uint i in p.Indices) idx.Add(baseIndex + i);
            if (px is null && p.DiffusePixelsRGBA is { } d && p.TexWidth > 0 && p.TexHeight > 0) { px = d; tw = p.TexWidth; th = p.TexHeight; }
        }
        if (px is null || maxY <= minY) return null;
        var positions = new Vector3[verts.Count];
        var lo = new Vector3(float.MaxValue); var hi = new Vector3(float.MinValue);
        for (int i = 0; i < verts.Count; i++) { positions[i] = verts[i].Position; lo = Vector3.Min(lo, positions[i]); hi = Vector3.Max(hi, positions[i]); }
        return new MarkerModelData(verts.ToArray(), px, tw, th, maxY - minY, positions, idx.ToArray(), lo, hi);
    }

    // Amedo 2026-09-19
    private GpuMesh? _lightIconQuad;
    private void EnsureLightIconQuad(GL gl)
    {
        if (_lightIconQuad is not null) return;
        var col = Vector4.One;
        GpuMesh.Vertex V(float x, float y, float u, float v, Vector3 n) => new() { Position = new(x, y, 0f), Normal = n, UV = new(u, v), Color = col };
        var F = Vector3.UnitZ; var B = -Vector3.UnitZ;
        const float s = 1.6f;
        _lightIconQuad = new GpuMesh(gl, new GpuMesh.Vertex[]
        {
            V(-s,-s, 0f,1f, F), V(s,-s, 1f,1f, F), V(s,s, 1f,0f, F),
            V(-s,-s, 0f,1f, F), V(s,s, 1f,0f, F), V(-s,s, 0f,0f, F),
            V(-s,-s, 0f,1f, B), V(s,s, 1f,0f, B), V(s,-s, 1f,1f, B),
            V(-s,-s, 0f,1f, B), V(-s,s, 0f,0f, B), V(s,s, 1f,0f, B),
        }, PrimitiveType.Triangles);
    }

    private void EnsureQuadMesh(GL gl)
    {
        if (_quadMesh is not null) return;
        var w = Vector4.One;
        GpuMesh.Vertex V(float x, float y, Vector3 n) => new() { Position = new(x, y, 0f), Normal = n, UV = Vector2.Zero, Color = w };
        var F = Vector3.UnitZ; var B = -Vector3.UnitZ;
        _quadMesh = new GpuMesh(gl, new GpuMesh.Vertex[]
        {
            V(-1,-1,F), V(1,-1,F), V(1,1,F), V(-1,-1,F), V(1,1,F), V(-1,1,F),
            V(-1,-1,B), V(1,1,B), V(1,-1,B), V(-1,-1,B), V(-1,1,B), V(1,1,B),
        }, PrimitiveType.Triangles);
    }

    private void EnsureBoxZoneMesh(GL gl)
    {
        if (_boxZoneMesh is not null) return;
        var w = Vector4.One;
        GpuMesh.Vertex V(float x, float y, float z, Vector3 n) => new() { Position = new(x, y, z), Normal = n, UV = Vector2.Zero, Color = w };
        var U = Vector3.UnitY;  var D = -Vector3.UnitY;
        var F = Vector3.UnitZ;  var B = -Vector3.UnitZ;
        var R = Vector3.UnitX;  var L = -Vector3.UnitX;
        _boxZoneMesh = new GpuMesh(gl, new GpuMesh.Vertex[]
        {
            V(-1,1,-1,U), V( 1,1,-1,U), V( 1,1, 1,U),
            V( 1,1, 1,U), V(-1,1, 1,U), V(-1,1,-1,U),
            V(-1,-1,-1,D), V( 1,-1, 1,D), V( 1,-1,-1,D),
            V( 1,-1, 1,D), V(-1,-1,-1,D), V(-1,-1, 1,D),
            V(-1,-1,1,F), V( 1,1,1,F), V(-1,1,1,F),
            V(-1,-1,1,F), V( 1,-1,1,F), V( 1,1,1,F),
            V(-1,-1,-1,B), V(-1,1,-1,B), V( 1,1,-1,B),
            V(-1,-1,-1,B), V( 1,1,-1,B), V( 1,-1,-1,B),
            V(1,-1,-1,R), V(1,1, 1,R), V(1,-1, 1,R),
            V(1,-1,-1,R), V(1,1,-1,R), V(1,1, 1,R),
            V(-1,-1,-1,L), V(-1,-1, 1,L), V(-1,1, 1,L),
            V(-1,-1,-1,L), V(-1,1, 1,L), V(-1,1,-1,L),
        }, PrimitiveType.Triangles);
    }
}
