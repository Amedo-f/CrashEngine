using CrashEngine.Core;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace CrashEngine.Renderer;

public sealed class Texture2D : IDisposable, IGpuResource // Amedo 2026-09-29
{
    private readonly GL   _gl;
    private uint _id; // Amedo 2026-09-29
    private byte[]? _pending;
    public uint Width { get; private set; }
    public uint Height { get; private set; }
    public bool Uploaded { get; private set; }

    public uint GlId
    {
        get
        {
            if (!Uploaded) Upload(); // Amedo 2026-09-29
            return _id;
        }
    }

    // Amedo 2026-09-21
    public bool IsOpaque { get; private set; }

    private uint _uploadW, _uploadH; // Amedo 2026-10-02
    private bool _mipmaps;
    public string? SourceKey { get; init; }
    public bool HighRes => _uploadW != Width || _uploadH != Height;
    public string HdLabel => HighRes ? $" (HD {_uploadW}x{_uploadH})" : "";

    public Texture2D(GL gl, ReadOnlySpan<byte> rgba, uint w, uint h) : this(gl, rgba, w, h, w, h, false) { } // Amedo 2026-10-02

    // Amedo
    public Texture2D(GL gl, ReadOnlySpan<byte> rgba, uint w, uint h, IReadOnlyList<(byte[] Rgba, uint W, uint H)>? gameMips)
        : this(gl, rgba, w, h, w, h, false, gameMips) { }

    public unsafe Texture2D(GL gl, ReadOnlySpan<byte> rgba, uint uploadW, uint uploadH, uint w, uint h, bool mipmaps,
                            IReadOnlyList<(byte[] Rgba, uint W, uint H)>? gameMips = null) // Amedo 2026-10-02
    {
        _gl    = gl;
        _gameMips = gameMips is { Count: > 0 } && !mipmaps ? gameMips : null; // Amedo
        Width  = w;
        Height = h;
        _uploadW = uploadW; // Amedo 2026-10-02
        _uploadH = uploadH;
        _mipmaps = mipmaps;

        // Amedo 2026-09-21
        IsOpaque = true;
        for (int i = 3; i < rgba.Length; i += 4)
            if (rgba[i] < 254) { IsOpaque = false; break; }

        if (GpuUploadQueue.Current is { } queue) // Amedo 2026-09-29
        {
            _pending = rgba.ToArray();
            queue.Add(this);
            return;
        }
        Create(rgba);
    }

