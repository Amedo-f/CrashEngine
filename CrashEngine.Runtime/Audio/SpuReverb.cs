namespace CrashEngine.Runtime.Audio;

// Amedo 2026-11-11
internal sealed class SpuReverb
{
    private static readonly ushort[][] Presets_4A70 =
    [
        [0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0001, 0x0001, 0x0000, 0x0000, 0x0000, 0x0000,
         0x0000, 0x0000, 0x0001, 0x0001, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000],
        [0x007D, 0x005B, 0x6D80, 0x54B8, 0xBED0, 0x0000, 0x0000, 0xBA80, 0x5800, 0x5300, 0x04D6, 0x0333, 0x03F0, 0x0227, 0x0374, 0x01EF,
         0x0336, 0x01B7, 0x0335, 0x01B6, 0x0334, 0x01B5, 0x0334, 0x01B5, 0x0334, 0x01B5, 0x01B4, 0x0136, 0x00B8, 0x005C, 0x8000, 0x8000],
        [0x0033, 0x0025, 0x70F0, 0x4FA8, 0xBCE0, 0x4410, 0xC0F0, 0x9C00, 0x5280, 0x4EC0, 0x03E4, 0x031B, 0x03A4, 0x02AF, 0x0372, 0x0266,
         0x031C, 0x025D, 0x025C, 0x018E, 0x022F, 0x0135, 0x01D2, 0x00B7, 0x018F, 0x00B5, 0x00B4, 0x0080, 0x004C, 0x0026, 0x8000, 0x8000],
        [0x00B1, 0x007F, 0x70F0, 0x4FA8, 0xBCE0, 0x4510, 0xBEF0, 0xB4C0, 0x5280, 0x4EC0, 0x0904, 0x076B, 0x0824, 0x065F, 0x07A2, 0x0616,
         0x076C, 0x05ED, 0x05EC, 0x042E, 0x050F, 0x0305, 0x0462, 0x02B7, 0x042F, 0x0265, 0x0264, 0x01B2, 0x0100, 0x0080, 0x8000, 0x8000],
        [0x00E3, 0x00A9, 0x6F60, 0x4FA8, 0xBCE0, 0x4510, 0xBEF0, 0xA680, 0x5680, 0x52C0, 0x0DFB, 0x0B58, 0x0D09, 0x0A3C, 0x0BD9, 0x0973,
         0x0B59, 0x08DA, 0x08D9, 0x05E9, 0x07EC, 0x04B0, 0x06EF, 0x03D2, 0x05EA, 0x031D, 0x031C, 0x0238, 0x0154, 0x00AA, 0x8000, 0x8000],
        [0x01A5, 0x0139, 0x6000, 0x5000, 0x4C00, 0xB800, 0xBC00, 0xC000, 0x6000, 0x5C00, 0x15BA, 0x11BB, 0x14C2, 0x10BD, 0x11BC, 0x0DC1,
         0x11C0, 0x0DC3, 0x0DC0, 0x09C1, 0x0BC4, 0x07C1, 0x0A00, 0x06CD, 0x09C2, 0x05C1, 0x05C0, 0x041A, 0x0274, 0x013A, 0x8000, 0x8000],
        [0x033D, 0x0231, 0x7E00, 0x5000, 0xB400, 0xB000, 0x4C00, 0xB000, 0x6000, 0x5400, 0x1ED6, 0x1A31, 0x1D14, 0x183B, 0x1BC2, 0x16B2,
         0x1A32, 0x15EF, 0x15EE, 0x1055, 0x1334, 0x0F2D, 0x11F6, 0x0C5D, 0x1056, 0x0AE1, 0x0AE0, 0x07A2, 0x0464, 0x0232, 0x8000, 0x8000],
        [0x0003, 0x0003, 0x7FFF, 0x7FFF, 0x0000, 0x0000, 0x0000, 0x8100, 0x0000, 0x0000, 0x1FFD, 0x0FFD, 0x1009, 0x0009, 0x0000, 0x0000,
         0x1009, 0x0009, 0x1FFF, 0x1FFF, 0x1FFE, 0x1FFE, 0x1FFE, 0x1FFE, 0x1FFE, 0x1FFE, 0x1008, 0x1004, 0x0008, 0x0004, 0x8000, 0x8000],
        [0x0003, 0x0003, 0x7FFF, 0x7FFF, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x1FFD, 0x0FFD, 0x1009, 0x0009, 0x0000, 0x0000,
         0x1009, 0x0009, 0x1FFF, 0x1FFF, 0x1FFE, 0x1FFE, 0x1FFE, 0x1FFE, 0x1FFE, 0x1FFE, 0x1008, 0x1004, 0x0008, 0x0004, 0x8000, 0x8000],
        [0x0017, 0x0013, 0x70F0, 0x4FA8, 0xBCE0, 0x4510, 0xBEF0, 0x8500, 0x5F80, 0x54C0, 0x0371, 0x02AF, 0x02E5, 0x01DF, 0x02B0, 0x01D7,
         0x0358, 0x026A, 0x01D6, 0x011E, 0x012D, 0x00B1, 0x011F, 0x0059, 0x01A0, 0x00E3, 0x0058, 0x0040, 0x0028, 0x0014, 0x8000, 0x8000],
    ];

    private static readonly int[] AreaSize_4A48 = [2, 1240, 1000, 2312, 3580, 5564, 7896, 12296, 12296, 1920];

    private static readonly short[] Fir =
    [
        -0x0001, 0, 0x0002, 0, -0x000A, 0, 0x0023, 0, -0x0067, 0, 0x010A, 0, -0x0268, 0, 0x0534, 0, -0x0B90, 0, 0x2806, 0x4000,
        0x2806, 0, -0x0B90, 0, 0x0534, 0, -0x0268, 0, 0x010A, 0, -0x0067, 0, 0x0023, 0, -0x000A, 0, 0x0002, 0, -0x0001,
    ];

