using CrashEngine.Core;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Renderer;

public sealed class PositionChainRenderer : Component
{
    public List<Vector3> Points = new();
    public Vector4 Color = new(1f, 0.85f, 0.1f, 1f);
    public float Radius = 0.06f;
    public int Sides = 8;

    private GpuMesh? _mesh;

    public override void OnRender(GL gl)
    {
        if (Points.Count < 2) { _mesh?.Dispose(); _mesh = null; return; }

        var verts = new List<GpuMesh.Vertex>();
        for (int i = 0; i < Points.Count - 1; i++)
            AppendTube(verts, Points[i], Points[i + 1], Radius, Sides, Color);

        _mesh?.Dispose();
        _mesh = new GpuMesh(gl, verts.ToArray(), PrimitiveType.Triangles);

        var pipeline = RenderPipeline.Instance;
        if (pipeline is null) return;
        var sh = pipeline.Shader;
        sh.Set("StartModel",                Matrix4x4.Identity);
        sh.Set("twin_material.use_texture", 0f);
        sh.Set("twin_material.double_color", 1f);
        sh.Set("twin_material.alpha_test",  0f);
        sh.Set("twin_material.alpha_blend", 0f);
        sh.Set("twin_material.env_map",     0f);
        sh.Set("twin_material.metalic_specular", 0f);
        sh.Set("twin_material.deform_speed",     Vector2.Zero);
        sh.Set("twin_material.uv_scroll_speed",  Vector2.Zero);
        sh.Set("twin_material.reflect_dist",     Vector2.Zero);
        sh.Set("twin_material.billboard_render", 0f);
        sh.Set("twin_material.two_sided_lighting", 1);
        sh.Set("twin_material.perform_fog", 0f);

        gl.Disable(EnableCap.CullFace);
        _mesh.Draw(gl);
    }

    public override void OnDestroy() => _mesh?.Dispose();

    private static void AppendTube(List<GpuMesh.Vertex> verts, Vector3 a, Vector3 b, float radius, int sides, Vector4 color)
    {
        var axis = b - a;
        float len = axis.Length();
        if (len < 0.0001f) return;
        var dir = axis / len;

        var up = Math.Abs(Vector3.Dot(dir, Vector3.UnitY)) > 0.99f ? Vector3.UnitX : Vector3.UnitY;
        var right = Vector3.Normalize(Vector3.Cross(up, dir));
        var trueUp = Vector3.Cross(dir, right);

        var bottom = new Vector3[sides];
        var top    = new Vector3[sides];
        var normal = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float t = (float)i / sides * MathF.PI * 2f;
            var n = right * MathF.Cos(t) + trueUp * MathF.Sin(t);
            normal[i] = n;
            bottom[i] = a + n * radius;
            top[i]    = b + n * radius;
        }

        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            verts.Add(V(bottom[i], normal[i], color));
            verts.Add(V(bottom[j], normal[j], color));
            verts.Add(V(top[i],    normal[i], color));

            verts.Add(V(bottom[j], normal[j], color));
            verts.Add(V(top[j],    normal[j], color));
            verts.Add(V(top[i],    normal[i], color));
        }
    }

    private static GpuMesh.Vertex V(Vector3 pos, Vector3 normal, Vector4 col) =>
        new() { Position = pos, Normal = normal, UV = Vector2.Zero, Color = col };
}
