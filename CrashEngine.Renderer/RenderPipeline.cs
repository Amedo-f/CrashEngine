using CrashEngine.Core;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Renderer;

public sealed class RenderPipeline : Component
{
    public static RenderPipeline? Instance { get; private set; }

    public Vector3 FogColor  { get; set; } = new(0.58f, 0.72f, 0.88f);
    public int     FogIndex  { get; set; } = -1; // Amedo 2026-09-26
    private Texture2D? _fogPalette; // Amedo 2026-09-26
    private int _fogPaletteIndex = -1; // Amedo 2026-09-26
    public bool    DebugRed  { get; set; } = false;

    public const int MaxDirLights = 4;
    public Vector3 AmbientLightColor { get; set; } = Vector3.One;
    public List<(Vector3 Color, Vector3 Direction)> DirectionalLights { get; set; } = new();

    // Amedo 2026-09-16
    public bool Wireframe { get; set; } = false;

    public TwinShaderProgram Shader { get; private set; } = null!;
    public CameraComponent   Camera { get; set; }        = null!;

    public static readonly List<(GpuMesh Mesh, Material Mat)> SkydomeDraws = new();
    public float SkydomeScale = 50f;

    // Amedo 2026-09-29
    public static IReadOnlyList<(GpuMesh Mesh, Material Mat)>? SkydomeOverride { get; set; }
    public static Matrix4x4 SkydomeRotation { get; set; } = Matrix4x4.Identity;
    public int? FogOverride { get; set; }
    public Vector3 FogOverrideColor { get; set; }
    private byte[]? _fogShown, _fogFrom, _fogTo, _fogMix;
    private int _fogToIndex = -1;
    private Vector3 _fogShownColor, _fogFromColor;
    private float _fogFadeStart, _fogFadeTime;

    // Amedo 2026-09-29
    public void FadeFog(float seconds)
    {
        if (_fogShown is null || seconds <= 0f)
        {
            _fogFrom = null;
            return;
        }
        _fogFrom = (byte[])_fogShown.Clone();
        _fogFromColor = _fogShownColor;
        _fogFadeStart = _time;
        _fogFadeTime = seconds;
    }

    private GL    _gl = null!;
    private float _time;

    public const int MaxBones = 64;
    public static readonly Matrix4x4[] IdentityBoneMatrices = BuildIdentityBones();
    private static Matrix4x4[] IdentityBones => IdentityBoneMatrices;
    private static Matrix4x4[] BuildIdentityBones()
    {
        var a = new Matrix4x4[MaxBones];
        for (int i = 0; i < a.Length; i++) a[i] = Matrix4x4.Identity;
        return a;
    }

    public override void OnLoad()
    {
        Instance = this;
        _gl      = Engine.Instance.GL;
        var dir  = Path.Combine(AppContext.BaseDirectory, "Shaders");
        Shader   = new TwinShaderProgram(_gl, dir, "MainPass.vert", "MainPass.frag");
        SceneTarget.Install(_gl); // Amedo
        Texture2D.FollowSettings();
        Engine.UiStateReset = () => { GlState.Invalidate(); GlBlend.Assume(false); }; // Amedo
    }

    public override void OnUpdate()
    {
        _time += EngineTime.Delta;
        if (ShaderClockStopped?.Invoke() != true) _shaderTime += EngineTime.Delta; // Amedo
    }
    public float TimeSeconds => _time; // Amedo 2026-09-26

    private float _shaderTime; // Amedo
    public float ShaderTime => _shaderTime; // Amedo
    public static Func<bool>? ShaderClockStopped { get; set; } // Amedo

    public static int Frame { get; private set; } // Amedo 2026-09-29
    public static Matrix4x4 ViewProjection { get; private set; }
    public static bool ViewProjectionValid { get; private set; }
    public static bool Culling { get; set; } = true;
    public static bool SkyAfterOpaque { get; set; } // Amedo 2026-10-27
    private bool _skyPending, _skyHooked;

