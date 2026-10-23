namespace CrashEngine.Runtime.Video;

// Amedo
public sealed class Mpeg2Reader : IDisposable
{
    private readonly MpegProgramStream _ps;
    private readonly Mpeg2Decoder _decoder = new();
    private readonly Queue<byte[]> _ready = new();
    private readonly Stack<byte[]> _free = new();
    private byte[]? _shown;
    private bool _flushed;
    private readonly Mpeg2Deblock _deblock = new(); // Amedo
    private readonly Mpeg2Upscale _upscale = new(); // Amedo
    private byte[] _base = Array.Empty<byte>(); // Amedo
    public bool Clean { get; set; } = true; // Amedo
    public bool Upscale { get; set; } // Amedo
    private int Scale => Clean && Upscale ? 2 : 1; // Amedo

    public Mpeg2Decoder Decoder => _decoder;
    public MpegProgramStream Stream => _ps;
    public int Width => _decoder.Width * Scale; // Amedo
    public int Height => _decoder.Height * Scale; // Amedo

    public Mpeg2Reader(Stream stream)
    {
        _ps = new MpegProgramStream(stream);
        _decoder.FrameReady = Converted;
    }

    private void Converted(Mpeg2Frame f)
    {
        int scale = Scale; // Amedo
        int size = f.Width * scale * f.Height * scale * 4;
        var rgba = _free.Count > 0 && _free.Peek().Length == size ? _free.Pop() : new byte[size];
        if (Clean && Upscale)
        {
            if (_base.Length != f.Width * f.Height * 4) _base = new byte[f.Width * f.Height * 4];
            _deblock.Apply(f);
            Mpeg2Yuv.ToRgba(f, _deblock.Y, _deblock.Cb, _deblock.Cr, _base);
            _upscale.Apply(_base, f.Width, f.Height, rgba);
        }
        else if (Clean)
        {
            _deblock.Apply(f);
            Mpeg2Yuv.ToRgba(f, _deblock.Y, _deblock.Cb, _deblock.Cr, rgba);
        }
        else Mpeg2Yuv.ToRgba(f, rgba);
        _ready.Enqueue(rgba);
    }

    // Amedo
    public byte[]? NextFrame()
    {
        while (_ready.Count == 0)
        {
            if (_flushed) return null;
            if (!_ps.ReadPacket())
            {
                var v = _ps.Video;
                if (v.Count > 0) _decoder.Decode(v.Buffer, v.Start, v.Count);
                v.Clear();
                _decoder.Flush();
                _flushed = true;
                continue;
            }
            Feed();
        }
        if (_shown is not null) _free.Push(_shown);
        _shown = _ready.Dequeue();
        return _shown;
    }

    private void Feed()
    {
        var v = _ps.Video;
        int cut = LastCut(v);
        if (cut <= 0) return;
        _decoder.Decode(v.Buffer, v.Start, cut);
        v.Consume(cut);
    }

    // Amedo
    private static int LastCut(ByteQueue q)
    {
        var b = q.Buffer;
        for (int i = q.Start + q.Count - 4; i > q.Start; i--)
            if (b[i] == 0 && b[i + 1] == 0 && b[i + 2] == 1 && (b[i + 3] == 0x00 || b[i + 3] == 0xB3 || b[i + 3] == 0xB8 || b[i + 3] == 0xB7))
                return i - q.Start;
        return 0;
    }

    public void Dispose() => _ps.Dispose();
}
