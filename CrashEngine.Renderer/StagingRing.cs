using CrashEngine.Core;
using Silk.NET.OpenGL;

namespace CrashEngine.Renderer;

// Amedo
internal static class StagingRing
{
    private const long Size = 64L * 1024 * 1024;
    private const int FramesInFlight = 5;

    private static GL? _gl;
    private static uint _buffer;
    private static unsafe byte* _base;
    private static long _head;
    private static long _frame = -1;
    private static readonly long[] FrameBytes = new long[FramesInFlight];

    public static bool Enabled { get; set; } = true;

    static StagingRing() => Engine.GpuContextStarted += ForgetContext;

    private static unsafe void ForgetContext()
    {
        _gl = null;
        _buffer = 0;
        _base = null;
        _head = 0;
        _frame = -1;
        Array.Clear(FrameBytes);
    }

    // Amedo
    public static unsafe bool Copy(GL gl, uint target, long targetOffset, void* data, long bytes)
    {
        if (!Enabled || !GlCaps.Dsa || bytes <= 0 || bytes > Size / 4 || !Ready(gl)) return false;
        long now = PerfStats.FrameNumber;
        if (now != _frame)
        {
            for (long f = Math.Max(_frame + 1, now - FramesInFlight + 1); f <= now; f++) FrameBytes[f % FramesInFlight] = 0;
            _frame = now;
        }
        long skipped = _head + bytes > Size ? Size - _head : 0;
        long recent = 0;
        foreach (long b in FrameBytes) recent += b;
        if (recent + skipped + bytes > Size) return false;
        int slot = (int)(now % FramesInFlight);
        if (skipped > 0)
        {
            FrameBytes[slot] += skipped;
            _head = 0;
        }
        System.Buffer.MemoryCopy(data, _base + _head, bytes, bytes);
        gl.CopyNamedBufferSubData(_buffer, target, (nint)_head, (nint)targetOffset, (nuint)bytes);
        FrameBytes[slot] += bytes;
        _head += bytes;
        return true;
    }

    private static unsafe bool Ready(GL gl)
    {
        if (_gl is not null) return _base != null;
        _gl = gl;
        var flags = BufferStorageMask.MapWriteBit | BufferStorageMask.MapPersistentBit | BufferStorageMask.MapCoherentBit;
        _buffer = gl.CreateBuffer();
        gl.NamedBufferStorage(_buffer, (nuint)Size, null, flags);
        _base = (byte*)gl.MapNamedBufferRange(_buffer, 0, (nuint)Size,
            MapBufferAccessMask.WriteBit | MapBufferAccessMask.PersistentBit | MapBufferAccessMask.CoherentBit);
        return _base != null;
    }
}
