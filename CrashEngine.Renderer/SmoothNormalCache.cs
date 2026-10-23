using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace CrashEngine.Renderer;

// Amedo
public static class SmoothNormalCache
{
    private const int FormatVersion = 1;
    private const long MaxMemoryVertices = 3_000_000;
    private static readonly byte[] Magic = "SMN1"u8.ToArray();

    public static string? Directory { get; set; }
    public static bool Enabled { get; set; } = true;
    public static bool Verify { get; set; }
    public static int Hits, DiskHits, Misses, VerifyFailures;

    private static readonly object Lock = new();
    private static readonly Dictionary<Key, Vector3[]> Memory = new();
    private static readonly Queue<Key> MemoryOrder = new();
    private static long _memoryVertices;
    private static readonly AsyncLocal<Pack?> CurrentPack = new();

    public readonly record struct Key(ulong A, ulong B);

    private sealed class Pack
    {
        public required string Path;
        public readonly Dictionary<Key, Vector3[]> Loaded = new();
        public readonly Dictionary<Key, Vector3[]> Used = new();
        public bool Added;
    }

    public static IDisposable? Scope(string name)
    {
        if (!Enabled || Directory is not { Length: > 0 } dir) return null;
        var pack = new Pack { Path = System.IO.Path.Combine(dir, SafeName(name) + ".smn") };
        try { Load(pack); } catch { pack.Loaded.Clear(); }
        var outer = CurrentPack.Value;
        CurrentPack.Value = pack;
        return new ScopeEnd(pack, outer);
    }

    private sealed class ScopeEnd(Pack pack, Pack? outer) : IDisposable
    {
        public void Dispose()
        {
            CurrentPack.Value = outer;
            bool stale;
            lock (pack) stale = pack.Added || pack.Used.Count != pack.Loaded.Count;
            if (!stale) return;
            Task.Run(() => { try { Save(pack); } catch { } });
        }
    }

    public static Key KeyOf(IReadOnlyList<GpuMesh.Vertex[]> parts, IReadOnlyList<uint[]> indices)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> head = stackalloc byte[24];
        BitConverter.TryWriteBytes(head, FormatVersion);
        BitConverter.TryWriteBytes(head[4..], GpuMesh.SmoothRadiusScale);
        BitConverter.TryWriteBytes(head[8..], GpuMesh.SkinRadiusScale);
        BitConverter.TryWriteBytes(head[12..], GpuMesh.SmoothRadiusMax);
        BitConverter.TryWriteBytes(head[16..], parts.Count);
        BitConverter.TryWriteBytes(head[20..], indices.Count);
        sha.AppendData(head);
        Span<Vector3> buffer = stackalloc Vector3[3 * 256];
        foreach (var part in parts)
        {
            BitConverter.TryWriteBytes(head, part.Length);
            sha.AppendData(head[..4]);
            int n = 0;
            foreach (ref readonly var v in part.AsSpan())
            {
                buffer[n++] = v.Position;
                buffer[n++] = v.BoneWeights;
                buffer[n++] = v.SmoothNormal;
                if (n == buffer.Length) { sha.AppendData(MemoryMarshal.AsBytes(buffer)); n = 0; }
            }
            if (n > 0) sha.AppendData(MemoryMarshal.AsBytes(buffer[..n]));
        }
        foreach (var idx in indices)
        {
            BitConverter.TryWriteBytes(head, idx.Length);
            sha.AppendData(head[..4]);
            sha.AppendData(MemoryMarshal.AsBytes(idx.AsSpan()));
        }
        Span<byte> hash = stackalloc byte[32];
        sha.GetHashAndReset(hash);
        return new Key(BitConverter.ToUInt64(hash), BitConverter.ToUInt64(hash[8..]));
    }

    public static bool TryGet(Key key, int vertices, out Vector3[] normals)
    {
        var pack = CurrentPack.Value;
        lock (Lock)
            if (Memory.TryGetValue(key, out normals!) && normals.Length == vertices)
            {
                if (pack is not null) lock (pack) pack.Used[key] = normals;
                Interlocked.Increment(ref Hits);
                return true;
            }
        if (pack is not null)
        {
            bool found;
            lock (pack) found = pack.Loaded.TryGetValue(key, out normals!) && normals.Length == vertices;
            if (found)
            {
                lock (pack) pack.Used[key] = normals;
                Remember(key, normals);
                Interlocked.Increment(ref DiskHits);
                return true;
            }
        }
        normals = [];
        Interlocked.Increment(ref Misses);
        return false;
    }

    public static void Put(Key key, Vector3[] normals)
    {
        Remember(key, normals);
        if (CurrentPack.Value is { } pack)
            lock (pack)
            {
                pack.Used[key] = normals;
                pack.Added = true;
            }
    }

    private static void Remember(Key key, Vector3[] normals)
    {
        lock (Lock)
        {
            if (!Memory.TryAdd(key, normals)) return;
            MemoryOrder.Enqueue(key);
            _memoryVertices += normals.Length;
            while (_memoryVertices > MaxMemoryVertices && MemoryOrder.TryDequeue(out var old))
                if (Memory.Remove(old, out var gone)) _memoryVertices -= gone.Length;
        }
    }

    private static string SafeName(string name)
    {
        var chars = name.ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '.' && chars[i] != '-') chars[i] = '_';
        return new string(chars);
    }

    private static void Load(Pack pack)
    {
        if (!File.Exists(pack.Path)) return;
        var data = File.ReadAllBytes(pack.Path);
        if (data.Length < 12 || !data.AsSpan(0, 4).SequenceEqual(Magic) || BitConverter.ToInt32(data, 4) != FormatVersion) return;
        int count = BitConverter.ToInt32(data, 8), at = 12;
        for (int e = 0; e < count; e++)
        {
            if (at + 20 > data.Length) { pack.Loaded.Clear(); return; }
            var key = new Key(BitConverter.ToUInt64(data, at), BitConverter.ToUInt64(data, at + 8));
            int n = BitConverter.ToInt32(data, at + 16);
            at += 20;
            if (n < 0 || at + (long)n * 12 > data.Length) { pack.Loaded.Clear(); return; }
            var normals = new Vector3[n];
            data.AsSpan(at, n * 12).CopyTo(MemoryMarshal.AsBytes(normals.AsSpan()));
            at += n * 12;
            pack.Loaded[key] = normals;
        }
    }

    private static void Save(Pack pack)
    {
        KeyValuePair<Key, Vector3[]>[] entries;
        lock (pack) entries = pack.Used.ToArray();
        if (entries.Length == 0) return;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pack.Path)!);
        string temp = pack.Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var w = new BinaryWriter(File.Create(temp)))
        {
            w.Write(Magic);
            w.Write(FormatVersion);
            w.Write(entries.Length);
            foreach (var (key, normals) in entries)
            {
                w.Write(key.A);
                w.Write(key.B);
                w.Write(normals.Length);
                w.Write(MemoryMarshal.AsBytes(normals.AsSpan()));
            }
        }
        File.Move(temp, pack.Path, overwrite: true);
    }
}