    // Amedo 2026-09-29
    public static bool Outside(Matrix4x4 world, Vector3 min, Vector3 max)
    {
        var m = world * ViewProjection;
        var d = max - min;
        var b = new Vector4(m.M41, m.M42, m.M43, m.M44) + new Vector4(m.M11, m.M12, m.M13, m.M14) * min.X
              + new Vector4(m.M21, m.M22, m.M23, m.M24) * min.Y + new Vector4(m.M31, m.M32, m.M33, m.M34) * min.Z;
        var ax = new Vector4(m.M11, m.M12, m.M13, m.M14) * d.X;
        var ay = new Vector4(m.M21, m.M22, m.M23, m.M24) * d.Y;
        var az = new Vector4(m.M31, m.M32, m.M33, m.M34) * d.Z;
        static bool Behind(in Vector4 plane, in Vector4 b, in Vector4 ax, in Vector4 ay, in Vector4 az) =>
            Vector4.Dot(plane, b) + MathF.Max(0f, Vector4.Dot(plane, ax)) + MathF.Max(0f, Vector4.Dot(plane, ay))
                                  + MathF.Max(0f, Vector4.Dot(plane, az)) < 0f;
        return Behind(new Vector4(1f, 0f, 0f, 1f), b, ax, ay, az) || Behind(new Vector4(-1f, 0f, 0f, 1f), b, ax, ay, az)
            || Behind(new Vector4(0f, 1f, 0f, 1f), b, ax, ay, az) || Behind(new Vector4(0f, -1f, 0f, 1f), b, ax, ay, az)
            || Behind(new Vector4(0f, 0f, 1f, 1f), b, ax, ay, az) || Behind(new Vector4(0f, 0f, -1f, 1f), b, ax, ay, az);
    }
    private static readonly string[] DirColorNames = ["DirLightColor[0]", "DirLightColor[1]", "DirLightColor[2]", "DirLightColor[3]"]; // Amedo 2026-09-29
    private static readonly string[] DirNames = ["DirLightDir[0]", "DirLightDir[1]", "DirLightDir[2]", "DirLightDir[3]"];

    public override void OnRender(GL gl)
    {
        Frame++; // Amedo 2026-09-29
        MeshPool.Maintain(gl); // Amedo
        ViewProjectionValid = false;
        GlBlend.Force(gl, false); // Amedo 2026-09-30
        GlState.Invalidate(); // Amedo 2026-10-01
        GlState.DepthMask(gl, true);
        GlState.DepthFunc(gl, DepthFunction.Lequal);
        GlState.DepthTest(gl, true);

        int fogIndex = FogOverride ?? FogIndex; // Amedo 2026-09-29
        var fogColor = FogOverride is null ? FogColor : FogOverrideColor;
        float fade = _fogFrom is null ? 1f : Math.Clamp((_time - _fogFadeStart) / _fogFadeTime, 0f, 1f);
        if (fade >= 1f) _fogFrom = null;
        if (_fogFrom is not null) fogColor = Vector3.Lerp(_fogFromColor, fogColor, fade);
        if (fogIndex >= 0) // Amedo 2026-09-26
        {
            if (_fogToIndex != fogIndex) // Amedo 2026-09-29
            {
                _fogTo = GameFog.BuildPalette(fogIndex);
                _fogToIndex = fogIndex;
            }
            byte[]? rgba = null;
            if (_fogFrom is not null)
            {
                _fogMix ??= new byte[_fogTo!.Length];
                GameFog.Blend_1AE530(_fogMix, _fogTo!, _fogFrom, fade);
                rgba = _fogMix;
                _fogPaletteIndex = -1;
            }
            else if (_fogPalette is null || _fogPaletteIndex != fogIndex)
            {
                rgba = _fogTo;
                _fogPaletteIndex = fogIndex;
            }
            if (rgba is not null)
            {
                if (_fogPalette is null) _fogPalette = new Texture2D(gl, rgba, 256, 1);
                else _fogPalette.UpdateData(rgba, 256, 1);
                _fogShown ??= new byte[rgba.Length];
                System.Buffer.BlockCopy(rgba, 0, _fogShown, 0, rgba.Length);
            }
            _fogShownColor = fogColor;
            _fogPalette!.Bind(7);
            GlState.ActiveTexture(gl, 0);
        }
        var clear = fogIndex >= 0 && _fogShown is { } shown ? GameFog.ClearUnderFog_19F1C8(shown) : fogColor; // Amedo
        gl.ClearColor(clear.X, clear.Y, clear.Z, 1f);
        gl.StencilMask(0xFF); // Amedo 2026-09-26
        gl.ClearStencil(GameShadowPass.ReceiverBit); // Amedo 2026-09-26
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit); // Amedo 2026-09-26

