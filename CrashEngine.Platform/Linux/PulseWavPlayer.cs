using System.Buffers.Binary;
using System.Runtime.Versioning;

namespace CrashEngine.Platform.Linux;

// Amedo 2026-11-15
[SupportedOSPlatform("linux")]
internal static class PulseWavPlayer
{
    private const int Chunk = 4096;

    private static readonly object Gate = new();
    private static Thread? _thread;
    private static volatile bool _stop;

    public static bool Play(byte[] wav)
    {
        if (!ReadWav(wav, out var spec, out int start, out int length)) return false;
        lock (Gate)
        {
            StopPlaying();
            uint latency = spec.Rate * spec.Channels * (spec.Format == PulseAudio.FormatU8 ? 1u : 2u) / 10;
            IntPtr s = PulseAudio.Open("CrashEngine preview", spec, latency, out _);
            if (s == IntPtr.Zero) return false;
            _stop = false;
            _thread = new Thread(() => Run(s, wav, start, length)) { IsBackground = true, Name = "CrashEngine preview" };
            _thread.Start();
            return true;
        }
    }

    public static void Stop()
    {
        lock (Gate) StopPlaying();
    }

    private static void StopPlaying()
    {
        _stop = true;
        _thread?.Join(500);
        _thread = null;
    }

    private static void Run(IntPtr s, byte[] wav, int start, int length)
    {
        for (int pos = 0; pos < length && !_stop; pos += Chunk)
            if (!PulseAudio.Write(s, wav, start + pos, Math.Min(Chunk, length - pos))) break;
        if (_stop) PulseAudio.Flush(s);
        else PulseAudio.Drain(s);
        PulseAudio.Free(s);
    }

    private static bool ReadWav(byte[] wav, out PulseAudio.SampleSpec spec, out int start, out int length)
    {
        spec = default;
        start = length = 0;
        var b = wav.AsSpan();
        if (b.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian(b) != 0x46464952 || BinaryPrimitives.ReadUInt32LittleEndian(b[8..]) != 0x45564157) return false;
        bool haveFormat = false;
        for (int i = 12; i + 8 <= b.Length;)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(b[i..]);
            int size = BinaryPrimitives.ReadInt32LittleEndian(b[(i + 4)..]);
            if (size < 0) return false;
            int body = i + 8;
            if (id == 0x20746D66 && body + 16 <= b.Length)
            {
                ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(b[body..]);
                ushort bits = BinaryPrimitives.ReadUInt16LittleEndian(b[(body + 14)..]);
                if (tag != 1 || (bits != 8 && bits != 16)) return false;
                spec.Channels = (byte)BinaryPrimitives.ReadUInt16LittleEndian(b[(body + 2)..]);
                spec.Rate = BinaryPrimitives.ReadUInt32LittleEndian(b[(body + 4)..]);
                spec.Format = bits == 8 ? PulseAudio.FormatU8 : PulseAudio.FormatS16Le;
                haveFormat = true;
            }
            else if (id == 0x61746164)
            {
                start = body;
                length = Math.Min(size, b.Length - body);
                return haveFormat && spec.Channels > 0 && spec.Rate > 0;
            }
            i = body + size + (size & 1);
        }
        return false;
    }
}
