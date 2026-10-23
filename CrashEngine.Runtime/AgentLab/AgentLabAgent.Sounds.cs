using System.Numerics;

namespace CrashEngine.Runtime.AgentLab;

// Amedo 2026-09-28
public sealed partial class AgentLabAgent
{
    private float RandomSpread(float range) => World.Rng.GetRandFloat__18C130(range); // Amedo

    // Amedo 2026-09-30
    private Func<bool>? _soundAlive;
    private Func<Vector3?>? _soundPosition;
    internal bool Released { get; set; }
    private Func<bool> SoundAlive => _soundAlive ??= () => !Destroyed && !Released && Entity.Active;
    private Func<Vector3?> SoundPosition => _soundPosition ??= () => SoundAlive() ? RootPoint(GamePosition) : null;
    private Vector3? MusicEmitterPosition() => ChunkA0 is { } c && c.DrawFrame == World.Frame ? RootPoint(GamePosition) : null; // Amedo

    private int RandomIndex(int n) => World.Rng.GetRandom_18C018(n); // Amedo

    private float SoundVariable(uint w)
    {
        if ((w & 6) != 4) return 0f;
        if ((w & 1) == 0) return BitConverter.UInt32BitsToSingle(w & ~7u);
        int index = (int)(w >> 3);
        var own = Instance.Source.ParamList2;
        int count = own?.Count ?? 0;
        if (index < count) return FloatParams is { } runtime && index < runtime.Length ? runtime[index] : own![index];
        var defaults = GameObject?.InstFloats;
        int k = index - count;
        return defaults is not null && k < defaults.Count ? defaults[k] : 0f;
    }

    private int IntVariable(uint w)
    {
        if ((w & 6) != 0) return 0;
        if ((w & 1) == 0) return (int)w >> 3;
        int index = (int)(w >> 3);
        var own = Instance.Source.ParamList3;
        int count = own?.Count ?? 0;
        if (index < count) return (int)own![index];
        var defaults = GameObject?.InstIntegers;
        int k = index - count;
        return defaults is not null && k < defaults.Count ? (int)defaults[k] : 0;
    }

    private void BeginMusic(IReadOnlyList<uint> a)
    {
        if (World.Music is not { } music) return;
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        int id = (short)IntVariable(Arg(0));
        uint flags = Arg(1);
        float volume = BitConverter.UInt32BitsToSingle(Arg(2)), time = BitConverter.UInt32BitsToSingle(Arg(3));
        int channel = (int)((flags >> 12) & 7);
        if ((flags & 0x7000) == 0) volume = RuntimeMusic.ChannelZeroVolume(id);
        if (channel == 3)
        {
            music.AddEmitter_1DDB10(() => Destroyed || Released, MusicEmitterPosition, id, volume, volume, BitConverter.UInt32BitsToSingle(Arg(4)), (flags & 0x8000) != 0, Entity.Name); // Amedo
            return;
        }
        if (channel > 3) return;
        music.Begin(channel, id, volume, volume, time, Entity.Name, loop: (flags & 0x8000) != 0); // Amedo 2026-09-28
    }

    private void EndContextMusic(IReadOnlyList<uint> a) =>
        World.Music?.End(1, BitConverter.UInt32BitsToSingle(a.Count > 0 ? a[0] : 0u));

    // Amedo 2026-09-29
    private void EndMusic_224A10(IReadOnlyList<uint> a) =>
        World.Music?.End((int)((a.Count > 1 ? a[1] : 0u) & 7), BitConverter.UInt32BitsToSingle(a.Count > 0 ? a[0] : 0u));

    internal void PlaySoundSlot(ushort slot)
    {
        if (World.Sounds is not { } sounds) return;
        ushort id = SoundForSlot(slot);
        if (id == 0xFFFF) return;
        sounds.PlayAt(id, RootPoint(GamePosition), -1f, -1f, $"{Entity.Name} slot {slot} (0x21E310)", SoundAlive); // Amedo 2026-09-30
    }

    internal ushort SoundForSlot(ushort slot)
    {
        if (slot == 0xFFFF) return 0xFFFF; // Amedo
        var slots = RootController().GameObject?.SoundSlots; // Amedo 2026-10-05
        ushort id = slots is not null && slot < slots.Count ? slots[slot] : (ushort)0xFFFF;
        if (id == 0xFFFF && SoundProxy_23CC18() is { } proxy && slot < proxy.SoundSlots.Count) id = proxy.SoundSlots[slot]; // Amedo
        if (id == 0xFFFF) return 0xFFFF;
        return (ushort)(id & 0x7FFF);
    }

    // Amedo 2026-09-28
    private void DoSound(IReadOnlyList<uint> a)
    {
        DoSoundPlay(a);
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        if ((Arg(0) & 0x40000) != 0) ShakeCamera(Arg(12), Arg(13), Arg(11), Arg(14), RootPoint(GamePosition));
    }

