using System.Numerics;

namespace CrashEngine.Importer;

// Amedo
public struct SkidQuad
{
    public Vector3 A, B, C, D;
    public uint ColorA, ColorB, ColorC, ColorD;
    public bool Add;
}

// Amedo
public sealed class SkidMarkRing
{
    public const int Count = 100;
    public readonly SkidQuad[] Quads = new SkidQuad[Count];
    public readonly bool[] Used = new bool[Count];
}