        // Amedo 2026-09-26
        GameShadowPass.MarkingReceivers = !Wireframe && GameShadowPass.MarksReceivers(Entity);
        GameShadowPass.ReceiversMarked = GameShadowPass.MarkingReceivers; // Amedo
        if (GameShadowPass.MarkingReceivers)
        {
            gl.Enable(EnableCap.StencilTest);
            GlState.StencilFunc(gl, StencilFunction.Always, GameShadowPass.ReceiverBit, 0xFF);
            gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace);
            gl.StencilMask(GameShadowPass.ReceiverBit);
        }
        else gl.Disable(EnableCap.StencilTest);

        Camera ??= Entity.Scene!.FindFirst<CameraComponent>()!;
        if (Camera is null) return;

        Shader.Use();

        Shader.Set("Time",        _shaderTime); // Amedo
        Shader.Set("Resolution",  new Vector2(Engine.Instance.SceneWidth, Engine.Instance.SceneHeight)); // Amedo
        Shader.Set("MipBias",     GraphicsSettings.Current.MipBias); // Amedo
        Shader.Set("EyePosition", Camera.Transform.Position);
        Shader.Set("EyeDirection", Camera.Transform.Forward);
        Shader.Set("FogColor",    fogColor); // Amedo 2026-09-29
        Shader.Set("FogAmount",   fogIndex >= 0 ? 1f : 0f); // Amedo 2026-09-26
        Shader.Set("Fov",        Camera.Fov * MathF.PI / 180f);
        Shader.Set("Aspect",      Engine.Instance.ViewHeight == 0 ? 1f
                                  : (float)Engine.Instance.ViewWidth / Engine.Instance.ViewHeight); // Amedo

        Shader.Set("AmbientLightColor", AmbientLightColor);
        int dirCount = Math.Min(DirectionalLights.Count, MaxDirLights);
        for (int i = 0; i < MaxDirLights; i++)
        {
            var (c, d) = i < dirCount ? DirectionalLights[i] : (Vector3.Zero, Vector3.UnitY);
            Shader.Set(DirColorNames[i], c); // Amedo 2026-09-29
            Shader.Set(DirNames[i],      d);
        }
        Shader.Set("DirLightCount", dirCount);
        Shader.Set("ObjLit", 0); // Amedo 2026-09-26

        if (!Wireframe && Entity.Scene is { } scene) SunShadowPass.Render(gl, Shader, scene, Camera.View); // Amedo
        SunShadowPass.EnableSampling(Shader, false); // Amedo

        Shader.Set("StartView",       Camera.View);
        Shader.Set("StartProjection", Camera.Projection);
        ViewProjection = Camera.View * Camera.Projection; // Amedo 2026-09-29
        ViewProjectionValid = true;
        Shader.SetMatrixArray("BoneMatrices", IdentityBones);

        Shader.Set("Diffuse",     Vector4.One);
        Shader.Set("Opacity",     1f);
        Shader.Set("FlipY",       0f);
        Shader.Set("DiffuseOnly", DebugRed ? 1f : 0f);

        if (SkyAfterOpaque) DeferSkydome(); // Amedo 2026-10-27
        else DrawSkydome(gl);
        SunShadowPass.EnableSampling(Shader, true); // Amedo

        if (GlCaps.PolygonMode) gl.PolygonMode(GLEnum.FrontAndBack, Wireframe ? GLEnum.Line : GLEnum.Fill);
    }

    // Amedo 2026-10-27
    private void DeferSkydome()
    {
        _skyPending = true;
        if (_skyHooked) return;
        Scene.AfterOpaque += DrawDeferredSkydome;
        _skyHooked = true;
    }

    // Amedo 2026-10-27
    private void DrawDeferredSkydome(GL gl)
    {
        if (!_skyPending || Camera is null) return;
        _skyPending = false;
        Shader.Use();
        SunShadowPass.EnableSampling(Shader, false);
        Shader.Set("ObjLit", 0);
        Shader.SetMatrixArray("BoneMatrices", IdentityBones);
        Shader.Set("Diffuse",     Vector4.One);
        Shader.Set("Opacity",     1f);
        Shader.Set("FlipY",       0f);
        Shader.Set("DiffuseOnly", DebugRed ? 1f : 0f);
        gl.DepthRange(1f, 1f);
        DrawSkydome(gl, behindScene: true);
        gl.DepthRange(0f, 1f);
        SunShadowPass.EnableSampling(Shader, true);
    }

    private void DrawSkydome(GL gl, bool behindScene = false)
    {
        var draws = SkydomeOverride ?? SkydomeDraws; // Amedo 2026-09-29
        if (draws.Count == 0) return;

        var model = Matrix4x4.CreateScale(new Vector3(-SkydomeScale, SkydomeScale, SkydomeScale))
                  * SkydomeRotation // Amedo 2026-09-29
                  * Matrix4x4.CreateTranslation(Camera.Transform.Position);
        Shader.Set("StartModel", model);
        Shader.Set("StartProjection", Camera.SkyProjection); // Amedo 2026-09-29

        GlState.DepthTest(gl, behindScene); // Amedo 2026-10-01
        GlState.Culling(gl, false);
        foreach (var (mesh, mat) in draws) // Amedo 2026-09-29
        {
            mat.Apply(gl, Shader);
            if (behindScene) // Amedo 2026-10-27
            {
                GlState.DepthTest(gl, true);
                GlState.DepthMask(gl, false);
            }
            mesh.Draw(gl);
            mat.Restore(gl, Shader);
        }
        GlState.DepthTest(gl, true); // Amedo 2026-10-01
        Shader.Set("StartProjection", Camera.Projection); // Amedo 2026-09-29
    }

    public override void OnDestroy()
    {
        Shader?.Dispose();
        _fogPalette?.Dispose(); // Amedo 2026-09-26
        if (_skyHooked) Scene.AfterOpaque -= DrawDeferredSkydome; // Amedo 2026-10-27
    }
}
