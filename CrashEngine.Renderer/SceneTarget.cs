using System.Numerics;
using CrashEngine.Core;
using Silk.NET.OpenGL;

namespace CrashEngine.Renderer;

// Amedo
public sealed class SceneTarget : IDisposable
{
    private const string Vertex = """
        #version 450 core
        void main()
        {
            vec2 p = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
            gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
        }
        """;

    private const string Fragment = """
        #version 450 core
        uniform sampler2D Scene;
        uniform vec2 SourceSize;
        uniform vec2 TargetSize;
        uniform vec2 TargetOrigin;
        uniform int Taps;
        uniform float InvGamma;
        uniform float Sharpen;
        out vec4 Color;

        vec4 CatmullRom(vec2 p)
        {
            vec2 q = p - 0.5;
            vec2 f = fract(q);
            vec2 b = floor(q) + 0.5;
            vec2 w0 = f * (-0.5 + f * (1.0 - 0.5 * f));
            vec2 w1 = 1.0 + f * f * (-2.5 + 1.5 * f);
            vec2 w2 = f * (0.5 + f * (2.0 - 1.5 * f));
            vec2 w3 = f * f * (-0.5 + 0.5 * f);
            vec2 w12 = w1 + w2;
            vec2 t0 = (b - 1.0) / SourceSize, t3 = (b + 2.0) / SourceSize, t12 = (b + w2 / w12) / SourceSize;
            vec4 c = texture(Scene, vec2(t0.x, t0.y)) * w0.x * w0.y + texture(Scene, vec2(t12.x, t0.y)) * w12.x * w0.y + texture(Scene, vec2(t3.x, t0.y)) * w3.x * w0.y
                   + texture(Scene, vec2(t0.x, t12.y)) * w0.x * w12.y + texture(Scene, vec2(t12.x, t12.y)) * w12.x * w12.y + texture(Scene, vec2(t3.x, t12.y)) * w3.x * w12.y
                   + texture(Scene, vec2(t0.x, t3.y)) * w0.x * w3.y + texture(Scene, vec2(t12.x, t3.y)) * w12.x * w3.y + texture(Scene, vec2(t3.x, t3.y)) * w3.x * w3.y;
            return max(c, vec4(0.0));
        }

        void main()
        {
            vec2 ratio = SourceSize / TargetSize;
            vec2 center = (gl_FragCoord.xy - TargetOrigin) * ratio;
            vec4 c;
            if (Taps == 0) c = CatmullRom(center);
            else
            {
                vec4 sum = vec4(0.0);
                for (int j = 0; j < Taps; j++)
                    for (int i = 0; i < Taps; i++)
                    {
                        vec2 offset = ((vec2(i, j) + 0.5) / float(Taps) - 0.5) * ratio;
                        sum += texture(Scene, (center + offset) / SourceSize);
                    }
                c = sum / float(Taps * Taps);
            }
            if (Sharpen > 0.0)
            {
                vec3 n = texture(Scene, (center + vec2(0.0, ratio.y)) / SourceSize).rgb;
                vec3 s = texture(Scene, (center - vec2(0.0, ratio.y)) / SourceSize).rgb;
                vec3 w = texture(Scene, (center - vec2(ratio.x, 0.0)) / SourceSize).rgb;
                vec3 e = texture(Scene, (center + vec2(ratio.x, 0.0)) / SourceSize).rgb;
                vec3 nw = texture(Scene, (center + vec2(-ratio.x, ratio.y)) / SourceSize).rgb;
                vec3 ne = texture(Scene, (center + ratio) / SourceSize).rgb;
                vec3 sw = texture(Scene, (center - ratio) / SourceSize).rgb;
                vec3 se = texture(Scene, (center + vec2(ratio.x, -ratio.y)) / SourceSize).rgb;
                vec3 m = c.rgb;
                vec3 lo = min(min(min(n, s), min(w, e)), m);
                lo += min(lo, min(min(nw, ne), min(sw, se)));
                vec3 hi = max(max(max(n, s), max(w, e)), m);
                hi += max(hi, max(max(nw, ne), max(sw, se)));
                vec3 amp = sqrt(clamp(min(lo, 2.0 - hi) / max(hi, vec3(1e-5)), 0.0, 1.0));
                vec3 k = amp * (-1.0 / mix(8.0, 5.0, Sharpen));
                c.rgb = clamp(((n + s + w + e) * k + m) / (1.0 + 4.0 * k), 0.0, 1.0);
            }
            if (InvGamma != 1.0) c.rgb = pow(max(c.rgb, vec3(0.0)), vec3(InvGamma));
            Color = c;
        }
        """;

