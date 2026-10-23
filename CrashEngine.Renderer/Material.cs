using CrashEngine.Core;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Renderer;

public sealed class Material
{
    public enum CullMode  { Front, Back, Both }
    public enum BlendMode { Standard, Additive, Subtractive }

    public string     Name      { get; set; } = "Default";
    public Texture2D? Albedo    { get; set; }
    public Vector4    BaseColor { get; set; } = Vector4.One;
    public CullMode   Culling   { get; set; } = CullMode.Both;

    public float     DoubleColor     { get; set; } = 1.0f;
    public float     AlphaTest       { get; set; } = 0.0f;
    public bool      AlphaBlend      { get; set; } = false;
    public BlendMode Blend           { get; set; } = BlendMode.Standard;
    public float     MetalicSpecular { get; set; } = 0.0f;
    public float     EnvMap          { get; set; } = 0.0f;
    public Vector2   DeformSpeed     { get; set; } = Vector2.Zero;
    public Vector2   ReflectDist     { get; set; } = Vector2.Zero;
    public bool      BillboardRender { get; set; } = false;
    public Vector2   UvScrollSpeed   { get; set; } = Vector2.Zero;
    public Vector2   UvScrollMode    { get; set; } = Vector2.Zero; // Amedo 2026-09-26
    public Vector2   UvScrollPhase   { get; set; } = Vector2.Zero; // Amedo 2026-09-26
    public bool      FogEnabled      { get; set; } = true;
    public bool      DepthWrite      { get; set; } = true;
    public bool      AlwaysOnTop     { get; set; } = false;
    public bool      IgnoreDepthTest { get; set; } = false;
    public bool      GameLit         { get; set; } = false; // Amedo 2026-09-26
    public bool      ShadowReceiver  { get; set; } = false; // Amedo 2026-09-26
    public List<Material>? Layers    { get; set; } // Amedo 2026-09-26
    public uint      DmaChain        { get; set; } // Amedo 2026-09-26
    public int       ClothKind       { get; set; } // Amedo 2026-09-26
    public int       ClothMode       { get; set; } // Amedo 2026-09-26
    public float     ClothSpeed      { get; set; } // Amedo 2026-09-26
    public Vector3   ClothAmplitude  { get; set; } // Amedo 2026-09-26
    public Vector3[]? ClothPhases    { get; set; } // Amedo 2026-09-26
    public System.Runtime.CompilerServices.StrongBox<double>? ClothClock { get; set; } // Amedo

    // Amedo
    public bool      GsMipmaps       { get; set; }
    public float     GsLodK          { get; set; }
    public float     GsLodL          { get; set; } = 1f;

    public bool      Unlit           { get => DoubleColor <= 1.0f; set => DoubleColor = value ? 1.0f : 2.0f; }

    public bool      UnlitToggledByUser { get; set; } = false;
    public Vector2   UvOffset        { get; set; } = Vector2.Zero;
    public Vector3?  LocalCenter     { get; set; }
    public float     BoundingRadius  { get; set; }
    public string    ShaderType      { get; set; } = "";

    public object?   SourceShader    { get; set; }

    public object?   SourceSubModel  { get; set; }

    private bool _blendWasOn;

    // Amedo 2026-10-01
    private sealed class Uniforms(TwinShaderProgram sh)
    {
        public readonly TwinShaderProgram.Slot UseTexture    = sh.Uniform("twin_material.use_texture");
        public readonly TwinShaderProgram.Slot DoubleColor   = sh.Uniform("twin_material.double_color");
        public readonly TwinShaderProgram.Slot DeformSpeed   = sh.Uniform("twin_material.deform_speed");
        public readonly TwinShaderProgram.Slot Billboard     = sh.Uniform("twin_material.billboard_render");
        public readonly TwinShaderProgram.Slot AlphaTest     = sh.Uniform("twin_material.alpha_test");
        public readonly TwinShaderProgram.Slot EnvMap        = sh.Uniform("twin_material.env_map");
        public readonly TwinShaderProgram.Slot Metalic       = sh.Uniform("twin_material.metalic_specular");
        public readonly TwinShaderProgram.Slot UvScrollSpeed = sh.Uniform("twin_material.uv_scroll_speed");
        public readonly TwinShaderProgram.Slot UvScrollMode  = sh.Uniform("twin_material.uv_scroll_mode");
        public readonly TwinShaderProgram.Slot UvScrollPhase = sh.Uniform("twin_material.uv_scroll_phase");
        public readonly TwinShaderProgram.Slot ClothKind     = sh.Uniform("twin_material.cloth_kind");
        public readonly TwinShaderProgram.Slot ClothMode     = sh.Uniform("twin_material.cloth_mode");
        public readonly TwinShaderProgram.Slot ClothPhase    = sh.Uniform("twin_material.cloth_phase");
        public readonly TwinShaderProgram.Slot ClothAmp      = sh.Uniform("twin_material.cloth_amp");
        public readonly TwinShaderProgram.Slot ReflectDist   = sh.Uniform("twin_material.reflect_dist");
        public readonly TwinShaderProgram.Slot TwoSided      = sh.Uniform("twin_material.two_sided_lighting");
        public readonly TwinShaderProgram.Slot Fog           = sh.Uniform("twin_material.perform_fog");
        public readonly TwinShaderProgram.Slot AlphaBlend    = sh.Uniform("twin_material.alpha_blend");
        public readonly TwinShaderProgram.Slot BlendFunc     = sh.Uniform("twin_material.blend_func");
        public readonly TwinShaderProgram.Slot BaseColor     = sh.Uniform("twin_material.base_color");
        public readonly TwinShaderProgram.Slot SelectPulse   = sh.Uniform("twin_material.select_pulse");
        public readonly TwinShaderProgram.Slot GsLod         = sh.Uniform("twin_material.gs_lod"); // Amedo
    }