    // Amedo 2026-09-28
    private void ShakeCamera(uint axesXZ, uint axisY, uint strength, uint falloff, Vector3 source)
    {
        float xz = BitConverter.UInt32BitsToSingle(axesXZ), y = BitConverter.UInt32BitsToSingle(axisY);
        float f = BitConverter.UInt32BitsToSingle(falloff);
        if (0.01f < xz || 0.01f < y) World.CameraImpulseAxes?.Invoke(xz, y, xz, f, source);
        else World.CameraImpulse?.Invoke(BitConverter.UInt32BitsToSingle(strength), f, source);
    }

    private void DoSoundPlay(IReadOnlyList<uint> a)
    {
        uint Arg(int i) => i < a.Count ? a[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(Arg(i));
        uint flags = Arg(0);
        float variable = -1f;
        if ((flags & 0x20000) != 0) variable = MathF.Min(SoundVariable(Arg(5)), 5f);
        ushort id, slot = 0xFFFF;
        if ((flags & 0x780000) != 0x780000)
        {
            // Amedo 2026-09-30
            if (!EffectSurface(out var surface, out _)) return;
            int index = (int)((flags >> 19) & 0xF);
            id = surface.SoundAt(index, out float surfaceVolume);
            if (index < 6) variable = surfaceVolume;
            if ((flags & 0x780000) == 0x180000 && OwnCharacter is not null && surface.Id == 10) DoubleFootprint_137988(); // Amedo 2026-09-30
        }
        else
        {
            int idx = RandomIndex((int)(flags & 0xF));
            uint word = Arg(1 + idx / 2);
            slot = (ushort)((idx & 1) == 0 ? word & 0xFFFF : word >> 16);
            id = SoundForSlot(slot);
        }
        if (id == 0xFFFF) return;
        float volume = 0f < variable ? variable : -1f, pitch = -1f;
        if ((flags & 0x8000) != 0) volume = (0f < variable ? variable : 1f) + RandomSpread(F(9));
        if ((flags & 0x10000) != 0) pitch = F(7);
        if ((flags & 0x4000) != 0) pitch = ((flags & 0x10000) != 0 ? pitch : 1f) + RandomSpread(F(8));
        bool store = (flags & 0x800000) != 0 && _storedSound160 < 0; // Amedo 2026-10-05
        if (World.Sounds is not { } sounds) return; // Amedo 2026-09-30
        string who = $"{Entity.Name} slot {slot} flags 0x{flags:X8} vol {volume:0.###} pitch {pitch:0.###}";
        int category = (int)((flags >> 8) & 7); // Amedo 2026-10-06
        if ((flags & 0x801800) != 0)
        {
            int h = sounds.PlayTracked(id, SoundPosition, volume, pitch, who, category, persistent: store); // Amedo 2026-10-06
            if (store && (flags & 0x800) == 0 && h >= 0) _storedSound160 = h; // Amedo 2026-10-05
        }
        else sounds.PlayAt(id, RootPoint(GamePosition), volume, pitch, who, SoundAlive, category); // Amedo 2026-10-06
    }

    // Amedo 2026-10-05
    private int _storedSound160 = -1;

    private void StopStoredSound_23DB30()
    {
        if (_storedSound160 < 0) return;
        World.Sounds?.Stop_1E50C8(_storedSound160);
        _storedSound160 = -1;
    }

    private void ModifyStoredSound_224CE8(uint arg0, float pitch, float volume) // Amedo
    {
        if (_storedSound160 < 0 || World.Sounds is not { } sounds) return;
        sounds.SetTracked_1E51C0(_storedSound160, (arg0 & 2) != 0 ? volume : null, (arg0 & 1) != 0 ? pitch : null);
    }

    // Amedo 2026-09-30
    private ushort TrailSlotSound(ulong flags, IReadOnlyList<uint> w)
    {
        uint W(int i) => i < w.Count ? w[i] : 0u;
        int idx = RandomIndex((int)((flags >> 46) & 0xF));
        int half = 7 + idx;
        ushort slot = (ushort)((half & 1) == 0 ? W(half / 2) & 0xFFFF : W(half / 2) >> 16);
        return slot == 0xFFFF ? (ushort)0xFFFF : SoundForSlot(slot);
    }

    private void TrailSound(ushort id, float volume, ulong flags, IReadOnlyList<uint> w, Vector3 position) // Amedo 2026-09-30
    {
        uint W(int i) => i < w.Count ? w[i] : 0u;
        float F(int i) => BitConverter.UInt32BitsToSingle(W(i));
        if (0f < F(22)) // Amedo
        {
            float k = (_trailLevel64 - F(22)) * 0.1f;
            volume = 0f < volume ? volume * k : k;
            if (3f < volume) volume = 3f;
        }
        float pitch = (flags & (1ul << 35)) != 0 ? F(20) : 1f;
        if ((flags & (1ul << 36)) != 0) pitch += RandomSpread(F(19));
        if ((flags & 0x8000000) != 0) pitch *= F(9); // Amedo 2026-09-30
        if (World.Sounds is not { } sounds) return; // Amedo 2026-09-30
        sounds.PlayAt(id, RootPoint(position), volume, pitch, $"{Entity.Name} trail", SoundAlive, ((flags >> 23) & 7) == 1 ? 1 : 0); // Amedo 2026-10-06
    }
}
