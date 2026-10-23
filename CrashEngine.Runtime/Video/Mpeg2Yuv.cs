namespace CrashEngine.Runtime.Video;

// Amedo
public static class Mpeg2Yuv
{
    private static readonly int[] YTab = new int[256], RV = new int[256], GU = new int[256], GV = new int[256], BU = new int[256];

    static Mpeg2Yuv()
    {
        for (int i = 0; i < 256; i++)
        {
            YTab[i] = (int)Math.Round(1.164383 * (i - 16) * 65536);
            RV[i] = (int)Math.Round(1.596027 * (i - 128) * 65536);
            GU[i] = (int)Math.Round(-0.391762 * (i - 128) * 65536);
            GV[i] = (int)Math.Round(-0.812968 * (i - 128) * 65536);
            BU[i] = (int)Math.Round(2.017232 * (i - 128) * 65536);
        }
    }

    public static void ToRgba(Mpeg2Frame f, byte[] rgba) => ToRgba(f, f.Y, f.Cb, f.Cr, rgba); // Amedo

    [ThreadStatic] private static int[]? _cbRow, _crRow; // Amedo

    // Amedo
    public static void ToRgba(Mpeg2Frame f, byte[] lum, byte[] cbPlane, byte[] crPlane, byte[] rgba)
    {
        int w = f.Width, h = f.Height, cw = (w + 1) >> 1, ch = (h + 1) >> 1;
        if (_cbRow is null || _crRow is null || _cbRow.Length < cw + 1) { _cbRow = new int[cw + 1]; _crRow = new int[cw + 1]; }
        int[] cbRow = _cbRow, crRow = _crRow;
        for (int y = 0; y < h; y++)
        {
            int near = y >> 1, far = (y & 1) == 0 ? Math.Max(near - 1, 0) : Math.Min(near + 1, ch - 1);
            int no = near * f.ChromaStride, fo = far * f.ChromaStride;
            for (int c = 0; c < cw; c++)
            {
                cbRow[c] = 3 * cbPlane[no + c] + cbPlane[fo + c];
                crRow[c] = 3 * crPlane[no + c] + crPlane[fo + c];
            }
            cbRow[cw] = cbRow[cw - 1];
            crRow[cw] = crRow[cw - 1];
            int yo = y * f.Stride, o = y * w * 4;
            for (int x = 0; x < w; x++, o += 4)
            {
                int c = x >> 1;
                int cb4 = (x & 1) == 0 ? cbRow[c] * 2 : cbRow[c] + cbRow[c + 1];
                int cr4 = (x & 1) == 0 ? crRow[c] * 2 : crRow[c] + crRow[c + 1];
                int cb = (cb4 + 4) >> 3, cr = (cr4 + 4) >> 3;
                int yy = YTab[lum[yo + x]];
                rgba[o] = Clamp(yy + RV[cr]);
                rgba[o + 1] = Clamp(yy + GU[cb] + GV[cr]);
                rgba[o + 2] = Clamp(yy + BU[cb]);
                rgba[o + 3] = 255;
            }
        }
    }

    private static byte Clamp(int v)
    {
        v = (v + 32768) >> 16;
        return (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
    }
}
