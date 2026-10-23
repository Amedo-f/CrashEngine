using System.Numerics;

namespace CrashEngine.Runtime.Physics;

// Amedo 2026-09-27
public sealed class TriangleCache
{
    public const uint TerrainMask = 0x50;
    public const int Limit = 0x400;

    private readonly List<int> _indices = new();

    public Vector3 Min { get; private set; }
    public Vector3 Max { get; private set; }
    public float Margin { get; set; } = 1f;
    public uint Mask { get; }
    public List<CollisionWorld.Triangle> Triangles { get; } = new();
    public int Refreshes { get; private set; }

    public TriangleCache(uint mask = TerrainMask) => Mask = mask;

    private CollisionWorld? _world; // Amedo 2026-09-29

    public bool Refresh(CollisionWorld world, Vector3 min, Vector3 max)
    {
        if (Refreshes > 0 && ReferenceEquals(world, _world) && CollisionWorld.BoxInside(min, max, Min, Max)) return false; // Amedo 2026-09-29
        _world = world;
        var reach = new Vector3(Margin);
        Min = min - reach;
        Max = max + reach;
        world.BoxQuery(Min, Max, Mask, _indices, Limit);
        Triangles.Clear();
        foreach (int i in _indices) Triangles.Add(world.TriangleAt(i));
        Refreshes++;
        return true;
    }
}
