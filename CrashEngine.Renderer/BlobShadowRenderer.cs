using CrashEngine.Core;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Renderer;

public sealed class BlobShadowRenderer : Component
{
    public bool Visible;
    public Vector3 GroundPoint;
    public Vector3 GroundNormal = Vector3.UnitY;
    public float Radius = 0.55f;
    public float Alpha = 0.45f;

    private GpuMesh? _mesh;
    private const int Sides = 20;

    public override void OnRender(GL gl)
    {
        if (!Visible) { _mesh?.Dispose(); _mesh = null; return; }

        var up = GroundNormal;
        var right = Vector3.Normalize(Vector3.Cross(Math.Abs(up.Y) > 0.99f ? Vector3.UnitX : Vector3.UnitY, up));
        var fwd   = Vector3.Cross(up, right);
        var center = GroundPoint + up * 0.02f;

        var color = new Vector4(0f, 0f, 0f, Alpha);
        var verts = new List<GpuMesh.Vertex>();
        for (int i = 0; i < Sides; i++)
        {
            float a0 = (float)i / Sides * MathF.PI * 2f;
            float a1 = (float)(i + 1) / Sides * MathF.PI * 2f;
            var p0 = center + (right * MathF.Cos(a0) + fwd * MathF.Sin(a0)) * Radius;
            var p1 = center + (right * MathF.Cos(a1) + fwd * MathF.Sin(a1)) * Radius;
            verts.Add(new GpuMesh.Vertex { Position = center, Normal = up, UV = Vector2.Zero, Color = color });
            verts.Add(new GpuMesh.Vertex { Position = p0,     Normal = up, UV = Vector2.Zero, Color = color });
            verts.Add(new GpuMesh.Vertex { Position = p1,     Normal = up, UV = Vector2.Zero, Color = color });
        }

        _mesh?.Dispose();
        _mesh = new GpuMesh(gl, verts.ToArray(), PrimitiveType.Triangles);

        var pipeline = RenderPipeline.Instance;
        if (pipeline is null) return;
        var sh = pipeline.Shader;
        sh.Set("StartModel",                Matrix4x4.Identity);
        sh.Set("twin_material.use_texture", 0f);
        sh.Set("twin_material.double_color", 1f);
        sh.Set("twin_material.alpha_test",  0f);
        sh.Set("twin_material.alpha_blend", 1f);
        sh.Set("twin_material.env_map",     0f);
        sh.Set("twin_material.metalic_specular", 0f);
        sh.Set("twin_material.deform_speed",     Vector2.Zero);
        sh.Set("twin_material.uv_scroll_speed",  Vector2.Zero);
        sh.Set("twin_material.reflect_dist",     Vector2.Zero);
        sh.Set("twin_material.billboard_render", 0f);
        sh.Set("twin_material.two_sided_lighting", 1);
        sh.Set("twin_material.perform_fog", 0f);

        gl.Enable(EnableCap.Blend);
        gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        gl.Disable(EnableCap.CullFace);
        gl.DepthMask(false);
        _mesh.Draw(gl);
        gl.DepthMask(true);
    }

    public override void OnDestroy() => _mesh?.Dispose();
}