    public static SceneTarget? Instance { get; private set; }

    private readonly GL _gl;
    private readonly ShaderProgram _present;
    private readonly uint _vao;
    private readonly int _maxSamples, _maxSize;
    private uint _fbo, _color, _depth, _msFbo, _msColor, _msDepth;
    private int _width, _height, _samples;
    private float _gamma = 1f;
    private float _sharpen; // Amedo
    private bool _failed;
    private bool _tiled;

    public int Width => _width;
    public int Height => _height;

    private SceneTarget(GL gl)
    {
        _gl = gl;
        _present = new ShaderProgram(gl, Vertex, Fragment);
        _vao = gl.GenVertexArray();
        gl.GetInteger((GetPName)0x8D57, out _maxSamples);
        gl.GetInteger(GetPName.MaxRenderbufferSize, out _maxSize);
        _maxSamples = Math.Max(1, _maxSamples);
        _maxSize = Math.Max(1, _maxSize);
    }

    static SceneTarget() => Engine.GpuContextStarted += ForgetContext;

    private static void ForgetContext()
    {
        if (Instance is null) return;
        Instance = null;
        Engine.BeginScene = null;
        Engine.EndScene = null;
        Engine.ReuseScene = null;
    }

    public static void Install(GL gl)
    {
        if (Instance is not null) return;
        Instance = new SceneTarget(gl);
        Engine.BeginScene = Instance.Begin;
        Engine.EndScene = Instance.End;
        Engine.ReuseScene = Instance.Reuse;
    }

    public static bool ReuseHeldFrames { get; set; }

    private readonly record struct PictureKey(int W, int H, int Samples, int SettingsVersion, int ScreenW, int ScreenH,
        (int X, int Y, int W, int H)? Viewport, Matrix4x4 View, Matrix4x4 Projection, long Uploads);

    private PictureKey _lastKey;
    private int _heldFrames;
    private static int _freshFrames;

    public static void DrawFresh(int frames) => _freshFrames = Math.Max(_freshFrames, frames);

    private bool Wanted(out int w, out int h, out int samples)
    {
        var settings = GraphicsSettings.Current;
        var engine = Engine.Instance;
        w = h = samples = 0;
        if (_failed || engine.Width < 1 || engine.Height < 1) return false; // Amedo
        if (engine.ViewWidth < 1 || engine.ViewHeight < 1) return false; // Amedo
        var (rw, rh) = GraphicsSettings.SceneSize(settings.RenderHeight, engine.ViewWidth, engine.ViewHeight, engine.ScreenHeight); // Amedo
        w = Math.Clamp(rw, 1, _maxSize);
        h = Math.Clamp(rh, 1, _maxSize);
        samples = Math.Min(settings.Msaa, _maxSamples);
        return !(engine.SceneViewport is null && w == engine.Width && h == engine.Height && samples == 1 && settings.Brightness == GraphicsSettings.BrightnessDefault && settings.Sharpness == 0); // Amedo
    }

    private static PictureKey KeyFor(int w, int h, int samples)
    {
        var engine = Engine.Instance;
        var camera = RenderPipeline.Instance?.Camera;
        return new PictureKey(w, h, samples, GraphicsSettings.Version, engine.Width, engine.Height, engine.SceneViewport,
                              camera?.View ?? default, camera?.Projection ?? default, GpuUploadQueue.LastFrame);
    }

    private const float CameraNoise = 1e-5f, CameraNoiseScale = 2e-6f;

    private static bool Near(in Matrix4x4 a, in Matrix4x4 b)
    {
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                if (MathF.Abs(a[r, c] - b[r, c]) > MathF.Max(CameraNoise, CameraNoiseScale * MathF.Max(MathF.Abs(a[r, c]), MathF.Abs(b[r, c])))) return false;
        return true;
    }

    private static bool Same(in PictureKey a, in PictureKey b) =>
        a with { View = default, Projection = default } == b with { View = default, Projection = default } && Near(a.View, b.View) && Near(a.Projection, b.Projection);

