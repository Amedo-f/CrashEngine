using System.Numerics;
using CrashEngine.Runtime.Physics;

namespace CrashEngine.Runtime.Character;

// Amedo 2026-09-27
public sealed class CharacterBody
{
    public CharacterBody(object owner) => Owner = owner;

    public object Owner { get; }
    public uint StateFlags;
    public bool Collidable = true; // Amedo 2026-09-28
    public Vector4? Sphere; // Amedo 2026-09-30
    public readonly List<(ConvexHull Hull, Matrix4x4 World)> Parts = new();
}
