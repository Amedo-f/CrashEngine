namespace CrashEngine.Runtime.Audio;

// Amedo 2026-09-28
public sealed class SoundMixer
{
    public const int VoiceCount = 48;
    public const int SampleRate = 48000;
    private const int FadeSamples = 240;

    private static readonly int[] Pos = [0, 60, 115, 98, 122];
    private static readonly int[] Neg = [0, 0, -52, -55, -60];
    private static readonly short[] Gauss = BuildGauss(); // Amedo 2026-10-06

    // Amedo 2026-10-06
    private static short[] BuildGauss()
    {
        var t = new double[512];
        for (int n = 0; n < 512; n++)
        {
            double k = 0.5 + n;
            double s = Math.Sin(Math.PI * k * 2.048 / 1024);
            double c1 = (Math.Cos(Math.PI * k * 2.0 / 1023) - 1) * 0.50;
            double c2 = (Math.Cos(Math.PI * k * 4.0 / 1023) - 1) * 0.08;
            t[511 - n] = s * (c1 + c2 + 1.0) / k;
        }
        double sum = 0;
        foreach (double x in t) sum += x;
        double scale = 0x7F80 * 128 / sum;
        for (int n = 0; n < 512; n++) t[n] *= scale;
        var g = new short[256 * 4];
        for (int i = 0; i < 256; i++)
        {
            double a = t[0xFF - i], b = t[0x1FF - i], c = t[0x100 + i], d = t[i];
            double diff = (a + b + c + d - 0x7F80) / 4;
            g[i * 4 + 0] = (short)Math.Round(a - diff, MidpointRounding.AwayFromZero);
            g[i * 4 + 1] = (short)Math.Round(b - diff, MidpointRounding.AwayFromZero);
            g[i * 4 + 2] = (short)Math.Round(c - diff, MidpointRounding.AwayFromZero);
            g[i * 4 + 3] = (short)Math.Round(d - diff, MidpointRounding.AwayFromZero);
        }
        return g;
    }

    private sealed class Voice
    {
        public byte[] Data = [];
        public readonly short[] Decoded = new short[28];
        public int Block, LoopBlock, Index, Flags;
        public int S1, S2;
        public int Counter, Prev, Cur;
        public int Oldest, Older; // Amedo 2026-10-06
        public int Pitch, VolL, VolR;
        public int Fade;
        public int Serial;
        public bool On;
        public bool Held; // Amedo
        public bool Stream;
        public bool StreamLoop = true; // Amedo 2026-09-28
        public int Group; // Amedo 2026-10-06
        public bool Send; // Amedo 2026-11-11
    }

    public const int CoreVoices = 24; // Amedo 2026-11-11
    private readonly SpuReverb[] _reverb = [new(), new()]; // Amedo 2026-11-11
    private readonly Voice[] _voices = new Voice[VoiceCount];
    private readonly int[] _groupL = [0x1000, 0x1000, 0x1000, 0x1000], _groupR = [0x1000, 0x1000, 0x1000, 0x1000]; // Amedo 2026-10-06
    private readonly int[] _effL = new int[VoiceCount], _effR = new int[VoiceCount];
    private readonly object _lock = new();
    private int _serial;

    public float MasterVolume { get; set; } = 1f;
    public bool Muted { get; set; }
    public bool Mono { get; set; } // Amedo

    private readonly short[] _pcm = new short[SampleRate * 2 * 2]; // Amedo
    private int _pcmRead, _pcmCount;
    private long _pcmPlayed;

    public long PcmPlayed { get { lock (_lock) return _pcmPlayed; } } // Amedo
    public int PcmQueued { get { lock (_lock) return _pcmCount; } }
    public int PcmCapacity => _pcm.Length / 2;

    // Amedo
    public int WritePcm(ReadOnlySpan<short> stereo)
    {
        lock (_lock)
        {
            int frames = Math.Min(stereo.Length / 2, _pcm.Length / 2 - _pcmCount);
            int w = (_pcmRead + _pcmCount) % (_pcm.Length / 2);
            for (int i = 0; i < frames; i++)
            {
                _pcm[2 * w] = stereo[2 * i];
                _pcm[2 * w + 1] = stereo[2 * i + 1];
                if (++w == _pcm.Length / 2) w = 0;
            }
            _pcmCount += frames;
            return frames;
        }
    }

