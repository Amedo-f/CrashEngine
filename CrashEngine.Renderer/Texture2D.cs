using Silk.NET.OpenGL;
using StbImageSharp;

namespace CrashEngine.Renderer;

public sealed class Texture2D : IDisposable
{
    private readonly GL   _gl;
    private readonly uint _id;
    public uint Width { get; private set; }
    public uint Height { get; private set; }

    public uint GlId => _id;

    public unsafe Texture2D(GL gl, ReadOnlySpan<byte> rgba, uint w, uint h)
    {
        _gl    = gl;
        Width  = w;
        Height = h;
        _id    = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, _id);

        fixed (byte* p = rgba)
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                          w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                        (int)TextureMinFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                        (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                        (int)TextureWrapMode.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                        (int)TextureWrapMode.Repeat);
        gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public static Texture2D White(GL gl)
    {
        ReadOnlySpan<byte> px = [255, 255, 255, 255];
        return new Texture2D(gl, px, 1, 1);
    }

    public static Texture2D? FromFile(GL gl, string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            return new Texture2D(gl, img.Data, (uint)img.Width, (uint)img.Height);
        }
        catch { return null; }
    }

    public unsafe void UpdateData(ReadOnlySpan<byte> rgba, uint w, uint h)
    {
        Width  = w;
        Height = h;
        _gl.BindTexture(TextureTarget.Texture2D, _id);
        fixed (byte* p = rgba)
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                          w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    public void Bind(uint slot = 0)
    {
        _gl.ActiveTexture(TextureUnit.Texture0 + (int)slot);
        _gl.BindTexture(TextureTarget.Texture2D, _id);
    }

    public void Dispose() => _gl.DeleteTexture(_id);
}
