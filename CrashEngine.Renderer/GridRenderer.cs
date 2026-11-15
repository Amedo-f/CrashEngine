using CrashEngine.Core;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Renderer;

public sealed class GridRenderer : Component
{
    private GpuMesh? _mesh;

    public override void OnLoad()
    {
        var verts  = new List<GpuMesh.Vertex>();
        int extent = 100;

        var minor = new Vector4(0.25f, 0.25f, 0.25f, 1f);
        var major = new Vector4(0.45f, 0.45f, 0.45f, 1f);
        var axisX = new Vector4(0.85f, 0.15f, 0.15f, 1f);
        var axisZ = new Vector4(0.15f, 0.30f, 0.85f, 1f);
        var axisY = new Vector4(0.15f, 0.75f, 0.15f, 1f);

        for (int z = -extent; z <= extent; z++)
        {
            if (z == 0) continue;
            var col = (z % 10 == 0) ? major : minor;
            verts.Add(V(new(-extent, 0, z), col));
            verts.Add(V(new( extent, 0, z), col));
        }

        for (int x = -extent; x <= extent; x++)
        {
            if (x == 0) continue;
            var col = (x % 10 == 0) ? major : minor;
            verts.Add(V(new(x, 0, -extent), col));
            verts.Add(V(new(x, 0,  extent), col));
        }

        verts.Add(V(new(-extent, 0, 0), axisX));
        verts.Add(V(new( extent, 0, 0), axisX));

        verts.Add(V(new(0, 0, -extent), axisZ));
        verts.Add(V(new(0, 0,  extent), axisZ));

        verts.Add(V(new(0, 0,   0), axisY));
        verts.Add(V(new(0, 20,  0), axisY));

        _mesh = new GpuMesh(Engine.Instance.GL, verts.ToArray(), PrimitiveType.Lines);
    }

    public override void OnRender(GL gl)
    {
        if (_mesh is null) return;
        var pipeline = RenderPipeline.Instance;
        if (pipeline is null) return;

        Material.ApplyVertexColorsOnly(pipeline.Shader, Matrix4x4.Identity); // Amedo 2026-11-15

        GlState.Culling(gl, false); // Amedo 2026-10-01
        _mesh.Draw(gl);
    }

    public override void OnDestroy() => _mesh?.Dispose();

    private static GpuMesh.Vertex V(Vector3 pos, Vector4 col) =>
        new() { Position = pos, Normal = Vector3.UnitY, UV = Vector2.Zero, Color = col };
}