    // Amedo 2026-09-29
    private unsafe void Create(ReadOnlySpan<byte> rgba)
    {
        GpuUploadQueue.RequireMainThread("Texture2D");
        var gl = _gl;
        _id    = GlNames.Texture(gl); // Amedo 2026-09-30
        gl.BindTexture(TextureTarget.Texture2D, _id);
        GlState.ForgetTextures();

        uint w = _uploadW, h = _uploadH; // Amedo 2026-10-02
        if (GpuStats.TextureStressScale > 1 && !HighRes) rgba = StressUpscale(rgba, ref w, ref h);
        fixed (byte* p = rgba)
            gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                          w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        _bytes = (long)w * h * 4; // Amedo 2026-09-30
        GpuStats.TextureBytes += _bytes;
        GpuStats.Textures++;

        if (_mipmaps) // Amedo 2026-10-02
        {
            gl.GenerateMipmap(TextureTarget.Texture2D);
            _bytes = _bytes * 4 / 3;
            GpuStats.TextureBytes += _bytes - (long)w * h * 4;
        }
        else if (_gameMips is { } levels && w == Width && h == Height) UploadGameMips(levels); // Amedo
        else _gameMips = null;
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                        (int)(_mipmaps ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.Linear));
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                        (int)TextureMagFilter.Linear);
        SetWrap();
        gl.BindTexture(TextureTarget.Texture2D, 0);
        Uploaded = true;
        lock (Live) Live.Add(this); // Amedo
        if (_filtering != TextureFiltering.Original || _gameMips is not null) ApplyFilter();
    }

    // Amedo
    private IReadOnlyList<(byte[] Rgba, uint W, uint H)>? _gameMips;

    // Amedo
    public int GameMipLevels => _gameMips?.Count ?? 0;

    // Amedo
    private unsafe void UploadGameMips(IReadOnlyList<(byte[] Rgba, uint W, uint H)> levels)
    {
        var gl = _gl;
        for (int i = 0; i < levels.Count; i++)
        {
            var (rgba, lw, lh) = levels[i];
            fixed (byte* p = rgba)
                gl.TexImage2D(TextureTarget.Texture2D, i + 1, InternalFormat.Rgba8, lw, lh, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            _bytes += (long)lw * lh * 4;
            GpuStats.TextureBytes += (long)lw * lh * 4;
        }
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, levels.Count);
    }

    private static readonly HashSet<Texture2D> Live = new(); // Amedo
    private static TextureFiltering _filtering = TextureFiltering.Original;
    private static int _anisotropy = 1;
    private static float _maxAnisotropy = -1f;
    private static bool _following;
    private bool _generatedMips;

    // Amedo
    public static void FollowSettings()
    {
        if (_following) return;
        _following = true;
        Use(GraphicsSettings.Current);
        GraphicsSettings.Changed += (old, now) =>
        {
            if (old.Filtering != now.Filtering) Use(now);
        };
    }

    // Amedo
    private static void Use(GraphicsSettings settings)
    {
        if (settings.Filtering == _filtering) return;
        _filtering = settings.Filtering;
        _anisotropy = settings.Anisotropy;
        Texture2D[] all;
        lock (Live) all = Live.ToArray();
        foreach (var texture in all) texture.ApplyFilter();
        Console.WriteLine($"[Texture2D] filtering {_filtering} on {all.Length} textures");
    }

    // Amedo
    private void ApplyFilter()
    {
        var gl = _gl;
        if (_maxAnisotropy < 0f)
        {
            gl.GetFloat((GetPName)0x84FF, out _maxAnisotropy);
            if (gl.GetError() != GLEnum.NoError) _maxAnisotropy = 0f;
        }
        gl.BindTexture(TextureTarget.Texture2D, _id);
        GlState.ForgetTextures();
        if (_gameMips is not null) // Amedo
        {
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                            (int)(_filtering == TextureFiltering.Original ? TextureMinFilter.LinearMipmapNearest : TextureMinFilter.LinearMipmapLinear));
            if (_maxAnisotropy >= 1f)
                gl.TexParameter(TextureTarget.Texture2D, (TextureParameterName)0x84FE, _filtering == TextureFiltering.Original ? 1f : MathF.Min(_anisotropy, _maxAnisotropy));
            gl.BindTexture(TextureTarget.Texture2D, 0);
            return;
        }
        bool mips = _mipmaps || _filtering != TextureFiltering.Original;
        if (mips && !_mipmaps && !_generatedMips)
        {
            gl.GenerateMipmap(TextureTarget.Texture2D);
            _generatedMips = true;
            GpuStats.TextureBytes += _bytes / 3;
            _bytes += _bytes / 3;
        }
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                        (int)(mips ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.Linear));
        if (_maxAnisotropy >= 1f)
            gl.TexParameter(TextureTarget.Texture2D, (TextureParameterName)0x84FE, MathF.Min(_anisotropy, _maxAnisotropy));
        gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private bool _clamp; // Amedo

    // Amedo
    public void ClampToEdge()
    {
        _clamp = true;
        if (!Uploaded) return;
        _gl.BindTexture(TextureTarget.Texture2D, _id);
        GlState.ForgetTextures();
        SetWrap();
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    // Amedo
    private void SetWrap()
    {
        var wrap = _clamp ? TextureWrapMode.ClampToEdge : TextureWrapMode.Repeat;
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrap);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrap);
    }

    // Amedo 2026-09-29
    public void Upload()
    {
        if (Uploaded || _pending is not { } data) return;
        Create(data);
        _pending = null;
    }

    private long _bytes; // Amedo 2026-09-30

    public long PendingBytes => _pending?.Length ?? 0; // Amedo

    public string Describe() => $"texture {_uploadW}x{_uploadH}{(_mipmaps ? " mips" : "")} {_bytes / 1024} KB"; // Amedo

    // Amedo 2026-09-30
    private static byte[] StressUpscale(ReadOnlySpan<byte> rgba, ref uint w, ref uint h)
    {
        uint s = (uint)GpuStats.TextureStressScale;
        while (s > 1 && (w * s > GpuStats.TextureStressMaxSide || h * s > GpuStats.TextureStressMaxSide)) s >>= 1;
        if (s <= 1 || rgba.Length < w * h * 4) return rgba.ToArray();
        uint nw = w * s, nh = h * s;
        var big = new byte[(long)nw * nh * 4];
        for (uint y = 0; y < nh; y++)
        {
            var src = rgba.Slice((int)((y / s) * w * 4), (int)(w * 4));
            var row = big.AsSpan((int)(y * nw * 4), (int)(nw * 4));
            for (uint x = 0; x < nw; x++) src.Slice((int)(x / s * 4), 4).CopyTo(row.Slice((int)(x * 4), 4));
        }
        (w, h) = (nw, nh);
        return big;
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

    // Amedo 2026-09-22
    public static byte[]? LoadRgba(string path, out int width, out int height)
    {
        width = 0; height = 0;
        try
        {
            using var stream = File.OpenRead(path);
            var img = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            width = img.Width; height = img.Height;
            return img.Data;
        }
        catch { return null; }
    }

    // Amedo 2026-10-02
    public unsafe void UpdateHighRes(ReadOnlySpan<byte> rgba, uint uploadW, uint uploadH, uint w, uint h)
    {
        if (!Uploaded) Upload();
        Width  = w;
        Height = h;
        _uploadW = uploadW;
        _uploadH = uploadH;
        _mipmaps = true;
        _gl.BindTexture(TextureTarget.Texture2D, _id);
        GlState.ForgetTextures();
        fixed (byte* p = rgba)
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                          uploadW, uploadH, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        _gameMips = null; // Amedo
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 1000);
        _gl.GenerateMipmap(TextureTarget.Texture2D);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        long bytes = (long)uploadW * uploadH * 4 * 4 / 3;
        GpuStats.TextureBytes += bytes - _bytes;
        _bytes = bytes;
        _generatedMips = false; // Amedo
        if (_filtering != TextureFiltering.Original) ApplyFilter();
    }

    public unsafe void UpdateData(ReadOnlySpan<byte> rgba, uint w, uint h)
    {
        if (!Uploaded) Upload(); // Amedo 2026-09-29
        Width  = w;
        Height = h;
        _uploadW = w; // Amedo 2026-10-02
        _uploadH = h;
        _gl.BindTexture(TextureTarget.Texture2D, _id);
        GlState.ForgetTextures();
        fixed (byte* p = rgba)
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
                          w, h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        if (_mipmaps || _gameMips is not null) // Amedo 2026-10-02
        {
            _mipmaps = false;
            _gameMips = null; // Amedo
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 1000);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        }
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        GpuStats.TextureBytes += (long)w * h * 4 - _bytes; // Amedo 2026-09-30
        _bytes = (long)w * h * 4;
        _generatedMips = false; // Amedo
        if (_filtering != TextureFiltering.Original) ApplyFilter();
    }

    public void Bind(uint slot = 0)
    {
        if (!Uploaded) Upload(); // Amedo 2026-09-29
        GlState.Texture(_gl, slot, _id);
    }

    public void Dispose()
    {
        _pending = null; // Amedo 2026-09-29
        if (!Uploaded) return;
        Uploaded = false;
        lock (Live) Live.Remove(this); // Amedo
        GpuStats.TextureBytes -= _bytes; // Amedo 2026-09-30
        GpuStats.Textures--;
        _gl.DeleteTexture(_id);
        GlState.ForgetTextures();
    }
}
