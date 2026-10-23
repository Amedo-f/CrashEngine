namespace CrashEngine.Runtime.Video;

// Amedo
public sealed class Mpeg2Frame
{
    public readonly int Width, Height, Stride, ChromaStride;
    public readonly int MbWidth, MbHeight; // Amedo
    public readonly byte[] Y, Cb, Cr;
    public readonly byte[] Quant; // Amedo
    public int PictureType, TemporalReference;

    public Mpeg2Frame(int mbWidth, int mbHeight, int width, int height)
    {
        Width = width;
        Height = height;
        MbWidth = mbWidth;
        MbHeight = mbHeight;
        Stride = mbWidth * 16;
        ChromaStride = mbWidth * 8;
        Y = new byte[Stride * mbHeight * 16];
        Cb = new byte[ChromaStride * mbHeight * 8];
        Cr = new byte[ChromaStride * mbHeight * 8];
        Quant = new byte[mbWidth * mbHeight];
    }
}

// Amedo
public sealed class Mpeg2Decoder
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int AspectCode { get; private set; }
    public int FrameRateCode { get; private set; }
    public bool Mpeg2 { get; private set; }
    public int Errors { get; private set; }
    public string? LastError { get; private set; }
    public double FrameRate => FrameRateCode switch { 1 => 24000.0 / 1001, 2 => 24, 3 => 25, 4 => 30000.0 / 1001, 5 => 30, 6 => 50, 7 => 60000.0 / 1001, 8 => 60, _ => 30 };

    public Action<Mpeg2Frame>? FrameReady { get; set; }

    private int _mbWidth, _mbHeight;
    private readonly int[] _intraQ = new int[64], _nonIntraQ = new int[64];
    private Mpeg2Frame? _forward, _backward, _current;
    private readonly List<Mpeg2Frame> _pool = new();

    private int _picType, _temporalRef, _dcPrecision, _picStructure = 3, _framePredFrameDct = 1, _concealment, _qScaleType, _intraVlc, _altScan;
    private readonly int[,] _fCode = new int[2, 2];
    private bool _havePicture;

    private byte[] _data = Array.Empty<byte>();
    private int _end;
    private long _bit;

    private int _qScale;
    private readonly int[] _dcPred = new int[3];
    private readonly int[,,] _pmv = new int[2, 2, 2];
    private readonly int[,,] _mv = new int[2, 2, 2];
    private readonly int[,] _fieldSelect = new int[2, 2];
    private int _lastType, _lastMotionType;
    private readonly int[] _block = new int[64];

    private const int MotionField = 1, MotionFrame = 2, MotionDualPrime = 3;

    // Amedo
    public void Decode(byte[] data, int offset, int count)
    {
        _data = data;
        _end = offset + count;
        int i = offset;
        while (true)
        {
            i = FindStartCode(i);
            if (i < 0) break;
            int code = _data[i + 3];
            _bit = (long)(i + 4) * 8;
            if (code == 0xB3) SequenceHeader();
            else if (code == 0xB5) Extension();
            else if (code == 0x00) PictureHeader();
            else if (code >= 0x01 && code <= 0xAF)
            {
                if (_havePicture && _current is not null) Slice(code);
            }
            else if (code == 0xB7) Flush();
            i += 4;
        }
    }

    // Amedo
    public void EndPicture()
    {
        if (!_havePicture || _current is null) return;
        _havePicture = false;
        if (_picType == 3) FrameReady?.Invoke(_current);
        else if (_forward is not null && !ReferenceEquals(_forward, _current)) FrameReady?.Invoke(_forward);
    }

    // Amedo
    public void Flush()
    {
        EndPicture();
        if (_backward is not null) FrameReady?.Invoke(_backward);
        _forward = _backward = null;
    }

    private int FindStartCode(int i)
    {
        for (; i + 3 < _end; i++)
            if (_data[i] == 0 && _data[i + 1] == 0 && _data[i + 2] == 1) return i;
        return -1;
    }

    private uint Peek(int n)
    {
        long byteIndex = _bit >> 3;
        int shift = (int)(_bit & 7);
        ulong v = 0;
        for (int k = 0; k < 5; k++)
        {
            long idx = byteIndex + k;
            v = (v << 8) | (idx < _end ? _data[idx] : 0u);
        }
        return (uint)((v << (24 + shift)) >> (64 - n));
    }

    private uint Read(int n)
    {
        if (n == 0) return 0;
        uint v = Peek(n);
        _bit += n;
        return v;
    }

    private bool Bit() => Read(1) != 0;

    private int Vlc(in Mpeg2Tables.Vlc table)
    {
        uint idx = Peek(table.Bits);
        int len = table.Length[idx];
        if (len == 0)
        {
            Error("bad vlc");
            _bit += 1;
            return 0;
        }
        _bit += len;
        return table.Value[idx];
    }

    private void Error(string what)
    {
        Errors++;
        LastError = what;
    }

    // Amedo
    private void SequenceHeader()
    {
        int w = (int)Read(12), h = (int)Read(12);
        AspectCode = (int)Read(4);
        FrameRateCode = (int)Read(4);
        Read(18);
        Read(1);
        Read(10);
        Read(1);
        if (Bit()) for (int i = 0; i < 64; i++) _intraQ[Mpeg2Tables.ZigZag[i]] = (int)Read(8);
        else for (int i = 0; i < 64; i++) _intraQ[i] = Mpeg2Tables.DefaultIntraMatrix[i];
        if (Bit()) for (int i = 0; i < 64; i++) _nonIntraQ[Mpeg2Tables.ZigZag[i]] = (int)Read(8);
        else Array.Fill(_nonIntraQ, 16);
        Resize(w, h);
    }

    private void Resize(int w, int h)
    {
        if (w == Width && h == Height && _pool.Count > 0) return;
        Width = w;
        Height = h;
        _mbWidth = (w + 15) >> 4;
        _mbHeight = (h + 15) >> 4;
        _pool.Clear();
        for (int i = 0; i < 3; i++) _pool.Add(new Mpeg2Frame(_mbWidth, _mbHeight, w, h));
        _forward = _backward = _current = null;
    }

    // Amedo
    private void Extension()
    {
        int id = (int)Read(4);
        switch (id)
        {
            case 1:
            {
                Mpeg2 = true;
                Read(8);
                Read(1);
                int chroma = (int)Read(2);
                int hExt = (int)Read(2), vExt = (int)Read(2);
                if (chroma != 1) Error("only 4:2:0 is supported");
                if (hExt != 0 || vExt != 0) Resize(Width | hExt << 12, Height | vExt << 12);
                break;
            }
            case 3:
                if (Bit()) for (int i = 0; i < 64; i++) _intraQ[Mpeg2Tables.ZigZag[i]] = (int)Read(8);
                if (Bit()) for (int i = 0; i < 64; i++) _nonIntraQ[Mpeg2Tables.ZigZag[i]] = (int)Read(8);
                break;
            case 8:
                _fCode[0, 0] = (int)Read(4);
                _fCode[0, 1] = (int)Read(4);
                _fCode[1, 0] = (int)Read(4);
                _fCode[1, 1] = (int)Read(4);
                _dcPrecision = (int)Read(2);
                _picStructure = (int)Read(2);
                Read(1);
                _framePredFrameDct = (int)Read(1);
                _concealment = (int)Read(1);
                _qScaleType = (int)Read(1);
                _intraVlc = (int)Read(1);
                _altScan = (int)Read(1);
                if (_picStructure != 3) Error("field pictures are not supported yet");
                break;
        }
    }

    // Amedo
    private void PictureHeader()
    {
        EndPicture();
        if (_pool.Count == 0) return;
        _temporalRef = (int)Read(10);
        _picType = (int)Read(3);
        Read(16);
        if (_picType is 2 or 3)
        {
            Read(1);
            int f = (int)Read(3);
            if (!Mpeg2) _fCode[0, 0] = _fCode[0, 1] = f;
        }
        if (_picType == 3)
        {
            Read(1);
            int f = (int)Read(3);
            if (!Mpeg2) _fCode[1, 0] = _fCode[1, 1] = f;
        }
        if (!Mpeg2)
        {
            _dcPrecision = 0;
            _picStructure = 3;
            _framePredFrameDct = 1;
            _concealment = _qScaleType = _intraVlc = _altScan = 0;
        }
        if (_picType is < 1 or > 3) return;
        if (_picType == 3)
        {
            if (_forward is null || _backward is null) return;
            _current = Free();
        }
        else
        {
            _current = Free();
            _forward = _backward;
            _backward = _current;
        }
        if (_current is null) return;
        _current.PictureType = _picType;
        _current.TemporalReference = _temporalRef;
        _havePicture = true;
    }

    private Mpeg2Frame? Free()
    {
        foreach (var f in _pool)
            if (!ReferenceEquals(f, _forward) && !ReferenceEquals(f, _backward)) return f;
        return null;
    }

    // Amedo
    private void Slice(int code)
    {
        int mbRow = code - 1;
        if (Height > 2800) mbRow += (int)Read(3) << 7;
        int qCode = (int)Read(5);
        _qScale = QScale(qCode);
        if (Bit())
        {
            Read(1);
            Read(1);
            Read(6);
            while (Bit()) Read(8);
        }
        ResetDc();
        ResetPmv();
        int mbAddr = mbRow * _mbWidth - 1;
        bool first = true;
        while (true)
        {
            int inc = 0;
            while (true)
            {
                if (Peek(23) == 0) return;
                int v = Vlc(Mpeg2Tables.MbAddressIncrement);
                if (v == Mpeg2Tables.MbStuffing) continue;
                if (v == Mpeg2Tables.MbEscape) { inc += 33; continue; }
                if (v == 0) return;
                inc += v;
                break;
            }
            if (first)
            {
                mbAddr += inc;
                first = false;
            }
            else
            {
                for (int s = 1; s < inc; s++) SkippedMacroblock(++mbAddr);
                mbAddr++;
            }
            if (mbAddr >= _mbWidth * _mbHeight) { Error("macroblock past the picture"); return; }
            Macroblock(mbAddr);
            if (Peek(23) == 0) return;
        }
    }

    private int QScale(int code) => _qScaleType != 0 ? Mpeg2Tables.NonLinearScale[code] : code * 2;

    private void ResetDc()
    {
        int v = 1 << (_dcPrecision + 7);
        _dcPred[0] = _dcPred[1] = _dcPred[2] = v;
    }

    private void ResetPmv() => Array.Clear(_pmv);

    // Amedo
    private void SkippedMacroblock(int mbAddr)
    {
        if (mbAddr >= _mbWidth * _mbHeight) return;
        ResetDc();
        _current!.Quant[mbAddr] = (byte)Math.Min(_qScale, 255); // Amedo
        int mbx = mbAddr % _mbWidth, mby = mbAddr / _mbWidth;
        if (_picType == 2)
        {
            ResetPmv();
            Array.Clear(_mv);
            Predict(mbx, mby, Mpeg2Tables.MbForward, MotionFrame);
        }
        else if (_picType == 3) Predict(mbx, mby, _lastType, _lastMotionType);
    }

    // Amedo
    private void Macroblock(int mbAddr)
    {
        int mbx = mbAddr % _mbWidth, mby = mbAddr / _mbWidth;
        int type = _picType switch
        {
            1 => Vlc(Mpeg2Tables.MbTypeI),
            2 => Vlc(Mpeg2Tables.MbTypeP),
            _ => Vlc(Mpeg2Tables.MbTypeB),
        };
        int motionType = MotionFrame;
        if ((type & (Mpeg2Tables.MbForward | Mpeg2Tables.MbBackward)) != 0 && _framePredFrameDct == 0) motionType = (int)Read(2);
        bool fieldDct = false;
        if ((type & (Mpeg2Tables.MbIntra | Mpeg2Tables.MbPattern)) != 0 && _framePredFrameDct == 0) fieldDct = Bit();
        if ((type & Mpeg2Tables.MbQuant) != 0) _qScale = QScale((int)Read(5));
        _current!.Quant[mbAddr] = (byte)Math.Min(_qScale, 255); // Amedo
        bool intra = (type & Mpeg2Tables.MbIntra) != 0;
        if (intra && _concealment != 0)
        {
            MotionVectors(0, MotionFrame);
            Read(1);
        }
        if ((type & Mpeg2Tables.MbForward) != 0) MotionVectors(0, motionType);
        if ((type & Mpeg2Tables.MbBackward) != 0) MotionVectors(1, motionType);
        if (intra)
        {
            if (_concealment == 0) ResetPmv();
        }
        else
        {
            ResetDc();
            if (_picType == 2 && (type & Mpeg2Tables.MbForward) == 0)
            {
                ResetPmv();
                Array.Clear(_mv);
                motionType = MotionFrame;
                type |= Mpeg2Tables.MbForward;
            }
            Predict(mbx, mby, type, motionType);
            _lastType = type & (Mpeg2Tables.MbForward | Mpeg2Tables.MbBackward);
            _lastMotionType = motionType;
        }
        int cbp = intra ? 63 : (type & Mpeg2Tables.MbPattern) != 0 ? Vlc(Mpeg2Tables.CodedBlockPattern) : 0;
        var frame = _current!;
        for (int b = 0; b < 6; b++)
        {
            if ((cbp & (32 >> b)) == 0) continue;
            Array.Clear(_block);
            if (intra) IntraBlock(b);
            else NonIntraBlock();
            Mpeg2Idct.Transform(_block);
            byte[] plane;
            int offset, step;
            if (b < 4)
            {
                plane = frame.Y;
                if (fieldDct)
                {
                    offset = (mby * 16 + (b >> 1)) * frame.Stride + mbx * 16 + (b & 1) * 8;
                    step = frame.Stride * 2;
                }
                else
                {
                    offset = (mby * 16 + (b >> 1) * 8) * frame.Stride + mbx * 16 + (b & 1) * 8;
                    step = frame.Stride;
                }
            }
            else
            {
                plane = b == 4 ? frame.Cb : frame.Cr;
                offset = mby * 8 * frame.ChromaStride + mbx * 8;
                step = frame.ChromaStride;
            }
            if (intra) Put(plane, offset, step);
            else Add(plane, offset, step);
        }
    }

    // Amedo
    private void MotionVectors(int s, int motionType)
    {
        if (motionType == MotionFrame)
        {
            Vector(0, s, false);
            _pmv[1, s, 0] = _pmv[0, s, 0];
            _pmv[1, s, 1] = _pmv[0, s, 1];
            return;
        }
        if (motionType == MotionField)
        {
            _fieldSelect[0, s] = (int)Read(1);
            Vector(0, s, true);
            _fieldSelect[1, s] = (int)Read(1);
            Vector(1, s, true);
            return;
        }
        Error("dual prime is not supported yet");
        Vector(0, s, true);
        ReadDualPrime();
        ReadDualPrime();
    }

    private void ReadDualPrime()
    {
        if (Bit()) Read(1);
    }

    private void Vector(int r, int s, bool field)
    {
        for (int t = 0; t < 2; t++)
        {
            int code = Vlc(Mpeg2Tables.MotionCode);
            if (code != 0 && Bit()) code = -code;
            int rSize = _fCode[s, t] - 1;
            int residual = rSize > 0 && code != 0 ? (int)Read(rSize) : 0;
            int pred = _pmv[r, s, t];
            if (field && t == 1) pred >>= 1;
            int f = 1 << rSize, high = 16 * f - 1, low = -16 * f, range = 32 * f;
            int delta = 0;
            if (code != 0)
            {
                delta = ((Math.Abs(code) - 1) << rSize) + residual + 1;
                if (code < 0) delta = -delta;
            }
            int v = pred + delta;
            if (v > high) v -= range;
            else if (v < low) v += range;
            _pmv[r, s, t] = field && t == 1 ? v << 1 : v;
            _mv[r, s, t] = v;
        }
    }

    // Amedo
    private void IntraBlock(int b)
    {
        int c = b < 4 ? 0 : b - 3;
        int size = Vlc(b < 4 ? Mpeg2Tables.DcSizeLuma : Mpeg2Tables.DcSizeChroma);
        int diff = 0;
        if (size > 0)
        {
            int bits = (int)Read(size);
            diff = bits < (1 << (size - 1)) ? bits - (1 << size) + 1 : bits;
        }
        _dcPred[c] += diff;
        int dc = _dcPred[c] * (8 >> _dcPrecision);
        _block[0] = dc;
        int sum = dc;
        var scan = _altScan != 0 ? Mpeg2Tables.Alternate : Mpeg2Tables.ZigZag;
        var table = _intraVlc != 0 ? Mpeg2Tables.DctOne : Mpeg2Tables.DctZero;
        int i = 0;
        while (true)
        {
            if (!Coefficient(table, false, out int run, out int level)) break;
            i += run + 1;
            if (i > 63) { Error("coefficient index past 63"); return; }
            int pos = scan[i];
            int v = Math.Abs(level) * _qScale * _intraQ[pos] * 2 / 32;
            if (level < 0) v = -v;
            v = Math.Clamp(v, -2048, 2047);
            _block[pos] = v;
            sum += v;
        }
        if ((sum & 1) == 0) _block[63] ^= 1;
    }

    // Amedo
    private void NonIntraBlock()
    {
        int sum = 0;
        var scan = _altScan != 0 ? Mpeg2Tables.Alternate : Mpeg2Tables.ZigZag;
        int i = -1;
        bool first = true;
        while (true)
        {
            int run, level;
            if (first && Peek(1) == 1)
            {
                Read(1);
                run = 0;
                level = Bit() ? -1 : 1;
            }
            else if (!Coefficient(Mpeg2Tables.DctZero, true, out run, out level)) break;
            first = false;
            i += run + 1;
            if (i > 63) { Error("coefficient index past 63"); return; }
            int pos = scan[i];
            int v = (2 * Math.Abs(level) + 1) * _qScale * _nonIntraQ[pos] / 32;
            if (level < 0) v = -v;
            v = Math.Clamp(v, -2048, 2047);
            _block[pos] = v;
            sum += v;
        }
        if ((sum & 1) == 0) _block[63] ^= 1;
    }

    private bool Coefficient(in Mpeg2Tables.Vlc table, bool nonIntra, out int run, out int level)
    {
        int v = Vlc(table);
        if (v == Mpeg2Tables.EndOfBlock)
        {
            run = level = 0;
            return false;
        }
        if (v == Mpeg2Tables.Escape)
        {
            run = (int)Read(6);
            int l = (int)Read(12);
            level = l >= 2048 ? l - 4096 : l;
            if (level == 0 || level == -2048) Error("bad escape level");
            return true;
        }
        if (v == 0)
        {
            run = level = 0;
            return false;
        }
        run = v >> 8;
        level = v & 0xFF;
        if (Bit()) level = -level;
        return true;
    }

    private void Put(byte[] plane, int offset, int step)
    {
        for (int y = 0; y < 8; y++, offset += step)
            for (int x = 0; x < 8; x++)
                plane[offset + x] = (byte)Math.Clamp(_block[y * 8 + x], 0, 255);
    }

    private void Add(byte[] plane, int offset, int step)
    {
        for (int y = 0; y < 8; y++, offset += step)
            for (int x = 0; x < 8; x++)
                plane[offset + x] = (byte)Math.Clamp(plane[offset + x] + _block[y * 8 + x], 0, 255);
    }

    // Amedo
    private void Predict(int mbx, int mby, int type, int motionType)
    {
        var dst = _current!;
        bool avg = false;
        for (int s = 0; s < 2; s++)
        {
            if ((type & (s == 0 ? Mpeg2Tables.MbForward : Mpeg2Tables.MbBackward)) == 0) continue;
            var src = _picType == 3 ? (s == 0 ? _forward : _backward) : _forward;
            if (src is null) return;
            if (motionType == MotionField)
            {
                for (int f = 0; f < 2; f++)
                {
                    int sel = _fieldSelect[f, s];
                    int mx = _mv[f, s, 0], my = _mv[f, s, 1];
                    Block(src.Y, dst.Y, dst.Stride, Width, Height, mbx * 16, mby * 8, mx, my, 16, 8, sel, f, avg);
                    int cmx = mx / 2, cmy = my / 2;
                    Block(src.Cb, dst.Cb, dst.ChromaStride, Width >> 1, Height >> 1, mbx * 8, mby * 4, cmx, cmy, 8, 4, sel, f, avg);
                    Block(src.Cr, dst.Cr, dst.ChromaStride, Width >> 1, Height >> 1, mbx * 8, mby * 4, cmx, cmy, 8, 4, sel, f, avg);
                }
            }
            else
            {
                int mx = _mv[0, s, 0], my = _mv[0, s, 1];
                Block(src.Y, dst.Y, dst.Stride, Width, Height, mbx * 16, mby * 16, mx, my, 16, 16, -1, -1, avg);
                int cmx = mx / 2, cmy = my / 2;
                Block(src.Cb, dst.Cb, dst.ChromaStride, Width >> 1, Height >> 1, mbx * 8, mby * 8, cmx, cmy, 8, 8, -1, -1, avg);
                Block(src.Cr, dst.Cr, dst.ChromaStride, Width >> 1, Height >> 1, mbx * 8, mby * 8, cmx, cmy, 8, 8, -1, -1, avg);
            }
            avg = true;
        }
    }

    // Amedo
    private static void Block(byte[] src, byte[] dst, int stride, int planeW, int planeH, int x, int y, int mx, int my, int w, int h, int srcField, int dstField, bool avg)
    {
        int sx = x + (mx >> 1), sy = y + (my >> 1);
        int hx = mx & 1, hy = my & 1;
        bool field = srcField >= 0;
        int lines = field ? planeH >> 1 : planeH;
        for (int j = 0; j < h; j++)
        {
            int r0 = Math.Clamp(sy + j, 0, lines - 1), r1 = Math.Clamp(sy + j + hy, 0, lines - 1);
            int row0 = (field ? r0 * 2 + srcField : r0) * stride, row1 = (field ? r1 * 2 + srcField : r1) * stride;
            int drow = (field ? (y + j) * 2 + dstField : y + j) * stride;
            for (int i = 0; i < w; i++)
            {
                int c0 = Math.Clamp(sx + i, 0, planeW - 1), c1 = Math.Clamp(sx + i + hx, 0, planeW - 1);
                int p;
                if (hx == 0 && hy == 0) p = src[row0 + c0];
                else if (hy == 0) p = (src[row0 + c0] + src[row0 + c1] + 1) >> 1;
                else if (hx == 0) p = (src[row0 + c0] + src[row1 + c0] + 1) >> 1;
                else p = (src[row0 + c0] + src[row0 + c1] + src[row1 + c0] + src[row1 + c1] + 2) >> 2;
                int d = drow + x + i;
                dst[d] = avg ? (byte)((dst[d] + p + 1) >> 1) : (byte)p;
            }
        }
    }
}
