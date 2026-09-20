using System.Numerics;

namespace CrashEngine.Stealth;

public sealed class PatrolPath
{
    public List<Vector3> Points { get; set; } = new();

    public Vector3 Get(int index) => Points[index % Points.Count];
    public int     Count         => Points.Count;
}
