using System.Numerics;
using System.Runtime.CompilerServices;
using PS2AnyOGI = Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.PS2AnyOGI;

namespace CrashEngine.Runtime.Physics;

// Amedo
public static class OgiHulls
{
    private static readonly ConditionalWeakTable<PS2AnyOGI, IReadOnlyList<ConvexHull>> Local = new();

    // Amedo
    public static bool IsDefaultBox(PS2AnyOGI ogi) => ogi.Collisions.Count == 0;

    public static int Count(PS2AnyOGI ogi) => IsDefaultBox(ogi) ? 1 : ogi.Collisions.Count;

    public static int Joint(PS2AnyOGI ogi, int i) =>
        !IsDefaultBox(ogi) && i < ogi.CollisionJointIndices.Count ? ogi.CollisionJointIndices[i] : 0xFF;

    public static IReadOnlyList<ConvexHull> Of(PS2AnyOGI ogi) => Local.GetValue(ogi, o => Enumerable.Range(0, Count(o)).Select(i => Fresh(o, i)).ToList());

    public static ConvexHull Fresh(PS2AnyOGI ogi, int i)
    {
        if (!IsDefaultBox(ogi)) return ConvexHull.From(ogi.Collisions[i]);
        var a = ogi.BoundingBox[0];
        var b = ogi.BoundingBox[1];
        return ConvexHull.Box(new Vector3(a.X, a.Y, a.Z), new Vector3(b.X, b.Y, b.Z));
    }
}