    private static string Changed(PictureKey a, PictureKey b)
    {
        var parts = new List<string>();
        if ((a.W, a.H, a.Samples) != (b.W, b.H, b.Samples)) parts.Add($"scene size {a.W}x{a.H}x{a.Samples} -> {b.W}x{b.H}x{b.Samples}");
        if (a.SettingsVersion != b.SettingsVersion) parts.Add("graphics settings");
        if ((a.ScreenW, a.ScreenH) != (b.ScreenW, b.ScreenH) || a.Viewport != b.Viewport) parts.Add("screen");
        if (!Near(a.View, b.View)) parts.Add($"camera moved {a.View.Translation} -> {b.View.Translation}");
        if (!Near(a.Projection, b.Projection)) parts.Add("camera lens");
        if (a.Uploads != b.Uploads) parts.Add($"new GPU data (frame {b.Uploads})");
        return string.Join(", ", parts);
    }

    private bool Reuse(GL gl)
    {
        if (_freshFrames > 0)
        {
            _freshFrames--;
            _heldFrames = 0;
            return false;
        }
        if (!ReuseHeldFrames || _heldFrames < 2 || _fbo == 0) return false;
        if (!Wanted(out int w, out int h, out int samples) || w != _width || h != _height || samples != _samples) return false;
        if (!Same(KeyFor(w, h, samples), _lastKey)) return false;
        Present(gl);
        return true;
    }

    private bool Begin(GL gl)
    {
        if (!Wanted(out int w, out int h, out int samples)) return false;
        var settings = GraphicsSettings.Current;
        var engine = Engine.Instance;
        if (!Ensure(gl, w, h, samples)) return false;
        _gamma = settings.Gamma;
        _sharpen = settings.SharpenAmount; // Amedo
        uint fbo = samples > 1 && !_tiled ? _msFbo : _fbo;
        engine.SceneFramebuffer = fbo;
        engine.SceneWidth = w;
        engine.SceneHeight = h;
        engine.SceneSamples = samples;
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        gl.Viewport(0, 0, (uint)w, (uint)h);
        Clear(gl);
        return true;
    }

    private static void Clear(GL gl)
    {
        gl.ClearColor(0.10f, 0.10f, 0.12f, 1f);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public void ResumeAfterOffscreen(GL gl)
    {
        if (_tiled && Engine.Instance.SceneFramebuffer == _fbo) Clear(gl);
    }

    private void End(GL gl)
    {
        int w = _width, h = _height;
        if (_samples > 1 && !_tiled)
        {
            gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _msFbo);
            gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _fbo);
            gl.BlitFramebuffer(0, 0, w, h, 0, 0, w, h, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        }
        if (GlCaps.InvalidateFramebuffer) Discard(gl, _samples > 1 && !_tiled ? _msFbo : _fbo, color: _samples > 1 && !_tiled);
        var key = KeyFor(w, h, _samples);
        bool held = Engine.SceneHeld?.Invoke() == true;
        bool same = Same(key, _lastKey);
        if (held && _heldFrames >= 2 && !same && ReuseHeldFrames) Console.WriteLine($"[SceneTarget] held picture drawn again: {Changed(_lastKey, key)}");
        _heldFrames = !held ? 0 : same ? _heldFrames + 1 : 1;
        _lastKey = key;
        Present(gl);
    }

