using CrashEngine.Core;
using Silk.NET.OpenGL;

namespace CrashEngine.Renderer;

public sealed class MeshRenderer : Component
{
    public GpuMesh? Mesh     { get; set; }
    public Material Material { get; set; } = new();

    public float SelectPulse { get; set; } = 1f;

    public override bool IsTranslucent => Material.AlphaBlend || !Material.DepthWrite;

    public override void OnRender(GL gl)
    {
        if (Mesh is null) return;
        var pipeline = RenderPipeline.Instance;
        if (pipeline is null) return;

        var sh = pipeline.Shader;
        sh.Set("StartModel", Transform.World);

        Material.Apply(gl, sh);
        sh.Set("twin_material.select_pulse", SelectPulse);
        Mesh.Draw(gl);
        Material.Restore(gl, sh);
    }
}
