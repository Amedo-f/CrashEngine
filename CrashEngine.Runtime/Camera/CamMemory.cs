using System.Buffers.Binary;
using System.Numerics;

namespace CrashEngine.Runtime.Camera;

// Amedo 2026-09-28
public sealed class CamMemory
{
    private sealed class Region
    {
        public uint Base;
        public byte[] Data = [];
        public string Name = "";
    }

    private readonly List<Region> _regions = new();
    private Region? _last;

    public Dictionary<uint, int> UnmappedReads { get; } = new();
    public Dictionary<uint, int> UnmappedWrites { get; } = new();

    public byte[] Map(uint address, int length, string name = "") => Map(address, new byte[length], name);

    public byte[] Map(uint address, byte[] data, string name = "")
    {
        _regions.RemoveAll(r => r.Base == address);
        _regions.Add(new Region { Base = address, Data = data, Name = name });
        _regions.Sort((a, b) => a.Base.CompareTo(b.Base));
        _last = null;
        return data;
    }

    public bool IsMapped(uint address) => Find(address, 1) is not null;

    private Region? Find(uint a, int size)
    {
        if (_last is { } l && a >= l.Base && a + (uint)size <= l.Base + (uint)l.Data.Length) return l;
        int lo = 0, hi = _regions.Count - 1; // Amedo 2026-09-29
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            var r = _regions[mid];
            if (a < r.Base) hi = mid - 1;
            else if (a + (uint)size > r.Base + (uint)r.Data.Length) lo = mid + 1;
            else return _last = r;
        }
        return null;
    }

    private Span<byte> Span(uint a, int size, bool write)
    {
        if (Find(a, size) is { } r) return r.Data.AsSpan((int)(a - r.Base), size);
        var log = write ? UnmappedWrites : UnmappedReads;
        log[a] = log.GetValueOrDefault(a) + 1;
        return new byte[size];
    }

    public uint U(uint a) => BinaryPrimitives.ReadUInt32LittleEndian(Span(a, 4, false));
    public int I(uint a) => BinaryPrimitives.ReadInt32LittleEndian(Span(a, 4, false));
    public ulong Q(uint a) => BinaryPrimitives.ReadUInt64LittleEndian(Span(a, 8, false));
    public float F(uint a) => BinaryPrimitives.ReadSingleLittleEndian(Span(a, 4, false));
    public byte B(uint a) => Span(a, 1, false)[0];
    public short S(uint a) => BinaryPrimitives.ReadInt16LittleEndian(Span(a, 2, false));

    public void U(uint a, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(Span(a, 4, true), v);
    public void I(uint a, int v) => BinaryPrimitives.WriteInt32LittleEndian(Span(a, 4, true), v);
    public void Q(uint a, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(Span(a, 8, true), v);
    public void F(uint a, float v) => BinaryPrimitives.WriteSingleLittleEndian(Span(a, 4, true), v);
    public void B(uint a, byte v) => Span(a, 1, true)[0] = v;

    public Vector4 V(uint a) => new(F(a), F(a + 4), F(a + 8), F(a + 12));
    public void V(uint a, Vector4 v) { F(a, v.X); F(a + 4, v.Y); F(a + 8, v.Z); F(a + 12, v.W); }

    public void Copy16(uint dst, uint src)
    {
        Span(src, 16, false).CopyTo(Span(dst, 16, true)); // Amedo 2026-09-29
    }

    public Matrix4x4 M(uint a) => new(F(a), F(a + 4), F(a + 8), F(a + 12), F(a + 16), F(a + 20), F(a + 24), F(a + 28),
                                      F(a + 32), F(a + 36), F(a + 40), F(a + 44), F(a + 48), F(a + 52), F(a + 56), F(a + 60));

    public void M(uint a, Matrix4x4 m)
    {
        F(a, m.M11); F(a + 4, m.M12); F(a + 8, m.M13); F(a + 12, m.M14);
        F(a + 16, m.M21); F(a + 20, m.M22); F(a + 24, m.M23); F(a + 28, m.M24);
        F(a + 32, m.M31); F(a + 36, m.M32); F(a + 40, m.M33); F(a + 44, m.M34);
        F(a + 48, m.M41); F(a + 52, m.M42); F(a + 56, m.M43); F(a + 60, m.M44);
    }
}
