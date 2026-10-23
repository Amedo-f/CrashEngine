namespace CrashEngine.Runtime.Video;

// Amedo
public sealed class ByteQueue
{
    public byte[] Buffer = new byte[1 << 16];
    public int Start, Count;

    public void Append(ReadOnlySpan<byte> data)
    {
        if (Start + Count + data.Length > Buffer.Length)
        {
            if (Count + data.Length > Buffer.Length)
            {
                var bigger = new byte[Math.Max(Buffer.Length * 2, Count + data.Length)];
                System.Buffer.BlockCopy(Buffer, Start, bigger, 0, Count);
                Buffer = bigger;
            }
            else System.Buffer.BlockCopy(Buffer, Start, Buffer, 0, Count);
            Start = 0;
        }
        data.CopyTo(Buffer.AsSpan(Start + Count));
        Count += data.Length;
    }

    public void Consume(int n)
    {
        Start += n;
        Count -= n;
        if (Count == 0) Start = 0;
    }

    public void Clear() => Start = Count = 0;
}

// Amedo
public sealed class MpegProgramStream : IDisposable
{
    private readonly Stream _stream;
    private readonly byte[] _packet = new byte[65536 + 16];
    public readonly ByteQueue Video = new(), Audio = new();
    public bool Ended { get; private set; }
    public int AudioTrack { get; set; } // Amedo
    public long Position => _stream.Position;
    public long Length => _stream.Length;

    public MpegProgramStream(Stream stream) => _stream = stream;

    // Amedo
    public bool ReadPacket()
    {
        if (Ended) return false;
        while (true)
        {
            if (!SyncToStartCode(out int code)) { Ended = true; return false; }
            switch (code)
            {
                case 0xBA:
                    if (!Fill(1)) { Ended = true; return false; }
                    if ((_packet[0] & 0xC0) == 0x40)
                    {
                        if (!Fill(9)) { Ended = true; return false; }
                        Skip(_packet[8] & 7);
                    }
                    else Skip(7);
                    continue;
                case 0xB9:
                    Ended = true;
                    return false;
                default:
                    if (code < 0xBB) continue;
                    if (!Fill(2)) { Ended = true; return false; }
                    int length = _packet[0] << 8 | _packet[1];
                    if (!Fill(length)) { Ended = true; return false; }
                    if (code == 0xBD || (code >= 0xC0 && code <= 0xEF)) Payload(code, length);
                    return true;
            }
        }
    }

    private void Payload(int code, int length)
    {
        int p = 0;
        if ((_packet[0] & 0xC0) == 0x80) p = 3 + _packet[2];
        else
        {
            while (p < length && _packet[p] == 0xFF) p++;
            if (p < length && (_packet[p] & 0xC0) == 0x40) p += 2;
            if (p < length && (_packet[p] & 0xF0) == 0x20) p += 5;
            else if (p < length && (_packet[p] & 0xF0) == 0x30) p += 10;
            else p++;
        }
        if (p > length) return;
        var payload = _packet.AsSpan(p, length - p);
        if (code >= 0xE0) Video.Append(payload);
        else if (code == 0xBD && payload.Length > 4 && payload[3] == AudioTrack) Audio.Append(payload[4..]); // Amedo
    }

    private bool SyncToStartCode(out int code)
    {
        code = 0;
        uint window = 0xFFFFFFFF;
        while (true)
        {
            int b = _stream.ReadByte();
            if (b < 0) return false;
            window = window << 8 | (uint)b;
            if ((window & 0xFFFFFF00) == 0x00000100)
            {
                code = (int)(window & 0xFF);
                return true;
            }
        }
    }

    private bool Fill(int n)
    {
        int got = 0;
        while (got < n)
        {
            int r = _stream.Read(_packet, got, n - got);
            if (r <= 0) return false;
            got += r;
        }
        return true;
    }

    private void Skip(int n)
    {
        if (n > 0) Fill(n);
    }

    public void Dispose() => _stream.Dispose();
}
