using CrashEngine.Core;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Renderer;

public sealed class RenderPipeline : Component
{
    public static RenderPipeline? Instance { get; private set; }

    public Vector3 FogColor  { get; set; } = new(0.58f, 0.72f, 0.88f);
    public bool    DebugRed  { get; set; } = false;

    public const int MaxDirLights = 4;
    public Vector3 AmbientLightColor { get; set; } = Vector3.One;
    public List<(Vector3 Color, Vector3 Direction)> DirectionalLights { get; set; } = new();

    public bool LitEnabled { get; set; } = false;

    // Amedo 2026-09-16
    public bool Wireframe { get; set; } = false;

    public TwinShaderProgram Shader { get; private set; } = null!;
    public CameraComponent   Camera { get; set; }        = null!;

    public static readonly List<(GpuMesh Mesh, Material Mat)> SkydomeDraws = new();
    public float SkydomeScale = 50f;

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
    }

    public override void OnUpdate() => _time += EngineTime.Delta;

    public override void OnRender(GL gl)
    {
        gl.Disable(EnableCap.Blend);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Lequal);
        gl.Enable(EnableCap.DepthTest);

        gl.ClearColor(FogColor.X, FogColor.Y, FogColor.Z, 1f);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        Camera ??= Entity.Scene!.FindFirst<CameraComponent>()!;
        if (Camera is null) return;

        Shader.Use();

        Shader.Set("Time",        _time);
        Shader.Set("Resolution",  new Vector2(Engine.Instance.Width, Engine.Instance.Height));
        Shader.Set("EyePosition", Camera.Transform.Position);
        Shader.Set("EyeDirection", Camera.Transform.Forward);
        Shader.Set("FogColor",    FogColor);
        Shader.Set("Fov",         Camera.Fov * MathF.PI / 180f);
        Shader.Set("Aspect",      Engine.Instance.Height == 0 ? 1f
                                  : (float)Engine.Instance.Width / Engine.Instance.Height);

        Shader.Set("AmbientLightColor", AmbientLightColor);
        int dirCount = Math.Min(DirectionalLights.Count, MaxDirLights);
        for (int i = 0; i < MaxDirLights; i++)
        {
            var (c, d) = i < dirCount ? DirectionalLights[i] : (Vector3.Zero, Vector3.UnitY);
            Shader.Set($"DirLightColor[{i}]", c);
            Shader.Set($"DirLightDir[{i}]",   d);
        }
        Shader.Set("DirLightCount", dirCount);
        Shader.Set("LitEnabled", LitEnabled ? 1 : 0);

        Shader.Set("StartView",       Camera.View);
        Shader.Set("StartProjection", Camera.Projection);
        Shader.SetMatrixArray("BoneMatrices", IdentityBones);

        Shader.Set("Diffuse",     Vector4.One);
        Shader.Set("Opacity",     1f);
        Shader.Set("FlipY",       0f);
        Shader.Set("DiffuseOnly", DebugRed ? 1f : 0f);

        DrawSkydome(gl);

        gl.PolygonMode(GLEnum.FrontAndBack, Wireframe ? GLEnum.Line : GLEnum.Fill);
    }

    private void DrawSkydome(GL gl)
    {
        if (SkydomeDraws.Count == 0) return;

        var model = Matrix4x4.CreateScale(new Vector3(-SkydomeScale, SkydomeScale, SkydomeScale))
                  * Matrix4x4.CreateTranslation(Camera.Transform.Position);
        Shader.Set("StartModel", model);

        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.CullFace);
        foreach (var (mesh, mat) in SkydomeDraws)
        {
            mat.Apply(gl, Shader);
            mesh.Draw(gl);
            mat.Restore(gl, Shader);
        }
        gl.Enable(EnableCap.DepthTest);
    }

    public override void OnDestroy() => Shader?.Dispose();
}
