namespace CrashEngine.Runtime.Video;

// Amedo
public sealed class Mpeg2Upscale
{
    private const int Na = -9, Nb = 111, Nc = 29, Nd = -3;
    private int[] _rows = Array.Empty<int>();
    private int[] _cols = Array.Empty<int>(), _lines = Array.Empty<int>();
    private int _w, _h;

    // Amedo
    public void Apply(byte[] src, int w, int h, byte[] dst)
    {
        int w2 = w * 2, h2 = h * 2;
        if (_w != w || _h != h) Prepare(w, h);
        var rows = _rows;
        var cols = _cols;
        for (int y = 0; y < h; y++)
        {
            int so = y * w * 4, ro = y * w2 * 3;
            for (int x = 0; x < w2; x++)
            {
                int t = x * 4;
                int a0 = so + cols[t], a1 = so + cols[t + 1], a2 = so + cols[t + 2], a3 = so + cols[t + 3];
                int o = ro + x * 3;
                if ((x & 1) == 0)
                {
                    rows[o] = Nd * src[a0] + Nc * src[a1] + Nb * src[a2] + Na * src[a3];
                    rows[o + 1] = Nd * src[a0 + 1] + Nc * src[a1 + 1] + Nb * src[a2 + 1] + Na * src[a3 + 1];
                    rows[o + 2] = Nd * src[a0 + 2] + Nc * src[a1 + 2] + Nb * src[a2 + 2] + Na * src[a3 + 2];
                }
                else
                {
                    rows[o] = Na * src[a0] + Nb * src[a1] + Nc * src[a2] + Nd * src[a3];
                    rows[o + 1] = Na * src[a0 + 1] + Nb * src[a1 + 1] + Nc * src[a2 + 1] + Nd * src[a3 + 1];
                    rows[o + 2] = Na * src[a0 + 2] + Nb * src[a1 + 2] + Nc * src[a2 + 2] + Nd * src[a3 + 2];
                }
            }
        }
        var lines = _lines;
        int n = w2 * 3;
        for (int y = 0; y < h2; y++)
        {
            int t = y * 4;
            int r0 = lines[t], r1 = lines[t + 1], r2 = lines[t + 2], r3 = lines[t + 3];
            int k0, k1, k2, k3;
            if ((y & 1) == 0) { k0 = Nd; k1 = Nc; k2 = Nb; k3 = Na; }
            else { k0 = Na; k1 = Nb; k2 = Nc; k3 = Nd; }
            int o = y * w2 * 4;
            for (int i = 0; i < n; i += 3, o += 4)
            {
                dst[o] = Pack(k0 * rows[r0 + i] + k1 * rows[r1 + i] + k2 * rows[r2 + i] + k3 * rows[r3 + i]);
                dst[o + 1] = Pack(k0 * rows[r0 + i + 1] + k1 * rows[r1 + i + 1] + k2 * rows[r2 + i + 1] + k3 * rows[r3 + i + 1]);
                dst[o + 2] = Pack(k0 * rows[r0 + i + 2] + k1 * rows[r1 + i + 2] + k2 * rows[r2 + i + 2] + k3 * rows[r3 + i + 2]);
                dst[o + 3] = 255;
            }
        }
    }

    private static byte Pack(int v)
    {
        v = (v + 8192) >> 14;
        return (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
    }

    private void Prepare(int w, int h)
    {
        _w = w;
        _h = h;
        int w2 = w * 2, h2 = h * 2;
        _rows = new int[w2 * h * 3];
        _cols = new int[w2 * 4];
        for (int x = 0; x < w2; x++)
        {
            int b = (x & 1) == 0 ? (x >> 1) - 2 : (x >> 1) - 1;
            for (int k = 0; k < 4; k++) _cols[x * 4 + k] = Math.Clamp(b + k, 0, w - 1) * 4;
        }
        _lines = new int[h2 * 4];
        for (int y = 0; y < h2; y++)
        {
            int b = (y & 1) == 0 ? (y >> 1) - 2 : (y >> 1) - 1;
            for (int k = 0; k < 4; k++) _lines[y * 4 + k] = Math.Clamp(b + k, 0, h - 1) * w2 * 3;
        }
    }
}
