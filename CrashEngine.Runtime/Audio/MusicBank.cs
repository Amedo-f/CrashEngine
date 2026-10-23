namespace CrashEngine.Runtime.Audio;

// Amedo 2026-09-28
public sealed class MusicBank
{
    public sealed record Track(int Type, int Size, uint Offset, int SampleRate, int LastChunk);

    private const uint MagicVag = 0x70474156, MagicMsv = 0x7056534D;

    private readonly string _mb;
    private readonly Dictionary<int, (byte[] Left, byte[]? Right)> _cache = new();
    private readonly Dictionary<int, int> _headerRates = new(); // Amedo 2026-09-29

    public int Interleave { get; }
    public IReadOnlyList<Track> Tracks { get; }
    public string Name { get; }

    private MusicBank(string mb, int interleave, List<Track> tracks, string name)
    {
        _mb = mb;
        Interleave = interleave;
        Tracks = tracks;
        Name = name;
    }

    public static MusicBank? Open(string directory, string name)
    {
        string mh = Path.Combine(directory, name + ".MH"), mb = Path.Combine(directory, name + ".MB");
        if (!File.Exists(mh) || !File.Exists(mb)) return null;
        using var r = new BinaryReader(File.OpenRead(mh));
        int count = r.ReadInt32(), interleave = r.ReadInt32();
        var tracks = new List<Track>(count);
        for (int i = 0; i < count; i++)
            tracks.Add(new Track(r.ReadInt32(), r.ReadInt32(), r.ReadUInt32(), r.ReadInt32(), r.ReadInt32()));
        return new MusicBank(mb, interleave, tracks, name);
    }

    public Track? Get(int id) => id >= 0 && id < Tracks.Count ? Tracks[id] : null;

    // Amedo 2026-09-29
    public int HeaderRate(int id) => _headerRates.TryGetValue(id, out int rate) ? rate : 0;

    public (byte[] Left, byte[]? Right)? Load(int id)
    {
        if (_cache.TryGetValue(id, out var hit)) return hit;
        if (Get(id) is not { } t || t.Type is not (0 or 1) || t.Size <= 0) return null;
        var data = new byte[t.Size];
        using (var f = File.OpenRead(_mb))
        {
            f.Position = t.Offset;
            f.ReadExactly(data);
        }
        (byte[], byte[]?) result;
        uint magic = data.Length >= 0x30 ? BitConverter.ToUInt32(data, 0) : 0u; // Amedo 2026-09-29
        bool header = magic is MagicVag or MagicMsv;
        if (header) _headerRates[id] = (data[0x12] << 8) | data[0x13];
        if (t.Type == 0) result = (data.AsSpan(header ? 0x30 : 0).ToArray(), null);
        else
        {
            var left = new List<byte>(t.Size / 2);
            var right = new List<byte>(t.Size / 2);
            for (int p = 0; p < t.Size; p += 2 * Interleave)
            {
                int n = Math.Min(Interleave, (t.Size - p) / 2);
                bool last = t.Size <= p + 2 * Interleave;
                int valid = last && 0 < t.LastChunk && t.LastChunk < n ? t.LastChunk : n;
                left.AddRange(data.AsSpan(p, valid).ToArray());
                right.AddRange(data.AsSpan(p + n, valid).ToArray());
            }
            result = (left.ToArray(), right.ToArray());
        }
        _cache[id] = result;
        return result;
    }
}