    private void Present(GL gl)
    {
        var engine = Engine.Instance;
        int w = _width, h = _height;
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, engine.TargetFramebuffer);
        if (GlCaps.InvalidateFramebuffer && engine.TargetFramebuffer == 0)
            gl.InvalidateFramebuffer(FramebufferTarget.Framebuffer, [InvalidateFramebufferAttachment.Depth, InvalidateFramebufferAttachment.Stencil]);
        var view = engine.SceneViewport ?? (0, 0, engine.Width, engine.Height); // Amedo
        int originY = engine.Height - view.Y - view.H;
        gl.Viewport(view.X, originY, (uint)view.W, (uint)view.H);
        gl.Disable(EnableCap.ScissorTest);
        gl.Disable(EnableCap.StencilTest);
        gl.ColorMask(true, true, true, true);
        GlBlend.Set(gl, false);
        GlState.DepthTest(gl, false);
        GlState.Culling(gl, false);
        float ratio = MathF.Max((float)w / view.W, (float)h / view.H); // Amedo
        _present.Use();
        _present.Set("Scene", 0);
        _present.Set("SourceSize", new Vector2(w, h));
        _present.Set("TargetSize", new Vector2(view.W, view.H)); // Amedo
        _present.Set("TargetOrigin", new Vector2(view.X, originY));
        _present.Set("Taps", ratio >= 2f ? (int)MathF.Ceiling(ratio) : GlCaps.Gles ? 1 : 0); // Amedo
        _present.Set("InvGamma", 1f / _gamma);
        _present.Set("Sharpen", _sharpen); // Amedo
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _color);
        GlState.VertexArray(gl, _vao);
        GlState.SendLater(gl);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GpuStats.Draws++;
        GlState.VertexArray(gl, 0);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        GlState.ForgetTextures();
        gl.UseProgram(0);
        gl.Viewport(0, 0, (uint)engine.Width, (uint)engine.Height); // Amedo
        GlState.DepthTest(gl, true);
        GlState.Culling(gl, true);
        GlBlend.Set(gl, true);
        engine.SceneFramebuffer = engine.TargetFramebuffer;
        engine.SceneWidth = engine.Width;
        engine.SceneHeight = engine.Height;
        engine.SceneSamples = 1;
    }

    private unsafe bool Ensure(GL gl, int w, int h, int samples)
    {
        if (_fbo != 0 && _width == w && _height == h && _samples == samples) return true;
        if (_fbo != 0)
        {
            gl.DeleteFramebuffer(_fbo);
            gl.DeleteTexture(_color);
            gl.DeleteRenderbuffer(_depth);
        }
        _fbo = gl.GenFramebuffer();
        _color = gl.GenTexture();
        _depth = gl.GenRenderbuffer();
        gl.BindTexture(TextureTarget.Texture2D, _color);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        GlState.ForgetTextures();
        bool tiled = samples > 1 && GlCaps.TiledMsaa;
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depth);
        if (tiled) GlesExtensions.RenderbufferStorageMultisample((uint)RenderbufferTarget.Renderbuffer, samples, (uint)InternalFormat.Depth24Stencil8, w, h);
        else gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)w, (uint)h);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        if (tiled) GlesExtensions.FramebufferTexture2DMultisample((uint)FramebufferTarget.Framebuffer, (uint)FramebufferAttachment.ColorAttachment0, (uint)TextureTarget.Texture2D, _color, 0, samples);
        else gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _color, 0);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depth);
        bool ok = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == GLEnum.FramebufferComplete;
        if (ok && samples > 1 && !tiled)
        {
            if (_msFbo == 0)
            {
                _msFbo = gl.GenFramebuffer();
                _msColor = gl.GenRenderbuffer();
                _msDepth = gl.GenRenderbuffer();
            }
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _msColor);
            gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, (uint)samples, InternalFormat.Rgba8, (uint)w, (uint)h);
            gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _msDepth);
            gl.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, (uint)samples, InternalFormat.Depth24Stencil8, (uint)w, (uint)h);
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, _msFbo);
            gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _msColor);
            gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _msDepth);
            ok = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == GLEnum.FramebufferComplete;
        }
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, Engine.Instance.TargetFramebuffer);
        _width = w;
        _height = h;
        _samples = samples;
        _tiled = tiled;
        Console.WriteLine($"[SceneTarget] scene {w}x{h}, MSAA {samples}x{(tiled ? " (tiled)" : "")}{(ok ? "" : " - incomplete, drawing straight to the screen")}");
        if (!ok) _failed = true;
        return ok;
    }

    private static void Discard(GL gl, uint fbo, bool color)
    {
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        Span<InvalidateFramebufferAttachment> attachments = [InvalidateFramebufferAttachment.DepthStencilAttachment, InvalidateFramebufferAttachment.ColorAttachment0];
        gl.InvalidateFramebuffer(FramebufferTarget.Framebuffer, color ? attachments : attachments[..1]);
    }

    public void Dispose()
    {
        if (_fbo != 0)
        {
            _gl.DeleteFramebuffer(_fbo);
            _gl.DeleteTexture(_color);
            _gl.DeleteRenderbuffer(_depth);
            GlState.ForgetTextures();
        }
        if (_msFbo != 0)
        {
            _gl.DeleteFramebuffer(_msFbo);
            _gl.DeleteRenderbuffer(_msColor);
            _gl.DeleteRenderbuffer(_msDepth);
        }
        _fbo = _msFbo = 0;
        _gl.DeleteVertexArray(_vao);
        _present.Dispose();
        if (Instance == this)
        {
            Instance = null;
            Engine.BeginScene = null;
            Engine.EndScene = null;
        }
    }
}