    // Amedo
    public void ClearPcm()
    {
        lock (_lock)
        {
            _pcmRead = _pcmCount = 0;
            _pcmPlayed = 0;
        }
    }

    public SoundMixer()
    {
        for (int i = 0; i < VoiceCount; i++) _voices[i] = new Voice();
    }

    public int Start(byte[] adpcm, int pitch, int volL, int volR, bool repeat = true, int group = 0, int key = 0) // Amedo 2026-11-11
    {
        lock (_lock) return Claim(adpcm, pitch, volL, volR, false, repeat, group, key);
    }

    // Amedo 2026-11-11
    public int ClaimBank_1DFBD8(int mode, int evol)
    {
        if (mode == 0) return 0;
        lock (_lock)
        {
            foreach (var bank in _reverb)
                if (bank.Mode == mode && bank.EvolL == evol) return mode;
            int best = 0, score = -1;
            for (int b = 0; b < _reverb.Length; b++)
            {
                int s = _reverb[b].Mode == 0 ? 10000 : FreeVoices(b);
                if (score < s)
                {
                    score = s;
                    best = b;
                }
            }
            _reverb[best].Set_A414(mode, evol, evol);
            return mode;
        }
    }

    // Amedo 2026-11-11
    public (int Mode, int Evol) Bank(int core)
    {
        lock (_lock) return (_reverb[core].Mode, _reverb[core].EvolL);
    }

    private int FreeVoices(int core)
    {
        int n = 0;
        for (int i = core * CoreVoices; i < (core + 1) * CoreVoices; i++)
            if (!_voices[i].On) n++;
        return n;
    }

    private int FreeVoice(int core)
    {
        for (int i = core * CoreVoices; i < (core + 1) * CoreVoices; i++)
            if (!_voices[i].On) return i;
        return -1;
    }

    // Amedo 2026-11-11
    private int Alloc_1DF7A0(int key)
    {
        int i;
        if (key != 0)
        {
            for (int b = 0; b < _reverb.Length; b++)
                if (_reverb[b].Mode == key && (i = FreeVoice(b)) >= 0) return i;
            for (int b = 0; b < _reverb.Length; b++)
                if (_reverb[b].Mode == 0 && (i = FreeVoice(b)) >= 0) return i;
        }
        for (int b = 0; b < _reverb.Length; b++)
            if ((i = FreeVoice(b)) >= 0) return i;
        return -1;
    }

    public (int Left, int Right) StartStream(byte[] left, byte[]? right, int pitch, int volL, int volR, bool loop = true, int group = 0) // Amedo 2026-10-06
    {
        lock (_lock)
        {
            if (right is null) return (Claim(left, pitch, volL, volR, true, loop, group), -1);
            int l = Claim(left, pitch, volL, 0, true, loop, group);
            if (l < 0) return (-1, -1);
            int r = Claim(right, pitch, 0, volR, true, loop, group); // Amedo 2026-10-06
            if (r < 0)
            {
                _voices[l & 63].On = false;
                return (-1, -1);
            }
            return (l, r);
        }
    }

    private int Claim(byte[] adpcm, int pitch, int volL, int volR, bool stream, bool loop = true, int group = 0, int key = 0) // Amedo 2026-11-11
    {
        int i = Alloc_1DF7A0(key);
        if (i >= 0)
        {
            var v = _voices[i];
            v.Send = key != 0;
            v.Data = adpcm;
            v.Stream = stream;
            v.StreamLoop = loop; // Amedo 2026-09-28
            v.Block = 0;
            v.LoopBlock = 0;
            v.S1 = v.S2 = 0;
            v.Counter = 0;
            v.Fade = -1;
            v.Pitch = Math.Clamp(pitch, 0, 0x3FFF);
            v.VolL = Math.Clamp(volL, 0, 0x3FFF);
            v.VolR = Math.Clamp(volR, 0, 0x3FFF);
            v.Group = group is >= 1 and <= 4 ? group : 0; // Amedo 2026-10-06
            v.Held = false; // Amedo
            v.On = true;
            v.Serial = ++_serial & 0x3FFFFFF;
            DecodeBlock(v);
            v.Oldest = v.Older = 0; // Amedo 2026-10-06
            v.Prev = 0;
            v.Cur = NextSample(v);
            return (v.Serial << 6) | i;
        }
        return -1;
    }

