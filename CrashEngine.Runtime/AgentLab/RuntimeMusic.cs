using System.Numerics;
using CrashEngine.Runtime.Audio;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed class RuntimeMusic
{
    public const int Free = 1, Prepare = 2, Ready = 3, FadeIn = 4, Playing = 5, FadeOut = 6, Silent = 7, Release = 8;

    private sealed class Player
    {
        public int State = Free;
        public int Id = -1;
        public bool Stereo, StopAfterFade;
        public float Time, TargetL, TargetR, StartL, StartR, CurL, CurR;
        public int Left = -1, Right = -1;
        public (byte[] Left, byte[]? Right)? Held; // Amedo 2026-09-28
        public int Pitch;
        public bool Loop = true;
        public int Group; // Amedo 2026-10-06
    }

    private sealed record Request(int Id, float Volume, float Volume2, float Time, string Who, bool AutoStart = true, bool Loop = true, int Group = 0); // Amedo 2026-10-06

    private readonly SoundMixer _mixer;
    private readonly MusicBank? _music;
    private readonly MusicBank? _speech;
    private readonly Player[] _players = [new(), new(), new()];
    private readonly Player?[] _channels = new Player?[4];
    private readonly Request?[] _pending = new Request?[4];
    private readonly (float L, float R, float T)?[] _playOnReady = new (float, float, float)?[4]; // Amedo

    public Action<string>? Log { get; set; }
    public Action<string, int, string>? Unimplemented { get; set; }
    public Func<double>? Clock { get; set; }
    public bool Available => _music is not null;
    public RuntimeSounds? Listener { get; set; } // Amedo

    // Amedo
    public const int EmitterCount = 32;

    private sealed class Emitter
    {
        public Func<bool> Gone = () => true;
        public Func<Vector3?> Position = () => null;
        public int Id;
        public bool Playing, Loop, Stereo;
        public Player? Player;
        public float Distance, Radius, Volume;
        public string Who = "";
    }

    private readonly Emitter?[] _emitters = new Emitter?[EmitterCount];
    private readonly Stack<int> _freeEmitters = new(Enumerable.Range(0, EmitterCount).Reverse());
    public int EmitterUsed => EmitterCount - _freeEmitters.Count;

    public string DescribeEmitters() => EmitterUsed == 0 ? "emitters: -" : "emitters: " + string.Join(", ",
        _emitters.Select((e, i) => (e, i)).Where(x => x.e is not null).Select(x => x.e!.Playing && x.e.Player is { } p
            ? $"{x.i}: track {x.e.Id} at {x.e.Distance:0.0}u {_mixer.Describe(p.Left)}{(p.Stereo ? " / " + _mixer.Describe(p.Right) : "")}"
            : $"{x.i}: track {x.e.Id} at {x.e.Distance:0.0}u silent"));

    public RuntimeMusic(SoundMixer mixer, string? crash6Directory, string discLanguage = PlaySettings.AmericanLanguage) // Amedo
    {
        _mixer = mixer;
        if (crash6Directory is null) return;
        _music = MusicBank.Open(crash6Directory, "MUSIC");
        _speech = MusicBank.Open(crash6Directory, discLanguage.ToUpperInvariant()); // Amedo
    }

    public static float ChannelZeroVolume(int id) => id switch
    {
        0x1D or 0x1E or 0x23 or 0x36 or 0x37 or 0x3D => 1f,
        0x3B => 0.8f,
        _ => 0.75f,
    };

    public string Describe(int channel) =>
        _channels[channel] is { } p ? $"ch{channel}: track {p.Id} state {p.State} vol {p.CurL:0.00}/{p.CurR:0.00}" : $"ch{channel}: -";

    // Amedo 2026-10-04
    public int GoldenTrack { get; set; } = -1;
    public float GoldenVolume { get; set; } = 0.75f;
    public Func<bool>? GoldenInvincible { get; set; }
    private Request _goldenSave = new(0, 0.75f, 0.75f, 0.2f, "golden save");
    private bool _goldenBypass;

    private bool GoldenPlaying => GoldenTrack >= 0 && _channels[0] is { } c && c.Id == GoldenTrack;

    // Amedo 2026-10-04
    public void GoldenStart_269818()
    {
        if (GoldenTrack < 0 || _music is null || GoldenPlaying) return;
        if (_channels[0] is { } cur)
        {
            _goldenSave = new Request(cur.Id, cur.TargetL, cur.TargetR, 0.2f, "golden save");
            StopNow(cur);
            _channels[0] = null;
        }
        _goldenBypass = true;
        Begin(0, GoldenTrack, GoldenVolume, GoldenVolume, 0.2f, "golden Aku Aku");
        _goldenBypass = false;
    }

    // Amedo 2026-10-04
    public void GoldenEnd_269890()
    {
        if (!GoldenPlaying) return;
        var s = _goldenSave;
        _goldenBypass = true;
        Begin(0, s.Id, s.Volume, s.Volume2, s.Time, "after golden Aku Aku");
        _goldenBypass = false;
    }

    public void Begin(int channel, int id, float volume, float volume2, float time, string who = "", bool autoStart = true, bool loop = true, int? category = null) // Amedo 2026-10-06
    {
        if (_music is null || channel is < 0 or > 3) return;
        if (channel == 0 && !_goldenBypass && GoldenPlaying && id != GoldenTrack && GoldenInvincible?.Invoke() == true) // Amedo 2026-10-04
        {
            _goldenSave = new Request(id, volume, volume2, time, $"{who} (after golden)", autoStart, loop);
            Log?.Invoke($"[music {Clock?.Invoke() ?? 0:0.00}s] track {id} by {who} saved for after the golden Aku Aku");
            return;
        }
        var free = _players.FirstOrDefault(p => p.State == Free);
        if (channel == 0) time = id is 7 or 0x1E ? 0f : id is 0x3D or 0x3E ? 1f : 3f;
        int group = SoundGroups.GroupOf_1E5DF0(category ?? (channel is 2 or 3 ? 0 : 2)); // Amedo 2026-10-06
        var req = new Request(id, volume, volume2, time, $"{who} ch{channel}", autoStart, loop, group);
        var cur = _channels[channel];
        Player? target;
        if (cur is null)
        {
            free ??= Steal(); // Amedo 2026-09-28
            if (free is null) Unimplemented?.Invoke("music", 0x1DE430, "steal a stream player");
            if (channel == 0) req = req with { Time = id == 0x1B ? 1f : 0f };
            target = free;
        }
        else
        {
            if (cur.Id == id)
            {
                Ramp(cur, volume, volume2, req.Time);
                return;
            }
            if (channel == 1)
            {
                StopNow(cur);
                target = cur;
            }
            else
            {
                FadeOutPlayer(cur, req.Time, true);
                if (free is null)
                {
                    _pending[channel] = req;
                    return;
                }
                target = free;
            }
        }
        if (target is null) return;
        _channels[channel] = target;
        Start(target, req);
    }

    public void End(int channel, float time)
    {
        if (channel is < 0 or > 3 || _channels[channel] is not { } p) return;
        FadeOutPlayer(p, time, true);
    }

    // Amedo
    public void Reset_29DD48(int channel)
    {
        if (channel is < 0 or > 3) return;
        _pending[channel] = null;
        if (_channels[channel] is not { } p) return;
        StopNow(p);
        _channels[channel] = null;
        Log?.Invoke($"[music {Clock?.Invoke() ?? 0:0.00}s] channel {channel} stopped (0x29DD48)");
    }

    // Amedo 2026-09-28
    public bool IsReady(int channel) => channel is >= 0 and <= 3 && _channels[channel] is { State: Ready };

    // Amedo 2026-09-28
    public bool Play(int channel, float volume, float volume2, float time)
    {
        if (!IsReady(channel) || _channels[channel] is not { Held: { } data } p) return false;
        p.Held = null;
        (p.Left, p.Right) = _mixer.StartStream(data.Left, data.Right, p.Pitch, 0, 0, p.Loop, p.Group); // Amedo 2026-10-06
        if (p.Left < 0)
        {
            Log?.Invoke($"[music {Clock?.Invoke() ?? 0:0.00}s] no free voice for track {p.Id}");
            p.State = Free;
            return false;
        }
        p.TargetL = volume;
        p.TargetR = volume2;
        p.Time = time;
        p.State = Prepare;
        Log?.Invoke($"[music {Clock?.Invoke() ?? 0:0.00}s] play ready track {p.Id} vol {volume:0.###}");
        return true;
    }

    public void Update(float dt, bool paused = false)
    {
        if (!paused) UpdateEmitters_1DDCF8(); // Amedo
        foreach (var p in _players)
            if (p.State != Free) Step(p, dt, paused); // Amedo
        for (int ch = 0; ch < 4; ch++)
        {
            if (_channels[ch] is { State: Free }) _channels[ch] = null;
            if (_pending[ch] is not { } req || _channels[ch] is not null) continue;
            var p = _players.FirstOrDefault(x => x.State == Free) ?? Steal(); // Amedo
            if (p is null)
            {
                Unimplemented?.Invoke("music", 0x1DE430, "steal a stream player");
                continue;
            }
            _channels[ch] = p;
            Start(p, req);
            _pending[ch] = null;
            if (_playOnReady[ch] is { } queued && IsReady(ch)) // Amedo
            {
                _playOnReady[ch] = null;
                Play(ch, queued.L, queued.R, queued.T);
            }
        }
    }

    // Amedo
    public bool PlaySpeech_29E088(int channel, float volume, float volume2, float time)
    {
        if (channel is < 0 or > 3) return false;
        if (Play(channel, volume, volume2, time))
        {
            _playOnReady[channel] = null;
            return true;
        }
        if (_pending[channel] is not null) _playOnReady[channel] = (volume, volume2, time);
        return false;
    }

    public void StopAll()
    {
        StopEmitters_1DE280(); // Amedo
        foreach (var p in _players) StopNow(p);
        Array.Clear(_channels);
        Array.Clear(_pending);
        Array.Clear(_playOnReady); // Amedo
    }

    // Amedo
    private int TrackType(int id)
    {
        if (_music is null) return -1;
        var bank = _music;
        if (bank.Get(id) is { Type: 2 } && _speech is not null) bank = _speech;
        return bank.Get(id)?.Type ?? -1;
    }

    // Amedo
    public bool AddEmitter_1DDB10(Func<bool> gone, Func<Vector3?> position, int id, float volume, float volume2, float radius, bool loop, string who)
    {
        if (_music is null) return false;
        if (_freeEmitters.Count == 0)
        {
            Log?.Invoke($"[music {Clock?.Invoke() ?? 0:0.00}s] emitter pool full, track {id} by {who} dropped (0x1DDB10)");
            return false;
        }
        int slot = _freeEmitters.Pop();
        var e = new Emitter
        {
            Gone = gone, Position = position, Id = id, Loop = loop, Radius = radius,
            Volume = (volume + volume2) * 0.5f, Stereo = TrackType(id) == 1, Who = who,
        };
        _emitters[slot] = e;
        Log?.Invoke($"[music {Clock?.Invoke() ?? 0:0.00}s] emitter {slot} track {id} by {who}: vol {e.Volume:0.###} radius {radius:0.##}{(e.Stereo ? " (stereo, no pan)" : "")}{(loop ? " loop" : "")} (0x1DDB10)");
        return true;
    }

    // Amedo
    private void UpdateEmitters_1DDCF8()
    {
        if (EmitterUsed == 0 || Listener is not { } listener) return;
        for (int i = 0; i < EmitterCount; i++)
        {
            if (_emitters[i] is not { } e) continue;
            if (e.Gone())
            {
                _emitters[i] = null;
                _freeEmitters.Push(i);
                continue;
            }
            bool audible = false;
            float l = 0f, r = 0f, pitch = 0f;
            if (e.Position() is { } pos)
            {
                if (e.Stereo)
                {
                    float dist = listener.ListenerDistance(pos);
                    float share = e.Volume - dist / RuntimeSounds.DefaultRange;
                    if (0.001f < share)
                    {
                        l = r = 1f < share ? 1f : share;
                        pitch = 1f;
                        audible = true;
                    }
                    e.Distance = dist;
                }
                else
                {
                    audible = listener.Place_1E0290(pos, e.Volume, e.Radius, 1, out l, out r, out pitch, out float dist); // Amedo
                    e.Distance = dist;
                }
            }
            if (e.Playing)
            {
                if (!audible)
                {
                    if (e.Player is { } p) p.State = Release;
                    e.Player = null;
                    e.Playing = false;
                    Log?.Invoke($"[music {Clock?.Invoke() ?? 0:0.00}s] emitter {i} track {e.Id} out of range at {e.Distance:0.0}u");
                }
                else if (e.Player is { } p)
                {
                    SetVolume_1E0D28(p, l, r);
                    SetPitch_1E66E8(p, pitch);
                }
                continue;
            }
            if (!audible) continue;
            var player = _players.FirstOrDefault(x => x.State == Free) ?? Steal(e.Distance);
            if (player is null) continue;
            Start(player, new Request(e.Id, l, r, 0f, $"{e.Who} emitter {i} at {e.Distance:0.0}u", true, e.Loop, SoundGroups.GroupOf_1E5DF0(0)));
            e.Player = player;
            e.Playing = true;
            SetPitch_1E66E8(player, pitch);
        }
    }

    // Amedo
    private void StopEmitters_1DE280()
    {
        for (int i = 0; i < EmitterCount; i++)
        {
            if (_emitters[i] is not { } e) continue;
            if (e.Playing && e.Player is { } p) StopNow(p);
            _emitters[i] = null;
        }
        _freeEmitters.Clear();
        for (int i = EmitterCount - 1; 0 <= i; i--) _freeEmitters.Push(i);
    }

    // Amedo
    private void SetVolume_1E0D28(Player p, float l, float r)
    {
        int vl = (int)(l * 16383f), vr = (int)(r * 16383f);
        if (p.Stereo)
        {
            _mixer.SetVolume(p.Left, vl, 0);
            _mixer.SetVolume(p.Right, 0, vr);
        }
        else _mixer.SetVolume(p.Left, vl, vr);
    }

    // Amedo
    private void SetPitch_1E66E8(Player p, float pitch)
    {
        int n = (int)(pitch * p.Pitch);
        _mixer.SetPitch(p.Left, n);
        if (p.Stereo) _mixer.SetPitch(p.Right, n);
    }

    private void Start(Player p, Request req)
    {
        var bank = _music!;
        if (bank.Get(req.Id) is { Type: 2 } && _speech is not null) bank = _speech;
        p.Id = req.Id;
        if (bank.Get(req.Id) is not { } track || bank.Load(req.Id) is not { } data)
        {
            Log?.Invoke($"[music {Clock?.Invoke() ?? 0:0.00}s] track {req.Id} empty in {bank.Name}");
            p.State = Free;
            return;
        }
        p.Stereo = data.Right is not null;
        int pitch = (track.SampleRate << 12) / 48000;
        if (bank.HeaderRate(req.Id) is > 0 and var rate) pitch = (int)(((ulong)(uint)(rate << 12) * 0x057619F1UL) >> 42); // Amedo 2026-09-29
        p.Loop = req.Loop; // Amedo 2026-09-28
        p.Group = req.Group; // Amedo 2026-10-06
        p.Pitch = pitch; // Amedo
        if (!req.AutoStart) // Amedo 2026-09-28
        {
            p.Held = data;
            p.Left = p.Right = -1;
            p.State = Ready;
            Log?.Invoke($"[music {Clock?.Invoke() ?? 0:0.00}s] ready track {req.Id} ({bank.Name}, {Hz(bank, req.Id, track)} Hz) by {req.Who}, group {req.Group}"); // Amedo 2026-10-06
            return;
        }
        (p.Left, p.Right) = _mixer.StartStream(data.Left, data.Right, pitch, 0, 0, p.Loop, p.Group); // Amedo 2026-10-06
        if (p.Left < 0)
        {
            Log?.Invoke($"[music {Clock?.Invoke() ?? 0:0.00}s] no free voice for track {req.Id}");
            p.State = Free;
            return;
        }
        p.TargetL = req.Volume;
        p.TargetR = req.Volume2;
        p.Time = req.Time;
        p.StopAfterFade = false;
        p.State = Prepare;
        Log?.Invoke($"[music {Clock?.Invoke() ?? 0:0.00}s] start track {req.Id} ({bank.Name}, {(p.Stereo ? "stereo" : "mono")} {Hz(bank, req.Id, track)} Hz) " + // Amedo 2026-09-29
                    $"vol {req.Volume:0.###} fade {req.Time:0.##}s by {req.Who}, group {req.Group}"); // Amedo 2026-10-06
    }

    // Amedo 2026-09-29
    private static int Hz(MusicBank bank, int id, MusicBank.Track track) => bank.HeaderRate(id) is > 0 and var rate ? rate : track.SampleRate;

    private void Step(Player p, float dt, bool paused)
    {
        switch (p.State)
        {
            case Prepare:
                if (paused) break; // Amedo
                p.CurL = p.CurR = 0f;
                p.StartL = p.StartR = 0f;
                if (p.Time <= 0f)
                {
                    p.CurL = p.TargetL;
                    p.CurR = p.TargetR;
                    p.State = Playing;
                }
                else p.State = FadeIn;
                Apply(p);
                break;
            case FadeIn:
                if (RampStep(p, dt)) p.State = Playing;
                break;
            case Playing:
                if (!_mixer.Playing(p.Left))
                {
                    p.State = Release;
                    break;
                }
                if (p.CurL != p.TargetL || p.CurR != p.TargetR) RampStep(p, dt);
                break;
            case FadeOut:
                if (RampStep(p, dt)) p.State = p.StopAfterFade ? Release : Silent;
                break;
            case Release:
                StopNow(p);
                break;
        }
    }

    private bool RampStep(Player p, float dt)
    {
        bool doneL, doneR;
        if (p.Time < 1e-5f)
        {
            p.CurL = p.TargetL;
            p.CurR = p.TargetR;
            doneL = doneR = true;
        }
        else
        {
            float k = dt / p.Time;
            p.CurL += (p.TargetL - p.StartL) * k;
            p.CurR += (p.TargetR - p.StartR) * k;
            doneL = p.StartL < p.TargetL ? p.TargetL <= p.CurL : p.CurL <= p.TargetL;
            if (doneL) p.CurL = p.TargetL;
            doneR = p.StartR < p.TargetR ? p.TargetR <= p.CurR : p.CurR <= p.TargetR;
            if (doneR) p.CurR = p.TargetR;
        }
        Apply(p);
        return doneL && doneR;
    }

    private void Ramp(Player p, float volume, float volume2, float time)
    {
        if (p.State == FadeOut) p.State = Playing;
        p.Time = time;
        p.StartL = p.CurL;
        p.StartR = p.CurR;
        p.TargetL = volume;
        p.TargetR = volume2;
    }

    private static void FadeOutPlayer(Player p, float time, bool stop)
    {
        p.Time = time < 0.3f ? 0.3f : time;
        p.State = FadeOut;
        p.StopAfterFade = stop;
        p.StartL = p.CurL;
        p.StartR = p.CurR;
        p.TargetL = p.TargetR = 0f;
    }

    // Amedo 2026-09-28
    private Player? Steal(float distance = 0f) // Amedo
    {
        Player? victim = null;
        Emitter? far = null; // Amedo
        float best = 0f;
        foreach (var e in _emitters)
            if (e is { Playing: true } && distance < e.Distance && best < e.Distance) { best = e.Distance; far = e; }
        if (far is not null)
        {
            victim = far.Player;
            if (victim is not null) StopNow(victim);
            far.Player = null;
            far.Playing = false;
        }
        if (victim is null)
            foreach (var p in _players)
                if (p.State == Release || (p.State == FadeOut && p.StopAfterFade)) { StopNow(p); victim = p; }
        if (victim is null && _channels[2] is { } ambience) { StopNow(ambience); victim = ambience; }
        if (victim is null) return null;
        for (int ch = 0; ch < _channels.Length; ch++)
            if (ReferenceEquals(_channels[ch], victim)) _channels[ch] = null;
        Log?.Invoke($"[music {Clock?.Invoke() ?? 0:0.00}s] stole stream player (track {victim.Id}) (0x1DE430)");
        return victim;
    }

    private void StopNow(Player p)
    {
        if (p.Left >= 0) _mixer.Stop(p.Left);
        if (p.Right >= 0) _mixer.Stop(p.Right);
        p.Left = p.Right = -1;
        p.Held = null; // Amedo 2026-09-28
        p.State = Free;
    }

    private void Apply(Player p)
    {
        int l = (int)(Math.Clamp(p.CurL, 0f, 2f) * 16383f), r = (int)(Math.Clamp(p.CurR, 0f, 2f) * 16383f);
        if (p.Stereo)
        {
            _mixer.SetVolume(p.Left, l, 0);
            _mixer.SetVolume(p.Right, 0, r);
        }
        else _mixer.SetVolume(p.Left, l, r);
    }
}
