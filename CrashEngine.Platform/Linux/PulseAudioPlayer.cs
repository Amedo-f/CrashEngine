using System.Runtime.Versioning;

namespace CrashEngine.Platform.Linux;

// Amedo 2026-11-15
[SupportedOSPlatform("linux")]
public sealed class PulseAudioPlayer : ISoundStream
{
    private const int Buffers = 4;
    private const int Frames = 768;

    private readonly Action<short[], int> _render;
    private readonly short[] _pcm = new short[Frames * 2];
    private readonly Thread? _thread;
    private IntPtr _stream;
    private volatile bool _running;

    public string? Error { get; }
    public int BuffersWritten { get; private set; }

    public PulseAudioPlayer(uint sampleRate, Action<short[], int> render)
    {
        _render = render;
        var spec = new PulseAudio.SampleSpec { Format = PulseAudio.FormatS16Le, Rate = sampleRate, Channels = 2 };
        _stream = PulseAudio.Open("CrashEngine game", spec, Buffers * Frames * 4, out var error);
        if (_stream == IntPtr.Zero)
        {
            Error = error;
            return;
        }
        _running = true;
        _thread = new Thread(Run) { IsBackground = true, Name = "CrashEngine audio", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    private void Run()
    {
        while (_running)
        {
            _render(_pcm, Frames);
            if (!PulseAudio.Write(_stream, _pcm)) break;
            BuffersWritten++;
        }
    }

    public void Dispose()
    {
        _running = false;
        _thread?.Join(500);
        if (_stream == IntPtr.Zero) return;
        PulseAudio.Flush(_stream);
        PulseAudio.Free(_stream);
        _stream = IntPtr.Zero;
    }
}
