namespace CrashEngine.Runtime.Video;

// Amedo
public sealed class Mpeg2Deblock
{
    private byte[] _y = Array.Empty<byte>(), _cb = Array.Empty<byte>(), _cr = Array.Empty<byte>();

    public byte[] Y => _y;
    public byte[] Cb => _cb;
    public byte[] Cr => _cr;

    // Amedo
    public void Apply(Mpeg2Frame f)
    {
        Copy(f.Y, ref _y);
        Copy(f.Cb, ref _cb);
        Copy(f.Cr, ref _cr);
        int cw = (f.Width + 1) >> 1, ch = (f.Height + 1) >> 1;
        Filter(_y, f.Stride, f.Width, f.Height, 16, f, true);
        Filter(_cb, f.ChromaStride, cw, ch, 8, f, false);
        Filter(_cr, f.ChromaStride, cw, ch, 8, f, false);
    }

    private static void Copy(byte[] from, ref byte[] to)
    {
        if (to.Length != from.Length) to = new byte[from.Length];
        Buffer.BlockCopy(from, 0, to, 0, from.Length);
    }

    private static int Q(Mpeg2Frame f, int x, int y, int mbSize)
    {
        int mbx = Math.Min(x / mbSize, f.MbWidth - 1), mby = Math.Min(y / mbSize, f.MbHeight - 1);
        return f.Quant[mby * f.MbWidth + mbx];
    }

    // Amedo
    private static void Filter(byte[] p, int stride, int w, int h, int mbSize, Mpeg2Frame f, bool luma)
    {
        for (int x = 8; x < w; x += 8)
            for (int y0 = 0; y0 < h; y0 += 8)
            {
                int qp = (Q(f, x - 1, y0, mbSize) + Q(f, x, y0, mbSize) + 1) >> 1;
                for (int y = y0, end = Math.Min(y0 + 8, h); y < end; y++) Edge(p, y * stride + x, 1, qp, luma);
            }
        for (int y = 8; y < h; y += 8)
            for (int x0 = 0; x0 < w; x0 += 8)
            {
                int qp = (Q(f, x0, y - 1, mbSize) + Q(f, x0, y, mbSize) + 1) >> 1;
                for (int x = x0, end = Math.Min(x0 + 8, w); x < end; x++) Edge(p, y * stride + x, stride, qp, luma);
            }
    }

    // Amedo
    private static void Edge(byte[] p, int q0i, int step, int qp, bool luma)
    {
        int alpha = Math.Min(2 * qp + 1, 64), beta = Math.Min(qp * 3 / 4 + 1, 24), tc = Math.Min(qp / 4 + 1, 12);
        int p0 = p[q0i - step], p1 = p[q0i - 2 * step], q0 = p[q0i], q1 = p[q0i + step];
        if (Math.Abs(p0 - q0) >= alpha || Math.Abs(p1 - p0) >= beta || Math.Abs(q1 - q0) >= beta) return;
        if (luma)
        {
            int p2 = p[q0i - 3 * step], p3 = p[q0i - 4 * step], q2 = p[q0i + 2 * step], q3 = p[q0i + 3 * step];
            if (Math.Abs(p0 - q0) < (alpha >> 2) + 2 && Math.Abs(p2 - p0) < beta && Math.Abs(q2 - q0) < beta)
            {
                p[q0i - step] = (byte)((p2 + 2 * p1 + 2 * p0 + 2 * q0 + q1 + 4) >> 3);
                p[q0i - 2 * step] = (byte)((p2 + p1 + p0 + q0 + 2) >> 2);
                p[q0i - 3 * step] = (byte)((2 * p3 + 3 * p2 + p1 + p0 + q0 + 4) >> 3);
                p[q0i] = (byte)((p1 + 2 * p0 + 2 * q0 + 2 * q1 + q2 + 4) >> 3);
                p[q0i + step] = (byte)((p0 + q0 + q1 + q2 + 2) >> 2);
                p[q0i + 2 * step] = (byte)((2 * q3 + 3 * q2 + q1 + q0 + p0 + 4) >> 3);
                return;
            }
        }
        int delta = Math.Clamp(((q0 - p0) * 4 + (p1 - q1) + 4) >> 3, -tc, tc);
        p[q0i - step] = (byte)Math.Clamp(p0 + delta, 0, 255);
        p[q0i] = (byte)Math.Clamp(q0 - delta, 0, 255);
    }
}
