namespace CrashEngine.Runtime.Video;

// Amedo
internal static class Mpeg2Tables
{
    // Amedo
    public readonly struct Vlc
    {
        public readonly short[] Value;
        public readonly byte[] Length;
        public readonly int Bits;

        public Vlc(int bits, (int Code, int Length, int Value)[] codes)
        {
            Bits = bits;
            Value = new short[1 << bits];
            Length = new byte[1 << bits];
            foreach (var (code, len, value) in codes)
            {
                int shift = bits - len;
                int first = code << shift, count = 1 << shift;
                for (int i = 0; i < count; i++)
                {
                    Value[first + i] = (short)value;
                    Length[first + i] = (byte)len;
                }
            }
        }
    }

    public const int Escape = -1, EndOfBlock = -2, MbEscape = 34, MbStuffing = 35;

    public static readonly Vlc MbAddressIncrement = new(11, Build(
        [(0x1, 1), (0x3, 3), (0x2, 3), (0x3, 4), (0x2, 4), (0x3, 5), (0x2, 5), (0x7, 7), (0x6, 7), (0xB, 8), (0xA, 8), (0x9, 8), (0x8, 8), (0x7, 8), (0x6, 8),
         (0x17, 10), (0x16, 10), (0x15, 10), (0x14, 10), (0x13, 10), (0x12, 10), (0x23, 11), (0x22, 11), (0x21, 11), (0x20, 11), (0x1F, 11), (0x1E, 11),
         (0x1D, 11), (0x1C, 11), (0x1B, 11), (0x1A, 11), (0x19, 11), (0x18, 11), (0x8, 11), (0xF, 11)], 1));

    public const int MbQuant = 1, MbForward = 2, MbBackward = 4, MbPattern = 8, MbIntra = 16;

    public static readonly Vlc MbTypeI = new(2, [(0x1, 1, MbIntra), (0x1, 2, MbIntra | MbQuant)]);

    public static readonly Vlc MbTypeP = new(6, [
        (0x1, 1, MbForward | MbPattern), (0x1, 2, MbPattern), (0x1, 3, MbForward), (0x3, 5, MbIntra),
        (0x2, 5, MbQuant | MbForward | MbPattern), (0x1, 5, MbQuant | MbPattern), (0x1, 6, MbIntra | MbQuant)]);

    public static readonly Vlc MbTypeB = new(6, [
        (0x2, 2, MbForward | MbBackward), (0x3, 2, MbForward | MbBackward | MbPattern), (0x2, 3, MbBackward), (0x3, 3, MbBackward | MbPattern),
        (0x2, 4, MbForward), (0x3, 4, MbForward | MbPattern), (0x3, 5, MbIntra), (0x2, 5, MbQuant | MbForward | MbBackward | MbPattern),
        (0x3, 6, MbQuant | MbForward | MbPattern), (0x2, 6, MbQuant | MbBackward | MbPattern), (0x1, 6, MbIntra | MbQuant)]);

    public static readonly Vlc CodedBlockPattern = new(9, Build(
        [(0x01, 9), (0x0b, 5), (0x09, 5), (0x0d, 6), (0x0d, 4), (0x17, 7), (0x13, 7), (0x1f, 8), (0x0c, 4), (0x16, 7), (0x12, 7), (0x1e, 8), (0x13, 5), (0x1b, 8), (0x17, 8), (0x13, 8),
         (0x0b, 4), (0x15, 7), (0x11, 7), (0x1d, 8), (0x11, 5), (0x19, 8), (0x15, 8), (0x11, 8), (0x0f, 6), (0x0f, 8), (0x0d, 8), (0x03, 9), (0x0f, 5), (0x0b, 8), (0x07, 8), (0x07, 9),
         (0x0a, 4), (0x14, 7), (0x10, 7), (0x1c, 8), (0x0e, 6), (0x0e, 8), (0x0c, 8), (0x02, 9), (0x10, 5), (0x18, 8), (0x14, 8), (0x10, 8), (0x0e, 5), (0x0a, 8), (0x06, 8), (0x06, 9),
         (0x12, 5), (0x1a, 8), (0x16, 8), (0x12, 8), (0x0d, 5), (0x09, 8), (0x05, 8), (0x05, 9), (0x0c, 5), (0x08, 8), (0x04, 8), (0x04, 9), (0x07, 3), (0x0a, 5), (0x08, 5), (0x0c, 6)], 0));

    public static readonly Vlc MotionCode = new(10, Build(
        [(0x1, 1), (0x1, 2), (0x1, 3), (0x1, 4), (0x3, 6), (0x5, 7), (0x4, 7), (0x3, 7), (0xb, 9), (0xa, 9), (0x9, 9), (0x11, 10), (0x10, 10), (0xf, 10), (0xe, 10), (0xd, 10), (0xc, 10)], 0));