    public bool Playing(int handle)
    {
        lock (_lock) return Find(handle) is { On: true };
    }

    public void Set(int handle, int pitch, int volL, int volR)
    {
        lock (_lock)
        {
            if (Find(handle) is not { } v) return;
            v.Pitch = Math.Clamp(pitch, 0, 0x3FFF);
            v.VolL = Math.Clamp(volL, 0, 0x3FFF);
            v.VolR = Math.Clamp(volR, 0, 0x3FFF);
        }
    }

    // Amedo
    public string Describe(int handle)
    {
        lock (_lock) return Find(handle) is { On: true } v ? $"L 0x{v.VolL:X4} R 0x{v.VolR:X4} pitch 0x{v.Pitch:X4}" : "off";
    }

    // Amedo
    public void SetPitch(int handle, int pitch)
    {
        lock (_lock)
        {
            if (Find(handle) is not { } v) return;
            v.Pitch = Math.Clamp(pitch, 0, 0x3FFF);
        }
    }

    public void SetVolume(int handle, int volL, int volR)
    {
        lock (_lock)
        {
            if (Find(handle) is not { } v) return;
            v.VolL = Math.Clamp(volL, 0, 0x3FFF);
            v.VolR = Math.Clamp(volR, 0, 0x3FFF);
        }
    }

    public void Stop(int handle)
    {
        lock (_lock)
        {
            if (Find(handle) is { On: true } v && v.Fade < 0) v.Fade = FadeSamples;
        }
    }

    // Amedo 2026-10-06
    public void SetGroup_1E7CB8(int group, int left, int right)
    {
        if (group is < 1 or > 4 || left is < 0 or > 0x1000 || right is < 0 or > 0x1000) return;
        lock (_lock)
        {
            _groupL[group - 1] = left;
            _groupR[group - 1] = right;
        }
    }

    // Amedo 2026-10-06
    private static int Scale_1E7E90(int group, int volume)
    {
        bool inverted = 0x4000 <= volume;
        int v = inverted ? 0x7FFF - volume : volume;
        int p = v * group;
        p = (p < 0 ? p + 0xFFF : p) >> 12;
        return inverted ? 0x7FFF - p : p;
    }

    public void StopAll()
    {
        lock (_lock)
            foreach (var v in _voices) v.On = false;
    }

    // Amedo
    public void HoldVoices()
    {
        lock (_lock)
            foreach (var v in _voices)
                if (v.On) v.Held = true; // Amedo
    }

    // Amedo
    public void ReleaseVoices()
    {
        lock (_lock)
            foreach (var v in _voices) v.Held = false;
    }

