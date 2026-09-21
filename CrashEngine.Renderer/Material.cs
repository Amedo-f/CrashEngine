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
    public bool      FogEnabled      { get; set; } = true;
    public bool      DepthWrite      { get; set; } = true;
    public bool      AlwaysOnTop     { get; set; } = false;
    public bool      IgnoreDepthTest { get; set; } = false;

    // Amedo 2026-09-21 -- faithful PS2 GS ALPHA blend (A-B)*C+D + alpha-test method, resolved at decode
    public BlendingFactor        BlendSrcFactor     = BlendingFactor.SrcAlpha;
    public BlendingFactor        BlendDstFactor     = BlendingFactor.OneMinusSrcAlpha;
    public BlendEquationModeEXT  BlendEquation      = BlendEquationModeEXT.FuncAdd;
    public float                 BlendConstantAlpha = 1f;
    public int                   AlphaTestFunc      = 5; // 0=NEVER 1=ALWAYS 2=LESS 3=LEQUAL 4=EQUAL 5=GEQUAL 6=GREATER 7=NOTEQUAL

    public bool      Unlit           { get => DoubleColor <= 1.0f; set => DoubleColor = value ? 1.0f : 2.0f; }

    public bool      UnlitToggledByUser { get; set; } = false;
    public Vector2   UvOffset        { get; set; } = Vector2.Zero;
    public Vector3?  LocalCenter     { get; set; }
    public float     BoundingRadius  { get; set; }
    public string    ShaderType      { get; set; } = "";

    public object?   SourceShader    { get; set; }

    public object?   SourceSubModel  { get; set; }

    private bool _blendWasOn;

    public void Apply(GL gl, TwinShaderProgram sh)
    {
        if (Albedo != null) Albedo.Bind(0);

        sh.Set("twin_material.use_texture",        Albedo != null ? 1f : 0f);
        sh.Set("twin_material.double_color",       DoubleColor);
        sh.Set("twin_material.deform_speed",       DeformSpeed);
        sh.Set("twin_material.billboard_render",   BillboardRender ? 1f : 0f);
        sh.Set("twin_material.alpha_test",         AlphaTest);
        sh.Set("twin_material.env_map",            EnvMap);
        sh.Set("twin_material.metalic_specular",   MetalicSpecular);
        sh.Set("twin_material.uv_scroll_speed",    UvScrollSpeed);
        sh.Set("twin_material.reflect_dist",       ReflectDist);
        sh.Set("twin_material.two_sided_lighting", Culling == CullMode.Both ? 1 : 0);
        sh.Set("twin_material.perform_fog",        FogEnabled ? 1f : 0f);
        sh.Set("twin_material.alpha_blend",        AlphaBlend ? 1f : 0f);
        sh.Set("twin_material.base_color",         BaseColor);
        sh.Set("twin_material.select_pulse",       1f);
        sh.Set("twin_material.alpha_test_func",    AlphaTestFunc); // Amedo 2026-09-21

        _blendWasOn = gl.IsEnabled(EnableCap.Blend);
        if (AlphaBlend)
        {
            // Amedo 2026-09-21 -- use the GS-derived (A-B)*C+D factors resolved at decode
            gl.Enable(EnableCap.Blend);
            gl.BlendColor(0f, 0f, 0f, BlendConstantAlpha);
            gl.BlendEquation(BlendEquation);
            gl.BlendFuncSeparate(BlendSrcFactor, BlendDstFactor, BlendSrcFactor, BlendDstFactor);
        }
        else gl.Disable(EnableCap.Blend);

        if (Culling == CullMode.Both) gl.Disable(EnableCap.CullFace);
        else { gl.Enable(EnableCap.CullFace); gl.CullFace(Culling == CullMode.Back ? TriangleFace.Front : TriangleFace.Back); }

        gl.DepthMask(DepthWrite);
        gl.DepthFunc(DepthFunction.Lequal);
        if (IgnoreDepthTest) gl.Disable(EnableCap.DepthTest);

        if (AlwaysOnTop)
        {
            gl.Enable(EnableCap.PolygonOffsetFill);
            gl.Enable(EnableCap.PolygonOffsetLine);
            gl.PolygonOffset(-1f, -2f);
        }
    }

    public void Restore(GL gl, TwinShaderProgram sh)
    {
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Lequal);
        if (IgnoreDepthTest) gl.Enable(EnableCap.DepthTest);
        if (AlwaysOnTop) { gl.Disable(EnableCap.PolygonOffsetFill); gl.Disable(EnableCap.PolygonOffsetLine); }
        if (Culling != CullMode.Both) gl.Disable(EnableCap.CullFace);
        if (_blendWasOn) gl.Enable(EnableCap.Blend); else gl.Disable(EnableCap.Blend);
    }
}
