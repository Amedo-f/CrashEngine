using CrashEngine.Core;
using Silk.NET.OpenGL;
using System.Numerics;

namespace CrashEngine.Renderer;

// Amedo
public static partial class SunShadowPass
{
    public const int MapSize = 2048;
    public const int Cascades = 4;
    public const int TextureUnit = 8;
    public const float NearRadius = 8f;
    public const float SharpRadius = 150f; // Amedo
    public const int FocusLayer = Cascades; // Amedo
    public const float FocusRadius = 1.25f, FocusLift = 0.75f, FocusReach = 15f; // Amedo

    private static readonly int[] Period = { 1, 2, 4, 8 };
    private static readonly int[] Phase = { 0, 1, 2, 0 };

    public static bool Active { get; private set; }
    public static bool InDepthPass { get; private set; }
    public static int LastDraws { get; private set; }
    public static int Draws;
    public static int DebugView { get; set; }
    public static Vector3 SunDirection { get; private set; } = Vector3.UnitY;
    public static event Action<GL>? AfterCascade;
    public static Entity? FocusEntity { get; set; } // Amedo: the player (set by the simulation); null = nearest character to the view centre
    public static Vector3? LevelDirection { get; set; } // Amedo: the level's first directional light (towards the light), filled during RenderDepth

    private static uint _fbo, _depth;
    private static bool _failed;
    private static readonly Matrix4x4[] _viewProjection = new Matrix4x4[Cascades + 1];
    private static Vector4 _texels;
    private static int _frame, _settingsVersion = -1;
    private static Scene? _lastScene;

    private static TwinShaderProgram[] _programs = [];
    private static TwinShaderProgram? _current;

    // Amedo: one scene walk per frame fills this; each cascade only tests the bounding spheres
    private struct Caster
    {
        public ISunCaster Who;
        public Matrix4x4 World;
        public Vector3 Center;
        public float Radius;
        public bool Character;
        public bool Player;
        public bool Still;
        public Vector3 PointCenter;
        public float PointRadius;
    }

    private static Caster[] _casters = new Caster[1024];
    private static int _casterCount;

    public static void AddCaster(ISunCaster who, in Matrix4x4 world, Vector3 center, float radius, bool character = false, bool player = false, bool still = false,
                                 Vector3? pointCenter = null, float pointRadius = -1f)
    {
        if (_casterCount == _casters.Length) Array.Resize(ref _casters, _casters.Length * 2);
        ref var c = ref _casters[_casterCount++];
        c.Who = who;
        c.World = world;
        c.Center = center;
        c.Radius = radius;
        c.Character = character;
        c.Player = player;
        c.Still = still;
        c.PointCenter = pointCenter ?? center;
        c.PointRadius = pointRadius >= 0f ? pointRadius : radius;
    }

    // Amedo: the character nearest the camera gets its own tiny, very sharp layer (self-shadow: hair on the back)
    private static bool FindFocus(Vector3 eyePos, Vector3 forward, out Vector3 focus)
    {
        focus = default;
        float best = float.MaxValue;
        bool found = false, player = false;
        for (int k = 0; k < _casterCount; k++)
        {
            ref var c = ref _casters[k];
            if (!c.Character || (player && !c.Player)) continue;
            var point = c.Center + Vector3.UnitY * FocusLift;
            var to = point - eyePos;
            float along = Vector3.Dot(to, forward);
            if (to.LengthSquared() > FocusReach * FocusReach || along <= 0f) continue;
            float score = c.Player ? to.LengthSquared() : (to - forward * along).LengthSquared();
            if (c.Player && !player) best = float.MaxValue;
            if (score >= best) continue;
            best = score;
            focus = point;
            found = true;
            player |= c.Player;
        }
        return found;
    }

    private static void DrawCasters(GL gl, in Matrix4x4 view, float halfSize, float depth, float minRadius)
    {
        for (int k = 0; k < _casterCount; k++)
        {
            ref var c = ref _casters[k];
            if (c.Radius < minRadius) continue;
            var p = Vector3.Transform(c.Center, view);
            if (MathF.Abs(p.X) > halfSize + c.Radius || MathF.Abs(p.Y) > halfSize + c.Radius || p.Z < -depth - c.Radius) continue;
            c.Who.DrawSunDepth(gl, c.World);
        }
    }

    // Amedo: [0] = solid (empty fragment shader, fastest), [1] = alpha-tested (leaves, grass)
    public static TwinShaderProgram UseDepthProgram(bool cutout)
    {
        if (_pointMode && _pointProgram is { } point) // Amedo
        {
            if (!ReferenceEquals(point, _current))
            {
                point.Use();
                _current = point;
            }
            point.Set("PointCutout", cutout ? 1 : 0);
            return point;
        }
        var program = _programs[cutout ? 1 : 0];
        if (!ReferenceEquals(program, _current))
        {
            program.Use();
            _current = program;
        }
        return program;
    }

