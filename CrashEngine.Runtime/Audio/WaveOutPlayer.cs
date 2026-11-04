using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace CrashEngine.Runtime.Audio;

// Amedo 2026-09-28
[SupportedOSPlatform("windows")]
public sealed class WaveOutPlayer : IAudioOutput // Amedo 2026-10-26
{
    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormat
    {
        public ushort Tag, Channels;
        public uint SamplesPerSec, AvgBytesPerSec;
        public ushort BlockAlign, BitsPerSample, Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public uint BufferLength, BytesRecorded;
        public IntPtr User;
        public uint Flags, Loops;
        public IntPtr Next, Reserved;
    }

    [DllImport("winmm.dll")] private static extern int waveOutOpen(out IntPtr device, uint id, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] private static extern int waveOutPrepareHeader(IntPtr device, IntPtr header, int size);
    [DllImport("winmm.dll")] private static extern int waveOutUnprepareHeader(IntPtr device, IntPtr header, int size);
    [DllImport("winmm.dll")] private static extern int waveOutWrite(IntPtr device, IntPtr header, int size);
    [DllImport("winmm.dll")] private static extern int waveOutReset(IntPtr device);
    [DllImport("winmm.dll")] private static extern int waveOutClose(IntPtr device);

    private const uint WaveMapper = 0xFFFFFFFF;
    private const uint CallbackEvent = 0x00050000;
    private const int Done = 1;
    private const int Buffers = 4;
    private const int Frames = 768;

    private readonly SoundMixer _mixer;
    private readonly AutoResetEvent _event = new(false);
    private readonly IntPtr[] _headers = new IntPtr[Buffers];
    private readonly IntPtr[] _data = new IntPtr[Buffers];
    private readonly short[] _pcm = new short[Frames * 2];
    private readonly int _headerSize = Marshal.SizeOf<WaveHeader>();
    private readonly int _flagsOffset = (int)Marshal.OffsetOf<WaveHeader>(nameof(WaveHeader.Flags));
    private readonly Thread? _thread;
    private IntPtr _device;
    private volatile bool _running;

    public string? Error { get; }
    public int BuffersWritten { get; private set; }

    public WaveOutPlayer(SoundMixer mixer)
    {
        _mixer = mixer;
        var format = new WaveFormat
        {
            Tag = 1, Channels = 2, SamplesPerSec = SoundMixer.SampleRate, AvgBytesPerSec = SoundMixer.SampleRate * 4,
            BlockAlign = 4, BitsPerSample = 16,
        };
        int rc = waveOutOpen(out _device, WaveMapper, ref format, _event.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CallbackEvent);
        if (rc != 0)
        {
            Error = $"waveOutOpen failed ({rc})";
            _device = IntPtr.Zero;
            return;
        }
        for (int i = 0; i < Buffers; i++)
        {
            _data[i] = Marshal.AllocHGlobal(Frames * 4);
            _headers[i] = Marshal.AllocHGlobal(_headerSize);
            Marshal.StructureToPtr(new WaveHeader { Data = _data[i], BufferLength = Frames * 4 }, _headers[i], false);
            waveOutPrepareHeader(_device, _headers[i], _headerSize);
            SetDone(i, true);
        }
        _running = true;
        _thread = new Thread(Run) { IsBackground = true, Name = "CrashEngine audio", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    private bool IsDone(int i) => (Marshal.ReadInt32(_headers[i], _flagsOffset) & Done) != 0;

    private void SetDone(int i, bool done)
    {
        int flags = Marshal.ReadInt32(_headers[i], _flagsOffset);
        Marshal.WriteInt32(_headers[i], _flagsOffset, done ? flags | Done : flags & ~Done);
    }

    private void Run()
    {
        int next = 0;
        while (_running)
        {
            if (!IsDone(next))
            {
                _event.WaitOne(20);
                continue;
            }
            _mixer.Render(_pcm, Frames);
            Marshal.Copy(_pcm, 0, _data[next], _pcm.Length);
            SetDone(next, false);
            waveOutWrite(_device, _headers[next], _headerSize);
            BuffersWritten++;
            next = (next + 1) % Buffers;
        }
    }

    public void Dispose()
    {
        _running = false;
        _event.Set();
        _thread?.Join(500);
        if (_device != IntPtr.Zero)
        {
            waveOutReset(_device);
            foreach (var h in _headers)
                if (h != IntPtr.Zero) waveOutUnprepareHeader(_device, h, _headerSize);
            waveOutClose(_device);
            _device = IntPtr.Zero;
        }
        for (int i = 0; i < Buffers; i++)
        {
            if (_headers[i] != IntPtr.Zero) Marshal.FreeHGlobal(_headers[i]);
            if (_data[i] != IntPtr.Zero) Marshal.FreeHGlobal(_data[i]);
            _headers[i] = _data[i] = IntPtr.Zero;
        }
        _event.Dispose();
    }
}
