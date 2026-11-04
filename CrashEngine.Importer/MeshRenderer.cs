using CrashEngine.Core;
using CrashEngine.Renderer;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Importer;

public sealed class MeshRenderer : Component, ISunCaster // Amedo
{
    public GpuMesh?  Mesh     { get; set; }
    public Material? Material { get; set; }

    public bool            IsSkinned { get; set; }
    public AnimatedObject? Skeleton  { get; set; }

    public float SelectPulse { get; set; } = 1f;

    public static int DrawCallsThisFrame = 0;

    // Amedo 2026-09-21
    public override bool IsTranslucent
    {
        get
        {
            if (Material is not { } m) return false;
            if (m.SourceShader is null && m.AlphaBlend && (m.Albedo?.IsOpaque ?? false)) return false; // Amedo 2026-09-26
            return m.AlphaBlend || !m.DepthWrite;
        }
    }

    public override int RenderOrder => Material is { SourceShader: not null } m ? (int)m.DmaChain : int.MaxValue; // Amedo 2026-09-26

    static MeshRenderer()
    {
        Scene.AfterOpaque += InstanceBatcher.Flush; // Amedo 2026-10-01
        SunShadowPass.AfterCascade += SunDepthBatcher.Flush; // Amedo
    }

    public override void OnRender(GL gl)
    {
        if (Mesh is null) return;
        var pipeline = RenderPipeline.Instance;
        if (pipeline is null) return;
        var world = Entity!.Transform.World; // Amedo 2026-10-01
        if (OffScreen(world)) return; // Amedo 2026-09-29
        if (Defer(world)) return; // Amedo 2026-10-01
        DrawNow(gl, world);
    }

    // Amedo
    public override void OnRenderDepth(GL gl)
    {
        if (Mesh is null || !SunShadowPass.InDepthPass) return;
        if (Material is not { } m || m.AlwaysOnTop || m.IgnoreDepthTest) return;
        if (IsTranslucent && m.AlphaTest <= 0f) return;
        var world = Entity!.Transform.World;
        float scale = MathF.Sqrt(MathF.Max(GameMath.LengthSquared(new Vector3(world.M11, world.M12, world.M13)),
                      MathF.Max(GameMath.LengthSquared(new Vector3(world.M21, world.M22, world.M23)), GameMath.LengthSquared(new Vector3(world.M31, world.M32, world.M33)))));
        if (!Mesh.LocalBounds(out var min, out var max))
        {
            // Amedo
            Vector3? pointCenter = null;
            float pointRadius = float.MaxValue;
            if (Mesh.BindBounds(out var bmin, out var bmax))
            {
                pointCenter = Vector3.Transform((bmin + bmax) * 0.5f, world);
                pointRadius = GameMath.Length((bmax - bmin)) * 0.55f * scale * 1.5f + 1f;
            }
            SunShadowPass.AddCaster(this, world, world.Translation, float.MaxValue, IsSkinned, IsSkinned && Under(SunShadowPass.FocusEntity), // Amedo
                                    pointCenter: pointCenter, pointRadius: pointRadius);
            return;
        }
        float radius = GameMath.Length((max - min)) * 0.55f * scale;
        bool moving = IsSkinned || m.BillboardRender || m.ClothKind > 0 || m.DeformSpeed != Vector2.Zero;
        if (moving) radius = radius * 1.5f + 1f;
        SunShadowPass.AddCaster(this, world, Vector3.Transform((min + max) * 0.5f, world), radius, still: !moving && !Mesh.Dynamic); // Amedo
    }

    // Amedo
    private bool Under(Entity? root)
    {
        for (var e = Entity; e is not null && root is not null; e = e.Parent)
            if (ReferenceEquals(e, root)) return true;
        return false;
    }

    // Amedo
    public void DrawSunDepth(GL gl, in Matrix4x4 world)
    {
        var m = Material!;
        if (InstanceBatcher.Enabled && !IsSkinned && !m.BillboardRender && !Mesh!.Dynamic)
        {
            SunDepthBatcher.Defer(Mesh, m, world);
            return;
        }
        var sh = SunShadowPass.UseDepthProgram(m.AlphaTest > 0f);
        sh.Set(sh.Cached(static s => new Uniforms(s)).StartModel, world);
        if (IsSkinned)
            sh.SetMatrixArray("BoneMatrices", Skeleton?.BoneMatrices ?? RenderPipeline.IdentityBoneMatrices);
        m.Apply(gl, sh);
        GlState.DepthMask(gl, true);
        GlState.Culling(gl, false);
        Mesh!.Draw(gl);
        m.Restore(gl, sh);
        if (IsSkinned) sh.SetMatrixArray("BoneMatrices", RenderPipeline.IdentityBoneMatrices);
        SunShadowPass.Draws++;
    }

    // Amedo 2026-10-01
    private bool Defer(in Matrix4x4 world)
    {
        if (!InstanceBatcher.Enabled || !Scene.InOpaquePass || IsSkinned || SelectPulse != 1f) return false;
        if (Material is not { } m || m.BillboardRender || m.AlwaysOnTop || m.IgnoreDepthTest || m.Layers is { Count: > 0 } || Mesh!.Dynamic) return false;
        Span<Vector4> light = stackalloc Vector4[7];
        if (GameLightingDraw.Instance(Entity!, m, light) < 0) return false;
        InstanceBatcher.Defer(this, Mesh, m, m.ShadowReceiver && GameShadowPass.MarkingReceivers, world, light);
        return true;
    }

