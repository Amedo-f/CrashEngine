namespace CrashEngine.Runtime.Video;

// Amedo
public sealed class PssAudio
{
    public int Type { get; private set; } = -1;
    public int Rate { get; private set; }
    public int Channels { get; private set; }
    public int Interleave { get; private set; }
    public bool Ready { get; private set; }
    public bool Supported => Ready && Type == 1 && Interleave > 0 && Channels is 1 or 2;

    // Amedo
    public int Take(ByteQueue q, short[] dst, int maxFrames)
    {
        if (!Ready)
        {
            if (q.Count < 40) return 0;
            var h = q.Buffer.AsSpan(q.Start, 40);
            if (h[0] != 'S' || h[1] != 'S' || h[2] != 'h' || h[3] != 'd')
            {
                q.Clear();
                return 0;
            }
            Type = I(h, 8);
            Rate = I(h, 12);
            Channels = I(h, 16);
            Interleave = I(h, 20);
            int skip = 8 + I(h, 4);
            if (q.Count < skip + 8) return 0;
            q.Consume(skip + 8);
            Ready = true;
        }
        if (!Supported) { q.Clear(); return 0; }
        int block = Interleave * Channels;
        int frames = 0;
        while (q.Count >= block && frames + Interleave / 2 <= Math.Min(maxFrames, dst.Length / 2))
        {
            var b = q.Buffer.AsSpan(q.Start, block);
            for (int k = 0; k < Interleave / 2; k++, frames++)
            {
                short l = (short)(b[2 * k] | b[2 * k + 1] << 8);
                short r = Channels == 2 ? (short)(b[Interleave + 2 * k] | b[Interleave + 2 * k + 1] << 8) : l;
                dst[2 * frames] = l;
                dst[2 * frames + 1] = r;
            }
            q.Consume(block);
        }
        return frames;
    }

    private static int I(ReadOnlySpan<byte> b, int o) => b[o] | b[o + 1] << 8 | b[o + 2] << 16 | b[o + 3] << 24;
}
