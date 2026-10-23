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

    public int Width => _width;
    public int Height => _height;
    public int Samples => _samples;

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

    public static void Install(GL gl)
    {
        if (Instance is not null) return;
        Instance = new SceneTarget(gl);
        Engine.BeginScene = Instance.Begin;
        Engine.EndScene = Instance.End;
    }

    private bool Begin(GL gl)
    {
        var settings = GraphicsSettings.Current;
        var engine = Engine.Instance;
        if (_failed || engine.Width < 1 || engine.Height < 1) return false; // Amedo
        if (engine.ViewWidth < 1 || engine.ViewHeight < 1) return false; // Amedo
        var (rw, rh) = GraphicsSettings.SceneSize(settings.RenderHeight, engine.ViewWidth, engine.ViewHeight, engine.ScreenHeight); // Amedo
        int w = Math.Clamp(rw, 1, _maxSize), h = Math.Clamp(rh, 1, _maxSize);
        int samples = Math.Min(settings.Msaa, _maxSamples);
        if (engine.SceneViewport is null && w == engine.Width && h == engine.Height && samples == 1 && settings.Brightness == GraphicsSettings.BrightnessDefault && settings.Sharpness == 0) return false; // Amedo
        if (!Ensure(gl, w, h, samples)) return false;
        _gamma = settings.Gamma;
        _sharpen = settings.SharpenAmount; // Amedo
        uint fbo = samples > 1 ? _msFbo : _fbo;
        engine.SceneFramebuffer = fbo;
        engine.SceneWidth = w;
        engine.SceneHeight = h;
        engine.SceneSamples = samples;
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        gl.Viewport(0, 0, (uint)w, (uint)h);
        gl.ClearColor(0.10f, 0.10f, 0.12f, 1f);
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        return true;
    }

    private void End(GL gl)
    {
        var engine = Engine.Instance;
        int w = _width, h = _height;
        if (_samples > 1)
        {
            gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _msFbo);
            gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _fbo);
            gl.BlitFramebuffer(0, 0, w, h, 0, 0, w, h, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        }
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, engine.TargetFramebuffer);
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
        _present.Set("Taps", ratio >= 2f ? (int)MathF.Ceiling(ratio) : 0); // Amedo
        _present.Set("InvGamma", 1f / _gamma);
        _present.Set("Sharpen", _sharpen); // Amedo
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, _color);
        GlState.VertexArray(gl, _vao);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GpuStats.Draws++;
        GlState.VertexArray(gl, 0);
        gl.BindTexture(TextureTarget.Texture2D, 0);
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
        if (_fbo == 0)
        {
            _fbo = gl.GenFramebuffer();
            _color = gl.GenTexture();
            _depth = gl.GenRenderbuffer();
        }
        gl.BindTexture(TextureTarget.Texture2D, _color);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depth);
        gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Depth24Stencil8, (uint)w, (uint)h);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _color, 0);
        gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depth);
        bool ok = gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == GLEnum.FramebufferComplete;
        if (ok && samples > 1)
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
        Console.WriteLine($"[SceneTarget] scene {w}x{h}, MSAA {samples}x{(ok ? "" : " - incomplete, drawing straight to the screen")}");
        if (!ok) _failed = true;
        return ok;
    }

    public void Dispose()
    {
        if (_fbo != 0)
        {
            _gl.DeleteFramebuffer(_fbo);
            _gl.DeleteTexture(_color);
            _gl.DeleteRenderbuffer(_depth);
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