    private const int dAPF1 = 0, dAPF2 = 1, vIIR = 2, vCOMB1 = 3, vCOMB2 = 4, vCOMB3 = 5, vCOMB4 = 6, vWALL = 7, vAPF1 = 8, vAPF2 = 9,
        mLSAME = 10, mRSAME = 11, mLCOMB1 = 12, mRCOMB1 = 13, mLCOMB2 = 14, mRCOMB2 = 15, dLSAME = 16, dRSAME = 17, mLDIFF = 18,
        mRDIFF = 19, mLCOMB3 = 20, mRCOMB3 = 21, mLCOMB4 = 22, mRCOMB4 = 23, dLDIFF = 24, dRDIFF = 25, mLAPF1 = 26, mRAPF1 = 27,
        mLAPF2 = 28, mRAPF2 = 29, vLIN = 30, vRIN = 31;

    public static int ModeOf_2FAFF0(int kind) => kind switch { 0 => 9, >= 1 and <= 6 => kind, _ => 0 };

    public int Mode { get; private set; }
    public int EvolL { get; private set; }
    public int EvolR { get; private set; }

    private readonly int[] _r = new int[32];
    private short[] _area = [];
    private int _pos;
    private readonly int[] _inL = new int[39], _inR = new int[39], _outL = new int[39], _outR = new int[39];
    private int _ring;
    private bool _odd;

    public void Set_A414(int mode, int evolL, int evolR)
    {
        Mode = mode is >= 1 and <= 9 ? mode : 0;
        EvolL = Mode == 0 ? 0 : evolL;
        EvolR = Mode == 0 ? 0 : evolR;
        Array.Clear(_inL);
        Array.Clear(_inR);
        Array.Clear(_outL);
        Array.Clear(_outR);
        _ring = 0;
        _odd = false;
        _pos = 0;
        if (Mode == 0)
        {
            _area = [];
            return;
        }
        var p = Presets_4A70[Mode];
        for (int i = 0; i < 32; i++)
            _r[i] = i is < 2 or (>= 10 and < 30) ? p[i] * 4 : (short)p[i];
        _area = new short[AreaSize_4A48[Mode] * 4];
    }

    public void Mix(int inL, int inR, ref int outL, ref int outR)
    {
        if (Mode == 0) return;
        _inL[_ring] = inL;
        _inR[_ring] = inR;
        int yl = 0, yr = 0;
        if (_odd) Step(Sat(Filter(_inL)), Sat(Filter(_inR)), out yl, out yr);
        _outL[_ring] = yl;
        _outR[_ring] = yr;
        int l = Sat(Filter(_outL) * 2), r = Sat(Filter(_outR) * 2);
        outL += (l * EvolL) >> 15;
        outR += (r * EvolR) >> 15;
        if (++_ring == Fir.Length) _ring = 0;
        _odd = !_odd;
    }

    private int Filter(int[] ring)
    {
        long s = 0;
        int k = _ring;
        for (int t = 0; t < Fir.Length; t++)
        {
            s += Fir[t] * ring[k];
            if (--k < 0) k = Fir.Length - 1;
        }
        return (int)(s >> 15);
    }

    private void Step(int inL, int inR, out int outL, out int outR)
    {
        long lin = (inL * (long)_r[vLIN]) >> 15, rin = (inR * (long)_r[vRIN]) >> 15;
        Iir(mLSAME, dLSAME, lin);
        Iir(mRSAME, dRSAME, rin);
        Iir(mLDIFF, dRDIFF, lin);
        Iir(mRDIFF, dLDIFF, rin);
        int l = Sat((_r[vCOMB1] * (long)Read(_r[mLCOMB1]) + _r[vCOMB2] * (long)Read(_r[mLCOMB2])
                   + _r[vCOMB3] * (long)Read(_r[mLCOMB3]) + _r[vCOMB4] * (long)Read(_r[mLCOMB4])) >> 15);
        int r = Sat((_r[vCOMB1] * (long)Read(_r[mRCOMB1]) + _r[vCOMB2] * (long)Read(_r[mRCOMB2])
                   + _r[vCOMB3] * (long)Read(_r[mRCOMB3]) + _r[vCOMB4] * (long)Read(_r[mRCOMB4])) >> 15);
        l = Apf(l, _r[mLAPF1], _r[dAPF1], _r[vAPF1]);
        r = Apf(r, _r[mRAPF1], _r[dAPF1], _r[vAPF1]);
        outL = Apf(l, _r[mLAPF2], _r[dAPF2], _r[vAPF2]);
        outR = Apf(r, _r[mRAPF2], _r[dAPF2], _r[vAPF2]);
        if (++_pos == _area.Length) _pos = 0;
    }

    private void Iir(int dst, int src, long input)
    {
        long prev = Read(_r[dst] - 1);
        long x = input + ((Read(_r[src]) * (long)_r[vWALL]) >> 15) - prev;
        Write(_r[dst], Sat(((x * _r[vIIR]) >> 15) + prev));
    }

    private int Apf(int x, int m, int d, int v)
    {
        long t = Read(m - d);
        int y = Sat(x - ((v * t) >> 15));
        Write(m, y);
        return Sat(((y * (long)v) >> 15) + t);
    }

    private int Index(int offset)
    {
        int a = (_pos + offset) % _area.Length;
        return a < 0 ? a + _area.Length : a;
    }

    private short Read(int offset) => _area[Index(offset)];

    private void Write(int offset, int value) => _area[Index(offset)] = (short)value;

    private static int Sat(long v) => (int)Math.Clamp(v, short.MinValue, short.MaxValue);
}
