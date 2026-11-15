using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CrashEngine.Platform.Linux;

// Amedo 2026-11-15
[SupportedOSPlatform("linux")]
internal static unsafe class PulseAudio
{
    private const string Simple = "libpulse-simple.so.0";
    private const string Pulse = "libpulse.so.0";
    private const int Playback = 1;

    public const int FormatU8 = 0;
    public const int FormatS16Le = 3;

    [StructLayout(LayoutKind.Sequential)]
    public struct SampleSpec
    {
        public int Format;
        public uint Rate;
        public byte Channels;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BufferAttr
    {
        public uint MaxLength, TLength, PreBuf, MinReq, FragSize;
    }

    [DllImport(Simple)] private static extern IntPtr pa_simple_new(string? server, string name, int dir, string? dev, string streamName, ref SampleSpec spec, IntPtr map, ref BufferAttr attr, out int error);
    [DllImport(Simple)] private static extern int pa_simple_write(IntPtr s, byte* data, nuint bytes, out int error);
    [DllImport(Simple)] private static extern int pa_simple_drain(IntPtr s, out int error);
    [DllImport(Simple)] private static extern int pa_simple_flush(IntPtr s, out int error);
    [DllImport(Simple)] private static extern void pa_simple_free(IntPtr s);
    [DllImport(Pulse)] private static extern IntPtr pa_strerror(int error);

    public static IntPtr Open(string streamName, SampleSpec spec, uint latencyBytes, out string? error)
    {
        var attr = new BufferAttr
        {
            MaxLength = uint.MaxValue, TLength = latencyBytes, PreBuf = uint.MaxValue,
            MinReq = uint.MaxValue, FragSize = uint.MaxValue,
        };
        try
        {
            IntPtr s = pa_simple_new(null, "CrashEngine", Playback, null, streamName, ref spec, IntPtr.Zero, ref attr, out int code);
            error = s == IntPtr.Zero ? $"PulseAudio: {Message(code)}" : null;
            return s;
        }
        catch (DllNotFoundException)
        {
            error = $"PulseAudio not found ({Simple})";
            return IntPtr.Zero;
        }
    }

    public static bool Write(IntPtr s, short[] pcm)
    {
        fixed (short* p = pcm) return pa_simple_write(s, (byte*)p, (nuint)(pcm.Length * 2), out _) == 0;
    }

    public static bool Write(IntPtr s, byte[] data, int offset, int count)
    {
        fixed (byte* p = data) return pa_simple_write(s, p + offset, (nuint)count, out _) == 0;
    }

    public static void Drain(IntPtr s) => pa_simple_drain(s, out _);

    public static void Flush(IntPtr s) => pa_simple_flush(s, out _);

    public static void Free(IntPtr s) => pa_simple_free(s);

    private static string Message(int code) => Marshal.PtrToStringUTF8(pa_strerror(code)) ?? $"error {code}";
}
