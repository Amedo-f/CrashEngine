namespace CrashEngine.Runtime.Video;

// Amedo
internal static class Mpeg2Idct
{
    private const int W1 = 2841, W2 = 2676, W3 = 2408, W5 = 1609, W6 = 1108, W7 = 565;

    public static void Transform(int[] b)
    {
        for (int r = 0; r < 64; r += 8) Row(b, r);
        for (int c = 0; c < 8; c++) Column(b, c);
    }

    private static void Row(int[] b, int o)
    {
        int x1 = b[o + 4] << 11, x2 = b[o + 6], x3 = b[o + 2], x4 = b[o + 1], x5 = b[o + 7], x6 = b[o + 5], x7 = b[o + 3];
        if ((x1 | x2 | x3 | x4 | x5 | x6 | x7) == 0)
        {
            int v = b[o] << 3;
            for (int k = 0; k < 8; k++) b[o + k] = v;
            return;
        }
        int x0 = (b[o] << 11) + 128;
        int x8 = W7 * (x4 + x5);
        x4 = x8 + (W1 - W7) * x4;
        x5 = x8 - (W1 + W7) * x5;
        x8 = W3 * (x6 + x7);
        x6 = x8 - (W3 - W5) * x6;
        x7 = x8 - (W3 + W5) * x7;
        x8 = x0 + x1;
        x0 -= x1;
        x1 = W6 * (x3 + x2);
        x2 = x1 - (W2 + W6) * x2;
        x3 = x1 + (W2 - W6) * x3;
        x1 = x4 + x6;
        x4 -= x6;
        x6 = x5 + x7;
        x5 -= x7;
        x7 = x8 + x3;
        x8 -= x3;
        x3 = x0 + x2;
        x0 -= x2;
        x2 = (181 * (x4 + x5) + 128) >> 8;
        x4 = (181 * (x4 - x5) + 128) >> 8;
        b[o] = (x7 + x1) >> 8;
        b[o + 1] = (x3 + x2) >> 8;
        b[o + 2] = (x0 + x4) >> 8;
        b[o + 3] = (x8 + x6) >> 8;
        b[o + 4] = (x8 - x6) >> 8;
        b[o + 5] = (x0 - x4) >> 8;
        b[o + 6] = (x3 - x2) >> 8;
        b[o + 7] = (x7 - x1) >> 8;
    }

    private static void Column(int[] b, int o)
    {
        int x1 = b[o + 32] << 8, x2 = b[o + 48], x3 = b[o + 16], x4 = b[o + 8], x5 = b[o + 56], x6 = b[o + 40], x7 = b[o + 24];
        if ((x1 | x2 | x3 | x4 | x5 | x6 | x7) == 0)
        {
            int v = Clip((b[o] + 32) >> 6);
            for (int k = 0; k < 64; k += 8) b[o + k] = v;
            return;
        }
        int x0 = (b[o] << 8) + 8192;
        int x8 = W7 * (x4 + x5) + 4;
        x4 = (x8 + (W1 - W7) * x4) >> 3;
        x5 = (x8 - (W1 + W7) * x5) >> 3;
        x8 = W3 * (x6 + x7) + 4;
        x6 = (x8 - (W3 - W5) * x6) >> 3;
        x7 = (x8 - (W3 + W5) * x7) >> 3;
        x8 = x0 + x1;
        x0 -= x1;
        x1 = W6 * (x3 + x2) + 4;
        x2 = (x1 - (W2 + W6) * x2) >> 3;
        x3 = (x1 + (W2 - W6) * x3) >> 3;
        x1 = x4 + x6;
        x4 -= x6;
        x6 = x5 + x7;
        x5 -= x7;
        x7 = x8 + x3;
        x8 -= x3;
        x3 = x0 + x2;
        x0 -= x2;
        x2 = (181 * (x4 + x5) + 128) >> 8;
        x4 = (181 * (x4 - x5) + 128) >> 8;
        b[o] = Clip((x7 + x1) >> 14);
        b[o + 8] = Clip((x3 + x2) >> 14);
        b[o + 16] = Clip((x0 + x4) >> 14);
        b[o + 24] = Clip((x8 + x6) >> 14);
        b[o + 32] = Clip((x8 - x6) >> 14);
        b[o + 40] = Clip((x0 - x4) >> 14);
        b[o + 48] = Clip((x3 - x2) >> 14);
        b[o + 56] = Clip((x7 - x1) >> 14);
    }

    private static int Clip(int v) => v < -256 ? -256 : v > 255 ? 255 : v;
}