    private sealed class Uniforms(TwinShaderProgram sh)
    {
        public readonly TwinShaderProgram.Slot Shadows = sh.Uniform("SunShadows");
        public readonly TwinShaderProgram.Slot Count = sh.Uniform("SunCascades");
        public readonly TwinShaderProgram.Slot Dir = sh.Uniform("SunDir");
        public readonly TwinShaderProgram.Slot Strength = sh.Uniform("SunStrength");
        public readonly TwinShaderProgram.Slot Penumbra = sh.Uniform("SunPenumbra");
        public readonly TwinShaderProgram.Slot ShapeSoft = sh.Uniform("ShapeSoft");
        public readonly TwinShaderProgram.Slot Texels = sh.Uniform("SunTexels");
        public readonly TwinShaderProgram.Slot Debug = sh.Uniform("SunDebug");
        public readonly TwinShaderProgram.Slot Focus = sh.Uniform("SunFocus");
        public readonly TwinShaderProgram.Slot FocusTexel = sh.Uniform("SunFocusTexel");
    }

    public static bool Wanted => GraphicsSettings.Current.Shadows == ShadowMode.Sun && GameShadowPass.ShowShadows;

    public static Vector3 DirectionFor(GraphicsSettings s)
    {
        float az = s.SunAzimuth * MathF.PI / 180f, el = s.SunElevation * MathF.PI / 180f;
        return Vector3.Normalize(new Vector3(MathF.Cos(el) * MathF.Sin(az), MathF.Sin(el), MathF.Cos(el) * MathF.Cos(az)));
    }

    public static float RadiusOf(int cascade, float range)
    {
        if (cascade == Cascades - 1) return MathF.Max(range, NearRadius);
        float sharp = Math.Clamp(range, NearRadius, SharpRadius);
        return NearRadius * MathF.Pow(sharp / NearRadius, cascade / (float)(Cascades - 2));
    }

