using System.Numerics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;

namespace CrashEngine.Importer;

// Amedo 2026-09-30
public sealed partial class DecalAssets
{
    public const int Groups = 16, TypesPerGroup = 15;

    public sealed class DecalType
    {
        public readonly Vector4[] Colour = new Vector4[4];
        public readonly Vector4[] Size = new Vector4[4];
        public float Lifetime => Size[0].W;
        public float Rate => Size[1].W;
    }

    public readonly DecalType?[]?[] Types = new DecalType?[Groups][];
    public Vector4[] Uv = Array.Empty<Vector4>();

    public DecalType? TypeOf(int group, int type) =>
        group >= 0 && group < Groups && Types[group] is { } g && type >= 0 && type < g.Length ? g[type] : null;

    public static DecalAssets From(PS2DefaultParticleData data)
    {
        var a = new DecalAssets();
        var t = data.UnkBlob;
        int rows = Math.Clamp(BitConverter.ToInt32(t, 0x410), 0, 0x1F) * 2;
        a.Uv = new Vector4[rows];
        for (int i = 0; i < rows; i++)
            a.Uv[i] = new Vector4(BitConverter.ToSingle(t, 0x10 + i * 16), BitConverter.ToSingle(t, 0x14 + i * 16),
                                  BitConverter.ToSingle(t, 0x18 + i * 16), BitConverter.ToSingle(t, 0x1C + i * 16));
        int blob = 0;
        for (int g = 0; g < Groups; g++)
        {
            if (data.UnkInts[g] == 0 || blob >= data.UnkBlobs.Count) continue;
            var b = data.UnkBlobs[blob++];
            int count = Math.Clamp(BitConverter.ToInt32(b, 0x790), 0, TypesPerGroup);
            var types = new DecalType?[count];
            for (int k = 0; k < count; k++)
            {
                var d = new DecalType();
                int o = 0x10 + k * 0x80;
                for (int c = 0; c < 4; c++)
                {
                    d.Colour[c] = V(b, o + c * 16);
                    d.Size[c] = V(b, o + 0x40 + c * 16);
                }
                types[k] = d;
            }
            a.Types[g] = types;
        }
        return a;
    }

    private static Vector4 V(byte[] b, int o) =>
        new(BitConverter.ToSingle(b, o), BitConverter.ToSingle(b, o + 4), BitConverter.ToSingle(b, o + 8), BitConverter.ToSingle(b, o + 12));
}

// Amedo 2026-09-30
public sealed class DecalWorld
{
    public const int BatchCount = 32, PerBatch = 32, Keys = 4;

    public struct Decal
    {
        public Vector3 Position;
        public int Frames;
        public int Flags;
        public int Type;
        public Vector3 Side, Dir;
    }

    public sealed class Batch
    {
        public int Count;
        public int Key;
        public int Next = -1;
        public readonly Decal[] Items = new Decal[PerBatch];
    }

    public readonly Batch[] Pool = new Batch[BatchCount];
    public readonly int[] GroupHead = new int[DecalAssets.Groups];
    private readonly int[] _freeNext = new int[BatchCount];
    private int _freeHead;
    public CrashEngine.Core.GameRandom Rng = CrashEngine.Core.GameRandom.Shared; // Amedo
    public DecalAssets? Assets;
    public int Count;

    public DecalWorld()
    {
        for (int i = 0; i < BatchCount; i++) Pool[i] = new Batch();
        Clear();
    }

    public void Clear()
    {
        for (int i = 0; i < BatchCount; i++)
        {
            Pool[i].Count = 0;
            Pool[i].Next = -1;
            _freeNext[i] = i + 1 < BatchCount ? i + 1 : -1;
        }
        _freeHead = 0;
        Array.Fill(GroupHead, -1);
        Count = 0;
    }

    private Batch? Alloc(int key, int group)
    {
        for (int i = GroupHead[group]; i >= 0; i = Pool[i].Next)
            if (Pool[i].Count < PerBatch && Pool[i].Key == key) return Pool[i];
        int b = _freeHead;
        if (b < 0) return null;
        _freeHead = _freeNext[b];
        var batch = Pool[b];
        batch.Key = key;
        batch.Count = 0;
        batch.Next = GroupHead[group];
        GroupHead[group] = b;
        return batch;
    }

    public void Add(Vector3 position, Vector3 normal, Vector3 direction, int type, int group, int flags, int key)
    {
        if (Assets?.TypeOf(group, type) is not { } t) return;
        var batch = Alloc(key, group);
        if (batch is null) return;
        Orthonormalise_1ACD28(ref normal, ref direction);
        ref var d = ref batch.Items[batch.Count++];
        d.Position = position;
        d.Frames = (int)(t.Lifetime * 60f);
        d.Flags = flags & 0x3F;
        d.Type = type;
        d.Side = Quantise(Vector3.Cross(normal, direction));
        d.Dir = Quantise(direction);
        Count++;
    }

    private static Vector3 Quantise(Vector3 v) =>
        new((short)(int)(v.X * 32768f) / 32768f, (short)(int)(v.Y * 32768f) / 32768f, (short)(int)(v.Z * 32768f) / 32768f);

    private void Orthonormalise_1ACD28(ref Vector3 n, ref Vector3 d)
    {
        n = Unit(n);
        d = Unit(d);
        float dot = Vector3.Dot(n, d);
        if (!(0.001f < dot)) return;
        if (dot < 0.99f) d = Unit(Vector3.Cross(Unit(Vector3.Cross(n, d)), n));
        else d = Unit(Vector3.Cross(n, new Vector3(RandomSigned(), RandomSigned(), RandomSigned())));
    }

    private static Vector3 Unit(Vector3 v)
    {
        float l = v.LengthSquared();
        return 2.4999998e-09f < l ? v * (1f / MathF.Sqrt(l)) : v;
    }

    private float RandomSigned() => Rng.RandomSigned_18C0F0(); // Amedo

    public void Step_1AD158()
    {
        if (Count == 0) return;
        for (int g = 0; g < DecalAssets.Groups; g++)
        {
            int prev = -1;
            for (int b = GroupHead[g]; b >= 0;)
            {
                var batch = Pool[b];
                int write = 0;
                for (int i = 0; i < batch.Count; i++)
                {
                    var d = batch.Items[i];
                    if (--d.Frames < 0) { Count--; continue; }
                    batch.Items[write++] = d;
                }
                batch.Count = write;
                int next = batch.Next;
                if (write == 0)
                {
                    if (prev < 0) GroupHead[g] = next;
                    else Pool[prev].Next = next;
                    batch.Next = -1;
                    _freeNext[b] = _freeHead;
                    _freeHead = b;
                }
                else prev = b;
                b = next;
            }
        }
    }
}
