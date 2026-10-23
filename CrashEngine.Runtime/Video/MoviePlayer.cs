namespace CrashEngine.Runtime.Video;

// Amedo
public sealed class MoviePlayer : IDisposable
{
    public uint Word_00;
    public int Width_04, Height_08;
    public string Name_0C = "";

    public Func<string, string?>? Resolve { get; set; }
    public Audio.SoundMixer? Mixer { get; set; }
    public Action<string>? Log { get; set; }

    public byte[]? Frame { get; private set; }
    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }
    public int FrameVersion { get; private set; }
    public int AspectCode { get; private set; }

    private const int QueueFrames = 4;
    private readonly object _lock = new();
    private readonly Queue<(byte[] Rgba, int Index, int W, int H)> _frames = new(); // Amedo
    private readonly Stack<byte[]> _free = new();
    private Thread? _thread;
    private volatile bool _stop, _decodeDone, _hasAudio;
    private double _fps = 30, _clock;
    private int _shownIndex = -1;

    public bool Playing => (Word_00 & 0xF000) == 0x2000;
    public bool Wide => (Word_00 & 0x40) != 0; // Amedo

    // Amedo
    public static (System.Numerics.Vector2 Min, System.Numerics.Vector2 Size) Rect_2AEB30(bool widescreen, bool wide, System.Numerics.Vector2 frame)
    {
        if (widescreen == wide) return (System.Numerics.Vector2.Zero, System.Numerics.Vector2.One);
        if (widescreen) return (new(0.5f - 0.375f, 0f), new(0.75f, 1f));
        float w = frame.X / frame.Y;
        return (new(0f, 0.5f - 0.28125f * w), new(1f, 0.5625f * w));
    }

    // Amedo
    public bool Start_2AFB18(string name, int track, bool wide, int width, int height)
    {
        if ((Word_00 & 0xF000) != 0) return false;
        Name_0C = name;
        uint w = Word_00;
        w = (w & ~0x40u) | ((wide ? 1u : 0u) << 6);
        w = (w & ~0x3Fu) | ((uint)track & 0x3F);
        Width_04 = width;
        Height_08 = height;
        Word_00 = (w & ~0xF00u) | 0x100;
        return true;
    }

    // Amedo
    public bool Stop_2AFBC8()
    {
        uint w = Word_00;
        if ((w & 0xF000) != 0x2000) return false;
        Word_00 = (w & ~0xF00u) | 0x300;
        return true;
    }

    // Amedo
    public bool Update_2AF0D8(double dt, Action? pauseSounds)
    {
        uint w = Word_00;
        if ((w & 0xF00) != 0x400)
        {
            w = (w & 0xFFFF0FFFu) | ((w << 4) & 0xF000);
            Word_00 = w = (w & ~0xF00u) | 0x400;
        }
        switch ((w >> 12) & 0xF)
        {
            case 1:
                pauseSounds?.Invoke();
                if (Open_0C())
                {
                    Word_00 = (Word_00 & 0xFFFF0FFFu) | 0x2000;
                    return true;
                }
                Word_00 &= 0xFFFF0FFFu;
                return false;
            case 2:
                if (Frame_24(dt)) return true;
                Word_00 &= 0xFFFF0FFFu;
                Close_14();
                return false;
            case 3:
                Word_00 &= ~0xF00u;
                Close_14();
                return false;
            default:
                return false;
        }
    }

    // Amedo
    private bool Open_0C()
    {
        string? path = Resolve?.Invoke(Name_0C);
        if (path is null || !File.Exists(path))
        {
            Log?.Invoke($"[movie] '{Name_0C}' not found");
            return false;
        }
        Close_14();
        _stop = false;
        _decodeDone = false;
        _hasAudio = false;
        _clock = 0;
        _shownIndex = -1;
        Frame = null;
        Mixer?.ClearPcm();
        _thread = new Thread(() => Decode(path)) { IsBackground = true, Name = "CrashEngine movie" };
        _thread.Start();
        Log?.Invoke($"[movie] '{Name_0C}' -> {path} ({Width_04}x{Height_08}, wide {(Word_00 >> 6) & 1})");
        return true;
    }

    // Amedo
    private bool Frame_24(double dt)
    {
        var mixer = Mixer;
        long played = mixer?.PcmPlayed ?? 0;
        if (_hasAudio && mixer is not null && played > 0) _clock = played / (double)Audio.SoundMixer.SampleRate;
        else _clock += dt;
        int due = (int)(_clock * _fps);
        lock (_lock)
        {
            while (_frames.Count > 0 && _frames.Peek().Index <= due)
            {
                var (rgba, index, w, h) = _frames.Dequeue(); // Amedo
                if (Frame is not null) _free.Push(Frame);
                Frame = rgba;
                FrameWidth = w;
                FrameHeight = h;
                _shownIndex = index;
                FrameVersion++;
            }
            Monitor.PulseAll(_lock);
            if (_decodeDone && _frames.Count == 0) return mixer is not null && _hasAudio && played > 0 && mixer.PcmQueued > 0;
        }
        return true;
    }

    // Amedo
    private void Close_14()
    {
        _stop = true;
        lock (_lock) Monitor.PulseAll(_lock);
        _thread?.Join();
        _thread = null;
        lock (_lock)
        {
            while (_frames.Count > 0) _free.Push(_frames.Dequeue().Rgba);
        }
        Mixer?.ClearPcm();
        Frame = null;
    }

    // Amedo
    private void Decode(string path)
    {
        try
        {
            using var reader = new Mpeg2Reader(File.OpenRead(path));
            reader.Stream.AudioTrack = (int)(Word_00 & 0x3F); // Amedo
            var audio = new PssAudio();
            var pcm = new short[16384];
            int index = 0;
            var watch = new System.Diagnostics.Stopwatch(); // Amedo
            double average = 0;
            while (!_stop)
            {
                lock (_lock)
                    while (!_stop && _frames.Count >= QueueFrames) Monitor.Wait(_lock, 50);
                if (_stop) break;
                watch.Restart(); // Amedo
                var rgba = reader.NextFrame();
                double spent = watch.Elapsed.TotalSeconds;
                PushAudio(reader.Stream.Audio, audio, pcm);
                if (rgba is null) break;
                if (index == 0)
                {
                    _fps = reader.Decoder.FrameRate;
                    AspectCode = reader.Decoder.AspectCode;
                }
                else KeepUp(reader, average = average * 0.8 + spent * 0.2); // Amedo
                int dw = reader.Decoder.Width, dh = reader.Decoder.Height, scale = rgba.Length == dw * dh * 16 ? 2 : 1; // Amedo
                byte[] copy;
                lock (_lock) copy = _free.Count > 0 && _free.Peek().Length == rgba.Length ? _free.Pop() : new byte[rgba.Length];
                Buffer.BlockCopy(rgba, 0, copy, 0, rgba.Length);
                lock (_lock) _frames.Enqueue((copy, index++, dw * scale, dh * scale));
            }
            long lastPlayed = Mixer?.PcmPlayed ?? 0;
            int stalled = 0;
            while (!_stop && audio.Supported && reader.Stream.Audio.Count > 0 && stalled < 25)
            {
                if (PushAudio(reader.Stream.Audio, audio, pcm)) continue;
                Thread.Sleep(20);
                long played = Mixer?.PcmPlayed ?? 0;
                stalled = played == lastPlayed ? stalled + 1 : 0;
                lastPlayed = played;
            }
            Log?.Invoke($"[movie] decoded {index} frames, decoder errors {reader.Decoder.Errors}{(reader.Decoder.LastError is { } e ? $" ({e})" : "")}");
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[movie] decode failed: {ex.Message}");
        }
        _decodeDone = true;
    }

    // Amedo
    private void KeepUp(Mpeg2Reader reader, double average)
    {
        double period = 1.0 / _fps;
        if (reader.Upscale && average > period * 0.8)
        {
            reader.Upscale = false;
            Log?.Invoke($"[movie] {average * 1000:0.0} ms a frame (budget {period * 1000:0.0}): the x2 smoothing is off for this movie");
        }
        else if (!reader.Upscale && reader.Clean && average > period * 0.9)
        {
            reader.Clean = false;
            Log?.Invoke($"[movie] {average * 1000:0.0} ms a frame (budget {period * 1000:0.0}): the deblocking is off for this movie");
        }
    }

    private bool PushAudio(ByteQueue q, PssAudio audio, short[] pcm)
    {
        if (Mixer is not { } mixer) { q.Clear(); return false; }
        int space = mixer.PcmCapacity - mixer.PcmQueued;
        if (space <= 0) return false;
        int frames = audio.Take(q, pcm, Math.Min(pcm.Length / 2, space));
        if (frames <= 0) return false;
        if (audio.Rate != Audio.SoundMixer.SampleRate) return false;
        _hasAudio = true;
        mixer.WritePcm(pcm.AsSpan(0, frames * 2));
        return true;
    }

    public void Dispose() => Close_14();
}
