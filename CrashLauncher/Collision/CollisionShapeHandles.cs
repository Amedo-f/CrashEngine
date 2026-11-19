using System.Numerics;

namespace CrashLauncher;

// Amedo 2026-11-19
public static class CollisionShapeHandles
{
    public const int Faces = 6;

    public static readonly (int A, int B)[] BoxEdges = BuildBoxEdges();

    public static int Axis(int face) => face >> 1;

    public static bool Positive(int face) => (face & 1) != 0;

    public static Vector3 Outward(int face)
    {
        float s = Positive(face) ? 1f : -1f;
        return Axis(face) switch { 0 => new Vector3(s, 0f, 0f), 1 => new Vector3(0f, s, 0f), _ => new Vector3(0f, 0f, s) };
    }

    public static bool OnFace(int corner, int face) => ((corner >> Axis(face)) & 1) == (Positive(face) ? 1 : 0);

    public static Vector3 FaceCenter(IReadOnlyList<Vector3> corners, int face)
    {
        var sum = Vector3.Zero;
        for (int i = 0; i < 8; i++)
            if (OnFace(i, face)) sum += corners[i];
        return sum * 0.25f;
    }

    public static float MoveBoxFace(Vector3[] corners, int face, float outward, float minSize)
    {
        int axis = Axis(face), bit = 1 << axis;
        float sign = Positive(face) ? 1f : -1f;
        float applied = outward;
        for (int i = 0; i < 8; i++)
        {
            if (!OnFace(i, face)) continue;
            float thickness = sign * (Component(corners[i], axis) - Component(corners[i ^ bit], axis));
            applied = MathF.Max(applied, MathF.Min(thickness, minSize) - thickness);
        }
        for (int i = 0; i < 8; i++)
            if (OnFace(i, face)) corners[i] = WithComponent(corners[i], axis, Component(corners[i], axis) + sign * applied);
        return applied;
    }

    public static float Grow(ref float size, float outward, float minSize)
    {
        float applied = MathF.Max(outward, MathF.Min(size, minSize) - size);
        size += applied;
        return applied;
    }

    private static float Component(Vector3 v, int axis) => axis switch { 0 => v.X, 1 => v.Y, _ => v.Z };

    private static Vector3 WithComponent(Vector3 v, int axis, float value)
    {
        if (axis == 0) v.X = value;
        else if (axis == 1) v.Y = value;
        else v.Z = value;
        return v;
    }

    private static (int A, int B)[] BuildBoxEdges()
    {
        var edges = new List<(int, int)>();
        for (int i = 0; i < 8; i++)
            for (int bit = 1; bit <= 4; bit <<= 1)
                if ((i & bit) == 0) edges.Add((i, i | bit));
        return edges.ToArray();
    }
}