    public void Render(short[] interleaved, int frames)
    {
        lock (_lock)
        {
            int master = Muted ? 0 : (int)(Math.Clamp(MasterVolume, 0f, 2f) * 4096f);
            for (int i = 0; i < VoiceCount; i++) // Amedo 2026-10-06
            {
                var g = _voices[i];
                _effL[i] = g.Group == 0 ? g.VolL : Scale_1E7E90(_groupL[g.Group - 1], g.VolL);
                _effR[i] = g.Group == 0 ? g.VolR : Scale_1E7E90(_groupR[g.Group - 1], g.VolR);
            }
            for (int f = 0; f < frames; f++)
            {
                int l = 0, r = 0;
                int wl0 = 0, wr0 = 0, wl1 = 0, wr1 = 0; // Amedo 2026-11-11
                for (int i = 0; i < VoiceCount; i++) // Amedo 2026-10-06
                {
                    var v = _voices[i];
                    if (!v.On || v.Held) continue;
                    int gi = (v.Counter & 0xFF0) >> 2; // Amedo 2026-10-06
                    int s = ((Gauss[gi] * v.Oldest) >> 15) + ((Gauss[gi + 1] * v.Older) >> 15)
                          + ((Gauss[gi + 2] * v.Prev) >> 15) + ((Gauss[gi + 3] * v.Cur) >> 15);
                    if (0 <= v.Fade)
                    {
                        s = s * v.Fade / FadeSamples;
                        if (--v.Fade <= 0) v.On = false;
                    }
                    int vl = (s * _effL[i]) >> 14, vr = (s * _effR[i]) >> 14; // Amedo 2026-11-11
                    l += vl;
                    r += vr;
                    if (v.Send)
                    {
                        if (i < CoreVoices) { wl0 += vl; wr0 += vr; }
                        else { wl1 += vl; wr1 += vr; }
                    }
                    v.Counter += v.Pitch;
                    while (v.On && 0x1000 <= v.Counter)
                    {
                        v.Counter -= 0x1000;
                        v.Oldest = v.Older; // Amedo 2026-10-06
                        v.Older = v.Prev;
                        v.Prev = v.Cur;
                        v.Cur = NextSample(v);
                    }
                }
                _reverb[0].Mix(wl0, wr0, ref l, ref r); // Amedo 2026-11-11
                _reverb[1].Mix(wl1, wr1, ref l, ref r);
                if (_pcmCount > 0) // Amedo
                {
                    l += _pcm[2 * _pcmRead];
                    r += _pcm[2 * _pcmRead + 1];
                    if (++_pcmRead == _pcm.Length / 2) _pcmRead = 0;
                    _pcmCount--;
                    _pcmPlayed++;
                }
                if (Mono) l = r = (l + r) >> 1; // Amedo
                interleaved[2 * f]     = (short)Math.Clamp((l * master) >> 12, short.MinValue, short.MaxValue);
                interleaved[2 * f + 1] = (short)Math.Clamp((r * master) >> 12, short.MinValue, short.MaxValue);
            }
        }
    }

    private Voice? Find(int handle)
    {
        int i = handle & 63;
        if (handle < 0 || VoiceCount <= i) return null;
        var v = _voices[i];
        return v.Serial == handle >> 6 ? v : null;
    }

    private static int NextSample(Voice v)
    {
        if (v.Index >= 28)
        {
            if (v.Stream)
            {
                if (!v.StreamLoop && v.Data.Length < (v.Block + 2) * 16) // Amedo 2026-09-28
                {
                    v.On = false;
                    return 0;
                }
                v.Block = v.Data.Length < (v.Block + 2) * 16 ? 0 : v.Block + 1;
                DecodeBlock(v);
                return v.On ? v.Decoded[v.Index++] : 0;
            }
            if ((v.Flags & 1) != 0)
            {
                if ((v.Flags & 2) == 0 || v.LoopBlock == v.Block || !v.StreamLoop) // Amedo 2026-09-30
                {
                    v.On = false;
                    return 0;
                }
                v.Block = v.LoopBlock;
            }
            else v.Block++;
            DecodeBlock(v);
            if (!v.On) return 0;
        }
        return v.Decoded[v.Index++];
    }

    private static void DecodeBlock(Voice v)
    {
        int b = v.Block * 16;
        if (v.Data.Length < b + 16)
        {
            v.On = false;
            return;
        }
        int hdr = v.Data[b];
        int shift = hdr & 0xF;
        if (12 < shift) shift = 9;
        int filter = Math.Min((hdr >> 4) & 7, 4);
        v.Flags = v.Data[b + 1];
        if ((v.Flags & 4) != 0) v.LoopBlock = v.Block;
        for (int i = 0; i < 28; i++)
        {
            int x = v.Data[b + 2 + (i >> 1)];
            int n = (i & 1) == 0 ? x & 0xF : x >> 4;
            int s = ((short)(n << 12)) >> shift;
            s += (v.S1 * Pos[filter] + v.S2 * Neg[filter] + 32) >> 6;
            s = Math.Clamp(s, short.MinValue, short.MaxValue);
            v.S2 = v.S1;
            v.S1 = s;
            v.Decoded[i] = (short)s;
        }
        v.Index = 0;
    }
}
