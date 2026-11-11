using System.Numerics;
using CrashEngine.Runtime.Audio;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using CrashEngine.Core;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed class RuntimeSounds : Screen.Oleg.IMenuSoundPlayer
{
    public const float DefaultRange = 60f;
    private const float Diagonal = 0.70710677f;

    private sealed class Tracked
    {
        public int Handle;
        public ushort Id;
        public Func<Vector3?> Position = () => null;
        public float Volume, Pitch;
        public int BasePitch;
        public Func<bool>? Alive; // Amedo 2026-09-30
        public int Key = -1, State, Category; // Amedo 2026-10-06
        public int Reverb; // Amedo 2026-11-11
        public bool Persistent;
        public PS2AnySound? Sound;
        public string Who = "";
    }

    private const int TrackReal = 1, TrackVirtual = 2; // Amedo 2026-10-06
    private int _nextKey;

    private readonly Dictionary<PS2AnySound, bool> _loops = new(ReferenceEqualityComparer.Instance); // Amedo 2026-09-30

    private readonly Dictionary<uint, (PS2AnySound Sound, int Refs)> _level = new(); // Amedo 2026-09-29
    private readonly Dictionary<uint, PS2AnySound> _default = new();
    private const int Languages = Constants.CODE_LANG_JPN_SECTION - Constants.CODE_LANG_ENG_SECTION + 1; // Amedo
    private readonly Dictionary<uint, (PS2AnySound Sound, int Refs)>[] _speech = Enumerable.Range(0, Languages).Select(_ => new Dictionary<uint, (PS2AnySound, int)>()).ToArray(); // Amedo
    private readonly Dictionary<uint, PS2AnySound>[] _speechDefault = Enumerable.Range(0, Languages).Select(_ => new Dictionary<uint, PS2AnySound>()).ToArray();

    // Amedo
    public Func<int>? Language { get; set; }
    public Func<ushort, int, PS2AnySound?>? Held { get; set; }
    private readonly List<Tracked> _tracked = new();

    public SoundMixer Mixer { get; }
    public Audio.SoundGroups Groups { get; } // Amedo 2026-10-06
    public Action<string>? Log { get; set; }
    public Func<double>? Clock { get; set; }
    public Func<int>? ReverbKey_22EA78 { get; set; } // Amedo 2026-11-11
    public int Started { get; private set; }
    public int Rejected { get; private set; }

    public Vector3 ListenerPosition { get; set; }
    public Vector3 ListenerRight { get; set; } = Vector3.UnitX;
    public Vector3 ListenerUp { get; set; } = Vector3.UnitY;
    public Vector3 ListenerForward { get; set; } = Vector3.UnitZ;

    public RuntimeSounds(PS2AnyTwinsanityRM2? global, PS2AnyTwinsanityRM2 level, SoundMixer mixer)
    {
        Mixer = mixer;
        Groups = new Audio.SoundGroups(mixer); // Amedo 2026-10-06
        foreach (var s in SoundsOf(global)) _default[s.GetID()] = s; // Amedo 2026-09-29
        for (int l = 0; l < Languages; l++)
            foreach (var s in SoundsOf(global, Constants.CODE_LANG_ENG_SECTION + l)) _speechDefault[l][s.GetID()] = s; // Amedo
        AddLevel(level);
    }

    public int LevelCount => _level.Count;
    public int DefaultCount => _default.Count;

    // Amedo 2026-09-29
    private static IEnumerable<PS2AnySound> SoundsOf(PS2AnyTwinsanityRM2? rm, int section = Constants.CODE_SOUND_EFFECTS_SECTION) // Amedo
    {
        var sec = rm?.GetItem<BaseTwinSection>((uint)Constants.LEVEL_CODE_SECTION)
                    ?.GetItem<BaseTwinSection>((uint)section);
        for (int i = 0; sec is not null && i < sec.GetItemsAmount(); i++)
            if (sec.GetItem(i) is PS2AnySound s) yield return s;
    }

    // Amedo 2026-09-29
    public void AddLevel(PS2AnyTwinsanityRM2? rm)
    {
        foreach (var s in SoundsOf(rm))
        {
            uint id = s.GetID();
            if (_default.ContainsKey(id)) continue;
            _level[id] = _level.TryGetValue(id, out var e) ? (e.Sound, e.Refs + 1) : (s, 1);
        }
        for (int l = 0; l < Languages; l++) // Amedo
            foreach (var s in SoundsOf(rm, Constants.CODE_LANG_ENG_SECTION + l))
            {
                uint id = s.GetID();
                var table = _speech[l];
                table[id] = table.TryGetValue(id, out var e) ? (e.Sound, e.Refs + 1) : (s, 1);
            }
    }

    // Amedo 2026-09-29
    public void RemoveLevel(PS2AnyTwinsanityRM2? rm)
    {
        foreach (var s in SoundsOf(rm))
        {
            uint id = s.GetID();
            if (_default.ContainsKey(id) || !_level.TryGetValue(id, out var e)) continue;
            if (e.Refs > 1) _level[id] = (e.Sound, e.Refs - 1);
            else _level.Remove(id);
        }
        for (int l = 0; l < Languages; l++) // Amedo
            foreach (var s in SoundsOf(rm, Constants.CODE_LANG_ENG_SECTION + l))
            {
                uint id = s.GetID();
                var table = _speech[l];
                if (!table.TryGetValue(id, out var e)) continue;
                if (e.Refs > 1) table[id] = (e.Sound, e.Refs - 1);
                else table.Remove(id);
            }
    }

    public PS2AnySound? Sound(ushort id) =>
        _default.TryGetValue(id, out var d) ? d : _level.TryGetValue(id, out var s) ? s.Sound : Speech(id) ?? Held?.Invoke(id, Constants.CODE_SOUND_EFFECTS_SECTION); // Amedo 2026-09-29

    // Amedo
    private PS2AnySound? Speech(ushort id)
    {
        int l = Math.Clamp(Language?.Invoke() ?? 0, 0, Languages - 1);
        return _speechDefault[l].TryGetValue(id, out var d) ? d : _speech[l].TryGetValue(id, out var s) ? s.Sound : Held?.Invoke(id, Constants.CODE_LANG_ENG_SECTION + l);
    }

    public static int BasePitchOf(PS2AnySound s) => (s.FreqFac << 8) | s.UnkFlag;

    public int PlayAt(ushort id, Vector3 position, float volume, float pitch, string who, Func<bool>? alive = null, int category = 0) // Amedo 2026-10-06
    {
        if (Sound(id) is not { } s) return Miss(id, who);
        if (!Spatial(position, volume, BasePitchOf(s), pitch, out int l, out int r, out int p))
        {
            Rejected++;
            Log?.Invoke($"[sound {Clock?.Invoke() ?? 0:0.00}s] inaudible 0x{id:X} by {who} at {GameMath.Distance(position, ListenerPosition):0.0}u");
            return -1;
        }
        bool loops = Loops(s); // Amedo 2026-09-30
        int h = Begin(s, id, p, l, r, $"3D at {GameMath.Distance(position, ListenerPosition):0.0}u{(loops ? ", looped sample played once" : "")}", who, repeat: false, category: category, key: ReverbKey_22EA78?.Invoke() ?? 0); // Amedo 2026-11-11
        if (h >= 0 && loops && alive is not null) _tracked.Add(new Tracked { Handle = h, Id = id, Alive = alive });
        return h;
    }

    // Amedo 2026-09-30
    public bool Loops(PS2AnySound s)
    {
        if (_loops.TryGetValue(s, out bool known)) return known;
        var data = s.Sound;
        int loopStart = 0;
        bool loops = false;
        for (int b = 0; b * 16 + 1 < data.Length; b++)
        {
            int flags = data[b * 16 + 1];
            if ((flags & 4) != 0) loopStart = b;
            if ((flags & 1) == 0) continue;
            loops = (flags & 2) != 0 && loopStart != b;
            break;
        }
        _loops[s] = loops;
        return loops;
    }

    // Amedo 2026-10-06
    public int PlayTracked(ushort id, Func<Vector3?> position, float volume, float pitch, string who, int category = 0, bool persistent = false)
    {
        if (Sound(id) is not { } s) return Miss(id, who);
        if (position() is not { } pos) return -1;
        int reverb = ReverbKey_22EA78?.Invoke() ?? 0; // Amedo 2026-11-11
        int h = Start3D(s, id, pos, volume, pitch, who, category, "tracked", reverb);
        if (h < 0 && !persistent) return -1;
        _nextKey = (_nextKey + 1) & 0x7FFFFFFF;
        var t = new Tracked
        {
            Handle = h, Id = id, Sound = s, Position = position, Volume = volume, Pitch = pitch, BasePitch = BasePitchOf(s),
            Key = _nextKey, State = h < 0 ? TrackVirtual : TrackReal, Category = category, Persistent = persistent, Who = who,
            Reverb = reverb,
        };
        _tracked.Add(t);
        if (h < 0) Log?.Invoke($"[sound {Clock?.Invoke() ?? 0:0.00}s] tracked 0x{id:X} by {who}: virtual at {GameMath.Distance(pos, ListenerPosition):0.0}u");
        return t.Key;
    }

    // Amedo 2026-10-06
    private int Start3D(PS2AnySound s, ushort id, Vector3 pos, float volume, float pitch, string who, int category, string kind, int reverb) // Amedo 2026-11-11
    {
        if (!Spatial(pos, volume, BasePitchOf(s), pitch, out int l, out int r, out int p)) return -1;
        return Begin(s, id, p, l, r, $"{kind} 3D at {GameMath.Distance(pos, ListenerPosition):0.0}u", who, category: category, key: reverb);
    }

    public int Play2D(ushort id, float volume, float pitch, string who, int category = 0) // Amedo 2026-10-06
    {
        if (Sound(id) is not { } s) return Miss(id, who);
        Start2D(volume, BasePitchOf(s), pitch, out int l, out int r, out int p);
        return Begin(s, id, p, l, r, "2D", who, category: category); // Amedo 2026-10-06
    }

    public int OutputMode_309AC8 { get; private set; } = 1; // Amedo

    // Amedo
    public void SetOutputMode_1E6190(int mode)
    {
        OutputMode_309AC8 = mode;
        Mixer.Mono = mode == 0;
    }

    // Amedo
    public void PauseAll_1E5E50() => Mixer.HoldVoices();

    // Amedo
    public void ResumeAll_1E5E70() => Mixer.ReleaseVoices();

    // Amedo
    public void PlaySound_1E5608(object sound, int group)
    {
        if (sound is not PS2AnySound s) return;
        Start2D(-1f, BasePitchOf(s), -1f, out int l, out int r, out int p);
        Begin(s, (ushort)s.GetID(), p, l, r, "2D menu", "menu", category: group);
    }

    public void Update()
    {
        for (int i = _tracked.Count - 1; 0 <= i; i--)
        {
            var t = _tracked[i];
            if (t.Alive is { } alive) // Amedo 2026-10-06
            {
                if (!Mixer.Playing(t.Handle)) _tracked.RemoveAt(i);
                else if (!alive()) Mixer.Stop(t.Handle);
                continue;
            }
            if (!UpdateTracked_1DCFA0(t)) _tracked.RemoveAt(i);
        }
    }

    public void StopAll()
    {
        _tracked.Clear();
        Mixer.StopAll();
    }

    // Amedo 2026-10-05
    public void Stop_1E50C8(int key) // Amedo 2026-10-06
    {
        for (int i = _tracked.Count - 1; 0 <= i; i--)
        {
            var t = _tracked[i];
            if (t.Key != key || t.Alive is not null) continue;
            if (0 <= t.Handle) Mixer.Stop(t.Handle);
            _tracked.RemoveAt(i);
        }
    }

    public void SetTracked_1E51C0(int key, float? volume, float? pitch) // Amedo 2026-10-06
    {
        foreach (var t in _tracked)
        {
            if (t.Key != key || t.Alive is not null) continue;
            if (volume is { } v) t.Volume = v;
            if (pitch is { } p) t.Pitch = p;
            if (t.State == TrackReal && t.Position() is { } pos &&
                Spatial(pos, t.Volume, t.BasePitch, t.Pitch, out int l, out int r, out int np)) Mixer.Set(t.Handle, np, l, r);
        }
    }

    // Amedo 2026-10-06
    private bool UpdateTracked_1DCFA0(Tracked t)
    {
        if (t.Position() is not { } pos)
        {
            if (0 <= t.Handle) Mixer.Stop(t.Handle);
            return false;
        }
        if (t.State == TrackVirtual)
        {
            if (!t.Persistent || t.Sound is null) return false;
            int h = Start3D(t.Sound, t.Id, pos, t.Volume, t.Pitch, t.Who, t.Category, "tracked restart", t.Reverb); // Amedo 2026-11-11
            if (0 <= h)
            {
                t.Handle = h;
                t.State = TrackReal;
            }
            return true;
        }
        if (!Mixer.Playing(t.Handle))
        {
            t.Handle = -1;
            t.State = TrackVirtual;
            return t.Persistent;
        }
        if (Spatial(pos, t.Volume, t.BasePitch, t.Pitch, out int l, out int r, out int p))
        {
            Mixer.Set(t.Handle, p, l, r);
            return true;
        }
        Mixer.Stop(t.Handle);
        t.Handle = -1;
        t.State = TrackVirtual;
        Log?.Invoke($"[sound {Clock?.Invoke() ?? 0:0.00}s] tracked 0x{t.Id:X} by {t.Who}: {(t.Persistent ? "virtual" : "removed")} (inaudible)");
        return t.Persistent;
    }

    private int Begin(PS2AnySound s, ushort id, int pitch, int l, int r, string kind, string who, bool repeat = true, int category = 0, int key = 0) // Amedo 2026-11-11
    {
        int h = Mixer.Start(s.Sound, pitch, l, r, repeat, Audio.SoundGroups.GroupOf_1E5DF0(category), key); // Amedo 2026-11-11
        if (h < 0)
        {
            Rejected++;
            Log?.Invoke($"[sound {Clock?.Invoke() ?? 0:0.00}s] no free voice for 0x{id:X} ({who})");
            return h;
        }
        Started++;
        Log?.Invoke($"[sound {Clock?.Invoke() ?? 0:0.00}s] {kind} 0x{id:X} by {who}: L 0x{l:X4} R 0x{r:X4} pitch 0x{pitch:X4}");
        return h;
    }

    private int Miss(ushort id, string who)
    {
        Rejected++;
        Log?.Invoke($"[sound {Clock?.Invoke() ?? 0:0.00}s] sound 0x{id:X} not in the level or Default ({who})");
        return -1;
    }

    private static void Start2D(float volume, int basePitch, float pitch, out int l, out int r, out int p)
    {
        if (volume <= 0f) volume = 1f;
        l = r = (int)(volume * 16383f);
        p = 0f < pitch ? (int)(basePitch * pitch) : basePitch;
    }

    // Amedo
    public float ListenerDistance(Vector3 position)
    {
        var d = position - ListenerPosition;
        return GameMath.Length(new Vector3(GameMath.Dot(d, ListenerRight), GameMath.Dot(d, ListenerUp), GameMath.Dot(d, ListenerForward)));
    }

    // Amedo
    public bool Place_1E0290(Vector3 position, float volume, float radius, int freq, out float left, out float right, out float pitch, out float dist)
    {
        if (radius == 0f) radius = DefaultRange;
        var d = position - ListenerPosition;
        var rel = new Vector3(GameMath.Dot(d, ListenerRight), GameMath.Dot(d, ListenerUp), GameMath.Dot(d, ListenerForward));
        dist = GameMath.Length(rel);
        float v = volume - dist / radius;
        left = right = pitch = 0f;
        if (!(0.005f < v)) return false;
        pitch = freq;
        var flat = new Vector3(rel.X, 0f, rel.Z);
        float sq = GameMath.LengthSquared(flat);
        float k = 2.4999998e-09f < sq ? 1f / MathF.Sqrt(sq) : 0f;
        flat *= k;
        float a = flat.X * Diagonal + flat.Z * Diagonal;
        float b = -flat.X * Diagonal + flat.Z * Diagonal;
        right = ((a * 0.5f + 0.5f) * 0.75f + 0.25f) * v;
        left = ((b * 0.5f + 0.5f) * 0.75f + 0.25f) * v;
        if (1f < right)
        {
            left += right - 1f;
            right = 1f;
            if (1f < left) left = 1f;
        }
        else if (1f < left)
        {
            right += left - 1f;
            left = 1f;
            if (1f < right) right = 1f;
        }
        if (flat.Z < 0f) pitch = flat.Z * 0.01f * pitch + pitch;
        return true;
    }

    private bool Spatial(Vector3 position, float volume, int basePitch, float pitch, out int l, out int r, out int p)
    {
        float vol = 1f;
        if (1.5f < volume) vol = 1.5f;
        else if (0f < volume) vol = volume;
        int freq = 0f < pitch ? (int)(basePitch * pitch) : basePitch;
        l = r = 0;
        p = freq;
        if (!Place_1E0290(position, vol, DefaultRange, freq, out float left, out float right, out float f, out _)) return false; // Amedo
        l = (int)(left * 16383f);
        r = (int)(right * 16383f);
        p = (int)f;
        return true;
    }
}