    private static Uniforms U(TwinShaderProgram sh) => sh.Cached(static s => new Uniforms(s)); // Amedo 2026-10-01

    public void Apply(GL gl, TwinShaderProgram sh)
    {
        if (Albedo != null) Albedo.Bind(0);

        var u = U(sh); // Amedo 2026-10-01
        sh.Set(u.UseTexture,    Albedo != null ? 1f : 0f);
        sh.Set(u.DoubleColor,   DoubleColor);
        sh.Set(u.DeformSpeed,   DeformSpeed);
        sh.Set(u.Billboard,     BillboardRender ? 1f : 0f);
        sh.Set(u.AlphaTest,     AlphaTest);
        sh.Set(u.EnvMap,        EnvMap);
        sh.Set(u.Metalic,       MetalicSpecular);
        sh.Set(u.UvScrollSpeed, UvScrollSpeed);
        sh.Set(u.UvScrollMode,  UvScrollMode); // Amedo 2026-09-26
        sh.Set(u.UvScrollPhase, UvScrollPhase); // Amedo 2026-09-26
        sh.Set(u.ClothKind,     ClothPhases is null ? 0f : ClothKind); // Amedo 2026-09-26
        if (ClothKind > 0 && ClothPhases is { } clothPhases) // Amedo 2026-09-26
        {
            double t = ClothClock?.Value ?? RenderPipeline.Instance?.TimeSeconds ?? 0.0; // Amedo
            sh.Set(u.ClothMode,  (float)ClothMode);
            sh.Set(u.ClothPhase, (float)(2.0 * Math.PI * ClothSpeed * t % (2.0 * Math.PI)));
            sh.Set(u.ClothAmp,   ClothAmplitude);
            sh.SetVector3Array("twin_material.cloth_p0[0]", clothPhases);
        }
        sh.Set(u.ReflectDist,   ReflectDist);
        sh.Set(u.TwoSided,      Culling == CullMode.Both ? 1 : 0);
        sh.Set(u.Fog,           FogEnabled ? 1f : 0f);
        sh.Set(u.AlphaBlend,    AlphaBlend ? 1f : 0f);
        sh.Set(u.BlendFunc,     AlphaBlend ? (int)Blend : 0); // Amedo 2026-09-26
        sh.Set(u.BaseColor,     BaseColor);
        sh.Set(u.SelectPulse,   1f);
        sh.Set(u.GsLod,         GsLod()); // Amedo

        _blendWasOn = GlBlend.IsOn(gl); // Amedo 2026-09-30
        if (AlphaBlend)
        {
            GlBlend.Set(gl, true); // Amedo 2026-09-30
            switch (Blend)
            {
                case BlendMode.Additive:
                    gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
                    gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.One,
                                         BlendingFactor.SrcAlpha, BlendingFactor.One);
                    break;
                case BlendMode.Subtractive:
                    gl.BlendEquation(BlendEquationModeEXT.FuncReverseSubtract);
                    gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.One,
                                         BlendingFactor.SrcAlpha, BlendingFactor.One);
                    break;
                default:
                    gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
                    gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha,
                                         BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
                    break;
            }
        }
        else GlBlend.Set(gl, false); // Amedo 2026-09-30

        if (Culling == CullMode.Both) GlState.Culling(gl, false); // Amedo 2026-10-01
        else { GlState.Culling(gl, true); GlState.CullFace(gl, Culling == CullMode.Back ? TriangleFace.Front : TriangleFace.Back); }

        GlState.DepthMask(gl, DepthWrite && !AlphaBlend); // Amedo 2026-10-01
        GlState.DepthFunc(gl, DepthFunction.Lequal);
        if (IgnoreDepthTest) GlState.DepthTest(gl, false);

        if (AlwaysOnTop)
        {
            GlState.PolygonOffset(gl, true); // Amedo 2026-10-01
            gl.PolygonOffset(-1f, -2f);
        }
    }

    public void Restore(GL gl, TwinShaderProgram sh)
    {
        GlState.DepthMask(gl, true); // Amedo 2026-10-01
        GlState.DepthFunc(gl, DepthFunction.Lequal);
        if (IgnoreDepthTest) GlState.DepthTest(gl, true);
        if (AlwaysOnTop) GlState.PolygonOffset(gl, false);
        if (Culling != CullMode.Both) GlState.Culling(gl, false);
        GlBlend.Set(gl, _blendWasOn); // Amedo 2026-09-30
        var u = U(sh); // Amedo 2026-10-01
        sh.Set(u.UvScrollMode,  Vector2.Zero); // Amedo 2026-09-26
        sh.Set(u.UvScrollPhase, Vector2.Zero); // Amedo 2026-09-26
        sh.Set(u.ClothKind,     0f); // Amedo 2026-09-26
        sh.Set(u.BlendFunc,     0); // Amedo 2026-09-26
    }

    // Amedo
    private Vector3 GsLod()
    {
        if (GraphicsSettings.Current.Filtering != TextureFiltering.Original || Albedo is not { HighRes: false } tex) return Vector3.Zero;
        return GsMipmaps && tex.GameMipLevels > 0 ? new Vector3(1f, GsLodK, GsLodL) : new Vector3(2f, 0f, 0f);
    }

    // Amedo 2026-10-01
    public static void SetSelectPulse(TwinShaderProgram sh, float pulse) => sh.Set(U(sh).SelectPulse, pulse);
}