    public static readonly Vlc DcSizeLuma = new(9, Build(
        [(0x4, 3), (0x0, 2), (0x1, 2), (0x5, 3), (0x6, 3), (0xE, 4), (0x1E, 5), (0x3E, 6), (0x7E, 7), (0xFE, 8), (0x1FE, 9), (0x1FF, 9)], 0));

    public static readonly Vlc DcSizeChroma = new(10, Build(
        [(0x0, 2), (0x1, 2), (0x2, 2), (0x6, 3), (0xE, 4), (0x1E, 5), (0x3E, 6), (0x7E, 7), (0xFE, 8), (0x1FE, 9), (0x3FE, 10), (0x3FF, 10)], 0));

    private static readonly (int Run, int Levels)[] RunLevels =
        [(0, 40), (1, 18), (2, 5), (3, 4), (4, 3), (5, 3), (6, 3), (7, 2), (8, 2), (9, 2), (10, 2), (11, 2), (12, 2), (13, 2), (14, 2), (15, 2), (16, 2),
         (17, 1), (18, 1), (19, 1), (20, 1), (21, 1), (22, 1), (23, 1), (24, 1), (25, 1), (26, 1), (27, 1), (28, 1), (29, 1), (30, 1), (31, 1)];

    private static readonly (int Code, int Length)[] DctZeroCodes =
    [
        (0x3, 2), (0x4, 4), (0x5, 5), (0x6, 7), (0x26, 8), (0x21, 8), (0xa, 10), (0x1d, 12), (0x18, 12), (0x13, 12), (0x10, 12), (0x1a, 13), (0x19, 13), (0x18, 13), (0x17, 13),
        (0x1f, 14), (0x1e, 14), (0x1d, 14), (0x1c, 14), (0x1b, 14), (0x1a, 14), (0x19, 14), (0x18, 14), (0x17, 14), (0x16, 14), (0x15, 14), (0x14, 14), (0x13, 14), (0x12, 14), (0x11, 14), (0x10, 14),
        (0x18, 15), (0x17, 15), (0x16, 15), (0x15, 15), (0x14, 15), (0x13, 15), (0x12, 15), (0x11, 15), (0x10, 15),
        (0x3, 3), (0x6, 6), (0x25, 8), (0xc, 10), (0x1b, 12), (0x16, 13), (0x15, 13), (0x1f, 15), (0x1e, 15), (0x1d, 15), (0x1c, 15), (0x1b, 15), (0x1a, 15), (0x19, 15), (0x13, 16), (0x12, 16), (0x11, 16), (0x10, 16),
        (0x5, 4), (0x4, 7), (0xb, 10), (0x14, 12), (0x14, 13),
        (0x7, 5), (0x24, 8), (0x1c, 12), (0x13, 13),
        (0x6, 5), (0xf, 10), (0x12, 12),
        (0x7, 6), (0x9, 10), (0x12, 13),
        (0x5, 6), (0x1e, 12), (0x14, 16),
        (0x4, 6), (0x15, 12),
        (0x7, 7), (0x11, 12),
        (0x5, 7), (0x11, 13),
        (0x27, 8), (0x10, 13),
        (0x23, 8), (0x1a, 16),
        (0x22, 8), (0x19, 16),
        (0x20, 8), (0x18, 16),
        (0xe, 10), (0x17, 16),
        (0xd, 10), (0x16, 16),
        (0x8, 10), (0x15, 16),
        (0x1f, 12), (0x1a, 12), (0x19, 12), (0x17, 12), (0x16, 12), (0x1f, 13), (0x1e, 13), (0x1d, 13), (0x1c, 13), (0x1b, 13), (0x1f, 16), (0x1e, 16), (0x1d, 16), (0x1c, 16), (0x1b, 16),
    ];