    // Amedo 2026-10-01
    private sealed class Uniforms(TwinShaderProgram sh)
    {
        public readonly TwinShaderProgram.Slot StartModel = sh.Uniform("StartModel");
        public readonly TwinShaderProgram.Slot ShapeCenter = sh.Uniform("ShapeCenter"); // Amedo
        public readonly TwinShaderProgram.Slot ShapeHalf = sh.Uniform("ShapeHalf"); // Amedo
    }

    // Amedo 2026-10-01
    internal void DrawNow(GL gl, in Matrix4x4 world)
    {
        var pipeline = RenderPipeline.Instance!;
        var sh  = pipeline.Shader;
        var u = sh.Cached(static s => new Uniforms(s)); // Amedo
        sh.Set(u.StartModel, world); // Amedo 2026-10-01
        bool shaped = Mesh!.BindBounds(out var shapeMin, out var shapeMax); // Amedo
        sh.Set(u.ShapeCenter, shaped ? new Vector4((shapeMin + shapeMax) * 0.5f, 1f) : Vector4.Zero);
        if (shaped) sh.Set(u.ShapeHalf, (shapeMax - shapeMin) * 0.5f);
        if (IsSkinned)
            sh.SetMatrixArray("BoneMatrices", Skeleton?.BoneMatrices ?? RenderPipeline.IdentityBoneMatrices);

        var mat = Material ?? new Material();
        mat.Apply(gl, sh);
        Material.SetSelectPulse(sh, SelectPulse); // Amedo 2026-10-01
        bool gameLit = GameLightingDraw.Apply(sh, Entity!, mat); // Amedo 2026-09-26
        bool receiver = mat.ShadowReceiver && GameShadowPass.MarkingReceivers; // Amedo 2026-09-26
        if (receiver) GlState.StencilFunc(gl, StencilFunction.Always, 0, 0xFF); // Amedo 2026-09-26
        Mesh.Draw(gl);
        if (receiver) GlState.StencilFuncLater(gl, StencilFunction.Always, GameShadowPass.ReceiverBit, 0xFF); // Amedo 2026-09-26
        if (gameLit) GameLightingDraw.Reset(sh); // Amedo 2026-09-26
        mat.Restore(gl, sh);
        sh.SetLater(u.ShapeCenter, Vector4.Zero); // Amedo

        DrawCallsThisFrame++;

        if (mat.Layers is { Count: > 0 } layers) // Amedo 2026-09-26
            foreach (var layer in layers)
                DrawLayer(gl, sh, layer);
    }

    // Amedo 2026-09-29
    private bool OffScreen(in Matrix4x4 world) // Amedo 2026-10-01
    {
        if (!RenderPipeline.Culling || !RenderPipeline.ViewProjectionValid || IsSkinned) return false;
        if (!Mesh!.LocalBounds(out var min, out var max)) return false;
        var lo = new Vector3(float.PositiveInfinity);
        var hi = new Vector3(float.NegativeInfinity);
        if (!Expand(Material, min, max, ref lo, ref hi)) return false;
        if (Material?.Layers is { } layers)
            foreach (var layer in layers)
                if (!Expand(layer, min, max, ref lo, ref hi)) return false;
        return RenderPipeline.Outside(world, lo, hi) // Amedo 2026-10-01
            || (PortalClip.Enabled && PortalClip.Of(Entity!) is { } clip && clip.Hides(world, lo, hi)); // Amedo 2026-10-27
    }

    // Amedo 2026-09-29
    private static bool Expand(Material? mat, Vector3 min, Vector3 max, ref Vector3 lo, ref Vector3 hi)
    {
        if (mat is not null)
        {
            if (mat.BillboardRender) return false;
            if (mat.ClothKind > 0 && mat.ClothPhases is not null)
            {
                var amp = Vector3.Abs(mat.ClothAmplitude);
                var e = amp * Vector3.Max(Vector3.One, amp);
                min -= e;
                max += e;
            }
            if (mat.DeformSpeed != Vector2.Zero)
            {
                float dx = MathF.Abs(mat.DeformSpeed.X) * 0.1f, dy = MathF.Abs(mat.DeformSpeed.Y) * 0.1f;
                var s = new Vector3(dx, dy, dx);
                min -= Vector3.Abs(min) * s;
                max += Vector3.Abs(max) * s;
            }
        }
        lo = Vector3.Min(lo, min);
        hi = Vector3.Max(hi, max);
        return true;
    }

    // Amedo 2026-09-26
    private void DrawLayer(GL gl, TwinShaderProgram sh, Material layer)
    {
        layer.Apply(gl, sh);
        Material.SetSelectPulse(sh, SelectPulse); // Amedo 2026-10-01
        bool lit = GameLightingDraw.Apply(sh, Entity!, layer);
        bool receiver = layer.ShadowReceiver && GameShadowPass.MarkingReceivers;
        if (receiver) GlState.StencilFunc(gl, StencilFunction.Always, 0, 0xFF);
        Mesh!.Draw(gl);
        if (receiver) GlState.StencilFuncLater(gl, StencilFunction.Always, GameShadowPass.ReceiverBit, 0xFF);
        if (lit) GameLightingDraw.Reset(sh);
        layer.Restore(gl, sh);
        DrawCallsThisFrame++;
    }
}
