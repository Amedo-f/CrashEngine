using System.Numerics;

namespace CrashEngine.Core;

// Amedo
public sealed class GameRandom
{
    public static GameRandom Shared { get; } = new();

    private const float TwoPowMinus31 = 4.656612873e-10f;
    private const float TwoPowMinus30 = 9.313225746e-10f;
    private const float TwoPow30 = 1073741824f;

    public uint State = 1;

    public int GetRand_2C8230()
    {
        State = State * 1103515245u + 12345u;
        return (int)(State & 0x7FFFFFFFu);
    }

    public int RandomInt_18BFF8() => GetRand_2C8230();

    public int GetRandom_18C018(int n) => n < 2 ? 0 : GetRand_2C8230() % n;

    public int GetRandRange_18C060(int lo, int n) => lo + (n < 2 ? 0 : GetRand_2C8230() % n);

    public float GetRandFloat_18C0B8() => CvtSW(GetRand_2C8230()) * TwoPowMinus31;

    public float RandomSigned_18C0F0() => (CvtSW(GetRand_2C8230()) - TwoPow30) * TwoPowMinus30;

    public float GetRandFloat__18C130(float x) => RandomSigned_18C0F0() * x;

    public float RandomBelowFloat_18C180(float x) => GetRandFloat_18C0B8() * x;

    public float RandomFromFloat_18C1C0(float a, float b) => a + GetRandFloat_18C0B8() * b;

    public float RandomAround_18C210(float a, float b) => a + RandomSigned_18C0F0() * b;

    public static float CvtSW(int v)
    {
        uint m = v < 0 ? (uint)(-(long)v) : (uint)v;
        int bits = 32 - BitOperations.LeadingZeroCount(m);
        if (bits > 24) m = (m >> (bits - 24)) << (bits - 24);
        return v < 0 ? -(float)m : m;
    }
}