    public static void Render(GL gl, TwinShaderProgram sh, Scene scene, Matrix4x4 cameraView)
    {
        var u = sh.Cached(static s => new Uniforms(s));
        Active = Wanted && EnsureTarget(gl);
        LastDraws = Draws;
        Draws = 0;
        if (!Active) return;

        var settings = GraphicsSettings.Current;
        bool all = _lastScene != scene || _settingsVersion != GraphicsSettings.Version;
        _lastScene = scene;
        _settingsVersion = GraphicsSettings.Version;
        _frame++;
        SunDirection = DirectionFor(settings);
        Matrix4x4.Invert(cameraView, out var eye);
        var eyePos = new Vector3(eye.M41, eye.M42, eye.M43);
        var forward = -Vector3.Normalize(new Vector3(eye.M31, eye.M32, eye.M33));

        Span<int> viewport = stackalloc int[4];
        gl.GetInteger(GetPName.Viewport, viewport);
        gl.GetInteger(GetPName.DrawFramebufferBinding, out int target);
        bool scissor = gl.IsEnabled(EnableCap.ScissorTest);
        bool stencil = gl.IsEnabled(EnableCap.StencilTest);
        if (scissor) gl.Disable(EnableCap.ScissorTest);
        if (stencil) gl.Disable(EnableCap.StencilTest);

        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        gl.Viewport(0, 0, MapSize, MapSize);
        gl.Enable(EnableCap.DepthClamp);
        GlState.DepthTest(gl, true);
        GlState.PolygonOffset(gl, true);
        gl.PolygonOffset(1.5f, 2f);
        sh.Set(u.Shadows, 0);
        foreach (var depthShader in _programs)
        {
            depthShader.Use();
            depthShader.Set("Time", RenderPipeline.Instance?.ShaderTime ?? 0f);
            depthShader.Set("EyePosition", eyePos);
            depthShader.SetMatrixArray("BoneMatrices", RenderPipeline.IdentityBoneMatrices);
        }
        _current = null;
        InDepthPass = true;
        try
        {
            _casterCount = 0;
            LevelDirection = null;
            BeginPointLights(); // Amedo
            scene.RenderDepth(gl);
            if (settings.SunFromLevel && (SkySun() ?? LevelDirection) is { } level) SunDirection = AboveHorizon(level); // Amedo
            for (int i = 0; i < Cascades; i++)
            {
                float radius = RadiusOf(i, settings.SunRange);
                _texels[i] = radius * 2f / MapSize;
                if (!all && _frame % Period[i] != Phase[i]) continue;
                var (view, projection, depth) = Fit(eyePos + forward * radius * 0.5f, radius, _texels[i]);
                _viewProjection[i] = view * projection;
                gl.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, _depth, 0, i);
                GlState.DepthMask(gl, true);
                gl.Clear(ClearBufferMask.DepthBufferBit);
                foreach (var depthShader in _programs)
                {
                    depthShader.Use();
                    depthShader.Set("StartView", view);
                    depthShader.Set("StartProjection", projection);
                }
                _current = _programs[1];
                long tris = GpuStats.Triangles;
                int draws = Draws;
                DrawCasters(gl, view, radius, depth, i == 0 ? 0f : _texels[i] * 1.5f);
                AfterCascade?.Invoke(gl);
                if (DebugView != 0 && _frame % 120 < 8)
                    Console.WriteLine($"[sun] frame {_frame} cascade {i} radius {radius:0.0}: {_casterCount} casters, {Draws - draws} draws, {(GpuStats.Triangles - tris) / 1000}k tris");
            }
            // Amedo
            _focus = FindFocus(eyePos, forward, out var focusPoint);
            if (_focus)
            {
                var (view, projection, depth) = Fit(focusPoint, FocusRadius, FocusTexel);
                _viewProjection[FocusLayer] = view * projection;
                gl.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, _depth, 0, FocusLayer);
                GlState.DepthMask(gl, true);
                gl.Clear(ClearBufferMask.DepthBufferBit);
                foreach (var depthShader in _programs)
                {
                    depthShader.Use();
                    depthShader.Set("StartView", view);
                    depthShader.Set("StartProjection", projection);
                }
                _current = _programs[1];
                long tris = GpuStats.Triangles;
                int draws = Draws;
                DrawCasters(gl, view, FocusRadius, depth, 0f);
                AfterCascade?.Invoke(gl);
                if (DebugView != 0 && _frame % 120 < 8)
                    Console.WriteLine($"[sun] frame {_frame} focus at {focusPoint}: {Draws - draws} draws, {(GpuStats.Triangles - tris) / 1000}k tris");
            }
            if (_pointProgram is null && PointWanted) EnsurePointTarget(gl); // Amedo
            if (_pointProgram is { } pointProgram)
            {
                pointProgram.Use();
                pointProgram.Set("Time", RenderPipeline.Instance?.ShaderTime ?? 0f);
                pointProgram.Set("EyePosition", eyePos);
                pointProgram.SetMatrixArray("BoneMatrices", RenderPipeline.IdentityBoneMatrices);
            }
            RenderPointLights(gl, eyePos);
        }
        finally
        {
            InDepthPass = false;
            Array.Clear(_casters, 0, _casterCount);
            _casterCount = 0;
            _current = null;
            sh.Use();
            GlState.PolygonOffset(gl, false);
            gl.Disable(EnableCap.DepthClamp);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)target);
            gl.Viewport(viewport[0], viewport[1], (uint)viewport[2], (uint)viewport[3]);
            if (scissor) gl.Enable(EnableCap.ScissorTest);
            if (stencil) gl.Enable(EnableCap.StencilTest);
        }

        gl.ActiveTexture(Silk.NET.OpenGL.TextureUnit.Texture0 + TextureUnit);
        gl.BindTexture(TextureTarget.Texture2DArray, _depth);
        gl.ActiveTexture(Silk.NET.OpenGL.TextureUnit.Texture0);
        sh.SetMatrixArray("SunViewProj", _viewProjection);
        sh.Set(u.Count, Cascades);
        sh.Set(u.Dir, SunDirection);
        sh.Set(u.Strength, settings.SunShadowAmount);
        sh.Set(u.Penumbra, settings.SunPenumbra);
        sh.Set(u.ShapeSoft, settings.ShapeSoft); // Amedo
        sh.Set(u.Texels, _texels);
        sh.Set(u.Debug, DebugView);
        sh.Set(u.Focus, _focus ? 1 : 0); // Amedo
        sh.Set(u.FocusTexel, FocusTexel);
        SetPointUniforms(gl, sh); // Amedo
    }

    private static bool _focus; // Amedo
    private const float FocusTexel = FocusRadius * 2f / MapSize; // Amedo

    // Amedo: the sun (or moon) drawn in the skydome = a sky mesh bunched around one direction (the domes surround the
    // viewer, so their vertex directions cancel out). Read from the mesh data itself, so any level / swapped sky follows.
    public static Vector3? SkySunDirection => SkySun();

    private static object? _skyList;
    private static GpuMesh? _skyFirst, _skyLast;
    private static int _skyCount = -1;
    private static Matrix4x4 _skyRotation;
    private static Vector3? _skyDir;

    private static Vector3? SkySun()
    {
        var draws = RenderPipeline.SkydomeOverride ?? RenderPipeline.SkydomeDraws;
        var first = draws.Count > 0 ? draws[0].Mesh : null;
        var last = draws.Count > 0 ? draws[^1].Mesh : null;
        if (ReferenceEquals(draws, _skyList) && draws.Count == _skyCount && ReferenceEquals(first, _skyFirst) && ReferenceEquals(last, _skyLast)
            && RenderPipeline.SkydomeRotation == _skyRotation) return _skyDir;
        _skyList = draws;
        _skyCount = draws.Count;
        _skyFirst = first;
        _skyLast = last;
        _skyRotation = RenderPipeline.SkydomeRotation;
        _skyDir = null;
        float best = SunBunching;
        foreach (var (mesh, _) in draws)
        {
            if (mesh.RaycastVertices is not { Length: >= 3 } verts) continue;
            var sum = Vector3.Zero;
            foreach (ref readonly var v in verts.AsSpan())
            {
                var p = new Vector3(-v.Position.X, v.Position.Y, v.Position.Z);
                if (p.LengthSquared() > 1e-12f) sum += Vector3.Normalize(p);
            }
            float bunched = sum.Length() / verts.Length;
            if (bunched <= best) continue;
            best = bunched;
            _skyDir = Vector3.Normalize(Vector3.TransformNormal(Vector3.Normalize(sum), _skyRotation));
        }
        return _skyDir;
    }

    private const float SunBunching = 0.9f; // Amedo: average vertex direction length; 0.9 ~ within 25 degrees (domes/clouds spread 55+)
    private static readonly float MinSunHeight = MathF.Sin(5f * MathF.PI / 180f);

    private static Vector3 AboveHorizon(Vector3 dir)
    {
        if (dir.Y >= MinSunHeight) return dir;
        var flat = new Vector3(dir.X, 0f, dir.Z);
        flat = flat.LengthSquared() > 1e-12f ? Vector3.Normalize(flat) : Vector3.UnitZ;
        return Vector3.Normalize(flat * MathF.Sqrt(1f - MinSunHeight * MinSunHeight) + Vector3.UnitY * MinSunHeight);
    }

    public static void EnableSampling(TwinShaderProgram sh, bool on) =>
        sh.Set(sh.Cached(static s => new Uniforms(s)).Shadows, on && Active ? 1 : 0);

    private static (Matrix4x4 View, Matrix4x4 Projection, float Depth) Fit(Vector3 center, float radius, float texel)
    {
        var up = MathF.Abs(SunDirection.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY;
        var rotation = CameraComponent.CreateLookAtRH(Vector3.Zero, -SunDirection, up);
        Matrix4x4.Invert(rotation, out var back);
        var local = Vector3.Transform(center, rotation);
        local.X = MathF.Floor(local.X / texel) * texel;
        local.Y = MathF.Floor(local.Y / texel) * texel;
        var snapped = Vector3.Transform(local, back);
        float toSun = radius * 2f + 500f, below = radius * 2f + 50f;
        var view = CameraComponent.CreateLookAtRH(snapped + SunDirection * toSun, snapped, up);
        return (view, CameraComponent.CreateOrthographicRH(radius * 2f, 1f, 0f, toSun + below), toSun + below);
    }

    private static unsafe bool EnsureTarget(GL gl)
    {
        if (_fbo != 0) return true;
        if (_failed) return false;
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Shaders");
            _programs = [new TwinShaderProgram(gl, dir, "MainPass.vert", "SunDepth.frag"), new TwinShaderProgram(gl, dir, "MainPass.vert", "SunDepthCutout.frag")];
        }
        catch (Exception ex)
        {
            Console.WriteLine("[SunShadowPass] depth shaders failed, sun shadows off: " + ex.Message);
            _failed = true;
            return false;
        }
        _depth = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2DArray, _depth);
        gl.TexImage3D(TextureTarget.Texture2DArray, 0, InternalFormat.DepthComponent32f, MapSize, MapSize, Cascades + 1, 0, PixelFormat.DepthComponent, PixelType.Float, null);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
        gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
        gl.BindTexture(TextureTarget.Texture2DArray, 0);

        gl.GetInteger(GetPName.DrawFramebufferBinding, out int previous);
        _fbo = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        gl.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, _depth, 0, 0);
        gl.DrawBuffer(DrawBufferMode.None);
        gl.ReadBuffer(ReadBufferMode.None);
        bool ok = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == GLEnum.FramebufferComplete;
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)previous);
        if (!ok)
        {
            Console.WriteLine("[SunShadowPass] depth target incomplete, sun shadows off");
            gl.DeleteFramebuffer(_fbo);
            gl.DeleteTexture(_depth);
            _fbo = _depth = 0;
            _failed = true;
        }
        return ok;
    }
}