    private static readonly (int Code, int Length)[] DctOneCodes =
    [
        (0x02, 2), (0x06, 3), (0x07, 4), (0x1c, 5), (0x1d, 5), (0x05, 6), (0x04, 6), (0x7b, 7), (0x7c, 7), (0x23, 8), (0x22, 8), (0xfa, 8), (0xfb, 8), (0xfe, 8), (0xff, 8),
        (0x1f, 14), (0x1e, 14), (0x1d, 14), (0x1c, 14), (0x1b, 14), (0x1a, 14), (0x19, 14), (0x18, 14), (0x17, 14), (0x16, 14), (0x15, 14), (0x14, 14), (0x13, 14), (0x12, 14), (0x11, 14), (0x10, 14),
        (0x18, 15), (0x17, 15), (0x16, 15), (0x15, 15), (0x14, 15), (0x13, 15), (0x12, 15), (0x11, 15), (0x10, 15),
        (0x02, 3), (0x06, 5), (0x79, 7), (0x27, 8), (0x20, 8), (0x16, 13), (0x15, 13), (0x1f, 15), (0x1e, 15), (0x1d, 15), (0x1c, 15), (0x1b, 15), (0x1a, 15), (0x19, 15), (0x13, 16), (0x12, 16), (0x11, 16), (0x10, 16),
        (0x05, 5), (0x07, 7), (0xfc, 8), (0x0c, 10), (0x14, 13),
        (0x07, 5), (0x26, 8), (0x1c, 12), (0x13, 13),
        (0x06, 6), (0xfd, 8), (0x12, 12),
        (0x07, 6), (0x04, 9), (0x12, 13),
        (0x06, 7), (0x1e, 12), (0x14, 16),
        (0x04, 7), (0x15, 12),
        (0x05, 7), (0x11, 12),
        (0x78, 7), (0x11, 13),
        (0x7a, 7), (0x10, 13),
        (0x21, 8), (0x1a, 16),
        (0x25, 8), (0x19, 16),
        (0x24, 8), (0x18, 16),
        (0x05, 9), (0x17, 16),
        (0x07, 9), (0x16, 16),
        (0x0d, 10), (0x15, 16),
        (0x1f, 12), (0x1a, 12), (0x19, 12), (0x17, 12), (0x16, 12), (0x1f, 13), (0x1e, 13), (0x1d, 13), (0x1c, 13), (0x1b, 13), (0x1f, 16), (0x1e, 16), (0x1d, 16), (0x1c, 16), (0x1b, 16),
    ];

    // Amedo
    public static readonly Vlc DctZero = new(16, Dct(DctZeroCodes, (0x1, 6), (0x2, 2)));
    public static readonly Vlc DctOne = new(16, Dct(DctOneCodes, (0x1, 6), (0x6, 4)));

    public static readonly byte[] ZigZag =
    [
        0, 1, 8, 16, 9, 2, 3, 10, 17, 24, 32, 25, 18, 11, 4, 5, 12, 19, 26, 33, 40, 48, 41, 34, 27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36, 29, 22, 15, 23, 30, 37, 44, 51, 58, 59, 52, 45, 38, 31, 39, 46, 53, 60, 61, 54, 47, 55, 62, 63,
    ];

    public static readonly byte[] Alternate =
    [
        0, 8, 16, 24, 1, 9, 2, 10, 17, 25, 32, 40, 48, 56, 57, 49, 41, 33, 26, 18, 3, 11, 4, 12, 19, 27, 34, 42, 50, 58, 35, 43,
        51, 59, 20, 28, 5, 13, 6, 14, 21, 29, 36, 44, 52, 60, 37, 45, 53, 61, 22, 30, 7, 15, 23, 31, 38, 46, 54, 62, 39, 47, 55, 63,
    ];

    public static readonly byte[] DefaultIntraMatrix =
    [
        8, 16, 19, 22, 26, 27, 29, 34,
        16, 16, 22, 24, 27, 29, 34, 37,
        19, 22, 26, 27, 29, 34, 34, 38,
        22, 22, 26, 27, 29, 34, 37, 40,
        22, 26, 27, 29, 32, 35, 40, 48,
        26, 27, 29, 32, 35, 40, 48, 58,
        26, 27, 29, 34, 38, 46, 56, 69,
        27, 29, 35, 38, 46, 56, 69, 83,
    ];

    public static readonly byte[] NonLinearScale =
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 14, 16, 18, 20, 22, 24, 28, 32, 36, 40, 44, 48, 52, 56, 64, 72, 80, 88, 96, 104, 112];

    private static (int, int, int)[] Build((int Code, int Length)[] codes, int first)
    {
        var list = new (int, int, int)[codes.Length];
        for (int i = 0; i < codes.Length; i++) list[i] = (codes[i].Code, codes[i].Length, i + first);
        return list;
    }

    private static (int, int, int)[] Dct((int Code, int Length)[] codes, (int Code, int Length) escape, (int Code, int Length) eob)
    {
        var list = new List<(int, int, int)>(codes.Length + 2);
        int k = 0;
        foreach (var (run, levels) in RunLevels)
            for (int level = 1; level <= levels; level++, k++)
                list.Add((codes[k].Code, codes[k].Length, run << 8 | level));
        list.Add((escape.Code, escape.Length, Escape));
        list.Add((eob.Code, eob.Length, EndOfBlock));
        return list.ToArray();
    }
}
