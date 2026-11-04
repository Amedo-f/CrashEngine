using System.Numerics;
using Silk.NET.OpenGL;

namespace CrashEngine.Renderer;

// Amedo
public sealed class MovieScaler : IDisposable
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
        uniform sampler2D Source;
        uniform vec2 SourceSize;
        uniform vec2 TargetSize;
        out vec4 Color;

        float Lanczos(float x)
        {
            x = abs(x);
            if (x < 1e-5) return 1.0;
            if (x >= 2.0) return 0.0;
            float px = 3.14159265 * x;
            return 2.0 * sin(px) * sin(px * 0.5) / (px * px);
        }

        vec3 Texel(ivec2 p)
        {
            p = clamp(p, ivec2(0), ivec2(SourceSize) - 1);
            return texelFetch(Source, p, 0).rgb;
        }

        void main()
        {
            vec2 c = gl_FragCoord.xy * SourceSize / TargetSize - 0.5;
            vec2 b = floor(c);
            vec2 f = c - b;
            ivec2 ib = ivec2(b);
            float wx[4];
            float wy[4];
            for (int i = 0; i < 4; i++)
            {
                wx[i] = Lanczos(float(i - 1) - f.x);
                wy[i] = Lanczos(float(i - 1) - f.y);
            }
            vec3 sum = vec3(0.0);
            float weight = 0.0;
            vec3 lo = vec3(1.0);
            vec3 hi = vec3(0.0);
            for (int j = 0; j < 4; j++)
                for (int i = 0; i < 4; i++)
                {
                    vec3 t = Texel(ib + ivec2(i - 1, j - 1));
                    float w = wx[i] * wy[j];
                    sum += t * w;
                    weight += w;
                    if (i >= 1 && i <= 2 && j >= 1 && j <= 2)
                    {
                        lo = min(lo, t);
                        hi = max(hi, t);
                    }
                }
            vec3 col = sum / weight;
            col = mix(col, clamp(col, lo, hi), 0.6);
            Color = vec4(col, 1.0);
        }
        """;

    private readonly GL _gl;
    private ShaderProgram? _program;
    private uint _vao, _fbo, _color;
    private int _width, _height;

    public MovieScaler(GL gl) => _gl = gl;

    public uint Output => _color;

    // Amedo
    public uint Scale(uint source, int sourceWidth, int sourceHeight, int width, int height)
    {
        if (width <= 0 || height <= 0 || sourceWidth <= 0 || sourceHeight <= 0) return 0;
        var gl = _gl;
        _program ??= new ShaderProgram(gl, Vertex, Fragment);
        if (_vao == 0) _vao = gl.GenVertexArray();
        if (_width != width || _height != height) Allocate(width, height);
        Span<int> viewport = stackalloc int[4];
        gl.GetInteger(GetPName.Viewport, viewport);
        gl.GetInteger(GetPName.DrawFramebufferBinding, out int framebuffer);
        bool scissor = gl.IsEnabled(EnableCap.ScissorTest), blend = gl.IsEnabled(EnableCap.Blend);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        gl.Viewport(0, 0, (uint)width, (uint)height);
        gl.Disable(EnableCap.ScissorTest);
        gl.Disable(EnableCap.Blend);
        gl.Disable(EnableCap.DepthTest);
        gl.Disable(EnableCap.CullFace);
        _program.Use();
        gl.ActiveTexture(TextureUnit.Texture0);
        gl.BindTexture(TextureTarget.Texture2D, source);
        _program.Set("Source", 0);
        _program.Set("SourceSize", new Vector2(sourceWidth, sourceHeight));
        _program.Set("TargetSize", new Vector2(width, height));
        gl.BindVertexArray(_vao);
        GlState.SendLater(gl);
        gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        gl.BindVertexArray(0);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        gl.UseProgram(0);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)framebuffer);
        gl.Viewport(viewport[0], viewport[1], (uint)viewport[2], (uint)viewport[3]);
        if (scissor) gl.Enable(EnableCap.ScissorTest);
        if (blend) gl.Enable(EnableCap.Blend);
        GlState.Invalidate();
        return _color;
    }

    private unsafe void Allocate(int width, int height)
    {
        Release();
        var gl = _gl;
        _width = width;
        _height = height;
        _color = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _color);
        gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        gl.BindTexture(TextureTarget.Texture2D, 0);
        GlState.ForgetTextures();
        _fbo = gl.GenFramebuffer();
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _color, 0);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    // Amedo
    public unsafe byte[] ReadPixels()
    {
        var pixels = new byte[_width * _height * 4];
        var gl = _gl;
        gl.GetInteger(GetPName.ReadFramebufferBinding, out int read);
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fbo);
        fixed (byte* p = pixels) gl.ReadPixels(0, 0, (uint)_width, (uint)_height, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, (uint)read);
        return pixels;
    }

    private void Release()
    {
        if (_fbo != 0) _gl.DeleteFramebuffer(_fbo);
        if (_color != 0) _gl.DeleteTexture(_color);
        GlState.ForgetTextures();
        _fbo = _color = 0;
        _width = _height = 0;
    }

    public void Dispose()
    {
        Release();
        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        _vao = 0;
        _program?.Dispose();
        _program = null;
    }
}
