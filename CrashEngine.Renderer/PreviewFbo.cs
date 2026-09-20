using Silk.NET.OpenGL;

namespace CrashEngine.Renderer;

public sealed class PreviewFbo : IDisposable
{
    private readonly GL _gl;
    private uint _fbo, _color, _depth;

    public int Width  { get; private set; }
    public int Height { get; private set; }
    public uint ColorTexture => _color;

    public PreviewFbo(GL gl, int width, int height)
    {
        _gl = gl;
        Allocate(width, height);
    }

    private unsafe void Allocate(int w, int h)
    {
        Width = w; Height = h;

        _color = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _color);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                       (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        _depth = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depth);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer,
                                InternalFormat.DepthComponent24, (uint)w, (uint)h);

        _fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                 TextureTarget.Texture2D, _color, 0);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                                    RenderbufferTarget.Renderbuffer, _depth);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Resize(int w, int h)
    {
        if (w == Width && h == Height) return;
        Release();
        Allocate(w, h);
    }

    public void Begin(System.Numerics.Vector3 clearColor)
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.ClearColor(clearColor.X, clearColor.Y, clearColor.Z, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public void End(int screenW, int screenH)
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)screenW, (uint)screenH);
    }

    public unsafe byte[] ReadPixels()
    {
        var buf = new byte[Width * Height * 4];
        fixed (byte* p = buf)
            _gl.ReadPixels(0, 0, (uint)Width, (uint)Height, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        return buf;
    }

    private void Release()
    {
        _gl.DeleteFramebuffer(_fbo);
        _gl.DeleteTexture(_color);
        _gl.DeleteRenderbuffer(_depth);
    }

    public void Dispose() => Release();
}
