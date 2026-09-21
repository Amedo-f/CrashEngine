using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;

namespace CrashEngine.Importer;

public sealed class MeshRenderer : Component
{
    public GpuMesh?  Mesh     { get; set; }
    public Material? Material { get; set; }

    public bool            IsSkinned { get; set; }
    public AnimatedObject? Skeleton  { get; set; }

    public float SelectPulse { get; set; } = 1f;

    public static int DrawCallsThisFrame = 0;

    // Amedo 2026-09-21
    // renders as opaque (solid backdrop), so 2-layer water composites right (caustics over base).
    public override bool IsTranslucent
    {
        get
        {
            if (Material is not { } m) return false;
            if (m.AlphaBlend && (m.Albedo?.IsOpaque ?? false)) return false;
            return m.AlphaBlend || !m.DepthWrite;
        }
    }

    public override void OnRender(GL gl)
    {
        if (Mesh is null) return;
        var pipeline = RenderPipeline.Instance;
        if (pipeline is null) return;

        var sh  = pipeline.Shader;
        sh.Set("StartModel", Entity!.Transform.World);
        if (IsSkinned)
            sh.SetMatrixArray("BoneMatrices", Skeleton?.BoneMatrices ?? RenderPipeline.IdentityBoneMatrices);

        var mat = Material ?? new Material();
        mat.Apply(gl, sh);
        sh.Set("twin_material.select_pulse", SelectPulse);
        Mesh.Draw(gl);
        mat.Restore(gl, sh);

        DrawCallsThisFrame++;
    }
}
